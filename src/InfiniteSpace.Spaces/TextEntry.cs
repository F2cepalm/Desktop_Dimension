using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using InfiniteSpace.Native;
using InfiniteSpace.Rendering;
using InfiniteSpace.Shell;

namespace InfiniteSpace.Spaces;

internal sealed class TextEntry : Window
{
	private sealed record Reshaping(BlockGrip Grip, Point From, double FontSize, double Width, double Height);

	private const double MinWrapWidth = 24.0;

	private const double Nudge = 10.0;

	private const double MoveThreshold = 4.0;

	private const double Reach = 16.0;

	private static readonly Brush LightBacking = Freeze(new SolidColorBrush(Color.FromArgb(102, byte.MaxValue, byte.MaxValue, byte.MaxValue)));

	private static readonly Brush DarkBacking = Freeze(new SolidColorBrush(Color.FromArgb(102, 0, 0, 0)));

	private readonly TextBox _box;

	private readonly Border _frame;

	private readonly InkPalette _palette;

	private readonly Color _themeInk;

	private readonly NoteFile? _file;

	private readonly string _originalText;

	private Point _origin;

	private string? _color;

	private double _fontSize;

	private double? _wrapWidth;

	private bool _bold;

	private bool _italic;

	private double _inset = 2.0;

	private Reshaping? _reshaping;

	private Point? _pressedAt;

	private Point _pressedOrigin;

	private int _pressedCaret;

	private bool _movingText;

	private bool _done;

	private bool _closing;

	private bool _gone;

	public event Action<TextNote>? Committed;

	public event Action<string?>? ColorPicked;

	public event Action<double>? FontSizePicked;

	public event Action<bool, bool>? StylePicked;

	public event Action? Gone;

	public TextEntry(Point origin, TextNote draft, Color themeInk)
	{
		_origin = origin;
		_themeInk = themeInk;
		_color = draft.Color;
		_fontSize = draft.FontSize;
		_wrapWidth = draft.Width;
		_bold = draft.Bold;
		_italic = draft.Italic;
		_file = draft.File;
		_originalText = draft.Text;
		if (_file != null)
		{
			double valueOrDefault = _wrapWidth.GetValueOrDefault();
			if (!_wrapWidth.HasValue)
			{
				valueOrDefault = 420.0;
				_wrapWidth = valueOrDefault;
			}
		}
		Title = "Infinite Space – text";
		WindowStyle = WindowStyle.None;
		AllowsTransparency = true;
		Background = Brushes.Transparent;
		ResizeMode = ResizeMode.NoResize;
		ShowInTaskbar = false;
		Topmost = true;
		SizeToContent = SizeToContent.WidthAndHeight;
		FrameworkElement frameworkElement = ArrowHandle.Move();
		frameworkElement.Cursor = Cursors.SizeAll;
		frameworkElement.Margin = new Thickness(0.0, 2.0, 4.0, 2.0);
		frameworkElement.ToolTip = "Drag this, or the text itself, to move the text (Alt+arrows)";
		frameworkElement.MouseLeftButtonDown += (object _, MouseButtonEventArgs e) =>
		{
			DragToMove(e);
		};
		_palette = new InkPalette(themeInk, _color, 3.0, _fontSize, stacked: false, paint: false, frameworkElement, _bold, _italic)
		{
			HorizontalAlignment = HorizontalAlignment.Left,
			Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
		};
		_palette.ColorPicked += (string? color) =>
		{
			_color = color;
			ApplyColor();
			ColorPicked?.Invoke(color);
		};
		_palette.FontSizePicked += (double size) =>
		{
			_fontSize = size;
			ApplyShape();
			FontSizePicked?.Invoke(size);
		};
		_palette.StylePicked += SetStyle;
		Typeface typeface = NoteText.Typeface;
		ControlTemplate boxTemplate = new ControlTemplate(typeof(TextBox));
		FrameworkElementFactory contentHost = new FrameworkElementFactory(typeof(ScrollViewer), "PART_ContentHost");
		contentHost.SetValue(ScrollViewer.MarginProperty, new Thickness(0.0));
		contentHost.SetValue(ScrollViewer.PaddingProperty, new Thickness(0.0));
		boxTemplate.VisualTree = contentHost;
		_box = new TextBox
		{
			Template = boxTemplate,
			FontFamily = typeface.FontFamily,
			FontStyle = typeface.Style,
			FontWeight = typeface.Weight,
			BorderThickness = new Thickness(0.0),
			Padding = new Thickness(0.0),
			Margin = new Thickness(0.0),
			AcceptsReturn = true,
			AcceptsTab = false,
			Text = draft.Text
		};
		if (_file != null)
		{
			_box.MaxHeight = Math.Max(120.0, _file.Height);
			_box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
		}
		_frame = new Border
		{
			Child = _box,
			BorderThickness = new Thickness(1.0),
			MinHeight = 22.0,
			Margin = new Thickness(0.0, 0.0, 16.0, 16.0)
		};
		FrameworkElement frameworkElement2 = ArrowHandle.Stretch(0.0);
		frameworkElement2.HorizontalAlignment = HorizontalAlignment.Right;
		frameworkElement2.VerticalAlignment = VerticalAlignment.Center;
		frameworkElement2.Margin = new Thickness(0.0, 0.0, 5.0, 16.0);
		frameworkElement2.Cursor = Cursors.SizeWE;
		frameworkElement2.ToolTip = "Drag to set where lines wrap (Alt+Shift+←/→) · double-click to unwrap";
		Reshapes(frameworkElement2, BlockGrip.Right);
		FrameworkElement frameworkElement3 = ArrowHandle.Stretch(45.0);
		frameworkElement3.HorizontalAlignment = HorizontalAlignment.Right;
		frameworkElement3.VerticalAlignment = VerticalAlignment.Bottom;
		frameworkElement3.Cursor = Cursors.SizeNWSE;
		frameworkElement3.ToolTip = "Drag to resize the text (Alt+Shift+↑/↓)";
		Reshapes(frameworkElement3, BlockGrip.Right | BlockGrip.Bottom);
		Content = new StackPanel
		{
			Children = 
			{
				(UIElement)_palette,
				(UIElement)new Grid
				{
					HorizontalAlignment = HorizontalAlignment.Left,
					Children = 
					{
						(UIElement)_frame,
						(UIElement)frameworkElement2,
						(UIElement)frameworkElement3
					}
				}
			}
		};
		Left = origin.X - 1.0 - _inset;
		Top = origin.Y - 1.0 - 34.0;
		ApplyColor();
		ApplyShape();
		_box.PreviewKeyDown += OnPreviewKeyDown;
		MovesByDraggingTheText();
		Loaded += (object _, RoutedEventArgs _) =>
		{
			_inset = 0.0;
			ApplyShape();
			AlignTextWithOrigin();
			_box.Focus();
			_box.CaretIndex = ((_file == null) ? _box.Text.Length : 0);
		};
		Deactivated += (object? _, EventArgs _) =>
		{
			Finish(commit: true);
		};
		Closed += (object? _, EventArgs _) =>
		{
			Leave();
		};
	}

