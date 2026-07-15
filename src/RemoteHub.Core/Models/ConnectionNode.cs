using System.Text.Json.Serialization;

namespace RemoteHub.Core.Models;

/// <summary>
/// Abstract base for anything that can live in the connection tree: a folder or a connection.
/// Polymorphic JSON serialization is configured here so the serializer can round-trip the
/// concrete derived types via a "$type" discriminator.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(FolderNode), "folder")]
[JsonDerivedType(typeof(RdpConnection), "rdp")]
public abstract class ConnectionNode
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;
}
