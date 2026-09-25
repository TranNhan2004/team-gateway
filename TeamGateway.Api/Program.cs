using Serilog;
using TeamGateway.Api.Extensions;
using TeamGateway.Api.Middlewares;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGatewaySecurity(builder.Configuration, builder.Environment);
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("gateway-session", policy => policy.RequireAuthenticatedUser());
});
builder.Services.AddControllers();
builder.Services.AddGatewayReverseProxy(builder.Configuration);

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCors", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    });
});

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services);
});

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseRouting();
app.UseCors("DefaultCors");
app.UseAuthentication();
app.UseMiddleware<AntiforgeryValidationMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapReverseProxy();

app.Run();
