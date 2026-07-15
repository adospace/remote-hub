using System.Xml.Linq;
using RemoteHub.Core.Models;

namespace RemoteHub.Core.Import;

/// <summary>
/// Imports Devolutions Remote Desktop Manager (RDM) XML exports. Only RDP entries are imported;
/// folder hierarchy is derived from the backslash-separated <c>Group</c> path. Credential fields
/// are never read.
/// </summary>
public sealed class RdmXmlImporter : IConnectionImporter
{
    // RDM's ConnectionType value for a configured RDP session. We also accept a bare "RDP".
    private static readonly HashSet<string> RdpTypeValues =
        new(StringComparer.OrdinalIgnoreCase) { "RDPConfigured", "RDP" };

    /// <inheritdoc />
    public ConnectionDocument Import(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidDataException("The RDM export is empty.");
        }

        XDocument xdoc;
        try
        {
            xdoc = XDocument.Parse(content);
        }
        catch (System.Xml.XmlException ex)
        {
            throw new InvalidDataException("The RDM export is not well-formed XML.", ex);
        }

        var root = xdoc.Root
            ?? throw new InvalidDataException("The RDM export has no root element.");

        // Accept any layout that contains <Connection> entries anywhere in the tree — real RDM
        // exports wrap them as <RDMExport><Connections><Connection>…, but flatter variants
        // (<Connections>, <ArrayOfConnection>) are equally valid. Matching is case-insensitive.
        if (!xdoc.Descendants().Any(e => NameIs(e, "Connection")))
        {
            throw new InvalidDataException(
                $"Unrecognized RDM export (root '<{root.Name.LocalName}>'): no <Connection> entries were found.");
        }

        var document = new ConnectionDocument();

        // Search the whole tree for <Connection> elements to tolerate wrapper variations.
        var connectionElements = xdoc.Descendants()
            .Where(e => NameIs(e, "Connection"));

        foreach (var element in connectionElements)
        {
            RdpConnection? connection;
            try
            {
                connection = TryBuildConnection(element);
            }
            catch
            {
                // Skip a single malformed entry rather than failing the whole import.
                continue;
            }

            if (connection is null)
            {
                // Not an RDP entry. If it's an explicit Group/folder, materialize the (possibly
                // empty) folder so the tree matches RDM even when the folder has no RDP children.
                var entryType = GetValue(element, "ConnectionType");
                if (entryType is not null && entryType.Trim().Equals("Group", StringComparison.OrdinalIgnoreCase))
                {
                    ResolveFolder(document, GetValue(element, "Group"));
                }

                continue; // Other types (TeamViewer, PowerShell, …) are skipped.
            }

            var groupPath = GetValue(element, "Group");
            var parent = ResolveFolder(document, groupPath);
            parent.Add(connection);
        }

