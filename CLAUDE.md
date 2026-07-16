# RemoteHub — project instructions

RemoteHub is a **.NET 10 / WPF** Windows app that organizes and launches Remote Desktop (RDP)
connections — an open-source, lightweight alternative to Remote Desktop Manager. Left panel = folder
tree of connections; each connection opens as a tab hosting the real Windows RDP ActiveX control.

**Read [`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md) for the full architecture, subsystems, and
verification techniques before making non-trivial changes.** For packaging, releases, and the
auto-update banner, see [`docs/DEPLOYMENT.md`](docs/DEPLOYMENT.md).

Repo: <https://github.com/adospace/remote-hub> (default branch `main`).

## Commands
```
dotnet build RemoteHub.sln -c Debug     # must be 0 errors
dotnet test                             # must stay green
dotnet run --project src/RemoteHub       # launch (Windows x64)
```
Built exe: `src/RemoteHub/bin/x64/Debug/net10.0-windows/RemoteHub.exe`.
Runtime data + logs: `%AppData%\RemoteHub\` (`connections.json`, `settings.json`, `logs\`).

## Layout
- `src/RemoteHub.Core` — models, JSON storage, RDM import, crypto vault. No UI. Fully unit-tested.
- `src/RemoteHub` — WPF app (MVVM via CommunityToolkit.Mvvm; DI via Microsoft.Extensions.DependencyInjection).
- `tests/RemoteHub.Tests` — xUnit. Add Core tests here for Core changes.

## Deployment & auto-update (implemented)
**Velopack** packaging + `.github/workflows/release.yml` (tag `vX.Y.Z` → build/pack/publish the
installer to the **Releases** page), plus an in-app **update banner** (bottom row of MainWindow:
5s startup check → auto-download → *Restart to update*). Feed = **GitHub Releases** via
`GithubSource`; **unsigned** (SmartScreen "Unknown publisher" on first run is expected).
Pieces: `Program.cs`, `Services/IUpdateService.cs` + `WindowsUpdateService.cs`,
`ViewModels/UpdateViewModel.cs`, `Controls/UpdateBar.xaml`. Updates no-op in DEBUG (empty `RepoUrl`)
and in non-installed builds (`UpdateManager.IsInstalled`). Cut a release: `git tag vX.Y.Z && git push origin vX.Y.Z`
(tag `main`, not a branch — the tag must live in `main`'s history).
`v0.1.0` shipped from a real tag and the workflow passed first run.
Detail + rationale: **`docs/DEPLOYMENT.md`**.

## Hard constraints — do NOT violate (each caused a real bug)
1. **No `<COMReference>`** — breaks `dotnet build` (MSB4803). RDP uses `AxHost` + `dynamic` (`RdpClientHost`).
2. **Don't hard-code the RDP CLSID** — probe it (`RdpClientHost.ResolveClsid`); `{791FA017-…}` isn't registered on Win11.
3. **Don't realize the `AxHost` during WPF layout** (e.g. as `WindowsFormsHost.Child` in a ctor/template) — it reenters the dispatcher and crashes. Host a WinForms `Panel` and create the control lazily on connect (`RdpSessionView.EnsureClient`).
4. **Don't use the default `TabControl` for live content** — use `TabControlEx` (keeps each tab's view/control alive).
5. **`UseWindowsForms` causes type ambiguity** (`Application`, `Panel`, `TabControl`, `UserControl`, `ColorDepth`, `KeyEventArgs`, `MessageBox`, …) — add a `using X = System.Windows.…;` alias in new files.
6. **Never store a plaintext password; never import RDM passwords.** Use `ICredentialProtector` (PBKDF2 + AES-GCM).
7. **All DI registrations live in `App.xaml.cs → ConfigureServices()`.**
8. Fluent theme via `Application.ThemeMode` (experimental; `WPF0001` suppressed in csproj).
9. **`VelopackApp.Build().Run()` must stay the first statement of `Program.Main`** — it runs the install/update/uninstall hooks and may kill the process. Hence `<StartupObject>` + `App.xaml` as a **`Page`, not `ApplicationDefinition`** (which would generate a competing `Main`) — don't "tidy" that csproj block away.
10. **Velopack needs a folder publish** (`-p:PublishSingleFile=false`), never single-file — it binary-deltas individual files between releases.
11. **`packId` (`com.adospace.remotehub`) must never change** — it is the installed app's identity; changing it orphans every existing install. Keep the `vpk` CLI version in `release.yml` in sync with the `Velopack` `PackageReference`.
12. **Velopack also defines `UpdateInfo`/`UpdateOptions`** — clashes with `RemoteHub.Services`' types. Never add `using Velopack;` to `App.xaml.cs` (CS0104); keep Velopack in `Program.cs` + `WindowsUpdateService.cs`.

## Conventions
- Match the existing style: file-scoped namespaces, nullable enabled, small methods, comment only non-obvious intent.
- Run `dotnet build` + `dotnet test` before committing; keep commits focused.
- The repo owner is the sole commit author — **no `Co-Authored-By` / "Generated with" trailers.**
- When diagnosing a crash: check `%AppData%\RemoteHub\logs\` and the Windows Application event log (`Application Error` / `CLR20r3`).
