using LaunchDarklyConsolePOC.Configuration;
using LaunchDarklyConsolePOC.Generator;
using LaunchDarklyConsolePOC.Interfaces;
using LaunchDarklyConsolePOC.Routing;
using LaunchDarklyConsolePOC.Services;
using LaunchDarklyConsolePOC.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LaunchDarklyConsolePOC.Extensions;

/// <summary>
/// Centralises all service registrations for the LaunchDarklyConsolePOC project.
/// Called once from Program.cs — keeps the entry point free of wiring noise.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all services, options, and infrastructure required by the POC.
    /// </summary>
    public static IServiceCollection AddLaunchDarklyConsolePOC(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Options (bound from appsettings.json) ─────────────────────────
        services.Configure<LaunchDarklyOptions>(
            configuration.GetSection(LaunchDarklyOptions.SectionName));

        services.Configure<AppOptions>(
            configuration.GetSection(AppOptions.SectionName));

        // ── Routing (singleton: one LdClient for the lifetime of the app) ─
        services.AddSingleton<IRoutingService, LaunchDarklyRoutingService>();

        // ── Destination services ──────────────────────────────────────────
        // Both are registered under IDestinationService.
        // IEnumerable<IDestinationService> in OrderProcessingService resolves
        // all of them; the processor selects the right one by VariationName.
        services.AddSingleton<IDestinationService, PythonDestinationService>();
        services.AddSingleton<IDestinationService, DotNetDestinationService>();

        // ── Core pipeline ─────────────────────────────────────────────────
        services.AddSingleton<IOrderGenerator, OrderGenerator>();
        services.AddSingleton<IOrderProcessor, OrderProcessingService>();

        // ── Validation ────────────────────────────────────────────────────
        services.AddSingleton<IConsistencyValidator, ConsistencyValidator>();

        return services;
    }
}
