# RemoteHub

A lightweight, open-source WPF Remote Desktop (RDP) connection manager for Windows — a
free alternative to Devolutions Remote Desktop Manager for organizing and launching RDP
sessions.

> Scaffold in progress. This README will be expanded during the implementation phase.

## Features (planned)

- Tree-organized folders and RDP connections
- Tabbed RDP sessions hosting the Microsoft RDP ActiveX control
- Import from Devolutions RDM XML exports (RDP entries only)
- Light / Dark / System theming via WPF's built-in Fluent theme
- JSON storage of connections and settings
- **Passwords are never imported or stored** — the RDP control prompts for credentials at connect time

## Build

```
dotnet build RemoteHub.sln -c Debug
```

## Run

```
dotnet run --project src/RemoteHub/RemoteHub.csproj
```

## Test

```
dotnet test
```

## Storage locations

- Settings: `%AppData%\RemoteHub\settings.json`
- Connections: `%AppData%\RemoteHub\connections.json` (configurable)

## License

MIT — see [LICENSE](LICENSE).
