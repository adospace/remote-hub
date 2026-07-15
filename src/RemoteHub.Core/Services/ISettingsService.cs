namespace RemoteHub.Core.Services;

/// <summary>
/// Loads and persists <see cref="AppSettings"/>.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Loads settings, returning sane defaults if none exist. The returned settings always
    /// have a non-null <see cref="AppSettings.ConnectionsFilePath"/>.
    /// </summary>
    AppSettings Load();

    void Save(AppSettings settings);
}
