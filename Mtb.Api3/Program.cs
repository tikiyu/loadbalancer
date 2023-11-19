var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var requestCount = 0;  // Variable to track the number of requests

app.MapGet("/api", async (HttpContext context) =>
{
    // Try to get the delay from the query string; use default if not provided or invalid
    if (!int.TryParse(context.Request.Query["delayInMs"], out int delayInMs))
    {
        delayInMs = requestCount < 3 ? 400 : 100;  // 400ms for the first three requests, 100ms for others
    }

    requestCount++;  // Increment the request count after handling each request
    await Task.Delay(delayInMs);
    return $"Mtb.Api3";
});

app.Run();
