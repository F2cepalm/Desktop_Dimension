using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using InfiniteSpace.Native;
using InfiniteSpace.Rendering;
using InfiniteSpace.Spaces;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace InfiniteSpace.Shell;

internal sealed class ShellController(SpaceManager spaces, SpaceStore store, Universe universe, ILogger<ShellController> logger) : IDisposable
{
	private sealed class HoverKey(ModifierKeys modifiers, Key key, Action handler, bool repeat = false)
	{
		private bool _held;

		private int? _id;

		public void Hold(GlobalHotkeys hotkeys, bool hold)
		{
			if (hold == _held)
			{
				return;
			}
			_held = hold;
			if (hold)
			{
				_id = hotkeys.Register(modifiers, key, handler, repeat);
				return;
			}
			int? id = _id;
			if (id.HasValue)
			{
				int valueOrDefault = id.GetValueOrDefault();
				hotkeys.Unregister(valueOrDefault);
				_id = null;
			}
		}
	}

	internal static readonly TimeSpan LetterKeysAfterMouseMove = TimeSpan.FromSeconds(3L);

	private readonly DispatcherTimer _hoverTimer = new DispatcherTimer(DispatcherPriority.Background)
	{
		Interval = TimeSpan.FromMilliseconds(50L)
	};

	private readonly CursorActivity _cursor = new CursorActivity(LetterKeysAfterMouseMove);

	private readonly Stopwatch _clock = Stopwatch.StartNew();

	private GlobalHotkeys? _hotkeys;

	private WindowEvents? _windowEvents;

	private TrayIcon? _tray;

	private GpuOverlay? _overlay;

	private InkPaletteWindow? _palette;

	private FrameStatsWindow? _frameStats;

	private AppSettings _settings = new AppSettings();

	private HoverKey[] _undoKeys = Array.Empty<HoverKey>();

	private HoverKey? _typeKey;

	private HoverKey? _editKey;

	public void Start()
	{
		_settings = AppSettings.Load(store.DataDirectory);
		ApplyAutoStart();
		spaces.ApplyTheme(Themes.Resolve(_settings.Theme));
		spaces.SetPen(_settings.Pen);
		spaces.PenChanged += (InkPen pen) =>
		{
			_settings.Pen = pen;
			SaveSettings();
		};
		spaces.PaletteRequested += ShowPalette;
		spaces.Notice += (string title, string text) =>
		{
			_tray?.Notify(title, text);
		};
		spaces.PaintingStarted += () =>
		{
			_palette?.FadeOut();
		};
		spaces.RestoreAll();
		_tray = new TrayIcon(new TrayActions(() =>
		{
			nint host = Foreground.CaptureHost();
			ShowOverlay(new CanvasOverlay(OverlayMode.Draw, spaces, universe, host), host);
		}, () =>
		{
			nint host = Foreground.CaptureHost();
			ShowOverlay(new BlockOverlay(spaces, universe, host), host);
		}, () =>
		{
			ShowOverlay(new MoveOverlay(spaces, universe), IntPtr.Zero);
		}, RemoveSpace, () =>
		{
			ShowPalette(CursorOnScreen());
		}, () =>
		{
			ShowOverlay(new CanvasOverlay(OverlayMode.Erase, spaces, universe, IntPtr.Zero), IntPtr.Zero);
		}, ConfirmEraseAll, Themes.All, () => Themes.Resolve(_settings.Theme), SetTheme, () => AutoStart.IsEnabled, SetAutoStart, OpenDataFolder, () =>
		{
			Application.Current.Shutdown();
		}));
		_hotkeys = new GlobalHotkeys();
		List<string> list = new List<string>();
		if (!_hotkeys.Register(ModifierKeys.Alt | ModifierKeys.Control, Key.S, ToggleDraw).HasValue)
		{
			list.Add("Ctrl+Alt+S");
		}
		if (!_hotkeys.Register(ModifierKeys.Alt | ModifierKeys.Control, Key.B, ToggleBlock).HasValue)
		{
			list.Add("Ctrl+Alt+B");
		}
		if (!_hotkeys.Register(ModifierKeys.Alt | ModifierKeys.Control, Key.M, ToggleMove).HasValue)
		{
			list.Add("Ctrl+Alt+M");
		}
		if (!_hotkeys.Register(ModifierKeys.Alt | ModifierKeys.Control, Key.E, RemoveSpace).HasValue)
		{
			list.Add("Ctrl+Alt+E");
		}
		_undoKeys = new HoverKey[2]
		{
			new HoverKey(ModifierKeys.Control, Key.Z, () =>
			{
				spaces.Undo(spaces.SpaceUnderCursor());
			}, repeat: true),
			new HoverKey(ModifierKeys.Control, Key.Y, () =>
			{
				spaces.Redo(spaces.SpaceUnderCursor());
			}, repeat: true)
		};
		_typeKey = new HoverKey(ModifierKeys.None, Key.T, () =>
		{
			StartTyping(spaces.TypeAtCursor);
		});
		_editKey = new HoverKey(ModifierKeys.None, Key.E, () =>
		{
			StartTyping(spaces.EditAtCursor);
		});
		_hoverTimer.Tick += (object? _, EventArgs _) =>
		{
			UpdateHoverKeys();
		};
		_hoverTimer.Start();
		_windowEvents = new WindowEvents(spaces.OnWindowEvent);
		if (_settings.ShowFrameStats)
		{
			_frameStats = new FrameStatsWindow(universe);
		}
		if (list.Count > 0)
		{
			logger.LogWarning("Hotkeys already taken by another app: {Hotkeys}", list);
			_tray.Notify("Hotkey unavailable", string.Join(" and ", list) + " is taken by another app. Use the tray menu instead.");
		}
		else if (spaces.Count == 0)
		{
			_tray.Notify("Infinite Space is running", "Press Ctrl+Alt+S and draw any shape to open a space.");
		}
	}

