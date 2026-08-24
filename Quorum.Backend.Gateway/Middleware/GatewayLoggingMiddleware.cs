namespace Quorum.Backend.Gateway.Middleware;

public class GatewayLoggingMiddleware
{
    private const string CorrelationIdHeaderName = "X-Correlation-ID";
    private readonly RequestDelegate _next;
    private readonly ILogger<GatewayLoggingMiddleware> _logger;

    public GatewayLoggingMiddleware(RequestDelegate next, ILogger<GatewayLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 1. Obsługa / Generowanie Correlation ID
        if (!context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
            context.Request.Headers.Add(CorrelationIdHeaderName, correlationId);
        }

        // Dodanie Correlation ID do odpowiedzi
        context.Response.Headers[CorrelationIdHeaderName] = correlationId;

        var startTime = DateTime.UtcNow;
        _logger.LogInformation("[Gateway] HTTP {Method} {Path} Started | CorrelationId: {CorrelationId}",
            context.Request.Method,
            context.Request.Path,
            correlationId);

        try
        {
            // 2. Przekazanie żądania dalej w potoku (Pipeline)
            await _next(context);
        }
        finally
        {
            var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogInformation("[Gateway] HTTP {Method} {Path} Responded {StatusCode} in {ElapsedMs}ms | CorrelationId: {CorrelationId}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                elapsedMs,
                correlationId);
        }
    }
}

// Extension Method ułatwiająca rejestrację middleware w Program.cs
public static class GatewayLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseGatewayLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GatewayLoggingMiddleware>();
    }
}