	public void Commit()
	{
		Finish(commit: true);
	}

	public void CommitAndFade()
	{
		Finish(commit: true, fade: true);
	}

	protected override void OnClosing(CancelEventArgs e)
	{
		_done = true;
		_closing = true;
		base.OnClosing(e);
	}

	private void OnPreviewKeyDown(object sender, KeyEventArgs e)
	{
		Key key = ((e.Key == Key.System) ? e.SystemKey : e.Key);
		ModifierKeys modifiers = Keyboard.Modifiers;
		bool flag = modifiers.HasFlag(ModifierKeys.Alt);
		if (flag)
		{
			bool flag2 = (uint)(key - 23) <= 3u;
			flag = flag2;
		}
		if (flag)
		{
			e.Handled = true;
			if (modifiers.HasFlag(ModifierKeys.Shift))
			{
				ReshapeBy(key);
				return;
			}
			MoveBy(key switch
			{
				Key.Left => new Vector(-10.0, 0.0), 
				Key.Right => new Vector(10.0, 0.0), 
				Key.Up => new Vector(0.0, -10.0), 
				_ => new Vector(0.0, 10.0), 
			});
			return;
		}
		flag = modifiers == ModifierKeys.Control;
		if (flag)
		{
			bool flag2 = ((key == Key.B || key == Key.I) ? true : false);
			flag = flag2;
		}
		if (flag)
		{
			e.Handled = true;
			SetStyle((key == Key.B) ? (!_bold) : _bold, (key == Key.I) ? (!_italic) : _italic);
			_palette.ShowStyle(_bold, _italic);
		}
		else if (e.Key == Key.Return && !modifiers.HasFlag(ModifierKeys.Shift))
		{
			e.Handled = true;
			Finish(commit: true);
		}
		else if (e.Key == Key.Escape)
		{
			e.Handled = true;
			Finish(commit: false);
		}
	}

	private void MoveBy(Vector offset)
	{
		_origin += offset;
		AlignTextWithOrigin();
	}

