# RemoteHub

A lightweight, open-source WPF Remote Desktop (RDP) connection manager for Windows — a
free alternative to Devolutions Remote Desktop Manager (RDM) for organizing and launching
your RDP sessions.

## Features

- **Connection tree** — organize connections into nested folders with a familiar tree view.
- **Tabbed embedded sessions** — RDP sessions run inside the app as tabs, hosting the native
  Windows Remote Desktop ActiveX control (`mstscax.dll` / MSTSC).
- **Pop-out sessions** — detach any session into its own standalone window.
- **RDM XML import** — import connections from a Devolutions RDM XML export. Only RDP entries
  are imported, and the folder tree is rebuilt from RDM `Group` paths. **No passwords are ever read.**
- **Master-password-protected credentials** — optionally save a password per connection, encrypted
  with a master password (PBKDF2-SHA256 + AES-256-GCM). The app asks for the master password on
  startup and auto-fills saved credentials at connect time.
- **JSON storage** — connections and settings are stored as human-readable JSON, with a
  configurable connections file path.
- **Fluent theming** — Light, Dark, or System theme via WPF's built-in Fluent theme.

## Screenshots

| Light | Dark |
| --- | --- |
| ![RemoteHub — light theme](docs/images/screenshot-light.png) | ![RemoteHub — dark theme](docs/images/screenshot-dark.png) |

The connection tree above was produced by importing [`docs/sample-rdm-export.xml`](docs/sample-rdm-export.xml).

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/)
- Windows (the app is `net10.0-windows`, x64, and depends on the Windows RDP ActiveX control)

## Build & Run

```
dotnet build RemoteHub.sln -c Debug
dotnet run --project src/RemoteHub
```

Run the tests with:

```
dotnet test
```

## How import works

Use **Import from RDM…** and point it at an RDM XML export. A representative sample lives at
[`docs/sample-rdm-export.xml`](docs/sample-rdm-export.xml) so you can try importing right away.

The importer:

- Reads only RDP entries (`ConnectionType` of `RDPConfigured` or `RDP`); other types such as
  SSH are skipped.
- Rebuilds the folder tree from the backslash-separated RDM `Group` path
  (e.g. `Production\Web Servers`).
- Maps host, port, username, domain, and description, tolerating format differences across RDM
  versions.

RDM export formats vary between versions. If import misses something from your real export,
please share a (sanitized) sample so the field mapping can be refined.

## Security

**Passwords are never imported from RDM.** The importer explicitly ignores any `Password` /
`SafePassword` / `Credential*` values in the RDM export.

**Saved passwords are encrypted with a master password.** Storing a connection password is
optional. When you set a master password (in **Settings**):

- A key is derived from it with **PBKDF2-SHA256** (600,000 iterations, per-vault random salt).
- Each saved password is encrypted with **AES-256-GCM** (authenticated encryption) and stored only
  as an opaque token in the JSON — never in plaintext.
- The master password itself is **never stored**. A small "verifier" token lets the app check the
  master password is correct without being able to recover it.
- The app prompts for the master password on startup and holds the derived key in memory only while
  running. You can **change** or **remove** the master password in Settings (changing re-encrypts
  all saved passwords; removing deletes them).

> **If you forget the master password, saved passwords cannot be recovered** — this is by design.
> Connections themselves (host, port, username) remain readable; only the encrypted passwords are lost.

When a connection has no saved password, the embedded RDP control prompts for credentials at connect
time (via CredSSP); username and domain are pre-filled as a convenience.

## Configuration

By default, files live under `%AppData%\RemoteHub`:

- `settings.json` — theme preference, window size, and the connections file path.
- `connections.json` — your folders and connections.

The connections file path is configurable in **Settings…** (with a Browse button), so you can
point RemoteHub at a file in OneDrive, a shared drive, or a repo.

## Tech stack

- .NET 10, WPF (`net10.0-windows`), Windows Forms interop for the RDP host control
- MVVM via [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet)
- Dependency injection via `Microsoft.Extensions.DependencyInjection`
- `System.Text.Json` for polymorphic connection serialization
- WPF built-in Fluent theme (`Application.ThemeMode`)
- xUnit for tests

The RDP ActiveX control is hosted through a hand-written `AxHost` subclass driven by late-bound
`dynamic` dispatch — no `COMReference` or generated interop assembly is used, so the solution
builds cleanly with `dotnet build`.

## Contributing

Contributions are welcome. Please open an issue or pull request. Sharing real (sanitized) RDM
exports is especially helpful for improving import coverage. Keep commits focused and run
`dotnet build` and `dotnet test` before submitting.

## License

MIT — see [LICENSE](LICENSE). Copyright (c) 2026 Adolfo Marinucci.
