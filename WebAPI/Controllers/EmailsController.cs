using System.Security.Claims;
using Application.Common;
using Application.DTOs;
using Application.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/schools/{schoolId:long}/emails")]
public sealed class EmailsController(IEmailManagementService service) : ControllerBase
{
    private ulong ActorId => ulong.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    [HttpGet("/api/email/schools")]
    public Task<IActionResult> Schools([FromQuery] EmailListQuery query, CancellationToken ct) => Respond(() => service.SchoolsAsync(query, ActorId, ct));

    [HttpGet("events")]
    public Task<IActionResult> Events(ulong schoolId, [FromQuery] EmailListQuery query, CancellationToken ct) => Respond(() => service.EventsAsync(schoolId, query, ActorId, ct));

    [HttpGet("events/{code}")]
    public Task<IActionResult> Event(ulong schoolId, string code, CancellationToken ct) => Respond(() => service.EventAsync(schoolId, code, ActorId, ct));

    [HttpPost("events")]
    public Task<IActionResult> CreateEvent(ulong schoolId, SaveEmailEventRequest request, CancellationToken ct) => Respond(() => service.SaveEventAsync(schoolId, null, request, ActorId, ct), true);

    [HttpPut("events/{code}")]
    public Task<IActionResult> UpdateEvent(ulong schoolId, string code, SaveEmailEventRequest request, CancellationToken ct) => Respond(() => service.SaveEventAsync(schoolId, code, request, ActorId, ct));

    [HttpPatch("events/{code}/status")]
    public Task<IActionResult> EventStatus(ulong schoolId, string code, EmailStatusRequest request, CancellationToken ct) => Respond(() => service.EventStatusAsync(schoolId, code, request, ActorId, ct));

    [HttpDelete("events/{code}")]
    public Task<IActionResult> DeleteEvent(ulong schoolId, string code, [FromQuery] uint version, CancellationToken ct) => Respond(() => service.DeleteEventAsync(schoolId, code, version, ActorId, ct));

    [HttpGet("delivery-status")]
    public Task<IActionResult> Runtime(ulong schoolId, CancellationToken ct) => Respond(() => service.RuntimeAsync(schoolId, ActorId, ct));

    [HttpPost("send/preview")]
    public Task<IActionResult> MessagePreview(ulong schoolId, SendEmailRequest request, CancellationToken ct) => Respond(() => service.MessagePreviewAsync(schoolId, request, ActorId, ct));

    [HttpPost("send")]
    public Task<IActionResult> Send(ulong schoolId, SendEmailRequest request, CancellationToken ct) => Respond(() => service.SendAsync(schoolId, request, ActorId, ct));

    [HttpPost("history/{id:long}/cancel")]
    public Task<IActionResult> Cancel(ulong schoolId, ulong id, CancelEmailRequest request, CancellationToken ct) => Respond(() => service.CancelAsync(schoolId, id, request.Version, ActorId, ct));

    [HttpGet("templates")]
    public Task<IActionResult> Templates(ulong schoolId, [FromQuery] EmailListQuery query, CancellationToken ct) => Respond(() => service.TemplatesAsync(schoolId, query, ActorId, ct));

    [HttpGet("templates/{id:long}")]
    public Task<IActionResult> Template(ulong schoolId, ulong id, CancellationToken ct) => Respond(() => service.TemplateAsync(schoolId, id, ActorId, ct));

    [HttpPost("templates")]
    public Task<IActionResult> CreateTemplate(ulong schoolId, SaveEmailTemplateRequest request, CancellationToken ct) => Respond(() => service.SaveTemplateAsync(schoolId, null, request, ActorId, ct), true);

    [HttpPut("templates/{id:long}")]
    public Task<IActionResult> UpdateTemplate(ulong schoolId, ulong id, SaveEmailTemplateRequest request, CancellationToken ct) => Respond(() => service.SaveTemplateAsync(schoolId, id, request, ActorId, ct));

    [HttpGet("templates/{id:long}/revisions")]
    public Task<IActionResult> Revisions(ulong schoolId, ulong id, [FromQuery] EmailListQuery query, CancellationToken ct) => Respond(() => service.RevisionsAsync(schoolId, id, query, ActorId, ct));

