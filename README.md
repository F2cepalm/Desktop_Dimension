# Desktop Dimension (InfiniteSpace)

Desktop Dimension is a Windows app for an endless, infinite canvas on your desktop. Write notes, sketch with ink, and arrange your work in spaces that you can pan and zoom freely, with a choice of visual themes.

## Features

- **Infinite canvas:** move around an unbounded workspace instead of a fixed page.
- **Notes:** write text and Markdown, with custom fonts, including a Kazakh-capable monospace font.
- **Ink:** draw and annotate with a pen.
- **Themes:** eight built-in looks, from Dreamy to Void, switchable at any time.
- **Tray icon:** keep the app running in the background and open it from the system tray.
- **Smooth rendering:** built on DirectComposition and Direct2D for responsive drawing.

## Requirements

- Windows 10 or later, 64-bit (x64)
- .NET 10 runtime, unless you use a self-contained build

## Install

Paste this into PowerShell to download, install, and launch the latest release. No .NET install or compiling needed:

```powershell
$d = "$env:LOCALAPPDATA\Programs\DesktopDimension"; Invoke-WebRequest https://github.com/F2cepalm/Desktop_Dimension/releases/download/v0.1.0/InfiniteSpace-v0.1.0-win-x64.zip -OutFile "$env:TEMP\dd.zip"; Expand-Archive "$env:TEMP\dd.zip" $d -Force; Remove-Item "$env:TEMP\dd.zip"; & "$d\InfiniteSpace\InfiniteSpace.exe"
```

The app installs to `%LOCALAPPDATA%\Programs\DesktopDimension`. You can also download the zip from the [Releases](https://github.com/F2cepalm/Desktop_Dimension/releases) page and extract it yourself. Windows SmartScreen may warn on first run because the executable is not code-signed.

## Build from source

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) and Windows.

```bash
dotnet build src/InfiniteSpace.csproj
```

To produce a runnable folder, publish the app into `publish/` and start `run.bat`:

```bash
dotnet publish src/InfiniteSpace.csproj -c Release -r win-x64 -o publish
```

## Project layout

| Path | Contents |
| --- | --- |
| `src/InfiniteSpace` | Application entry point |
| `src/InfiniteSpace.Shell` | Windows, tray icon, overlays, and app settings |
| `src/InfiniteSpace.Rendering` | Canvas rendering, themes, and text layout |
| `src/InfiniteSpace.Spaces` | Spaces, notes, ink, and file storage |
| `src/InfiniteSpace.Native` | Native interop |
| `fonts/` | Bundled fonts and font test assets |
| `scripts/` | Helper scripts, such as the font build |

## Support the project

Desktop Dimension is free to use. If it's useful to you, support for development will be available here soon.

## License

Desktop Dimension is released under the [MIT License](LICENSE). Bundled fonts and third-party components are listed in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