	private void ToggleDraw()
	{
		FadeInkTools();
		nint host = Foreground.CaptureHost();
		if (!(CloseOverlay() is CanvasOverlay { Mode: OverlayMode.Draw }))
		{
			ShowOverlay(new CanvasOverlay(OverlayMode.Draw, spaces, universe, host), host);
		}
	}

	private void ToggleBlock()
	{
		FadeInkTools();
		nint host = Foreground.CaptureHost();
		if (!(CloseOverlay() is BlockOverlay))
		{
			ShowOverlay(new BlockOverlay(spaces, universe, host), host);
		}
	}

	private void ToggleMove()
	{
		FadeInkTools();
		nint host = Foreground.CaptureHost();
		if (!(CloseOverlay() is MoveOverlay))
		{
			ShowOverlay(new MoveOverlay(spaces, universe), host);
		}
	}

	private GpuOverlay? CloseOverlay()
	{
		GpuOverlay? overlay = _overlay;
		if (overlay != null)
		{
			overlay.Dismiss();
			return overlay;
		}
		return overlay;
	}

	private void ShowOverlay(GpuOverlay overlay, nint host)
	{
		_overlay?.Dismiss();
		FadeInkTools();
		overlay.Closed += (object? _, EventArgs _) =>
		{
			if (_overlay == overlay)
			{
				_overlay = null;
			}
			Foreground.RestoreTo(host);
			spaces.Restack();
		};
		_overlay = overlay;
		overlay.Show();
	}

	private void UpdateHoverKeys()
	{
		if (_hotkeys != null)
		{
			bool flag = Win32.GetCursorPos(out var point) && _cursor.Sample(new Point(point.X, point.Y), _clock.Elapsed);
			SpaceManager.Hover hover = ((_overlay == null && !spaces.IsTyping) ? spaces.HoverAtCursor() : SpaceManager.Hover.None);
			HoverKey[] undoKeys = _undoKeys;
			for (int i = 0; i < undoKeys.Length; i++)
			{
				undoKeys[i].Hold(_hotkeys, hover != SpaceManager.Hover.None);
			}
			_typeKey?.Hold(_hotkeys, flag && hover != SpaceManager.Hover.None);
			_editKey?.Hold(_hotkeys, flag && hover == SpaceManager.Hover.Note);
		}
	}

	private void StartTyping(Func<bool> open)
	{
		_palette?.FadeOut();
		if (open())
		{
			UpdateHoverKeys();
		}
	}

