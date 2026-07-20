namespace LaunchDarklyPOC.MessageWorker.Models;

public sealed record RoutingResult(
    string Destination,
    string Variation,
    int HttpStatusCode,
    long ProcessingTimeMs,
    bool Success,
    string? ErrorMessage = null
);
