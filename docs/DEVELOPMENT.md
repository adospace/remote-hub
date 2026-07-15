# RemoteHub — Developer Guide

Orientation for anyone (human or AI agent) picking up RemoteHub to drive further development.
Read this end-to-end before making changes; the **Landmines** section in particular documents
non-obvious constraints that have already caused real bugs.

---

## 1. What this is

RemoteHub is a Windows desktop app (**.NET 10 / WPF**) that organizes and launches Remote Desktop
(RDP) connections — a lightweight, open-source alternative to Devolutions Remote Desktop Manager.
It shows a folder tree of connections on the left and opens each connection as a tab hosting the
real Windows RDP ActiveX control.

**Status:** functional. Tree, RDM import, master-password credential vault, theming, and multi-tab
embedded RDP sessions all work and are covered by tests and/or manual verification. Live connecting
requires a reachable host (VPN up, credentials). See **Roadmap** for what's next.

---

## 2. Quick start

```powershell
# from the repo root
dotnet build RemoteHub.sln -c Debug        # 0 errors expected
dotnet test                                # all tests should pass
dotnet run --project src/RemoteHub          # launch the app
```

Requirements: **.NET 10 SDK**, **Windows** (x64). The RDP control (`mstscax.dll`) ships with Windows.

Runtime data (not in the repo) lives under `%AppData%\RemoteHub\`:
- `settings.json` — theme, window size, and the connections file path.
- `connections.json` — the connection tree (path is configurable in Settings).
- `logs\remotehub-YYYYMMDD.log` — app log (see **Diagnostics**).

---

## 3. Solution layout

Three projects. `RemoteHub.Core` has **no UI dependency** and holds everything testable; the WPF
app depends on it.

```
RemoteHub.sln
Directory.Build.props            # shared: LangVersion latest, Nullable enable, ImplicitUsings
src/
  RemoteHub.Core/                # net10.0 — models, storage, import, crypto (fully unit-tested)
    Models/                      # ConnectionNode, FolderNode, RdpConnection, RdpDisplaySettings,
                                 #   ConnectionDocument, VaultHeader
    Serialization/               # ConnectionSerializer (System.Text.Json, polymorphic)
    Services/                    # ConnectionStore, SettingsService, AppSettings, ThemePreference
    Security/                    # ICredentialProtector + MasterKeyService (PBKDF2 + AES-GCM)
    Import/                      # IConnectionImporter + RdmXmlImporter
  RemoteHub/                     # net10.0-windows — WPF app (x64, UseWPF + UseWindowsForms)
    App.xaml(.cs)                # DI container, theme, global exception handlers (owns ALL DI regs)
    Controls/                    # RdpClientHost, RdpSessionView, RdpSessionWindow, TabControlEx
    Diagnostics/                 # Log
    Services/                    # DialogService, ThemeManager
    ViewModels/                  # Main, TreeNode, Sessions, Session, Settings, ConnectionEditor
    Views/                       # MainWindow, SettingsDialog, ConnectionEditorDialog, MasterPasswordDialog
    Assets/                      # app.ico (multi-res) + app-icon.png (embedded resources)
tests/RemoteHub.Tests/           # net10.0 — xUnit
docs/
  DEVELOPMENT.md                 # this file
  sample-rdm-export.xml          # representative RDM export used by importer tests + demo
  images/                        # README screenshots
