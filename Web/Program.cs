using PRN222_FINAL.BLL.Services.Chat;
using PRN222_FINAL.BLL.Services.Accounts;
using PRN222_FINAL.BLL.Security;
using PRN222_FINAL.BLL.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using PRN222_FINAL.Web.Hubs;
using PRN222_FINAL.Web.Security;
using PRN222_FINAL.BLL;

namespace PRN222_FINAL.Web
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.InputEncoding = System.Text.Encoding.UTF8;

            var contentRootPath = Directory.GetCurrentDirectory();
            var bootstrapConfiguration = new ConfigurationBuilder()
                .SetBasePath(contentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();
            var configuredEnvironment = bootstrapConfiguration["Hosting:Environment"];

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = args,
                ContentRootPath = contentRootPath,
                EnvironmentName = string.IsNullOrWhiteSpace(configuredEnvironment)
                    ? Environments.Production
                    : configuredEnvironment.Trim()
            });

            builder.Configuration.Sources.Clear();
            builder.Configuration
                .SetBasePath(builder.Environment.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables();
            if (builder.Environment.IsDevelopment())
            {
                builder.Configuration.AddUserSecrets<Program>(optional: true);
            }

            builder.Services.AddRazorPages();
            builder.Services
                .AddDataProtection()
                .SetApplicationName("Group07MVC.CourseAssistant");
            builder.Services.AddSignalR();
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddPolicy("auth", context => RateLimitPartition.GetSlidingWindowLimiter(
                    $"{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}:{context.Request.Method}",
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = HttpMethods.IsPost(context.Request.Method) ? 10 : 60,
                        Window = TimeSpan.FromMinutes(15),
                        SegmentsPerWindow = 3,
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
            });

            var authenticationBuilder = builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;
            });
            authenticationBuilder.AddCookie(options =>
            {
                options.LoginPath = "/Account/Login";
                options.LogoutPath = "/Account/Logout";
                options.AccessDeniedPath = "/Account/AccessDenied";
                options.Cookie.Name = "CourseAssistant.Auth";
                options.Events = new Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationEvents
                {
                    OnValidatePrincipal = async context =>
                    {
                        var userIdValue = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                        if (!Guid.TryParse(userIdValue, out var userId))
                        {
                            context.RejectPrincipal();
                            await context.HttpContext.SignOutAsync(
                                Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
                            return;
                        }

                        var users = context.HttpContext.RequestServices.GetRequiredService<PRN222_FINAL.BLL.Services.Accounts.IUserAccountService>();
                        PRN222_FINAL.BLL.Models.UserAccount? user;
                        try
                        {
                            using var validationTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                            user = await users.FindByIdAsync(userId, validationTimeout.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            context.RejectPrincipal();
                            await context.HttpContext.SignOutAsync(
                                Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
                            return;
                        }

                        if (user is null || user.IsSuspended || user.LockoutEnd > DateTimeOffset.UtcNow)
                        {
                            context.RejectPrincipal();
                            await context.HttpContext.SignOutAsync(
                                Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
                            return;
                        }

                        if (user.LastActiveAt is null || user.LastActiveAt < DateTimeOffset.UtcNow.AddMinutes(-15))
                        {
                            try
                            {
                                await users.MarkActiveAsync(user.Id, context.HttpContext.RequestAborted);
                                user.LastActiveAt = DateTimeOffset.UtcNow;
                            }
                            catch (Exception exception) when (exception is not OperationCanceledException)
                            {
                                var logger = context.HttpContext.RequestServices
                                    .GetRequiredService<ILoggerFactory>()
                                    .CreateLogger("UserActivity");
                                logger.LogWarning(exception, "Could not update last activity for user {UserId}", user.Id);
                            }
                        }

                        var normalizedRole = AppRoles.Normalize(user.Role);
                        if (context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value == user.Id.ToString()
                            && context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value == user.FullName
                            && context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value == normalizedRole)
                        {
                            return;
                        }

                        var claims = new[]
                        {
                            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()),
                            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, user.FullName),
                            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, user.Email),
                            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, normalizedRole)
                        };
                        var identity = new System.Security.Claims.ClaimsIdentity(
                            claims,
                            Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
                        context.ReplacePrincipal(new System.Security.Claims.ClaimsPrincipal(identity));
                        context.ShouldRenew = true;
                    }
                };
            });
            authenticationBuilder.AddCookie("External", options =>
            {
                options.Cookie.Name = "CourseAssistant.External";
                options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
            });

            var googleClientId = builder.Configuration["Authentication:Google:ClientId"] ?? string.Empty;
            var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
            {
                authenticationBuilder.AddGoogle(options =>
                {
                    options.ClientId = googleClientId;
                    options.ClientSecret = googleClientSecret;
                    options.CallbackPath = "/signin-google";
                    options.SignInScheme = "External";
                    options.SaveTokens = false;

                    options.Events.OnRemoteFailure = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("GoogleAuthentication");
                        logger.LogWarning(context.Failure, "Google OAuth callback failed.");
                        context.HandleResponse();
                        context.Response.Redirect("/Account/Login?googleError=oauth_failed");
                        return Task.CompletedTask;
                    };

                });
            }

            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy(AuthorizationPolicies.ChatAccess, policy =>
                    policy.RequireRole(AppRoles.Student, AppRoles.Lecturer, AppRoles.Admin));
                options.AddPolicy(AuthorizationPolicies.DocumentRead, policy =>
                    policy.RequireRole(AppRoles.Lecturer, AppRoles.Admin));
                options.AddPolicy(AuthorizationPolicies.DocumentManagement, policy =>
                    policy.RequireRole(AppRoles.Lecturer, AppRoles.Admin));
                options.AddPolicy(AuthorizationPolicies.AdminOnly, policy =>
                    policy.RequireRole(AppRoles.Admin));
            });

            var geminiSection = builder.Configuration.GetSection("Gemini");
            var geminiApiKey = geminiSection["ApiKey"] ?? string.Empty;
            var geminiEnabled = !bool.TryParse(geminiSection["Enabled"], out var parsedGeminiEnabled)
                || parsedGeminiEnabled;
            var geminiTimeoutSeconds = int.TryParse(geminiSection["TimeoutSeconds"], out var parsedGeminiTimeout)
                ? parsedGeminiTimeout
                : 60;
            var geminiEmbeddingDimensions = int.TryParse(geminiSection["EmbeddingDimensions"], out var parsedGeminiEmbeddingDimensions)
                ? parsedGeminiEmbeddingDimensions
                : int.TryParse(builder.Configuration["Embedding:OutputDimensionality"], out var parsedEmbeddingDimensions)
                    ? parsedEmbeddingDimensions
                    : 768;
            var geminiOptions = new PRN222_FINAL.BLL.GeminiOptions(
                geminiEnabled,
                geminiApiKey,
                geminiSection["ChatModel"] ?? "gemini-3.5-flash",
                geminiSection["EmbeddingModel"] ?? "gemini-embedding-2",
                geminiEmbeddingDimensions,
                geminiTimeoutSeconds,
                geminiSection["ChatBaseUrl"] ?? "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions",
                geminiSection["EmbeddingBaseUrl"] ?? "https://generativelanguage.googleapis.com/v1beta");

            builder.Services.AddSingleton(geminiOptions);

            var groqSection = builder.Configuration.GetSection("Groq");
            var groqApiKey = groqSection["ApiKey"] ?? string.Empty;
            var chatGenerationOptions = new ChatGenerationOptions
            {
                Provider = ChatProviders.Normalize(builder.Configuration["Chat:Provider"]),
                Model = geminiOptions.ChatModel,
                GeminiEnabled = geminiOptions.Enabled,
                GeminiApiKey = geminiOptions.ApiKey,
                GeminiBaseUrl = geminiOptions.ChatBaseUrl,
                GroqEnabled = !bool.TryParse(groqSection["Enabled"], out var parsedGroqEnabled)
                    ? !string.IsNullOrWhiteSpace(groqApiKey)
                    : parsedGroqEnabled,
                GroqApiKey = groqApiKey,
                GroqBaseUrl = groqSection["ChatBaseUrl"] ?? "https://api.groq.com/openai/v1/chat/completions",
                TimeoutSeconds = int.TryParse(groqSection["TimeoutSeconds"], out var parsedGroqTimeout)
                    ? Math.Clamp(parsedGroqTimeout, 5, 180)
                    : geminiOptions.TimeoutSeconds
            };
            builder.Services.AddSingleton(chatGenerationOptions);

            builder.Services.AddKnowledgeBusinessServices(builder.Configuration, builder.Environment.ContentRootPath);
            builder.Services.AddSingleton<PRN222_FINAL.BLL.IDocumentTextExtractor, PRN222_FINAL.BLL.DocumentTextExtractor>();
            builder.Services.AddSingleton<PRN222_FINAL.BLL.FlmSyllabusAwareTextChunker>();
            builder.Services.AddSingleton<PRN222_FINAL.BLL.ITextChunker>(provider =>
                provider.GetRequiredService<PRN222_FINAL.BLL.FlmSyllabusAwareTextChunker>());
            builder.Services.AddSingleton<PRN222_FINAL.BLL.IChunkRetrievalEnrichmentService, PRN222_FINAL.BLL.AiChunkRetrievalEnrichmentService>();
            builder.Services.AddSingleton<PRN222_FINAL.BLL.IDocumentIndexJobQueue, PRN222_FINAL.BLL.DocumentIndexJobQueue>();
            builder.Services.AddSingleton<PRN222_FINAL.Web.Services.IDocumentStatusNotifier, PRN222_FINAL.Web.Services.SignalRDocumentStatusNotifier>();
            builder.Services.AddSingleton<PRN222_FINAL.Web.Services.IOnlineUserPresenceTracker, PRN222_FINAL.Web.Services.InMemoryOnlineUserPresenceTracker>();
            builder.Services.AddScoped<PRN222_FINAL.BLL.IDocumentIndexingService, PRN222_FINAL.BLL.DocumentIndexingService>();
            builder.Services.AddScoped<PRN222_FINAL.BLL.IRagChatService, PRN222_FINAL.BLL.RagChatService>();
            builder.Services.AddHostedService<PRN222_FINAL.Web.Services.DocumentIndexWorker>();
            builder.Services.AddHostedService<PRN222_FINAL.Web.Services.AccountEmailWorker>();

            var app = builder.Build();
            _ = app.Services.GetRequiredService<PRN222_FINAL.BLL.Services.IAiSettingsService>();
            _ = app.Services.GetRequiredService<PRN222_FINAL.BLL.IKnowledgeService>();
            _ = app.Services.GetRequiredService<PRN222_FINAL.BLL.Services.Accounts.IUserAccountService>()
                .HasAnyUsersAsync()
                .GetAwaiter()
                .GetResult();

            var hostingEnvironment = (builder.Configuration["Hosting:Environment"] ?? string.Empty).Trim();
            var isDevelopment = hostingEnvironment.Equals("Development", StringComparison.OrdinalIgnoreCase);
            if (!isDevelopment)
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseRouting();
            app.UseRateLimiter();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapHub<DocumentStatusHub>("/hubs/documents");
            app.MapRazorPages()
                .WithStaticAssets();

            app.Run();
        }
    }
}

