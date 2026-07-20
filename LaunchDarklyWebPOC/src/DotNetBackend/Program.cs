// ============================================================
// DOTNET BACKEND — ASP.NET Core Minimal API
// Port: 5001
//
// This service represents the NEW .NET implementation that is
// gradually replacing the legacy TIBCO/Python system.
//
// In the canary deployment:
//   - Initially receives 10% of production traffic.
//   - Traffic percentage increases as confidence grows.
//   - Enabled/disabled by updating LaunchDarkly rollout (no redeploy needed).
//
// This is intentionally minimal — in a real scenario this would
// contain the full business logic migrated from TIBCO.
// ============================================================

using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "LaunchDarkly Canary POC — .NET Backend",
        Version = "v1",
        Description = "The new .NET backend service. Receives 10% of traffic in the initial canary rollout."
    });
});

// Structured logging
builder.Logging.AddConsole();

var app = builder.Build();

// Always show Swagger in this POC
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", ".NET Backend v1");
    c.RoutePrefix = "swagger";
});

// ============================================================
// ENDPOINT: GET /api/orders/{userId}
//
// Returns a sample order response identifying this as the .NET backend.
// The "backend" field in the response lets testers confirm routing is working.
// ============================================================
app.MapGet("/api/orders/{userId}", (
    string userId,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    // Log incoming request — visible in the terminal when debugging.
    // In production, use structured logging with correlation IDs.
    logger.LogInformation(
        ".NET backend called for UserId={UserId} at {Timestamp}",
        userId, DateTime.UtcNow);

    // Simulate a stable orderId derived from the userId hash.
    // This ensures the same user always gets the same orderId (deterministic).
    // In production, this would query a real database or service.
    var orderId = Math.Abs(userId.GetHashCode() % 10000);

    var response = new
    {
        // "dotnet" is the variation key from LaunchDarkly.
        // The Gateway checks this field to confirm routing worked correctly.
        backend = "dotnet",
        orderId,
        customer = userId.ToUpperInvariant(),
        message = "Response from .NET Backend",
        timestamp = DateTime.UtcNow.ToString("O"), // ISO-8601 UTC
        userId
    };

    logger.LogInformation(
        ".NET backend returning OrderId={OrderId} for UserId={UserId}",
        orderId, userId);

    return Results.Ok(response);
})
.WithName("GetOrder")
.WithSummary("Get order by user ID")
.WithDescription("Returns a sample order. This is the new .NET implementation in the canary rollout.")
.WithTags("Orders");

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = ".NET Backend",
    timestamp = DateTime.UtcNow
}))
.WithName("Health")
.WithTags("System");

// Version endpoint
app.MapGet("/version", () => Results.Ok(new
{
    service = "DotNetBackend",
    version = "1.0.0",
    framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
}))
.WithName("Version")
.WithTags("System");

app.Run();