    [HttpPatch("templates/{id:long}/status")]
    public Task<IActionResult> TemplateStatus(ulong schoolId, ulong id, EmailStatusRequest request, CancellationToken ct) => Respond(() => service.TemplateStatusAsync(schoolId, id, request, ActorId, ct));

    [HttpDelete("templates/{id:long}")]
    public Task<IActionResult> DeleteTemplate(ulong schoolId, ulong id, [FromQuery] uint version, CancellationToken ct) => Respond(() => service.DeleteTemplateAsync(schoolId, id, version, ActorId, ct));

    [HttpGet("configurations")]
    public Task<IActionResult> Configurations(ulong schoolId, [FromQuery] EmailListQuery query, CancellationToken ct) => Respond(() => service.ConfigurationsAsync(schoolId, query, ActorId, ct));

    [HttpGet("configuration/{eventCode}")]
    public Task<IActionResult> Configuration(ulong schoolId, string eventCode, CancellationToken ct) => Respond(() => service.ConfigurationAsync(schoolId, eventCode, ActorId, ct));

    [HttpPut("configuration/{eventCode}")]
    public Task<IActionResult> SaveConfiguration(ulong schoolId, string eventCode, SaveEmailConfigRequest request, CancellationToken ct) => Respond(() => service.SaveConfigurationAsync(schoolId, eventCode, request, ActorId, ct));

    [HttpGet("recipients")]
    public Task<IActionResult> Recipients(ulong schoolId, [FromQuery] string kind, [FromQuery] EmailListQuery query, CancellationToken ct) => Respond(() => service.RecipientsAsync(schoolId, kind, query, ActorId, ct));

    [HttpPost("recipients/preview")]
    public Task<IActionResult> Preview(ulong schoolId, SaveEmailConfigRequest request, [FromQuery] EmailListQuery query, CancellationToken ct) => Respond(() => service.PreviewAsync(schoolId, request, query, ActorId, ct));

    [HttpGet("history")]
    public Task<IActionResult> History(ulong schoolId, [FromQuery] EmailListQuery query, CancellationToken ct) => Respond(() => service.HistoryAsync(schoolId, query, ActorId, ct));

    [HttpGet("history/{id:long}")]
    public Task<IActionResult> HistoryDetail(ulong schoolId, ulong id, CancellationToken ct) => Respond(() => service.HistoryDetailAsync(schoolId, id, ActorId, ct));

    [HttpGet("history/{id:long}/recipients")]
    public Task<IActionResult> Deliveries(ulong schoolId, ulong id, [FromQuery] EmailListQuery query, CancellationToken ct) => Respond(() => service.DeliveriesAsync(schoolId, id, query, ActorId, ct));

    [HttpPost("test")]
    public Task<IActionResult> Test(ulong schoolId, SendTestEmailRequest request, CancellationToken ct) => Respond(() => service.TestAsync(schoolId, request, ActorId, ct));

    private async Task<IActionResult> Respond<T>(Func<Task<ServiceResult<T>>> action, bool created = false)
    {
        try
        {
            var result = await action();
            if (result.IsSuccess) return StatusCode(created ? 201 : 200, ApiResponse<T>.Ok(result.Value!));
            var error = result.Error!;
            var status = error.Code switch
            {
                "VALIDATION_ERROR" => 422,
                "NOT_FOUND" => 404,
                "FORBIDDEN" => 403,
                "RATE_LIMITED" => 429,
                "EMAIL_NOT_CONFIGURED" or "UNAVAILABLE" => 503,
                _ => 409
            };
            return StatusCode(status, ApiResponse<T>.Fail(error.Code, error.Message, error.Details));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(ApiResponse<T>.Fail("STALE_VERSION", "Dữ liệu đã thay đổi. Vui lòng tải lại."));
        }
        catch (Exception ex) when (IsWriteConflict(ex))
        {
            var mysql = ex as MySqlException ?? ex.InnerException as MySqlException;
            return Conflict(ApiResponse<T>.Fail(mysql?.Number is 1205 or 1213 ? "STALE_VERSION" : "CONFLICT",
                "Dữ liệu trùng hoặc vừa được thay đổi. Vui lòng tải lại và thử lại."));
        }
    }

    private static bool IsWriteConflict(Exception error) => error is MySqlException { Number: 1062 or 1205 or 1213 or 1451 or 1452 }
        || error.InnerException is MySqlException { Number: 1062 or 1205 or 1213 or 1451 or 1452 };
}
