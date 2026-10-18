using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using InfiniteSpace.Native;
using InfiniteSpace.Spaces;

namespace InfiniteSpace.Shell;

internal sealed class InkPaletteWindow : Window
{
	private InkPen _pen;

	private bool _closing;

	private bool _fading;

	private bool _gone;

	private bool _openingFile;

	public event Action? Gone;

	public InkPaletteWindow(Point at, InkPen pen, Color themeInk, Action<InkPen> changed, Action? openFile = null)
	{
		InkPaletteWindow inkPaletteWindow = this;
		_pen = pen;
		Title = "Infinite Space – ink";
		WindowStyle = WindowStyle.None;
		AllowsTransparency = true;
		Background = Brushes.Transparent;
		ResizeMode = ResizeMode.NoResize;
		ShowInTaskbar = false;
		Topmost = true;
		SizeToContent = SizeToContent.WidthAndHeight;
		Left = at.X;
		Top = at.Y;
		InkPalette inkPalette = new InkPalette(themeInk, pen.Color, pen.Thickness, pen.FontSize, stacked: true, paint: true, null, pen.Bold, pen.Italic);
		inkPalette.ColorPicked += (string? color) =>
		{
			Action<InkPen> action = changed;
			InkPaletteWindow inkPaletteWindow2 = inkPaletteWindow;
			InkPen inkPen = inkPaletteWindow._pen with
			{
				Color = color
			};
			InkPen obj = inkPen;
			inkPaletteWindow2._pen = inkPen;
			action(obj);
		};
		inkPalette.ThicknessPicked += (double thickness) =>
		{
			Action<InkPen> action = changed;
			InkPaletteWindow inkPaletteWindow2 = inkPaletteWindow;
			InkPen inkPen = inkPaletteWindow._pen with
			{
				Thickness = thickness
			};
			InkPen obj = inkPen;
			inkPaletteWindow2._pen = inkPen;
			action(obj);
		};
		inkPalette.FontSizePicked += (double fontSize) =>
		{
			Action<InkPen> action = changed;
			InkPaletteWindow inkPaletteWindow2 = inkPaletteWindow;
			InkPen inkPen = inkPaletteWindow._pen with
			{
				FontSize = fontSize
			};
			InkPen obj = inkPen;
			inkPaletteWindow2._pen = inkPen;
			action(obj);
		};
		inkPalette.StylePicked += (bool bold, bool italic) =>
		{
			Action<InkPen> action = changed;
			InkPaletteWindow inkPaletteWindow2 = inkPaletteWindow;
			InkPen inkPen = inkPaletteWindow._pen with
			{
				Bold = bold,
				Italic = italic
			};
			InkPen obj = inkPen;
			inkPaletteWindow2._pen = inkPen;
			action(obj);
		};
		Content = ((openFile == null) ? ((object)inkPalette) : ((object)new StackPanel
		{
			Children = 
			{
				(UIElement)inkPalette,
				(UIElement)OpenFileButton(openFile)
			}
		}));
		Loaded += (object _, RoutedEventArgs _) =>
		{
			inkPaletteWindow.KeepOnScreen();
		};
		Deactivated += (object? _, EventArgs _) =>
		{
			if (!inkPaletteWindow._openingFile)
			{
				inkPaletteWindow.FadeOut();
			}
		};
		Closed += (object? _, EventArgs _) =>
		{
			inkPaletteWindow.Leave();
		};
	}

	public void Dismiss()
	{
		if (!_closing)
		{
			Close();
		}
	}

	public void FadeOut()
	{
		if (!_closing && !_fading)
		{
			_fading = true;
			Leave();
			WindowFade.Out(this, () =>
			{
				InfiniteSpace.Native.Foreground.WhenMouseUp(Dismiss);
			});
		}
	}

	private void Leave()
	{
		if (!_gone)
		{
			_gone = true;
			Gone?.Invoke();
		}
	}

	protected override void OnClosing(CancelEventArgs e)
	{
		_closing = true;
		base.OnClosing(e);
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		Key key = e.Key;
		if ((key == Key.Return || key == Key.Escape) ? true : false)
		{
			e.Handled = true;
			Dismiss();
		}
	}

	private Border OpenFileButton(Action openFile)
	{
		SolidColorBrush chrome = new SolidColorBrush(Color.FromArgb(230, 30, 30, 30));
		SolidColorBrush hover = new SolidColorBrush(Color.FromArgb(byte.MaxValue, 58, 58, 58));
		Border button = new Border
		{
			Background = chrome,
			CornerRadius = new CornerRadius(6.0),
			Margin = new Thickness(0.0, 4.0, 0.0, 0.0),
			Padding = new Thickness(10.0, 5.0, 10.0, 6.0),
			Cursor = Cursors.Hand,
			ToolTip = "Put a .txt or .md file on the space here (you can also drop files on a space)",
			Child = new TextBlock
			{
				Text = "Open a file…",
				Foreground = Brushes.White,
				FontSize = 12.0
			}
		};
		button.MouseEnter += (object _, MouseEventArgs _) =>
		{
			button.Background = hover;
		};
		button.MouseLeave += (object _, MouseEventArgs _) =>
		{
			button.Background = chrome;
		};
		button.MouseLeftButtonDown += (object _, MouseButtonEventArgs e) =>
		{
			e.Handled = true;
		};
		button.MouseLeftButtonUp += (object _, MouseButtonEventArgs e) =>
		{
			e.Handled = true;
			_openingFile = true;
			Hide();
			try
			{
				openFile();
			}
			finally
			{
				Dismiss();
			}
		};
		return button;
	}

	private void KeepOnScreen()
	{
		double num = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth;
		double num2 = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;
		Left = Math.Max(SystemParameters.VirtualScreenLeft, Math.Min(Left, num - ActualWidth));
		Top = Math.Max(SystemParameters.VirtualScreenTop, Math.Min(Top, num2 - ActualHeight));
	}
}
