using System.Text.Json;
using System.Text.Json.Serialization;
using RemoteHub.Core.Models;

namespace RemoteHub.Core.Serialization;

/// <summary>
/// Serializes and deserializes <see cref="ConnectionDocument"/> to/from JSON. Polymorphic
/// handling of folder vs. connection nodes is configured on <see cref="ConnectionNode"/>.
/// </summary>
public sealed class ConnectionSerializer
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    public string Serialize(ConnectionDocument doc) =>
        JsonSerializer.Serialize(doc, Options);

    public ConnectionDocument Deserialize(string json) =>
        JsonSerializer.Deserialize<ConnectionDocument>(json, Options)
            ?? new ConnectionDocument();
}
