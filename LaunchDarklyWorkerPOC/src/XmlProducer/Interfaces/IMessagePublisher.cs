namespace LaunchDarklyPOC.XmlProducer.Interfaces;

public interface IMessagePublisher
{
    Task PublishAsync(string xmlMessage, string messageId, CancellationToken cancellationToken = default);
}
