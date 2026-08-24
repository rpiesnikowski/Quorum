namespace Quorum.Backend.Gateway.Middleware;

public static class MiddlewareExtensions
{
    public static IApplicationBuilder UseGatewayLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GatewayLoggingMiddleware>();
    }
    public static IApplicationBuilder UseProxy2ManyHostsMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<Proxy2ManyHostsMiddleware>();
    }
}