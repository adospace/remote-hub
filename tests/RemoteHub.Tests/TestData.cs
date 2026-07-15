using RemoteHub.Core.Models;

namespace RemoteHub.Tests;

/// <summary>
/// Shared test helpers: locating the sample RDM export and flattening a document's tree.
/// </summary>
internal static class TestData
{
    /// <summary>
    /// Loads the representative RDM export. Prefers the on-disk <c>docs/sample-rdm-export.xml</c>
    /// (found by walking up from the test output directory) and falls back to an embedded copy so
    /// the tests stay deterministic regardless of the working directory / build layout.
    /// </summary>
    public static string SampleRdmExport()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "docs", "sample-rdm-export.xml");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            dir = dir.Parent;
        }

        return EmbeddedSample;
    }

    /// <summary>Depth-first enumeration of every RDP connection in the document.</summary>
    public static IEnumerable<RdpConnection> AllConnections(ConnectionDocument doc) =>
        AllConnections(doc.Roots);

    private static IEnumerable<RdpConnection> AllConnections(IEnumerable<ConnectionNode> nodes)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case RdpConnection rdp:
                    yield return rdp;
                    break;
                case FolderNode folder:
                    foreach (var child in AllConnections(folder.Children))
                    {
                        yield return child;
                    }

                    break;
            }
        }
    }

    /// <summary>Finds a folder by (case-insensitive) name among a node list.</summary>
    public static FolderNode? Folder(IEnumerable<ConnectionNode> nodes, string name) =>
        nodes.OfType<FolderNode>()
            .FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Finds a connection by (case-insensitive) name among a node list.</summary>
    public static RdpConnection? Connection(IEnumerable<ConnectionNode> nodes, string name) =>
        nodes.OfType<RdpConnection>()
            .FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

    private const string EmbeddedSample = """
        <?xml version="1.0" encoding="utf-8"?>
        <Connections>
          <Connection>
            <Name>Web01</Name>
            <ConnectionType>RDPConfigured</ConnectionType>
            <Group>Production\Web Servers</Group>
            <Url>web01.contoso.com</Url>
            <UserName>svc_web</UserName>
            <Domain>CONTOSO</Domain>
            <Description>Primary web front-end</Description>
            <Password>THIS-MUST-BE-IGNORED</Password>
          </Connection>
          <Connection>
            <Name>Web02</Name>
            <ConnectionType>RDPConfigured</ConnectionType>
            <Group>Production\Web Servers</Group>
            <HostName>web02.contoso.com:3390</HostName>
            <UserName>svc_web</UserName>
            <Domain>CONTOSO</Domain>
          </Connection>
          <Connection>
            <Name>DB01</Name>
            <ConnectionType>RDPConfigured</ConnectionType>
            <Group>Production\Databases</Group>
            <Host>db01.contoso.com</Host>
            <RDPPort>3391</RDPPort>
            <UserName>dba</UserName>
            <Domain>CONTOSO</Domain>
            <Description>SQL primary</Description>
          </Connection>
          <Connection>
            <Name>Lab-DC</Name>
            <ConnectionType>RDP</ConnectionType>
            <Group>Lab</Group>
            <Url>10.0.0.10</Url>
            <UserName>administrator</UserName>
          </Connection>
          <Connection>
            <Name>Jump Box</Name>
            <ConnectionType>RDPConfigured</ConnectionType>
            <Url>jump.contoso.com</Url>
            <Description>Root-level bastion (no group)</Description>
          </Connection>
          <Connection>
            <Name>edge-router</Name>
            <ConnectionType>SSHShell</ConnectionType>
            <Group>Network</Group>
            <Host>edge.contoso.com</Host>
            <UserName>netadmin</UserName>
            <Password>SSH-SECRET-IGNORE</Password>
          </Connection>
          <Connection>
            <Name>Production</Name>
            <ConnectionType>Group</ConnectionType>
            <Group>Production</Group>
          </Connection>
        </Connections>
        """;
}
