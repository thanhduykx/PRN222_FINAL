using PRN222_FINAL.BLL.Services.Chat;
using PRN222_FINAL.BLL.Services.Accounts;
using PRN222_FINAL.BLL.Security;
using System.Security.Claims;
using PRN222_FINAL.BLL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222_FINAL.Web.Models;
using PRN222_FINAL.Web.Security;
using PRN222_FINAL.Web.Services;
using PRN222_FINAL.BLL;

namespace PRN222_FINAL.Web.Pages.Home;

[Authorize(Policy = AuthorizationPolicies.ChatAccess)]
public sealed class AskModel : HomePageModelBase
{
    public AskModel(
        ILogger<HomePageModelBase> logger,
        IKnowledgeService knowledge,
        IDocumentIndexingService indexingService,
        IWebPageTextExtractor webPageTextExtractor,
        IRagChatService chatService,
        IUserAccountService users,
        IWebHostEnvironment environment,
        IDocumentIndexJobQueue indexJobQueue,
        IChatUsageService chatUsage)
        : base(logger, knowledge, indexingService, webPageTextExtractor, chatService, users, environment, indexJobQueue)
    {
        _chatUsage = chatUsage;
    }

    private readonly IChatUsageService _chatUsage;

    public async Task<IActionResult> OnPostAsync([FromBody] ChatRequest? request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return new JsonResult(new { error = "Invalid question payload." });
        }

        var question = request.Question?.Trim() ?? string.Empty;
        if (question.Length is < 1 or > 4000)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return new JsonResult(new { error = "Câu hỏi cần có từ 1 đến 4.000 ký tự." });
        }

        if (!Guid.TryParse(request.SessionId, out var sessionId))
        {
            sessionId = Guid.NewGuid();
        }

        try
        {
            var currentUser = await GetCurrentUserAccountAsync(cancellationToken);
            if (currentUser is not null && CurrentRole() == AppRoles.Student)
            {
                var usageBefore = await _chatUsage.GetAsync(currentUser.Id, cancellationToken);
                if (usageBefore.IsExhausted)
                {
                    Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    return new JsonResult(new
                    {
                        error = "Bạn chưa có gói hoạt động hoặc đã dùng hết số câu hỏi trong tháng. Hãy xem các gói dịch vụ để tiếp tục.",
                        answerStatus = "quota_exhausted"
                    });
                }
            }
            var chatScope = BuildDocumentAccessScope(DocumentAccessMode.Chat);
            var allowedSubjects = await _knowledge.GetIndexedSubjectsAsync(chatScope, cancellationToken);
            var displayName = User.FindFirstValue(ClaimTypes.Name)
                ?? User.FindFirstValue(ClaimTypes.Email)?.Split('@')[0];
            if (currentUser is not null)
            {
                var existingSession = await _knowledge.GetSessionAsync(sessionId, cancellationToken);
                if (existingSession?.OwnerUserId is { } ownerUserId && ownerUserId != currentUser.Id)
                {
                    sessionId = Guid.NewGuid();
                }
            }

            var answer = await _chatService.AskAsync(
                sessionId,
                question,
                displayName,
                request.SubjectFilter,
                request.Language,
                allowedSubjects,
                BuildChatSessionOwnerInfo(),
                chatScope,
                request.AnswerDepth,
                cancellationToken);

            var usage = currentUser is not null && CurrentRole() == AppRoles.Student
                ? await _chatUsage.GetAsync(currentUser.Id, cancellationToken)
                : null;
            return new JsonResult(new
            {
                sessionId,
                answer = answer.Answer,
                citations = answer.Citations,
                resolvedSubject = answer.ResolvedSubject,
                needsClarification = answer.NeedsClarification,
                subjectOptions = answer.SubjectOptions,
                answerSource = answer.AnswerSource,
                answerStatus = answer.AnswerStatus,
                hasDirectCitation = answer.HasDirectCitation,
                fallbackModel = answer.FallbackModel,
                questionsRemaining = usage?.Remaining
            });
        }
        catch (Exception ex) when (IsDataAccessTimeout(ex))
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return new JsonResult(new
            {
                error = "Database unavailable/timeout. Vui long thu lai sau vai giay.",
                answerStatus = "technical_error"
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Chat request could not be completed.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return new JsonResult(new
            {
                error = "Hệ thống tạm thời chưa thể xử lý câu hỏi. Vui lòng thử lại sau ít phút.",
                answerStatus = "technical_error"
            });
        }
    }

}

