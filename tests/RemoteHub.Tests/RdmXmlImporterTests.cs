using RemoteHub.Core.Import;
using RemoteHub.Core.Models;
using RemoteHub.Core.Serialization;
using Xunit;

namespace RemoteHub.Tests;

public class RdmXmlImporterTests
{
    private static ConnectionDocument ImportSample()
    {
        var importer = new RdmXmlImporter();
        return importer.Import(TestData.SampleRdmExport());
    }

    [Fact]
    public void ImportsOnlyRdpEntries_SkippingSshAndGroupOnly()
    {
        var doc = ImportSample();

        var names = TestData.AllConnections(doc).Select(c => c.Name).ToList();

        Assert.Equal(5, names.Count);
        Assert.Contains("Web01", names);
        Assert.Contains("Web02", names);
        Assert.Contains("DB01", names);
        Assert.Contains("Lab-DC", names);
        Assert.Contains("Jump Box", names);

        // SSH entry and the Group-only entry must not become connections.
        Assert.DoesNotContain("edge-router", names);
        Assert.DoesNotContain("Production", names); // "Production" only exists as a folder.
    }

    [Fact]
    public void BuildsNestedFolderTreeFromGroupPaths()
    {
        var doc = ImportSample();

        var production = TestData.Folder(doc.Roots, "Production");
        Assert.NotNull(production);

        var webServers = TestData.Folder(production!.Children, "Web Servers");
        Assert.NotNull(webServers);
        Assert.NotNull(TestData.Connection(webServers!.Children, "Web01"));
        Assert.NotNull(TestData.Connection(webServers.Children, "Web02"));

        var databases = TestData.Folder(production.Children, "Databases");
        Assert.NotNull(databases);
        Assert.NotNull(TestData.Connection(databases!.Children, "DB01"));

        var lab = TestData.Folder(doc.Roots, "Lab");
        Assert.NotNull(lab);
        Assert.NotNull(TestData.Connection(lab!.Children, "Lab-DC"));
    }

    [Fact]
    public void PlacesGrouplessConnectionAtRoot()
    {
        var doc = ImportSample();

        var jump = TestData.Connection(doc.Roots, "Jump Box");
        Assert.NotNull(jump);
        Assert.Equal("jump.contoso.com", jump!.Host);
    }

