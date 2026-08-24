using System.Diagnostics;
using Bokmal.Database;

namespace Bokmal.Api.Observability;

/// <summary>
/// Ties every log line produced while handling one request to the request that caused it.
///
/// A single borrow crosses two processes: the browser posts to a Next.js server action,
/// which calls this API. Without a shared identifier those are three separate stories in two
/// log streams, and reconstructing one of them means matching timestamps by eye. With one,
/// grepping the id returns the whole thing in order.
///
/// The id is taken from the incoming header when there is one, so the caller's id wins and
/// the chain stays unbroken. It is generated only at the outermost edge that has none.
/// </summary>
public static class CorrelationId
{
    public const string HeaderName = "X-Correlation-Id";

    /// <summary>The key the id appears under in log scopes, and in problem responses.</summary>
    public const string LogPropertyName = "CorrelationId";

    public static string Of(HttpContext context)
        => context.Items.TryGetValue(LogPropertyName, out var value) && value is string id
            ? id
            : "unknown";
}

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[CorrelationId.HeaderName].FirstOrDefault();

        var correlationId = string.IsNullOrWhiteSpace(incoming)
            ? BokmalId.New().ToString()
            : incoming;

        context.Items[CorrelationId.LogPropertyName] = correlationId;
        context.Response.Headers[CorrelationId.HeaderName] = correlationId;

        // A logger scope rather than a parameter. Scopes are ambient for the rest of the
        // async flow, so every log line from every service below picks the id up without
        // anything having to thread it down by hand.
        //
        // Written as a message template rather than a dictionary on purpose. The console
        // formatter renders a scope by calling ToString() on it, and a dictionary renders as
        // its own type name -- the id is carried but invisible. A template gives a readable
        // line for a human and keeps the value a named property for a structured sink.
        using var scope = logger.BeginScope("CorrelationId:{CorrelationId}", correlationId);

        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            await next(context);
        }
        finally
        {
            // One line per request, which the framework's own logging does not give us at
            // the Warning level this app runs it at. Without it the log shows what the
            // borrow flow chose to say and no evidence that anybody called anything.
            logger.LogInformation(
                "{Method} {Path} responded {StatusCode} in {ElapsedMs}ms",
                context.Request.Method,
                context.Request.Path.Value,
                context.Response.StatusCode,
                (int)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}
