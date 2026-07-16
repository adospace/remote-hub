---
name: verify
description: Launch RemoteHub (WPF) and drive its real UI to verify a change — screenshots, clicks, window-drag checks. Use when verifying UI/behaviour changes in src/RemoteHub.
---

# Verifying RemoteHub by running it

RemoteHub is a WPF desktop app. The only surface that proves a UI change is the running
window: build it, launch it, click it, screenshot it. Tests (`dotnet test`) cover
`RemoteHub.Core` only and prove nothing about the shell.

## Build & launch

```
dotnet build RemoteHub.sln -c Debug
```
Exe: `src/RemoteHub/bin/x64/Debug/net10.0-windows/RemoteHub.exe`

## Gotcha 1 — the master-password gate will kill your session

If the connections document has a `security` header, startup prompts "Unlock RemoteHub",
and **cancelling calls `Application.Current.Shutdown()`** (`MainViewModel.InitializeAsync`).
Without the password you cannot get past it — the app just exits.

**Don't touch the user's real data.** The connections path is *not* hard-coded: it comes from
`connectionsFilePath` in `%AppData%\RemoteHub\settings.json` (on this machine it points at
OneDrive, not the AppData copy). To get a usable app:

1. Back up `%AppData%\RemoteHub\settings.json`.
2. Point `connectionsFilePath` at a throwaway doc with **no `security` key** → no prompt.
3. Restore the backup when done.

Use **forward slashes** in that JSON path. A bash heredoc mangles `\\` into `\`, which is
invalid JSON and the app silently falls back.

Minimal test doc (`{"version":2,"roots":[...]}`), one entry per connection:
```json
{"$type":"rdp","host":"10.99.99.1","port":3389,"username":"tester",
 "id":"11111111-1111-1111-1111-111111111111","name":"Test Alpha",
 "display":{"screenMode":"fitToWindow","desktopWidth":1920,"desktopHeight":1080,
            "colorDepth":"bpp32","fullScreen":false,"redirectClipboard":true,"audio":"local"}}
```
Opening a tab does **not** auto-connect (only the pop-out window does), so unreachable hosts
like `10.99.99.x` are fine — you get a tab with the toolbar and a "Disconnected" overlay, and
never touch a real server.

## Gotcha 2 — SetForegroundWindow is unreliable; pin topmost instead

Windows blocks foreground stealing, so `SetForegroundWindow` often silently fails and your
clicks land on **whatever window is actually on top** (you'll see a screenshot of someone
else's app and think the feature is broken). This wasted the most time.

Before clicking, pin the window and confirm the target pixel belongs to it:

```powershell
SetWindowPos($h, (IntPtr)(-1), 0,0,0,0, 0x0002|0x0001|0x0040)   # HWND_TOPMOST
WindowFromPoint(pt)  ->  GetWindowThreadProcessId  ->  must equal RemoteHub's pid
```

Screenshots use `Graphics.CopyFromScreen(GetWindowRect(h))` — that captures *screen pixels*,
so an un-pinned window screenshots as whatever covers it.

Coordinate mapping is otherwise exact: screenshot pixel (x,y) == screen (rect.X + x, rect.Y + y).
Verify a coordinate is live by hovering first — buttons show a hover background.

## Gotcha 3 — UI Automation only sees part of the tree

`AutomationElement` finds the nav-toolbar and caption buttons, but **not** the session action
toolbar inside `RdpSessionView`. Don't conclude a button is missing/dead from UIA alone.
A modal `ShowDialog()` also blocks the owner's message pump, so a dialog that *is* open may
not show up when you enumerate windows — screenshot instead of trusting the enumeration.
UIA `BoundingRectangle.X` is the **left edge**, not the centre.

## Driving it

- Click: `SetCursorPos` then `mouse_event(LDOWN)`/`(LUP)`.
- Window drag: press, **move in ~20 steps** (a teleport doesn't trigger WM_NCHITTEST drag),
  release. Assert via `GetWindowRect` delta.
- Find dialogs by enumerating the pid's visible windows; dismiss with `WM_CLOSE` (0x0010).
  The connection editor's window title is `Connection`.

## Flows worth driving

| Change | How to reach it |
|---|---|
| Caption drag / chrome | Drag from empty caption band right of the tabs → rect must move by the delta. Then drag **from a tab** → rect must NOT move (tabs stay interactive). |
| Tab overflow | Needs ~7+ tabs at 1200px wide. The "..." toggle appears only when `PART_HeaderScroller.ScrollableWidth > 0`. |
| Session toolbar | Open a tab; toolbar is at window-rel y≈51, centred. |
| Update banner | Bottom row; disabled in DEBUG (empty `RepoUrl`) — log says "Update repo URL not configured". |

Logs (`%AppData%\RemoteHub\logs\`) are the fastest way to see whether a handler ran.
