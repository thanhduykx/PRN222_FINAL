using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222_FINAL.BLL.Services.Billing;
using PRN222_FINAL.BLL.Models;
using PRN222_FINAL.BLL.Contracts.Billing;

namespace PRN222_FINAL.Web.Pages.Payments;

#pragma warning disable S4502 // External MoMo callbacks cannot carry a browser CSRF token; HMAC validation is mandatory in PaymentService.
[IgnoreAntiforgeryToken]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Security",
    "S4502:CSRF protection is intentionally disabled for this signed external callback",
    Justification = "MoMo cannot provide a browser antiforgery token; PaymentService rejects callbacks unless their HMAC signature is valid.")]
public sealed class MomoWebhookModel : PageModel
{
    private readonly IPaymentService _payments;

    public MomoWebhookModel(IPaymentService payments)
    {
        _payments = payments;
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        try
        {
            var (rawBody, values) = await ReadWebhookValuesAsync(cancellationToken);
            var result = await _payments.HandleWebhookAsync(new PaymentWebhookDto
            {
                Provider = PaymentProvider.MoMo,
                RawBody = rawBody,
                Values = values
            }, cancellationToken);

            return new JsonResult(new { resultCode = 0, message = result.Message });
        }
        catch (Exception ex)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return new JsonResult(new { resultCode = 1, message = ex.Message });
        }
    }

    private async Task<(string RawBody, IReadOnlyDictionary<string, string> Values)> ReadWebhookValuesAsync(CancellationToken cancellationToken)
    {
        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(cancellationToken);
            return (string.Empty, form.ToDictionary(pair => pair.Key, pair => pair.Value.ToString(), StringComparer.OrdinalIgnoreCase));
        }

        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);
        return (rawBody, ParseFlatJson(rawBody));
    }

    private static IReadOnlyDictionary<string, string> ParseFlatJson(string rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        using var json = JsonDocument.Parse(rawBody);
        return json.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.ToString(), StringComparer.OrdinalIgnoreCase);
    }
}