	private void ReshapeBy(Key key)
	{
		if (key == Key.Left || key == Key.Right)
		{
			_wrapWidth = Math.Max(24.0, TextWidth() + ((key == Key.Right) ? 10.0 : (-10.0)));
		}
		else
		{
			_fontSize = InkColors.Step(InkColors.FontSizes, _fontSize, (key == Key.Up) ? 1 : (-1));
		}
		ApplyShape();
	}

	private void MovesByDraggingTheText()
	{
		_box.PreviewMouseLeftButtonDown += (object _, MouseButtonEventArgs e) =>
		{
			bool flag = e.OriginalSource is DependencyObject element && Ancestor<ScrollBar>(element) != null;
			_pressedAt = ((e.ClickCount < 2 && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && !flag) ? new Point?(CursorOnScreen()) : ((Point?)null));
			_pressedOrigin = _origin;
		};
		_box.AddHandler(UIElement.MouseLeftButtonDownEvent, (MouseButtonEventHandler)((object _, MouseButtonEventArgs _) =>
		{
			_pressedCaret = _box.CaretIndex;
		}), handledEventsToo: true);
		PreviewMouseMove += (object _, MouseEventArgs e) =>
		{
			Point? pressedAt = _pressedAt;
			if (pressedAt.HasValue)
			{
				Point valueOrDefault = pressedAt.GetValueOrDefault();
				if (e.LeftButton != MouseButtonState.Pressed)
				{
					EndTextMove();
				}
				else
				{
					Vector vector = CursorOnScreen() - valueOrDefault;
					if (!_movingText)
					{
						if (vector.Length < 4.0)
						{
							return;
						}
						_movingText = true;
						_frame.CaptureMouse();
						_box.Select(_pressedCaret, 0);
						Mouse.OverrideCursor = Cursors.SizeAll;
					}
					e.Handled = true;
					_origin = _pressedOrigin + vector;
					AlignTextWithOrigin();
				}
			}
		};
		PreviewMouseLeftButtonUp += (object _, MouseButtonEventArgs e) =>
		{
			if (_movingText)
			{
				e.Handled = true;
			}
			EndTextMove();
		};
		_frame.LostMouseCapture += (object _, MouseEventArgs _) =>
		{
			EndTextMove();
		};
	}

	private void EndTextMove()
	{
		_pressedAt = null;
		if (_movingText)
		{
			_movingText = false;
			Mouse.OverrideCursor = null;
			if (_frame.IsMouseCaptured)
			{
				_frame.ReleaseMouseCapture();
			}
		}
	}

	private void DragToMove(MouseButtonEventArgs e)
	{
		e.Handled = true;
		try
		{
			DragMove();
		}
		catch (InvalidOperationException)
		{
			return;
		}
		_origin = TextOrigin();
	}

	private void Reshapes(FrameworkElement arrow, BlockGrip grip)
	{
		arrow.MouseLeftButtonDown += (object _, MouseButtonEventArgs e) =>
		{
			e.Handled = true;
			if (e.ClickCount == 2 && grip == BlockGrip.Right)
			{
				_wrapWidth = null;
				ApplyShape();
			}
			else
			{
				_reshaping = new Reshaping(grip, CursorOnScreen(), _fontSize, TextWidth(), Math.Max(1.0, _box.ActualHeight));
				arrow.CaptureMouse();
			}
		};
		arrow.MouseMove += (object _, MouseEventArgs _) =>
		{
			if ((object)_reshaping != null && arrow.IsMouseCaptured)
			{
				Reshape(_reshaping, CursorOnScreen() - _reshaping.From);
			}
		};
		arrow.MouseLeftButtonUp += (object _, MouseButtonEventArgs e) =>
		{
			if (arrow.IsMouseCaptured)
			{
				e.Handled = true;
				arrow.ReleaseMouseCapture();
			}
		};
		arrow.LostMouseCapture += (object _, MouseEventArgs _) =>
		{
			_reshaping = null;
		};
	}

	private void Reshape(Reshaping from, Vector drag)
	{
		if (from.Grip == BlockGrip.Right)
		{
			_wrapWidth = Math.Max(24.0, from.Width + drag.X);
		}
		else
		{
			double num = ((from.Width + drag.X) / from.Width + (from.Height + drag.Y) / from.Height) / 2.0;
			_fontSize = Math.Round(Math.Clamp(from.FontSize * num, 8.0, 200.0) * 2.0) / 2.0;
			double? wrapWidth = _wrapWidth;
			if (wrapWidth.HasValue)
			{
				_wrapWidth = Math.Max(24.0, from.Width * _fontSize / from.FontSize);
			}
		}
		ApplyShape();
	}

