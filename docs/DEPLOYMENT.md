# Deployment & Auto-Update — implementation plan

**Status: NOT yet implemented.** This is the hand-off plan for the next session. When you pick this
up, implement Parts A and B, verify a real release end-to-end, then update this file's status and the
README's install section.

## Goal

1. Package RemoteHub as a Windows installer (`Setup.exe`) with **[Velopack](https://velopack.io)**.
2. A **GitHub Actions** workflow that, on a version tag (`vX.Y.Z`), builds → packs → publishes the
   installer **to the repo's Releases page**.
3. An in-app **auto-update banner** that appears when a newer version is available (auto-downloads,
   then offers *Restart to update*) — the feature the owner liked in SideDoc.
4. **No code-signing certificate** → the build is unsigned (see the SmartScreen note in Part B).

## Reference implementation: SideDoc

Model this on the SideDoc app at `C:\Source\ado\side-doc`. Copy/adapt these files:

| Concern | SideDoc file |
| --- | --- |
| CI workflow | `.github/workflows/build-and-deploy.yml` (desktop job only) |
| Velopack bootstrap | `src/SideDoc.AppWindow/App.xaml.cs` → `VelopackApp.Build().Run();` (first line of `Main`) |
| Update service iface | `src/SideDoc.AppCore/Services/IUpdateService.cs` (`IUpdateService`, `UpdateInfo`, `UpdateStatus`, `UpdateOptions`) |
| Update service impl | `src/SideDoc.AppWindow/Services/WindowsUpdateService.cs` (Velopack `UpdateManager`) |
| Banner view-model | `src/SideDoc.AppWindow/ViewModels/UpdateViewModel.cs` (5s startup check → auto-download → restart) |
| Banner control | `src/SideDoc.AppWindow/Controls/UpdateBar.xaml(.cs)` |
| DI registration | `src/SideDoc.AppWindow/App.xaml.cs` (~line 182: `UpdateOptions`, `IUpdateService`) |

**Two deliberate differences from SideDoc** (SideDoc has infra RemoteHub does not):
- SideDoc's update **feed is Azure Storage** (`SimpleWebSource(url)`). RemoteHub has no Azure —
  use **GitHub Releases as the feed** via `Velopack.Sources.GithubSource`.
- SideDoc **code-signs** with Azure Trusted Signing. RemoteHub has **no certificate** — drop the
  entire signing block; ship unsigned.

---

## Part A — App integration (in `src/RemoteHub`, the WPF app)

Keep all of this in the WPF app project; **`RemoteHub.Core` must stay UI/dependency-free** (no Velopack there).

**A1. Package.** Add to `src/RemoteHub/RemoteHub.csproj`:
```xml
<PackageReference Include="Velopack" Version="<latest-stable>" />
```

**A2. Bootstrap Velopack first.** RemoteHub currently uses WPF's auto-generated `Main`. Add an explicit
entry point so `VelopackApp` runs before anything else:
```csharp
// src/RemoteHub/Program.cs
using System;
using Velopack;
using Application = System.Windows.Application; // UseWindowsForms type ambiguity

namespace RemoteHub;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        // MUST be first: handles install/update/uninstall hooks, then may terminate the process.
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
```
Then set `<StartupObject>RemoteHub.Program</StartupObject>` in the csproj (this disables the
auto-generated `Main`; `App.xaml` stays an `ApplicationDefinition`). `App.OnStartup` (DI, theme,
window) is unchanged and runs after `VelopackApp.Run()`.

**A3. Update service (GitHub feed).** Port `IUpdateService`/`UpdateInfo`/`UpdateStatus`/`UpdateOptions`
and `WindowsUpdateService`, but build the manager from a `GithubSource`:
```csharp
using Velopack;
using Velopack.Sources;
// ...
var source = new GithubSource("https://github.com/adospace/remote-hub", accessToken: null, prerelease: false);
_updateManager = new UpdateManager(source);
```
`GithubSource` reads the **latest GitHub Release** and its Velopack assets (`releases.win.json` +
`*-full.nupkg`). This is why the CI must upload the *full* Velopack output as release assets (Part B).
Drop SideDoc's Serilog/Sentry calls — use RemoteHub's `RemoteHub.Diagnostics.Log` instead.

**A4. Banner view-model.** Port `UpdateViewModel`: 5-second startup delay → `CheckForUpdatesAsync` →
if newer, auto-download → `IsUpdateDownloaded` → *Restart to update* (`ApplyUpdatesAndRestart`), plus
Dismiss/Retry. Kick off `CheckForUpdatesOnStartupAsync()` after the main window loads.

**A5. Banner control.** Port `UpdateBar.xaml`, restyled with RemoteHub's Fluent brushes
(`SolidBackgroundFillColorBaseAltBrush`, `AccentTextFillColorPrimaryBrush`, `TextFillColor*`, etc.,
matching the rest of the app). It shows an update glyph, status text, a progress bar, and
*Restart to update* / *Retry* / *Dismiss (×)*; the root `Border` visibility binds to `IsUpdateAvailable`.
- **Placement (decide during impl):** a full-width strip at the **top of the content body**, above
  the session tabs' content. **Airspace caveat** (see DEVELOPMENT.md landmines): the banner is WPF and
  must not overlap an active session's `WindowsFormsHost`, so give it its own row *above* the tab
  content rather than overlaying the remote surface. A clean option: wrap the content column body in a
  `DockPanel` with the `UpdateBar` docked `Top` (Auto height, collapsed when no update) and the tab
  content filling the rest.

**A6. DI + wiring** (in `App.xaml.cs → ConfigureServices`, per the project's "all DI here" rule):
```csharp
services.AddSingleton(new UpdateOptions
{
#if DEBUG
    RepoUrl = ""                                   // updates disabled in debug
#else
    RepoUrl = "https://github.com/adospace/remote-hub"
#endif
});
services.AddSingleton<IUpdateService, WindowsUpdateService>();
services.AddSingleton<UpdateViewModel>();
```
Expose `UpdateViewModel` off `MainViewModel` (e.g. `public UpdateViewModel Update`) and bind the
banner's `DataContext` to it.

**A7. Version.** CI passes `-p:Version=X.Y.Z` (from the tag). At runtime `UpdateManager.CurrentVersion`
reports it. In DEBUG / non-installed runs there is no Velopack context, so `CheckForUpdates` should
no-op gracefully (guard on an empty `RepoUrl` and/or a null manager, exactly as SideDoc guards on an
empty `UpdateUrl`).

---

## Part B — GitHub Actions workflow

Create `.github/workflows/release.yml`. Trigger on tag `v*`. Adapted from SideDoc, **minus Azure and
signing**:

```yaml
name: Release
on:
  push:
    tags: ['v*']
  workflow_dispatch:
permissions:
  contents: write            # create the GitHub Release
jobs:
  release-windows:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
        with: { fetch-depth: 0 }               # full history for release notes
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '10.0.x', dotnet-quality: 'ga' }
      - name: Install Velopack CLI
        run: dotnet tool install -g vpk
      - name: Version from tag
        id: version
        shell: pwsh
        run: '"version=$("${{ github.ref_name }}".TrimStart(''v''))" >> $env:GITHUB_OUTPUT'
      - name: Publish (win-x64, self-contained, folder — NOT single-file)
        run: >
          dotnet publish src/RemoteHub/RemoteHub.csproj
          -c Release -r win-x64 --self-contained true
          -p:PublishSingleFile=false
          -p:Version=${{ steps.version.outputs.version }}
          -o publish/app
      - name: Pack with Velopack
        shell: pwsh
        run: |
          vpk pack `
            --packId com.adospace.remotehub `
            --packVersion ${{ steps.version.outputs.version }} `
            --packDir publish/app `
            --mainExe RemoteHub.exe `
            --packTitle RemoteHub `
            --icon src/RemoteHub/Assets/app.ico `
            --outputDir publish/releases
      - name: Publish release to GitHub (Velopack)
        shell: pwsh
        run: >
          vpk upload github
          --repoUrl https://github.com/adospace/remote-hub
          --publish
          --releaseName "RemoteHub ${{ github.ref_name }}"
          --tag ${{ github.ref_name }}
          --token ${{ secrets.GITHUB_TOKEN }}
          --outputDir publish/releases
```

**Why `vpk upload github` (not `softprops/action-gh-release`):** `vpk upload github` creates/updates the
Release for the tag and uploads **all** Velopack assets (`*-Setup.exe`, `*-full.nupkg`, delta
`*-delta.nupkg`, `releases.win.json`, `assets.win.json`) — precisely what `GithubSource` needs to
serve updates. Uploading only `Setup.exe` would break the auto-update feed.

**No code signing:** omit SideDoc's Azure Trusted Signing steps and the `--signTemplate` arg. The
unsigned `Setup.exe` triggers Windows **SmartScreen "Unknown publisher"** on first download/run — users
click *More info → Run anyway*. Document this in the README. Future hardening: Azure Trusted Signing or
a purchased OV/EV cert, then add `--signTemplate` to `vpk pack` (see SideDoc for the exact wiring).

**Token:** the built-in `GITHUB_TOKEN` with `contents: write` is enough for same-repo releases (no PAT).

---

## Cutting a release (once implemented)

```bash
git tag v0.1.0
git push origin v0.1.0        # → workflow builds, packs, and publishes the Release
```
Users install from the `*-Setup.exe` on the Releases page. From the 2nd release on, Velopack computes
delta updates automatically and the in-app banner offers them.

## Open decisions for the implementer

- Final banner placement in the custom-chrome layout (see A5).
- Confirm `packId` (`com.adospace.remotehub` suggested — it must stay stable across releases).
- Optional **"Check for updates"** button in the Settings dialog (manual check), mirroring SideDoc.
- First tag `v0.1.0` establishes the baseline (auto-update only works for Velopack-installed copies).

## Landmines that still apply (see DEVELOPMENT.md §6)

- `VelopackApp.Build().Run()` **must** be the first line of `Main`.
- Velopack requires a **folder** publish (`PublishSingleFile=false`), never single-file.
- `UseWindowsForms` type ambiguity → add `using X = System.Windows…;` aliases in new files
  (`Program.cs`, the update service, the banner code-behind).
