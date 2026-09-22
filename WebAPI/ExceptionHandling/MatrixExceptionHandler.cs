using System.Text.Json;
using Application.Common;
using Domain.Entities.QuestionBank;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.ExceptionHandling;

public sealed class MatrixExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var result = Map(exception);
        if (result is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = result.StatusCode;
        httpContext.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = result.StatusCode,
            Title = result.Title,
            Detail = result.Detail,
            Type = $"https://httpstatuses.com/{result.StatusCode}"
        };
        problem.Extensions["code"] = result.Code;

        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }

    private static ErrorResult? Map(Exception exception)
    {
        if (exception is UnauthorizedAccessException)
        {
            return new ErrorResult(
                401,
                "Chưa xác thực",
                string.IsNullOrWhiteSpace(exception.Message) ? "Cần đăng nhập để thực hiện thao tác này." : exception.Message,
                "Unauthorized");
        }

        if (exception is MatrixApplicationException applicationException)
        {
            return FromCode(applicationException.Code, applicationException.Message, 400);
        }

        // Domain rules and the repositories (persistence constraints, reference checks) use the same codes.
        if (exception is MatrixDomainException domainException)
        {
            return FromCode(domainException.Code, domainException.Message, 422);
        }

        if (exception is ArgumentException or JsonException)
        {
            return new ErrorResult(400, "Yêu cầu không hợp lệ", "Không thể xử lý yêu cầu.", "BadRequest");
        }

        return null;
    }

    private static ErrorResult FromCode(string code, string message, int defaultStatus)
    {
        var statusCode = code switch
        {
            "Forbidden" => 403,
            "NotFound" or "TaskNotFound" => 404,
            "ConcurrencyConflict" or
                "TaskAlreadyHasMatrix" or
                "PersistenceConflict" or
                "InvalidTransition" or
                "DirectMatrixRequired" or
                "MatrixNotEditable" => 409,
            "InvalidRequest" or
                "EmptyMatrix" or
                "InvalidDetail" or
                "InvalidTotalScore" or
                "DuplicateDetail" or
                "InvalidReference" or
                "InvalidAssignee" or
                "InvalidTaskType" or
                "TaskScopeRequired" or
                "TaskImmutable" => 422,
            _ => defaultStatus
        };

        return new ErrorResult(
            statusCode,
            statusCode == 409 ? "Xung đột dữ liệu" : "Yêu cầu ma trận không hợp lệ",
            message,
            code);
    }

    private sealed record ErrorResult(
        int StatusCode,
        string Title,
        string Detail,
        string Code);
}
