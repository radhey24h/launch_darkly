namespace LaunchDarklyPOC.MessageWorker.Interfaces;

public interface ILaunchDarklyService
{
    string EvaluateRoutingFlag(string accountId);
    bool IsInitialized { get; }
}