        return document;
    }

    /// <inheritdoc />
    public ConnectionDocument ImportFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var content = File.ReadAllText(path);
        return Import(content);
    }

    /// <summary>
    /// Builds an <see cref="RdpConnection"/> from a &lt;Connection&gt; element, or returns null if
    /// the entry is not an RDP connection. Never reads any credential/password field.
    /// </summary>
    private static RdpConnection? TryBuildConnection(XElement element)
    {
        var type = GetValue(element, "ConnectionType");
        if (type is null || !RdpTypeValues.Contains(type.Trim()))
        {
            return null;
        }

        // Host: prefer Url, then HostName, then Host. May carry an inline ":port".
        var rawHost = FirstValue(element, "Url", "HostName", "Host") ?? string.Empty;
        var (host, portFromHost) = SplitHostPort(rawHost);

        // Explicit port wins over one embedded in the host; fall back to 3389.
        var port = ParsePort(FirstValue(element, "Port", "RDPPort"))
            ?? portFromHost
            ?? 3389;

        var name = GetValue(element, "Name");
        if (string.IsNullOrWhiteSpace(name))
        {
            name = string.IsNullOrWhiteSpace(host) ? "(unnamed)" : host;
        }

        // RDM nests credentials/settings under an <RDP> sub-element, e.g.
        //   <Connection><RDP><UserName>…</UserName><Domain>…</Domain></RDP></Connection>
        // so read UserName/Domain from the connection's direct children first, then from <RDP>.
        // The password (<SafePassword>, RDM-encrypted) is deliberately never read.
        var rdp = element.Elements().FirstOrDefault(e => NameIs(e, "RDP"));

        return new RdpConnection
        {
            Name = name.Trim(),
            Host = host,
            Port = port,
            Username = NullIfEmpty(FromConnectionOrRdp(element, rdp, "UserName", "Username")),
            Domain = NullIfEmpty(FromConnectionOrRdp(element, rdp, "Domain")),
            Description = NullIfEmpty(GetValue(element, "Description")),
        };
    }

    /// <summary>Reads a value from the connection's direct children, falling back to the &lt;RDP&gt; child.</summary>
    private static string? FromConnectionOrRdp(XElement element, XElement? rdp, params string[] names) =>
        FirstValue(element, names) ?? (rdp is null ? null : FirstValue(rdp, names));

    /// <summary>
    /// Walks/creates the folder chain described by a backslash-separated RDM Group path and
    /// returns the children list of the deepest folder. Empty/missing group → document roots.
    /// </summary>
    private static List<ConnectionNode> ResolveFolder(ConnectionDocument document, string? groupPath)
    {
        if (string.IsNullOrWhiteSpace(groupPath))
        {
            return document.Roots;
        }

        var segments = groupPath
            .Split('\\', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0);

        var children = document.Roots;
        foreach (var segment in segments)
        {
            var folder = children
                .OfType<FolderNode>()
                .FirstOrDefault(f => string.Equals(f.Name, segment, StringComparison.OrdinalIgnoreCase));

            if (folder is null)
            {
                folder = new FolderNode { Name = segment };
                children.Add(folder);
            }

            children = folder.Children;
        }

        return children;
    }

    private static (string Host, int? Port) SplitHostPort(string raw)
    {
        raw = raw.Trim();
        if (raw.Length == 0)
        {
            return (string.Empty, null);
        }

        // Only treat a single trailing ":<number>" as a port. Leave IPv6/other colons alone.
        var idx = raw.LastIndexOf(':');
        if (idx > 0 && idx < raw.Length - 1 && raw.IndexOf(':') == idx)
        {
            var portPart = raw[(idx + 1)..];
            if (int.TryParse(portPart, out var p) && p is > 0 and <= 65535)
            {
                return (raw[..idx].Trim(), p);
            }
        }

        return (raw, null);
    }

    private static int? ParsePort(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && int.TryParse(value.Trim(), out var p)
            && p is > 0 and <= 65535)
        {
            return p;
        }

        return null;
    }

    /// <summary>Reads the first direct/descendant child element value matching any of the names.</summary>
    private static string? FirstValue(XElement parent, params string[] names)
    {
        foreach (var name in names)
        {
            var value = GetValue(parent, name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>
    /// Reads a child element's text by case-insensitive local name. Only looks at direct children
    /// so nested &lt;Connection&gt; data (if any) does not bleed across entries.
    /// </summary>
    private static string? GetValue(XElement parent, string name)
    {
        var child = parent.Elements().FirstOrDefault(e => NameIs(e, name));
        if (child is not null)
        {
            return child.Value;
        }

        // Also tolerate the value being stored as an attribute.
        var attr = parent.Attributes()
            .FirstOrDefault(a => string.Equals(a.Name.LocalName, name, StringComparison.OrdinalIgnoreCase));
        return attr?.Value;
    }

    private static bool NameIs(XElement element, string name) =>
        string.Equals(element.Name.LocalName, name, StringComparison.OrdinalIgnoreCase);

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
