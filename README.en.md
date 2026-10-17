# Wincy for Windows

[繁體中文](README.md) | **English**

A native Windows rewrite of the macOS clipboard manager [Maccy](https://github.com/p0deje/Maccy), named **Wincy**
(originally "Maccy for Windows"; renamed so it is not confused with the original project):
tray icon, global hotkey, and a searchable, pinnable clipboard history that pastes back into the window you came from.

- **One 160 KB executable** — no .NET SDK, no NuGet, no Node, and no runtime download
  (it is built with the .NET Framework and `csc.exe` that ship with Windows 10/11).
- **Native Win32/WinForms**: ~80 ms cold start, ~32 MB resident, no Electron or WebView.
- **UI follows the system language** (Traditional Chinese / English) and the **system light/dark theme**.

![Wincy for Windows](docs/popup-dark.png)

---

## Contents

- [Features](#features)
- [Screenshots](#screenshots)
- [Quick start](#quick-start)
- [Keyboard and mouse](#keyboard-and-mouse)
- [Settings](#settings)
- [Command line](#command-line)
- [Data and privacy](#data-and-privacy)
- [Compared with the original Maccy](#compared-with-the-original-maccy)
- [Architecture](#architecture)
- [Testing](#testing)
- [Development and release](#development-and-release)
- [Known limitations](#known-limitations)
- [License](#license)

---

## Features

**Capture**

- Records clipboard text, HTML/RTF rich text, images (PNG) and file lists automatically.
- Duplicates do not create new entries; the existing item is moved to the top instead.
- Respects Windows' confidentiality markers: `CanIncludeInClipboardHistory=0`,
  `CanUploadToCloudClipboard=0`, `ExcludeClipboardContentFromMonitorProcessing` and
  `Clipboard Viewer Ignore` are always skipped (password fields are never recorded).
- Exclude specific applications by executable name (for example `keepass` or `1password`).

**Using it**

- `Ctrl+Shift+V` opens the history list above any application (configurable; if the shortcut is
  taken it falls back to `Ctrl+Alt+V` automatically).
- Type to search: matches titles, full text, file names and the source app, with hit highlighting.
- The focused item is previewed on the right: full text (wrapped, scrollable), a fitted image,
  or the file list.
- The popup opens with nothing focused: point at an item (or use the arrow keys) to focus it and
  update the preview.
- `Enter` pastes, `Shift+Enter` pastes as plain text, `Ctrl+Enter` only copies, `Ctrl+1`–`Ctrl+9`
  pastes the Nth item directly.
- Pinned items (`Ctrl+P`) always stay on top and are never removed by the history limit.
- Right-click menu: paste / paste as plain text / copy / pin / delete / clear all.
- Pasting remembers the previous foreground window, hides the popup, restores focus and sends `Ctrl+V`.

**Appearance and system integration**

- Tray icon: left click toggles the history, right click opens a menu (show, launch at login,
  settings, clear, about, quit).
- Dark / light / follow system; rounded windows on Windows 11 (square on Windows 10).
- Per-Monitor V2 DPI awareness, rendered natively — no blur at 150% or 200% scaling.
- Single instance: launching again forwards the request to the running copy.
- Launch at login (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`).
- UI strings follow the system language (Chinese / English) and can be pinned in settings.

---

## Screenshots

| Search and highlighting | Context menu |
| --- | --- |
| ![Search](docs/popup-search.png) | ![Menu](docs/context-menu.png) |

| Settings (dark) | Light theme |
| --- | --- |
| ![Settings](docs/settings-dark.png) | ![Light](docs/popup-light.png) |

![About](docs/about-dark.png)

Image preview (right-hand pane):

![Preview](docs/preview-image.png)

---

## Quick start

### Run it

```powershell
dist\Wincy.exe
```

It appears in the tray. Press `Ctrl+Shift+V` (or `Ctrl+Alt+V`) to open the history list.

> Requirements: Windows 10 1809+ or Windows 11. .NET Framework 4.8 is part of Windows,
> so nothing else needs to be installed.

### Build from source

```powershell
.\build.ps1          # build dist\Wincy.exe
.\build.ps1 -Test    # build and run the 88 logic self tests
.\build.ps1 -Run     # build and start
.\build.ps1 -Clean   # remove build output
```

The build script locates `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`
(the C# 5 compiler bundled with Windows), compiles `src\*.cs` into a single WinExe and embeds
`src\app.manifest` (DPI awareness, supported OS versions, Common Controls v6) plus `assets\wincy.ico`.

The icon can be regenerated with Pillow:

```powershell
python assets\make_icon.py
```

---

## Keyboard and mouse

| Key | Action |
| --- | --- |
| `Ctrl+Shift+V` | Open / close the history list (default; configurable) |
| Just type | Search the current list |
| `↑` `↓` / `PageUp` `PageDown` / `Home` `End` | Move the selection |
| `Enter` | Paste (keeps the original formatting) |
| `Shift+Enter` | Paste as plain text |
| `Ctrl+Enter` | Copy to the clipboard only, without pasting |
| `Ctrl+1` … `Ctrl+9` | Paste the Nth item directly |
| `Ctrl+P` | Pin / unpin |
| `Delete` | Delete the selected item (when the search box is empty) |
| `Ctrl+,` | Open settings |
| `Esc` | Close the list |
| Mouse over an item | Focus it and update the preview |
| Left click | Paste that item |
| Right click | Open the item menu |
| Wheel | Scroll the list; scrolls the preview when the pointer is over it |

Tray icon: left click toggles the list, right click opens the menu, double click opens the list.

---

## Settings

| Setting | Description |
| --- | --- |
| Items to keep | 10–5000, default 200; pinned items do not count |
| Store image content | When off, only text and files are recorded |
| Open history | The global shortcut; click it and press the new combination to record it |
| Paste after selecting | When off, items are only copied to the clipboard (no `Ctrl+V`) |
| Paste as plain text by default | Makes `Enter` paste as plain text |
| Ignore passwords and sensitive fields | Honours Windows' confidential clipboard markers |
| Launch at login | Writes to the current user's Run key |
| Theme | Follow system / light / dark |
| Ignored applications | One executable name per line (`.exe` optional) |

Settings apply immediately and are written back to `settings.json`; there is no Save button.

---

## Command line

A second launch does not start another resident copy; it forwards the command to the running
Wincy (named mutex plus a broadcast message). Handy for shortcuts or AutoHotkey scripts:

```powershell
Wincy.exe             # start; opens the history if already running
Wincy.exe --show      # open the history
Wincy.exe --toggle    # toggle the history
Wincy.exe --settings  # open settings
Wincy.exe --about     # open the about window
Wincy.exe --clear     # clear the history
Wincy.exe --quit      # quit Wincy
```

---

## Data and privacy

Data lives in `%APPDATA%\Wincy\` by default:

```
%APPDATA%\Wincy\
├── settings.json      # settings (indented, safe to edit by hand)
├── history.json       # history (indented, readable, editable)
├── images\<id>.png    # original PNG for image items
└── wincy.log          # diagnostics, rotated at 512 KB
```

- **Portable mode**: drop an empty `portable.txt` next to `Wincy.exe` and data moves to `WincyData\`.
- **Fully offline**: no update check, no telemetry, no network connections.
- **Never records itself**: clipboard writes carry a private `Wincy.SelfWrite` format that the
  watcher uses to skip them.
- History is written atomically (temp file plus `File.Replace`), so a crash cannot leave half a file.

---

## Compared with the original Maccy

| Maccy (macOS, original project) | Wincy (this project) |
| --- | --- |
| Menu bar icon | Tray icon (left click toggles, right click opens a menu) |
| `⌘⇧C` | `Ctrl+Shift+V` (changeable; falls back to `Ctrl+Alt+V` on conflict) |
| History list + search | Same, with substring matching, hit highlighting, source app and age |
| Pin, delete, clear all | Same |
| Paste as plain text (`⌥↵`) | `Shift+Enter`, the context menu, or the default setting |
| Ignore apps | "Ignored applications" in settings |
| Ignore confidential content | Honours Windows' confidential clipboard markers |
| History limit | 10–5000, default 200 |
| Launch at login | Current user's Run key |
| Text / images / files | Text, HTML/RTF, images (PNG), file lists |
| Sparkle auto-update | Deliberately omitted (stays offline, zero network) |
| Localisation | Traditional Chinese / English, following the system |

---

## Architecture

```
src/
├── Program.cs          Entry point: DPI init, single instance, command line, global exception handling
├── WincyApp.cs         ApplicationContext: wires the tray, watcher, hotkey, store and windows together
├── MainForm.cs         Floating history window (search box, list, footer, keyboard handling)
├── HistoryList.cs      Owner-drawn list: icons/thumbnails, highlighting, hover, selection, custom scrollbar
├── PreviewPane.cs      Right-hand preview: wrapped full text, fitted image, file list
├── SettingsForm.cs     Settings window (toggles, stepper, hotkey recorder, ignore list)
├── InfoForm.cs         About window
├── MiniMenu.cs         Owner-drawn popup menu (tray menu, context menu, confirmations)
├── Controls.cs         SearchBox / ToggleSwitch / Stepper / Segmented / HotkeyBox / IconButton / TextButton
├── Ui.cs               Rounded corners, ellipsis, segmented highlighting, app icon and thumbnail caches
├── Theme.cs            DPI scaling, light/dark palette, fonts
├── MessageWindow.cs    Hidden window: WM_CLIPBOARDUPDATE, WM_HOTKEY, WM_SETTINGCHANGE, cross-instance messages
├── ClipboardReader.cs  Reads the clipboard into a ClipboardItem (confidentiality and source app checks)
├── ClipboardWriter.cs  Writes an item back to the clipboard (text/RTF/HTML/image/files)
├── Paster.cs           Foreground window tracking + sending `Ctrl+V` (AttachThreadInput to beat the foreground lock)
├── HotkeyService.cs    RegisterHotKey wrapper + Run-key autostart
├── HistoryStore.cs     History: ordering, dedupe, pins, limit, atomic saves, orphan image cleanup
├── ClipboardItem.cs    Data model + JSON serialisation + one-line summary
├── AppSettings.cs      settings.json model with tolerant loading
├── HotkeySpec.cs       Shortcut parsing, formatting and serialisation
├── Search.cs           Substring matching, fuzzy matching (with scoring) and relative time
├── Json.cs             Hand-written minimal JSON parser/writer (keeps files readable and ordered)
├── Paths.cs            Data folder location, portable mode, atomic file writes
├── I18n.cs             Chinese/English string tables
├── Native.cs           Win32 P/Invoke
└── Log.cs              Rotating log file
```

**A copy, end to end**

```
User copies
   → Windows sends WM_CLIPBOARDUPDATE
   → MessageWindow → WincyApp (70 ms debounce, since one copy can raise several notifications)
   → ClipboardReader (confidentiality markers, source app, format)
   → HistoryStore.Add (dedupe / pin ordering / limit trimming) → history.json + images\*.png
   → the open list refreshes immediately
```

**A paste, end to end**

```
Enter / left click
   → WincyApp.UseItem (remember the foreground window → hide the window → write the clipboard → move to top)
   → Paster.SendPaste (restore the foreground window, release stuck Shift/Alt/Win, send Ctrl+V)
```

---

## Testing

```powershell
.\build.ps1 -Test              # 88 logic self tests: JSON, settings, history, search, shortcuts
.\tests\behavior-test.ps1      # 16 UI behaviour checks: capture, dedupe, delete, pin, sensitive content, dismiss
.\tests\paste-test.ps1         # end-to-end paste (Enter)
.\tests\paste-test.ps1 -Plain  # end-to-end plain-text paste (Shift+Enter)
.\tests\paste-test.ps1 -Rich   # rich text: whether HTML survives
.\tests\make-screenshots.ps1   # regenerate the docs\ screenshots
```

`behavior-test.ps1` and `paste-test.ps1` drive real mouse and keyboard events, so they need an
interactive desktop session (they cannot run over a remote or locked session). `tests\ui.ps1`
provides helpers for window enumeration, screenshots, clicks and keystrokes.

---

## Development and release

```powershell
git init -b main
git add .
git commit -m "Wincy for Windows 1.0"
git remote add origin https://github.com/<your-account>/wincy.git
git push -u origin main
```

- `.github/workflows/build.yml`: builds and runs the self tests on `windows-latest` for every push
  and pull request, uploads the `dist\Wincy.exe` artifact, and attaches the executable to a GitHub
  Release when a `v*` tag is pushed.
- `.gitignore` excludes `dist\`, test binaries and `.pdb` files: the executable is distributed as a
  Release asset rather than committed.
- `.gitattributes` normalises line endings (`*.ps1` and `*.cs` as CRLF) and marks images and
  executables as binary.

Cutting a release:

```powershell
# bump AssemblyVersion in src\AssemblyInfo.cs first
git tag v1.0.1
git push origin v1.0.1
```

## Known limitations

- **Privilege levels**: Windows UIPI does not let a normal-privilege process send keys to an
  elevated window, so Wincy cannot paste into programs running as administrator
  (`Ctrl+Enter` still copies, so you can paste manually).
- **Exclusive full-screen** games and some UWP apps may not accept the simulated `Ctrl+V`.
- **Icon source**: icons are extracted from the source executable; some UWP/Store apps only expose
  a generic icon.
- **No cloud sync** (the original Maccy has none either); everything stays on this machine.
- Windows 10 has no rounded window corners and misses a few Windows 11 visual effects
  (functionality is identical).
- The settings window needs roughly 820 dp of screen height; on short screens such as 1366×768 the
  bottom may fall off-screen (drag the title bar to move it, or use the keyboard).
- **No auto-update**: updating means rebuilding, or replacing `Wincy.exe`.

---

## License

Released under the MIT license (see [LICENSE](LICENSE)).

The original [Maccy](https://github.com/p0deje/Maccy) is developed by
[p0deje](https://github.com/p0deje) under the MIT license. This Windows version is an independent
rewrite that follows the same product idea and interaction model; all code and UI components here
are new implementations.
