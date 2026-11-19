using LocalRag.Api.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LocalRag.Api.Middleware;

internal sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            OllamaException ollamaException => new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Local model request failed",
                Detail = ollamaException.Message,
                Instance = httpContext.Request.Path
            },
            HttpRequestException => new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Local model service unavailable",
                Detail = "Could not connect to Ollama. Check that it is running and that Ollama:BaseUrl is correct.",
                Instance = httpContext.Request.Path
            },
            _ => null
        };

        if (problem is null)
        {
            return false;
        }

        logger.LogWarning(exception, "A local model request failed.");
        httpContext.Response.StatusCode = problem.Status!.Value;
        await Results.Problem(problem).ExecuteAsync(httpContext);
        return true;
    }
}
