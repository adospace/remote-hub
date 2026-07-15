using RemoteHub.Core.Models;

namespace RemoteHub.Core.Import;

/// <summary>
/// Imports connections from an external format into a <see cref="ConnectionDocument"/>.
/// </summary>
public interface IConnectionImporter
{
    /// <summary>Imports from the raw export text (e.g. RDM XML).</summary>
    ConnectionDocument Import(string content);

    /// <summary>Imports from a file on disk.</summary>
    ConnectionDocument ImportFile(string path);
}
