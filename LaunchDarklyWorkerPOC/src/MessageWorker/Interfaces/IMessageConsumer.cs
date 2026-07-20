namespace LaunchDarklyPOC.MessageWorker.Interfaces;

public interface IMessageConsumer
{
    Task StartConsumingAsync(CancellationToken cancellationToken);
}
