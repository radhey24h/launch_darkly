using Gateway.Clients;
using Gateway.Middleware;
using Gateway.Options;
using Gateway.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using System.Reflection;

// ============================================================
// GATEWAY — ASP.NET Core Web API
// Port: 5000 (HTTP) / 5443 (HTTPS)
//
// This is the single entry point for all client requests.
// It evaluates the LaunchDarkly feature flag and routes
// traffic to either the .NET backend or the Python backend.
// ============================================================

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// CONFIGURATION — Options Pattern
// Bind strongly-typed options from appsettings.json sections.
// ============================================================
builder.Services.Configure<LaunchDarklyOptions>(
    builder.Configuration.GetSection(LaunchDarklyOptions.SectionName));

builder.Services.Configure<BackendOptions>(
    builder.Configuration.GetSection(BackendOptions.SectionName));

// ============================================================
// LAUNCHDARKLY — Singleton
// The SDK must be a singleton: it manages streaming connections,
// in-memory flag store, and event queue.
// ============================================================
builder.Services.AddSingleton<ILaunchDarklyService, LaunchDarklyService>();

// ============================================================
// TYPED HTTP CLIENTS via HttpClientFactory
//
// Why AddHttpClient<TClient, TImpl>(name)?
//   - HttpClientFactory manages HttpMessageHandler lifecycle.
//   - Prevents socket exhaustion (unlike new HttpClient()).
//   - Handlers are pooled and recycled (default: every 2 minutes).
//   - Base URLs are configured once here, not in client classes.
// ============================================================

// Read backend URLs from configuration at startup for registration.
var backendOptions = builder.Configuration
    .GetSection(BackendOptions.SectionName)
    .Get<BackendOptions>() ?? new BackendOptions();

// .NET backend typed client
builder.Services.AddHttpClient<IDotNetBackendClient, DotNetBackendClient>(client =>
{
    client.BaseAddress = new Uri(backendOptions.DotNetBackendUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Python backend typed client (mock TIBCO)
builder.Services.AddHttpClient<IPythonBackendClient, PythonBackendClient>(client =>
{
    client.BaseAddress = new Uri(backendOptions.PythonBackendUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// ============================================================
// ROUTING SERVICE
// Encapsulates LaunchDarkly evaluation + backend dispatch.
// Scoped lifetime: created per request, disposed after.
// ============================================================
builder.Services.AddScoped<IBackendRouter, BackendRouter>();

// ============================================================
// ASP.NET CORE FRAMEWORK SERVICES
// ============================================================
builder.Services.AddControllers();

// ============================================================
// SWAGGER / OPENAPI
// Available at /swagger in Development.
// ============================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "LaunchDarkly Canary POC — Gateway",
        Version = "v1",
        Description = """
            ASP.NET Core Gateway that evaluates a LaunchDarkly feature flag
            to route traffic between the new .NET backend (10%) and the
            legacy Python/TIBCO backend (90%).
            
            Try different userIds to observe deterministic sticky routing:
              • alice, bob, charlie, david, emma
            """,
        Contact = new OpenApiContact
        {
            Name = "Platform Engineering",
            Email = "platform@yourcompany.com"
        }
    });

    // Include XML comments from the controller for richer Swagger UI.
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});

// ============================================================
// HEALTH CHECKS
// GET /health — returns 200 OK with component status.
// ============================================================
builder.Services.AddHealthChecks()
    .AddCheck<LaunchDarklyHealthCheck>("launchdarkly");

// ============================================================
// LOGGING
// Console + Debug providers included by default.
// Add Serilog/OpenTelemetry for production.
// ============================================================
builder.Logging.AddConsole();

// ============================================================
// BUILD
// ============================================================
var app = builder.Build();

// ============================================================
// MIDDLEWARE PIPELINE
// Order matters! Register in this exact sequence.
// ============================================================

// 1. Correlation ID — must be FIRST so all subsequent middleware has an ID.
app.UseMiddleware<CorrelationIdMiddleware>();

// 2. Request logging — after Correlation ID so we can log the ID.
app.UseMiddleware<RequestLoggingMiddleware>();

// 3. Exception handling — catches unhandled exceptions from downstream.
app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            title = "Internal Server Error",
            status = 500,
            detail = "An unexpected error occurred. Please contact support.",
            correlationId = context.Items[CorrelationIdMiddleware.CorrelationIdKey]
        });
    });
});

// 4. Swagger UI (all environments for POC; restrict to Development in production).
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Gateway v1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "LaunchDarkly Canary Gateway";
});

// 5. Routing
app.UseRouting();

// 6. Controllers
app.MapControllers();

// 7. Health check endpoint
app.MapHealthChecks("/health");

// 8. Version endpoint (minimal API inline)
app.MapGet("/version", () => new
{
    service = "Gateway",
    version = "1.0.0",
    framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
    timestamp = DateTime.UtcNow
})
.WithName("GetVersion")
.WithTags("System");

app.Run();

// ============================================================
// LAUNCHDARKLY HEALTH CHECK
// Reports whether the SDK is connected and receiving flag data.
// ============================================================

/// <summary>
/// Health check that verifies the LaunchDarkly SDK is initialised.
/// Exposed at GET /health — integrated with Kubernetes liveness/readiness probes.
/// </summary>
public sealed class LaunchDarklyHealthCheck : IHealthCheck
{
    private readonly ILaunchDarklyService _ldService;

    public LaunchDarklyHealthCheck(ILaunchDarklyService ldService)
    {
        _ldService = ldService;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_ldService.IsInitialized)
        {
            return Task.FromResult(
                HealthCheckResult.Healthy("LaunchDarkly SDK is connected and serving flags."));
        }

        return Task.FromResult(
            HealthCheckResult.Degraded(
                "LaunchDarkly SDK is not fully initialised. " +
                "Using fallback variation for flag evaluations."));
    }
}
