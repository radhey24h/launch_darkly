using LaunchDarklyPOC.Shared.Models;

namespace LaunchDarklyPOC.Shared.Interfaces;

public interface IXmlParser
{
    TransactionMessage Deserialize(string xml);
    string Serialize(TransactionMessage message);
}
