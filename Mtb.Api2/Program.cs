var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();


var random = new Random();

app.MapGet("/api", async (HttpContext context) =>
{
    // Try to get the delay from the query string; use default if not provided or invalid
    if (!int.TryParse(context.Request.Query["delayInMs"], out int delayInMs))
    {
        delayInMs = random.Next(90, 100);
    }

    await Task.Delay(delayInMs);
    return $"Mtb.Api2";
});

app.Run();
