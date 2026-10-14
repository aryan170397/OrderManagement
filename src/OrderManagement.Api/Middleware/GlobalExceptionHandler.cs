using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderManagement.Application.Exceptions;

namespace OrderManagement.Api.Middleware;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        var mapped = Map(exception);
        if (mapped.Status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception. TraceId: {TraceId}", httpContext.TraceIdentifier);
        }
        else
        {
            logger.LogWarning(
                "Request rejected with status {StatusCode} and error code {ErrorCode}. TraceId: {TraceId}",
                mapped.Status,
                mapped.ErrorCode,
                httpContext.TraceIdentifier);
        }

        var problem = new ProblemDetails
        {
            Type = mapped.Type,
            Title = mapped.Title,
            Status = mapped.Status,
            Detail = mapped.Detail,
            Instance = httpContext.Request.Path
        };
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;
        problem.Extensions["errorCode"] = mapped.ErrorCode;

        httpContext.Response.StatusCode = mapped.Status;
        await httpContext.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken);
        return true;
    }

    private static MappedProblem Map(Exception exception) =>
        exception switch
        {
            AppException app => new MappedProblem(app.StatusCode, app.Title, app.Type, app.Message, app.ErrorCode),
            DbUpdateConcurrencyException => new MappedProblem(
                StatusCodes.Status409Conflict,
                "Conflict",
                ProblemTypes.Conflict,
                "The record was modified by another request. Refresh and retry.",
                "concurrency_conflict"),
            _ => new MappedProblem(
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.",
                ProblemTypes.Unexpected,
                "An unexpected error occurred.",
                "unexpected")
        };

    private sealed record MappedProblem(int Status, string Title, string Type, string Detail, string ErrorCode);
}
