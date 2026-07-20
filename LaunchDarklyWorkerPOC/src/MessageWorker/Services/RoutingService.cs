using System.Diagnostics;
using LaunchDarklyPOC.MessageWorker.Interfaces;
using LaunchDarklyPOC.MessageWorker.Models;
using LaunchDarklyPOC.Shared.Constants;
using LaunchDarklyPOC.Shared.Models;
using Microsoft.Extensions.Logging;

namespace LaunchDarklyPOC.MessageWorker.Services;

public sealed class RoutingService : IRoutingService
{
    private readonly ILaunchDarklyService _ldService;
    private readonly IPythonClient _pythonClient;
    private readonly IDotNetClient _dotNetClient;
    private readonly ILogger<RoutingService> _logger;

    public RoutingService(
        ILaunchDarklyService ldService,
        IPythonClient pythonClient,
        IDotNetClient dotNetClient,
        ILogger<RoutingService> logger)
    {
        _ldService = ldService;
        _pythonClient = pythonClient;
        _dotNetClient = dotNetClient;
        _logger = logger;
    }

    public async Task<RoutingResult> RouteAsync(TransactionMessage message, CancellationToken cancellationToken = default)
    {
        var variation = _ldService.EvaluateRoutingFlag(message.AccountId);
        var destination = variation == AppConstants.DotNetVariation
            ? "DotNetMiddleware"
            : "PythonMiddleware";

        _logger.LogInformation(
            "Routing | AccountId={AccountId} FlagKey={FlagKey} Variation={Variation} → {Destination}",
            message.AccountId, AppConstants.FeatureFlagKey, variation, destination);

        var request = MapToProcessRequest(message);
        var sw = Stopwatch.StartNew();

        try
        {
            var (statusCode, isSuccess) = variation == AppConstants.DotNetVariation
                ? await _dotNetClient.ProcessAsync(request, cancellationToken)
                : await _pythonClient.ProcessAsync(request, cancellationToken);

            sw.Stop();

            return new RoutingResult(destination, variation, statusCode, sw.ElapsedMilliseconds, isSuccess);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Error calling {Destination} for AccountId={AccountId}", destination, message.AccountId);
            return new RoutingResult(destination, variation, 500, sw.ElapsedMilliseconds, false, ex.Message);
        }
    }

    private static ProcessRequest MapToProcessRequest(TransactionMessage m) => new()
    {
        AccountId = m.AccountId,
        CustomerId = m.CustomerId,
        TransactionId = m.TransactionId,
        Amount = m.Amount,
        Currency = m.Currency,
        Timestamp = m.Timestamp,
        Operation = m.Operation,
        ReferenceNumber = m.ReferenceNumber,
        CorrelationId = m.CorrelationId,
        Status = m.Status,
        RoutedFrom = "MessageWorker"
    };
}
