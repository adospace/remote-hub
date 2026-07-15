using RemoteHub.Core.Models;

namespace RemoteHub.Core.Import;

/// <summary>
/// Imports Devolutions Remote Desktop Manager (RDM) XML exports. Only RDP entries are imported;
/// folder hierarchy is derived from the backslash-separated <c>Group</c> path. Credential fields
/// are never read.
/// </summary>
public sealed class RdmXmlImporter : IConnectionImporter
{
    // TODO(Implement): parse RDM XML per spec §8 (defensive, case-insensitive, no passwords).
    public ConnectionDocument Import(string content)
    {
        throw new NotImplementedException();
    }

    public ConnectionDocument ImportFile(string path)
    {
        throw new NotImplementedException();
    }
}