	private void FadeInkTools()
	{
		_palette?.FadeOut();
		spaces.CommitTyping(fade: true);
	}

	private void ShowPalette(Point at)
	{
		_palette?.Dismiss();
		spaces.CommitTyping();
		nint previousApp = Foreground.CaptureHost();
		Action openFile = (spaces.HasSpaceAt(at) ? ((Action)(() =>
		{
			OpenFilesAt(at);
		})) : null);
		InkPaletteWindow palette = new InkPaletteWindow(at, spaces.Pen, Themes.InkOf(spaces.Theme), spaces.SetPen, openFile);
		palette.Gone += () =>
		{
			if (_palette == palette)
			{
				_palette = null;
			}
			Foreground.WhenMouseUp(() =>
			{
				Foreground.RestoreTo(previousApp);
				spaces.Restack();
			});
		};
		_palette = palette;
		palette.Show();
		palette.Activate();
	}

	private void OpenFilesAt(Point at)
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = "Open on the space",
			Filter = "Text and Markdown files|*.txt;*.md;*.markdown;*.mdown;*.mkd;*.text;*.log|All files|*.*",
			Multiselect = true
		};
		if (openFileDialog.ShowDialog() == true)
		{
			spaces.OpenFiles(openFileDialog.FileNames, at);
		}
	}

	private static Point CursorOnScreen()
	{
		if (!Win32.GetCursorPos(out var point))
		{
			return new Point(SystemParameters.PrimaryScreenWidth / 2.0, SystemParameters.PrimaryScreenHeight / 2.0);
		}
		return new Point((double)point.X / Dpi.Scale, (double)point.Y / Dpi.Scale);
	}

	private void RemoveSpace()
	{
		_overlay?.Dismiss();
		FadeInkTools();
		if (Win32.GetCursorPos(out var point))
		{
			spaces.RemoveAt(new Point((double)point.X / Dpi.Scale, (double)point.Y / Dpi.Scale));
		}
	}

	private void SetTheme(string name)
	{
		_settings.Theme = Themes.Resolve(name);
		spaces.ApplyTheme(_settings.Theme);
		SaveSettings();
	}

	private void ApplyAutoStart()
	{
		if (_settings.StartWithWindows != false && TrySetAutoStart(enabled: true) && !_settings.StartWithWindows.HasValue)
		{
			_settings.StartWithWindows = true;
			SaveSettings();
		}
	}

	private void SetAutoStart(bool enabled)
	{
		if (TrySetAutoStart(enabled))
		{
			_settings.StartWithWindows = enabled;
			SaveSettings();
		}
	}

	private bool TrySetAutoStart(bool enabled)
	{
		try
		{
			AutoStart.Set(enabled);
			return true;
		}
		catch (Exception ex) when ((ex is UnauthorizedAccessException || ex is SecurityException || ex is IOException) ? true : false)
		{
			logger.LogWarning(ex, "Could not change Start with Windows");
			return false;
		}
	}

	private void SaveSettings()
	{
		try
		{
			_settings.Save(store.DataDirectory);
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) ? true : false)
		{
			logger.LogWarning(ex, "Could not save settings");
		}
	}

	private void ConfirmEraseAll()
	{
		if (spaces.Count != 0)
		{
			FadeInkTools();
			if (MessageBox.Show($"Erase all {spaces.Count} space(s)? This cannot be undone.", "Infinite Space", MessageBoxButton.YesNo, MessageBoxImage.Exclamation, MessageBoxResult.No) == MessageBoxResult.Yes)
			{
				spaces.EraseAll();
			}
		}
	}

	private void OpenDataFolder()
	{
		Directory.CreateDirectory(store.DataDirectory);
		Process.Start(new ProcessStartInfo("explorer.exe", "\"" + store.DataDirectory + "\"")
		{
			UseShellExecute = true
		});
	}

	public void Dispose()
	{
		_hoverTimer.Stop();
		_frameStats?.Dispose();
		_windowEvents?.Dispose();
		_overlay?.Dismiss();
		_palette?.Dismiss();
		_hotkeys?.Dispose();
		_tray?.Dispose();
		spaces.Dispose();
		universe.Dispose();
	}
}
