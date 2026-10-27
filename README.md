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

Pre-built downloads will be published on the [Releases](https://github.com/F2cepalm/Desktop_Dimension/releases) page.

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

License to be announced. Until a `LICENSE` file is added to this repository, all rights are reserved by the author.
