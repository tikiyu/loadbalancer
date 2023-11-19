using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
builder.Services.AddSingleton<LoadBalancerService>();

var app = builder.Build();

var loadBalancer = app.Services.GetRequiredService<LoadBalancerService>();

app.MapGet("/api", async (HttpContext context) =>
{
    var response = await loadBalancer.ForwardRequest(context.Request);
    await context.Response.WriteAsync(await response.Content.ReadAsStringAsync());
});

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        var result = report.Status == HealthStatus.Healthy ? "Load Balancer is healthy" : "Load Balancer is unhealthy";
        context.Response.ContentType = "text/plain";
        await context.Response.WriteAsync(result);
    }
});


app.Run();
