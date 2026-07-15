namespace RemoteHub.Core.Services;

/// <summary>
/// Persists <see cref="AppSettings"/> as JSON under <c>%AppData%\RemoteHub</c>.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    // TODO(Implement): read/write settings.json; resolve default ConnectionsFilePath.
    public AppSettings Load()
    {
        throw new NotImplementedException();
    }

    public void Save(AppSettings settings)
    {
        throw new NotImplementedException();
    }
}
