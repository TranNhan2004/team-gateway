using System.Net.Http.Headers;
using TeamGateway.Api.Services;
using Yarp.ReverseProxy.Transforms;

namespace TeamGateway.Api.Extensions;

public static class ReverseProxyExtensions
{
    public static IServiceCollection AddGatewayReverseProxy(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddReverseProxy()
            .LoadFromConfig(configuration.GetSection("ReverseProxy"))
            .AddTransforms(builderContext =>
            {
                builderContext.AddRequestTransform(context =>
                {
                    var tokenIssuer = context.HttpContext.RequestServices.GetRequiredService<IInternalJwtIssuer>();
                    var token = tokenIssuer.Create(context.HttpContext.User);
                    context.ProxyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    return ValueTask.CompletedTask;
                });
            });

        return services;
    }
}
