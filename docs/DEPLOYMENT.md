# Deployment & Auto-Update

**Status: implemented and shipping.** Velopack packaging, the tag-triggered GitHub Actions workflow,
and the in-app update banner are all in the tree. `v0.1.0` was published from a real tag push and
`release.yml` passed on its first run, producing `*-Setup.exe`, `*-full.nupkg` and `releases.win.json`
on the Releases page — the assets `GithubSource` needs to serve updates.

Known-good as of `v0.1.0`: build → pack → publish. `v0.1.0` had no predecessor, so it generated no
delta packages and could not exercise the in-app banner (nothing to update *from*) — the first
release after it is what proves the update path end-to-end.

## What ships

1. RemoteHub is packaged as a Windows installer (`*-Setup.exe`) with **[Velopack](https://velopack.io)**.
2. Pushing a version tag (`vX.Y.Z`) runs **`.github/workflows/release.yml`**, which builds → packs →
   publishes the installer and its update feed **to the repo's Releases page**.
3. The app checks for updates 5s after startup, auto-downloads a newer version, and shows a banner
   offering **Restart to update**.
4. **No code-signing certificate** → the installer is unsigned (see [SmartScreen](#no-code-signing--smartscreen)).

## How the pieces fit

```
git tag v0.1.0 && git push origin v0.1.0
        │
        ▼
.github/workflows/release.yml           (windows-latest)
        │  dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=false -o publish/app
        │  vpk download github …        ← seeds publish/releases with the PREVIOUS release
        │  vpk pack …                   → Setup.exe + *-full.nupkg + *-delta.nupkg + releases.win.json
        │  vpk upload github --publish  → GitHub Release for the tag, all assets attached
        ▼
GitHub Releases page  ──── GithubSource ────►  WindowsUpdateService  ──►  UpdateViewModel  ──►  UpdateBar
   (the update feed)                            (Velopack UpdateManager)   (5s check, auto-DL)   (banner)
```

### Where each piece lives

| Concern | File |
| --- | --- |
| CI workflow | `.github/workflows/release.yml` |
| Velopack bootstrap | `src/RemoteHub/Program.cs` (`VelopackApp.Build().Run()` first in `Main`) |
| Entry-point wiring | `src/RemoteHub/RemoteHub.csproj` (`<StartupObject>`, `App.xaml` as `Page`) |
| Update service iface | `src/RemoteHub/Services/IUpdateService.cs` (`IUpdateService`, `UpdateInfo`, `UpdateStatus`, `UpdateOptions`) |
| Update service impl | `src/RemoteHub/Services/WindowsUpdateService.cs` (Velopack `UpdateManager` + `GithubSource`) |
| Banner view-model | `src/RemoteHub/ViewModels/UpdateViewModel.cs` |
| Banner control | `src/RemoteHub/Controls/UpdateBar.xaml(.cs)` |
| Banner placement | `src/RemoteHub/Views/MainWindow.xaml` (Grid row 2, `DataContext="{Binding Update}"`) |
| Startup kick-off | `src/RemoteHub/ViewModels/MainViewModel.cs` (`_ = _update.CheckForUpdatesOnStartupAsync()`) |
| DI registration | `src/RemoteHub/App.xaml.cs → ConfigureServices()` |

Everything lives in the WPF app project. **`RemoteHub.Core` stays UI/dependency-free — no Velopack there.**

## App integration notes

**Entry point.** WPF's auto-generated `Main` had to go: `VelopackApp.Build().Run()` must run before
anything else, because it handles the install/update/uninstall hooks and may terminate the process
outright. `RemoteHub.csproj` therefore sets `<StartupObject>RemoteHub.Program</StartupObject>` and
removes `App.xaml` from `ApplicationDefinition`, re-adding it as a `Page` — an `ApplicationDefinition`
generates its own `Main`, which collides with `Program.Main`. `App.OnStartup` (DI, theme, window) is
unchanged and runs after `VelopackApp.Run()`.

**Feed.** `WindowsUpdateService` builds `new GithubSource(repoUrl, accessToken: null, prerelease: false)`
and reads the **latest GitHub Release**'s Velopack assets (`releases.win.json` + `*-full.nupkg`).
This is why CI must upload the *full* Velopack output, not just `Setup.exe`.

**Graceful no-op.** Updates are disabled in two cases, and both leave the app fully functional:
- `UpdateOptions.RepoUrl` is empty — DEBUG builds, via `#if DEBUG` in `ConfigureServices()`.
- `UpdateManager.IsInstalled` is false — a plain `dotnet run` / xcopy build has no Velopack context.

In both cases `_updateManager` stays null, `IsSupported` is false, and every method returns
null/false. The banner simply never appears. Failures are logged via `RemoteHub.Diagnostics.Log` and
are never fatal.

**Type-ambiguity trap.** Velopack defines its own `UpdateInfo` **and** `UpdateOptions`, colliding with
`RemoteHub.Services`' types of the same name:
- In `App.xaml.cs`, adding `using Velopack;` breaks the build (CS0104). Velopack is confined to
  `Program.cs` and `WindowsUpdateService.cs`.
- In `WindowsUpdateService.cs`, only Velopack's needs an alias (`using VelopackUpdateInfo = Velopack.UpdateInfo;`)
  — a namespace member beats a using-directive, so the unqualified names already bind locally.

This is on top of the usual `UseWindowsForms` ambiguity (`Application`, `UserControl`, …) — see the
[landmines](#landmines-see-developmentmd-6).

## Decisions made during implementation

These were open questions in the plan; here is what was actually decided.

- **Banner placement: bottom of the window** (not the top, as originally sketched). It owns a real
  `Auto` row (row 2) of MainWindow's root Grid, spanning both columns, so it reads as a footer under
  the nav pane and the content half. **Why a row and not an overlay:** the RDP `WindowsFormsHost` is
  an HWND and paints above *all* WPF content regardless of Z-order (the airspace landmine), so an
  overlay would be invisible whenever a session is live. The `TabControlEx` covers rows 0–1 — its
  template re-partitions that space into the 40px caption band + the body — so row 2 is the only
  space the HWND can never reach. The root `Border` is collapsed until `IsUpdateAvailable`, so the
  `Auto` row costs zero pixels normally.
  - The bar's `Padding` has a load-bearing 8px bottom: `WindowChrome.ResizeBorderThickness` is 6px,
    so controls any closer to the bottom edge are swallowed by the resize grip and become unclickable.
- **`packId` = `com.adospace.remotehub`.** Set in `release.yml` (`PACK_ID`). **It must never change** —
  Velopack keys the installed app's identity off it, and a change orphans every existing install.
- **No "Check for updates" button in Settings.** `UpdateViewModel` *does* expose a
  `CheckForUpdatesCommand` (manual check → "You're up to date."), but nothing binds it today. Wiring
  a Settings button to it is a drop-in change if it's ever wanted.
- **Startup flow:** 5s delay (so the check never competes with the first paint) → check → if newer,
  show banner and auto-download → *Restart to update*. Plus Retry (download failures only) and Dismiss.
  Kicked off fire-and-forget from `MainViewModel.LoadAsync`.
- **First tag `v0.1.0` establishes the baseline.** Auto-update only works for Velopack-installed
  copies, so nobody can update *into* the first release — they install it manually.

## The release workflow

`.github/workflows/release.yml` triggers on `push` of a `v*` tag (and `workflow_dispatch`, which
validates that the selected ref *is* a tag). Beyond the plan's sketch it also:

- **Validates the tag** against `^v(\d+\.\d+\.\d+(-prerelease)?)$` and derives `Version` from it.
- **Generates release notes** by `git log`-ing between the previous tag and this one, XML-escaping the
  result (Velopack embeds the notes in nuspec XML, so a `&` in a commit subject would produce
  invalid XML), and passes them to `vpk pack --releaseNotes`.
- **Seeds delta generation** with `vpk download github` before packing. Velopack builds deltas by
  diffing against the previous `*-full.nupkg` **already present in `--outputDir`**; the runner starts
  empty, so without this step every release would be a full ~150MB download for every user. A first
  release finds nothing to download — expected, and the step tolerates it.
- **Flags prereleases.** If the version contains `-`, `--pre` is added to `vpk upload github`. `vpk`
  does **not** infer this from the version string, and `GithubSource` filters on GitHub's own
  prerelease boolean — so without `--pre`, a `v1.2.0-beta.1` becomes the latest *stable* release and
  is auto-shipped to every stable user, irreversibly.
- **Pins the `vpk` CLI** (`VELOPACK_VERSION`) to the same version as the `Velopack` `PackageReference`
  in `RemoteHub.csproj`. **Bump both together.** The packer validates the library version it finds in
  the `packDir`; an unpinned tool drifts onto a newer `vpk` and either fails the pack step or emits a
  package the pinned `UpdateManager` cannot read.

Two `pwsh` idioms in there are deliberate, not noise:
- `$PSNativeCommandUseErrorActionPreference = $false` around `git describe` / `vpk download`, whose
  non-zero exit on a first release is expected.
- The explicit `exit 0` at the end of the release-notes step: the pwsh wrapper ends with
  `exit $LASTEXITCODE`, and a failed `git describe` leaves `128` there.

### Why `vpk upload github` and not `softprops/action-gh-release`

`vpk upload github` creates/updates the Release for the tag and uploads **all** Velopack assets —
`*-Setup.exe`, `*-full.nupkg`, delta `*-delta.nupkg`, `releases.win.json`, `assets.win.json` —
which is precisely what `GithubSource` needs to serve updates. **Uploading only `Setup.exe` would
break the auto-update feed.**

### Why a folder publish, not single-file

Velopack requires a **folder** publish (`-p:PublishSingleFile=false`). It packages a directory tree
and applies binary deltas to individual files between releases; a single-file bundle defeats both.

**Token:** the built-in `GITHUB_TOKEN` with `contents: write` is enough for same-repo releases — no PAT.

## Cutting a release

```bash
git tag v0.1.0
git push origin v0.1.0        # → workflow builds, packs, and publishes the Release
```

Users install from the `*-Setup.exe` on the
[Releases page](https://github.com/adospace/remote-hub/releases). From the 2nd release on, Velopack
computes delta updates automatically and the in-app banner offers them.

## No code signing → SmartScreen

The installer is **unsigned** (this is a free OSS project without a certificate). Windows SmartScreen
shows **"Windows protected your PC / Unknown publisher"** on first download/run; users click
*More info → Run anyway*. This is expected, not a bug, and it is documented in the README.

**Future hardening:** obtain Azure Trusted Signing or a purchased OV/EV certificate, then add
`--signTemplate` to `vpk pack` (SideDoc at `C:\Source\ado\side-doc` has the exact wiring, including
the Azure Trusted Signing steps this workflow deliberately omits).

## Landmines (see DEVELOPMENT.md §6)

- `VelopackApp.Build().Run()` **must** stay the first statement of `Main`. Anything before it also
  runs during the install/update/uninstall hooks.
- Velopack requires a **folder** publish (`PublishSingleFile=false`), never single-file.
- `App.xaml` must stay a `Page`, not an `ApplicationDefinition` — the latter generates a competing `Main`.
- `packId` (`com.adospace.remotehub`) must stay stable across releases forever.
- The `vpk` CLI version in `release.yml` and the `Velopack` `PackageReference` must match.
- The update banner must own a layout row, never overlay the session surface (RDP HWND airspace).
- `UseWindowsForms` type ambiguity → add `using X = System.Windows…;` aliases in new files
  (`Program.cs`, the banner code-behind). Velopack adds its own `UpdateInfo`/`UpdateOptions` clash on top.
