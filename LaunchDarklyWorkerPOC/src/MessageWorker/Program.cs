using LaunchDarklyPOC.MessageWorker;
using LaunchDarklyPOC.MessageWorker.Configuration;
using LaunchDarklyPOC.MessageWorker.Interfaces;
using LaunchDarklyPOC.MessageWorker.Services;
using LaunchDarklyPOC.Shared.Interfaces;
using LaunchDarklyPOC.Shared.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Serilog;

const string OutputTemplate =
    "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: OutputTemplate)
    .CreateBootstrapLogger();

try
{
    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog((context, services, configuration) =>
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate: OutputTemplate))
        .ConfigureServices((context, services) =>
        {
            services.Configure<RabbitMqOptions>(
                context.Configuration.GetSection(RabbitMqOptions.SectionName));
            services.Configure<LaunchDarklyOptions>(
                context.Configuration.GetSection(LaunchDarklyOptions.SectionName));
            services.Configure<MiddlewareOptions>(
                context.Configuration.GetSection(MiddlewareOptions.SectionName));

            var middlewareConfig = context.Configuration
                .GetSection(MiddlewareOptions.SectionName)
                .Get<MiddlewareOptions>() ?? new MiddlewareOptions();

            services.AddHttpClient("PythonMiddleware", client =>
            {
                client.BaseAddress = new Uri(middlewareConfig.Python.BaseUrl);
            })
            .AddStandardResilienceHandler(options =>
            {
                options.Retry.MaxRetryAttempts = middlewareConfig.Python.RetryCount;
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(middlewareConfig.Python.TimeoutSeconds);
                // Circuit breaker sampling duration must be >= 2x the attempt timeout.
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(middlewareConfig.Python.TimeoutSeconds * 2);
                options.TotalRequestTimeout.Timeout =
                    TimeSpan.FromSeconds(middlewareConfig.Python.TimeoutSeconds * (middlewareConfig.Python.RetryCount + 2));
            });

            services.AddHttpClient("DotNetMiddleware", client =>
            {
                client.BaseAddress = new Uri(middlewareConfig.DotNet.BaseUrl);
            })
            .AddStandardResilienceHandler(options =>
            {
                options.Retry.MaxRetryAttempts = middlewareConfig.DotNet.RetryCount;
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(middlewareConfig.DotNet.TimeoutSeconds);
                // Circuit breaker sampling duration must be >= 2x the attempt timeout.
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(middlewareConfig.DotNet.TimeoutSeconds * 2);
                options.TotalRequestTimeout.Timeout =
                    TimeSpan.FromSeconds(middlewareConfig.DotNet.TimeoutSeconds * (middlewareConfig.DotNet.RetryCount + 2));
            });

            services.AddSingleton<IXmlParser, XmlParser>();
            services.AddSingleton<ILaunchDarklyService, LaunchDarklyService>();
            services.AddSingleton<IPythonClient, PythonMiddlewareClient>();
            services.AddSingleton<IDotNetClient, DotNetMiddlewareClient>();
            services.AddSingleton<IRoutingService, RoutingService>();
            services.AddSingleton<IMessageConsumer, RabbitMqConsumer>();
            services.AddHostedService<Worker>();
        })
        .Build();

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "MessageWorker terminated unexpectedly.");
}
finally
{
    await Log.CloseAndFlushAsync();
}