    [Fact]
    public void ReusesFoldersRatherThanDuplicating()
    {
        var doc = ImportSample();

        // Only one "Production" folder even though multiple entries reference it.
        var productionFolders = doc.Roots
            .OfType<FolderNode>()
            .Count(f => string.Equals(f.Name, "Production", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(1, productionFolders);
    }

    [Fact]
    public void MapsFields_HostUsernameDomainDescription()
    {
        var doc = ImportSample();

        var web01 = TestData.AllConnections(doc).Single(c => c.Name == "Web01");
        Assert.Equal("web01.contoso.com", web01.Host);
        Assert.Equal(3389, web01.Port); // default when none specified
        Assert.Equal("svc_web", web01.Username);
        Assert.Equal("CONTOSO", web01.Domain);
        Assert.Equal("Primary web front-end", web01.Description);
    }

    [Fact]
    public void ParsesPortFromInlineHost()
    {
        var doc = ImportSample();

        var web02 = TestData.AllConnections(doc).Single(c => c.Name == "Web02");
        Assert.Equal("web02.contoso.com", web02.Host);
        Assert.Equal(3390, web02.Port);
    }

    [Fact]
    public void ExplicitRdpPortWins()
    {
        var doc = ImportSample();

        var db01 = TestData.AllConnections(doc).Single(c => c.Name == "DB01");
        Assert.Equal("db01.contoso.com", db01.Host);
        Assert.Equal(3391, db01.Port);
    }

    [Fact]
    public void NeverLeaksPasswordData_Anywhere()
    {
        var doc = ImportSample();

        // Serialize the entire imported document and assert none of the bogus secrets survive.
        var json = new ConnectionSerializer().Serialize(doc);

        Assert.DoesNotContain("IGNORE", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SECRET", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CaseInsensitiveElementNames()
    {
        const string xml = """
            <connections>
              <connection>
                <name>Mixed</name>
                <connectiontype>rdpconfigured</connectiontype>
                <group>Team\Ops</group>
                <url>host.example</url>
              </connection>
            </connections>
            """;

        var doc = new RdmXmlImporter().Import(xml);

        var team = TestData.Folder(doc.Roots, "Team");
        Assert.NotNull(team);
        var ops = TestData.Folder(team!.Children, "Ops");
        Assert.NotNull(ops);
        Assert.NotNull(TestData.Connection(ops!.Children, "Mixed"));
    }

    [Fact]
    public void HandlesArrayOfConnectionRoot()
    {
        const string xml = """
            <ArrayOfConnection>
              <Connection>
                <Name>Solo</Name>
                <ConnectionType>RDP</ConnectionType>
                <Url>solo.example</Url>
              </Connection>
            </ArrayOfConnection>
            """;

        var doc = new RdmXmlImporter().Import(xml);
        Assert.Single(doc.Roots);
        Assert.NotNull(TestData.Connection(doc.Roots, "Solo"));
    }

    [Fact]
    public void FallsBackToHostForMissingName()
    {
        const string xml = """
            <Connections>
              <Connection>
                <ConnectionType>RDP</ConnectionType>
                <Url>nameless.example</Url>
              </Connection>
            </Connections>
            """;

        var doc = new RdmXmlImporter().Import(xml);
        var conn = Assert.IsType<RdpConnection>(doc.Roots.Single());
        Assert.Equal("nameless.example", conn.Name);
    }

    [Fact]
    public void ThrowsInvalidData_OnUnrecognizedRoot()
    {
        const string xml = "<Something><Else /></Something>";
        Assert.Throws<InvalidDataException>(() => new RdmXmlImporter().Import(xml));
    }

    [Fact]
    public void ThrowsInvalidData_OnMalformedXml()
    {
        Assert.Throws<InvalidDataException>(() => new RdmXmlImporter().Import("<not-closed>"));
    }

    [Fact]
    public void ThrowsInvalidData_OnEmptyInput()
    {
        Assert.Throws<InvalidDataException>(() => new RdmXmlImporter().Import("   "));
    }

    // Mirrors the real Devolutions RDM export shape: an <RDMExport> wrapper, credentials nested
    // under an <RDP> sub-element, an inline ":port" in <Url>, an explicit <Group> folder entry,
    // and non-RDP entries (TeamViewer/PowerShell) that must be skipped without leaking secrets.
    private const string RealShapeExport = """
        <?xml version="1.0" encoding="utf-8"?>
        <RDMExport>
          <Connections>
            <Connection>
              <Url>10.0.0.5:33890</Url>
              <ConnectionType>RDPConfigured</ConnectionType>
              <Group>Roma\SIT</Group>
              <Name>Server SIT</Name>
              <RDP>
                <Domain>SIT-IE</Domain>
                <SafePassword>lY2l2T06wsVM18XXceV84w==</SafePassword>
                <UserName>administrator</UserName>
              </RDP>
            </Connection>
            <Connection>
              <ConnectionType>Group</ConnectionType>
              <Group>Roma\EmptyFolder</Group>
              <Name>EmptyFolder</Name>
            </Connection>
            <Connection>
              <ConnectionType>TeamViewer</ConnectionType>
              <Name>Casa</Name>
              <TeamViewer />
            </Connection>
            <Connection>
              <ConnectionType>PowerShell</ConnectionType>
              <Name>PS</Name>
            </Connection>
          </Connections>
        </RDMExport>
        """;

    [Fact]
    public void ImportsRealRdmExport_Shape()
    {
        var doc = new RdmXmlImporter().Import(RealShapeExport);

        // Root <RDMExport> wrapper is accepted; only the RDP entry becomes a connection.
        var roma = TestData.Folder(doc.Roots, "Roma");
        Assert.NotNull(roma);

        var sit = TestData.Folder(roma!.Children, "SIT");
        Assert.NotNull(sit);

        var server = TestData.Connection(sit!.Children, "Server SIT");
        Assert.NotNull(server);

        // Credentials nested under <RDP> are read; the inline port is parsed.
        Assert.Equal("10.0.0.5", server!.Host);
        Assert.Equal(33890, server.Port);
        Assert.Equal("administrator", server.Username);
        Assert.Equal("SIT-IE", server.Domain);

        // The explicit Group entry materializes even though it has no RDP children.
        Assert.NotNull(TestData.Folder(roma.Children, "EmptyFolder"));

        // TeamViewer/PowerShell are skipped; only one connection total.
        Assert.Single(TestData.AllConnections(doc));

        // The RDM-encrypted SafePassword must never appear in the imported/serialized output.
        var json = new ConnectionSerializer().Serialize(doc);
        Assert.DoesNotContain("SafePassword", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lY2l2T06", json, StringComparison.Ordinal);
    }
}
