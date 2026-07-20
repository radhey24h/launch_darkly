using LaunchDarklyPOC.Shared.Interfaces;
using LaunchDarklyPOC.Shared.Utilities;
using LaunchDarklyPOC.XmlProducer.Configuration;
using LaunchDarklyPOC.XmlProducer.Interfaces;
using LaunchDarklyPOC.XmlProducer.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

            services.AddSingleton<IXmlParser, XmlParser>();
            services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();
            services.AddHostedService<ProducerService>();
        })
        .Build();

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "XmlProducer terminated unexpectedly.");
}
finally
{
    await Log.CloseAndFlushAsync();
}
