using System.Xml.Serialization;
using LaunchDarklyPOC.Shared.Interfaces;
using LaunchDarklyPOC.Shared.Models;

namespace LaunchDarklyPOC.Shared.Utilities;

public sealed class XmlParser : IXmlParser
{
    private static readonly XmlSerializer Serializer = new(typeof(TransactionMessage));

    public TransactionMessage Deserialize(string xml)
    {
        using var reader = new StringReader(xml);
        return (TransactionMessage)Serializer.Deserialize(reader)!;
    }

    public string Serialize(TransactionMessage message)
    {
        using var writer = new StringWriter();
        Serializer.Serialize(writer, message);
        return writer.ToString();
    }
}