	private double TextWidth()
	{
		return _wrapWidth ?? Math.Max(1.0, _box.ActualWidth);
	}

	internal static void Shape(TextBox box, double fontSize, double? wrapWidth, double inset)
	{
		box.FontSize = fontSize;
		if (wrapWidth.HasValue)
		{
			double valueOrDefault = wrapWidth.GetValueOrDefault();
			box.TextWrapping = TextWrapping.Wrap;
			box.MinWidth = 0.0;
			box.Width = valueOrDefault;
		}
		else
		{
			box.TextWrapping = TextWrapping.NoWrap;
			box.MinWidth = 80.0;
			box.Width = double.NaN;
		}
	}

	private void SetStyle(bool bold, bool italic)
	{
		bool bold2 = bold;
		bool italic2 = italic;
		_bold = bold2;
		_italic = italic2;
		ApplyShape();
		StylePicked?.Invoke(bold, italic);
	}

	private void ApplyShape()
	{
		_box.FontWeight = ((_bold && _file == null) ? FontWeights.Bold : NoteText.Typeface.Weight);
		_box.FontStyle = ((_italic && _file == null) ? FontStyles.Italic : NoteText.Typeface.Style);
		Shape(_box, _fontSize, _wrapWidth, _inset);
		_palette.ShowFontSize(_fontSize);
		if (IsLoaded)
		{
			UpdateLayout();
			AlignTextWithOrigin();
		}
	}

	private void ApplyColor()
	{
		Color color = InkColors.Resolve(_color, _themeInk);
		SolidColorBrush solidColorBrush = new SolidColorBrush(color);
		_box.Foreground = solidColorBrush;
		_box.CaretBrush = solidColorBrush;
		_box.Background = (InkColors.IsDark(color) ? LightBacking : DarkBacking);
		_frame.BorderBrush = new SolidColorBrush(Color.FromArgb(153, color.R, color.G, color.B));
	}

	private void AlignTextWithOrigin()
	{
		Point point = _box.TranslatePoint(new Point(0.0, 0.0), this);
		Left = _origin.X - point.X;
		Top = _origin.Y - point.Y;
	}

	private Point TextOrigin()
	{
		Point point = _box.TranslatePoint(new Point(0.0, 0.0), this);
		return new Point(Left + point.X, Top + point.Y);
	}

	private void Finish(bool commit, bool fade = false)
	{
		if (_done)
		{
			return;
		}
		_done = true;
		EndTextMove();
		string text = _box.Text.TrimEnd();
		TextNote textNote = new TextNote
		{
			X = Math.Round(_origin.X, 2),
			Y = Math.Round(_origin.Y, 2),
			Text = text,
			FontSize = _fontSize,
			Color = _color
		};
		double? wrapWidth = _wrapWidth;
		double? width;
		if (wrapWidth.HasValue)
		{
			double valueOrDefault = wrapWidth.GetValueOrDefault();
			width = Math.Round(valueOrDefault, 2);
		}
		else
		{
			width = null;
		}
		textNote.Width = width;
		textNote.Bold = _file == null && _bold;
		textNote.Italic = _file == null && _italic;
		TextNote textNote2 = textNote;
		NoteFile? file;
		if (_file == null)
		{
			file = null;
		}
		else
		{
			file = ((text == _originalText) ? _file.Copy() : _file.ForNewText());
		}
		textNote2.File = file;
		TextNote obj = textNote;
		try
		{
			if (commit)
			{
				Committed?.Invoke(obj);
			}
		}
		finally
		{
			if (fade && IsLoaded)
			{
				Leave();
				WindowFade.Out(this, () =>
				{
					InfiniteSpace.Native.Foreground.WhenMouseUp(() =>
					{
						if (!_closing)
						{
							Close();
						}
					});
				});
			}
			else
			{
				Close();
			}
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

	private static Point CursorOnScreen()
	{
		if (!Win32.GetCursorPos(out var point))
		{
			return default;
		}
		return new Point((double)point.X / Dpi.Scale, (double)point.Y / Dpi.Scale);
	}

	private static T? Ancestor<T>(DependencyObject element) where T : DependencyObject
	{
		DependencyObject dependencyObject = element;
		while (dependencyObject != null)
		{
			if (dependencyObject is T result)
			{
				return result;
			}
			bool flag = ((dependencyObject is Visual || dependencyObject is Visual3D) ? true : false);
			dependencyObject = (flag ? VisualTreeHelper.GetParent(dependencyObject) : LogicalTreeHelper.GetParent(dependencyObject));
		}
		return null;
	}

	private static T Freeze<T>(T freezable) where T : Freezable
	{
		freezable.Freeze();
		return freezable;
	}
}