assets/icon/                     # icon vector source (remotehub.svg) + 1024 master png
```

Namespaces mirror folders: `RemoteHub.Core.Models/Serialization/Services/Security/Import`,
`RemoteHub` (App), `RemoteHub.Controls/Diagnostics/Services/ViewModels/Views`.

---

## 4. Architecture

- **MVVM** via [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet)
  (`[ObservableProperty]`, `[RelayCommand]`, `ObservableObject`; classes are `partial`).
- **DI** via `Microsoft.Extensions.DependencyInjection`. **All registrations live in
  `App.xaml.cs` → `ConfigureServices()`** — add new services/VMs there. Core services and the
  two long-lived VMs (`MainViewModel`, `SessionsViewModel`) are singletons; dialog VMs are transient.
- **View ↔ VM wiring:** Windows/dialogs resolve their VM from the container (`App.Services`).
  `MainWindow` sets `DataContext = MainViewModel`.
- **Serialization:** `System.Text.Json`, polymorphic on `ConnectionNode` (`$type` = `folder`/`rdp`),
  camelCase, enums as camelCase strings, nulls omitted.

**Startup sequence** (`App.OnStartup`):
1. Install global exception handlers + start logging.
2. Build the DI `ServiceProvider`.
3. Load `AppSettings`; apply the Fluent theme (`ThemeManager.Apply`).
4. Resolve and show `MainWindow`.
5. `MainWindow.Loaded` → `MainViewModel.InitializeAsync()`: load the connection document; if it has
   a `Security` header (vault), prompt for the master password (Cancel → `Application.Shutdown()`);
   build the tree.

`MainViewModel` owns the loaded `ConnectionDocument` and persists it via `IConnectionStore` after
every mutation (add/edit/delete/import). After the Settings dialog closes it reloads the document so
master-password re-encryption and path changes are reflected.

---

## 5. Subsystems

### Data model & storage
- `ConnectionNode` (abstract) → `FolderNode` (has `Children`) or `RdpConnection` (host, port,
  username, domain, description, `EncryptedPassword?`, `Display`). **No plaintext password field.**
- `ConnectionDocument` = `{ Version=2, Security: VaultHeader?, Roots: List<ConnectionNode> }`.
- `ConnectionStore` loads/saves the document; **save is atomic** (temp file then move). Missing file
  → empty document.

### Settings
- `SettingsService` persists `AppSettings` to `%AppData%\RemoteHub\settings.json`. `Load()` always
  returns a non-null `ConnectionsFilePath` (defaults under `%AppData%`). Has a test constructor
  that redirects the directory.

### RDM import (`RdmXmlImporter`)
Real Devolutions exports look like `<RDMExport><Connections><Connection>…`. Key rules (all
case-insensitive, defensive — a bad entry is skipped, not fatal):
- Accepts any root containing `<Connection>` descendants (`RDMExport`, `Connections`, `ArrayOfConnection`).
- Imports only RDP entries (`ConnectionType` `RDPConfigured`/`RDP`); other types are skipped, but
  explicit `Group` entries still materialize (possibly empty) folders.
- Folder tree from the backslash-separated `<Group>` path.
- Host from `<Url>`/`<HostName>`/`<Host>` (parses inline `:port`); credentials (`UserName`/`Domain`)
  are read from the nested **`<RDP>`** sub-element as well as direct children.
- **Never reads `<Password>`/`<SafePassword>`/`Credential*`.** (Note: users sometimes type passwords
  into connection *names/descriptions* in RDM — those are plain text fields and import verbatim.)

### Credential vault (master password)
- `MasterKeyService : ICredentialProtector` (Core). **PBKDF2-SHA256** (600k iters, random 16-byte
  salt) derives a 32-byte key; each password is encrypted with **AES-256-GCM** into an opaque base64
  token `nonce(12)|tag(16)|ciphertext`. A "verifier" (a known constant encrypted under the key) lets
  us check the password without recovering it.
- The `VaultHeader` (salt, iterations, verifier) is stored in `ConnectionDocument.Security`; each
  connection's token in `RdpConnection.EncryptedPassword`. The master password/key are **never**
  written to disk; the key is held in memory only while unlocked and zeroed on `Lock()`.
- **Flow:** no vault → no startup prompt; set one in Settings before you can save passwords. Vault
  present → unlock at startup. Settings can Set / Change (re-encrypts all) / Remove (clears saved
  passwords). Editing a connection shows a password field only when the vault is unlocked. At connect
  time the decrypted password is passed to the control via `ClearTextPassword` (never stored).

### RDP session hosting — the tricky part
Three cooperating pieces:
- **`RdpClientHost` : `System.Windows.Forms.AxHost`** — wraps the MSTSC ActiveX control with **no
  COMReference / no generated interop**; drives it via late-bound `dynamic`. It **probes at runtime**
  for a working control CLSID (`ResolveClsid`, MsRdpClient v11→v6) and applies settings defensively
  across versions (`GetBestAdvancedSettings` walks `AdvancedSettings9`→`2`). Connection state is
  surfaced by polling the `Connected` property on a WinForms `Timer`.
- **`RdpSessionView`** (UserControl) — hosts a WinForms `Panel` inside a `WindowsFormsHost`, and
  **creates the `RdpClientHost` lazily on Connect** (`EnsureClient`), adding it to the panel — never
  during WPF layout (see Landmines). A WPF overlay shows Disconnected/Connecting state (the
  WindowsFormsHost is collapsed while disconnected to avoid airspace issues).
- **`RdpSessionWindow`** — the pop-out: a standalone window with a fresh view/session for the same
  connection.

### Tabs (`TabControlEx`)
A `TabControl` subclass that keeps **one `ContentPresenter` per item alive** (visibility-toggled)
instead of the default single-shared-presenter behaviour. Required so each tab keeps its own live
`RdpSessionView`/RDP control; without it, switching tabs would not switch the active remote desktop.

### UI & theming
- Fluent look via WPF's built-in **`Application.ThemeMode`** (System/Light/Dark), applied by
  `ThemeManager`. Experimental API → `WPF0001` is suppressed in the csproj.
- `MainWindow`: a top command bar (Segoe Fluent Icons buttons + Settings gear), a left nav pane
  (tree, starts collapsed), and the content area (empty-state placeholder or session tabs). Surfaces
  use Fluent theme brushes (`SolidBackgroundFillColorBaseAltBrush`, `LayerFillColorDefaultBrush`,
  `DividerStrokeColorDefaultBrush`, `AccentTextFillColorPrimaryBrush`, `TextFillColor*`, etc.) via
  `DynamicResource` so they follow the theme.

### Diagnostics (`RemoteHub.Diagnostics.Log`)
Thread-safe file logger to `%AppData%\RemoteHub\logs\`. `App` installs handlers for
`DispatcherUnhandledException` (logs + message box + keeps the app alive),
`AppDomain.UnhandledException`, and `TaskScheduler.UnobservedTaskException`. **First stop when
diagnosing a crash: this log, plus the Windows Application event log (`Application Error` /
`CLR20r3`) for native/early failures.**

---

## 6. Landmines (do NOT reintroduce these)

1. **Never add `<COMReference>`.** It fails `dotnet build` (`MSB4803`, `ResolveComReference`
   unsupported by the SDK MSBuild). The RDP control is hosted via `AxHost` + `dynamic` instead.
2. **Never hard-code the RDP control CLSID.** The old `{791FA017-…}` is *not registered* on modern
   Windows. Use `RdpClientHost.ResolveClsid()` (probe newest-working).
3. **Never realize the `AxHost` during a WPF layout pass** (e.g. `WindowsFormsHost.Child = axHost`
   in a ctor/template). Its OLE in-place activation pumps messages and reenters the dispatcher →
   `InvalidOperationException: "Dispatcher processing has been suspended…"` (a hang-then-crash). Host
   a WinForms `Panel` and add the control **on connect** (`RdpSessionView.EnsureClient`).
4. **Never use the default `TabControl` for live content.** It shares one content presenter; use
   `TabControlEx` so each session keeps its own view/control.
5. **`UseWindowsForms` makes many types ambiguous** (`Application`, `UserControl`, `Panel`,
   `TabControl`, `ColorDepth`, `KeyEventArgs`, `MouseButtonEventArgs`, `MessageBox`, …). Add a
   `using X = System.Windows.…;` alias in any new file that touches them (existing files show the pattern).
6. **Never persist a plaintext password**, and **never import RDM passwords**. Passwords only exist
   as AES-GCM tokens produced by `ICredentialProtector`.
7. **All DI registrations go in `App.xaml.cs`.** Don't scatter them.
8. **Commits:** the repo owner is the sole author — do **not** add `Co-Authored-By` or "Generated
   with" trailers.

---

## 7. Testing & verifying changes

- **Always** run `dotnet build RemoteHub.sln -c Debug` and `dotnet test` before committing.
- Core logic (import, storage, settings, crypto) is unit-tested in `tests/RemoteHub.Tests`. Add tests
  there for any Core change; keep them deterministic (temp dirs, no wall-clock/random reliance).
- UI/RDP behaviour can't be fully unit-tested. Techniques that worked well here:
  - **Launch + screenshot:** run the built exe
    (`src/RemoteHub/bin/x64/Debug/net10.0-windows/RemoteHub.exe`) and capture it. Prefer `PrintWindow`
    (flag 2) — it captures only the target window (content-safe, no desktop bleed); a `CopyFromScreen`
    grab needs its shadow margins cropped and can leak background content.
  - **RDP interop harness:** a tiny WPF console app that `<Compile Include>`s the *real*
    `RdpClientHost.cs` and drives create→Setup→Connect, with global handlers logging exceptions. This
    is how the connect crash was isolated without the full UI.
  - **UI automation:** `System.Windows.Automation` from PowerShell (find window by ProcessId, find
    elements by Name via `TrueCondition` + filter, `InvokePattern`/`SelectionItemPattern`, or synthetic
    mouse via `mouse_event`). Verify behaviour by asserting on the app log.
  - **Crashes:** read `%AppData%\RemoteHub\logs\` and the Windows Application event log.

---

## 8. Roadmap / good next tasks

- **Tree UX:** drag-and-drop reordering/move; connection duplication; search/filter; optional
  persistence of folder expansion state (currently always starts collapsed by design).
- **Sessions:** per-connection "always prompt for password" toggle; idle auto-lock of the vault;
  auto-disconnect background tabs to save resources; multi-monitor / display-resolution options in
  the editor.
- **Import:** refine the RDM mapping against more export variants; optionally import other RDM types.
- **Distribution:** GitHub Actions CI (build + test); publish a single-file/self-contained release;
  code signing.
- **Extensibility:** the data model can grow to SSH/VNC (add node types + hosts + editors).
- **Docs:** contributor setup, screenshots of the dialogs.

Keep changes focused, respect the Landmines, and update this file when you change architecture.
