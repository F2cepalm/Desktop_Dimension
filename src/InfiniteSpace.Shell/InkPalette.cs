using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using InfiniteSpace.Spaces;

namespace InfiniteSpace.Shell;

internal sealed class InkPalette : Border
{
	private const double Cell = 22.0;

	private static readonly Brush Chrome = Freeze(new SolidColorBrush(Color.FromArgb(230, 30, 30, 30)));

	private static readonly Brush Hover = Freeze(new SolidColorBrush(Color.FromArgb(64, byte.MaxValue, byte.MaxValue, byte.MaxValue)));

	private static readonly Brush On = Freeze(new SolidColorBrush(Color.FromArgb(112, byte.MaxValue, byte.MaxValue, byte.MaxValue)));

	private static readonly Brush SwatchEdge = Freeze(new SolidColorBrush(Color.FromArgb(128, byte.MaxValue, byte.MaxValue, byte.MaxValue)));

	private readonly Color _themeInk;

	private readonly Dictionary<string, Ellipse> _rings = new Dictionary<string, Ellipse>();

	private readonly TextBlock _fontSizeLabel = new TextBlock
	{
		VerticalAlignment = VerticalAlignment.Center,
		TextAlignment = TextAlignment.Center,
		MinWidth = 26.0
	};

	private readonly Ellipse _thicknessDot = new Ellipse
	{
		VerticalAlignment = VerticalAlignment.Center,
		HorizontalAlignment = HorizontalAlignment.Center
	};

	private double _thickness;

	private double _fontSize;

	private readonly Border _boldButton;

	private readonly Border _italicButton;

	private bool _bold;

	private bool _italic;

	public event Action<string?>? ColorPicked;

	public event Action<double>? ThicknessPicked;

	public event Action<double>? FontSizePicked;

	public event Action<bool, bool>? StylePicked;

	public InkPalette(Color themeInk, string? color, double thickness, double fontSize, bool stacked, bool paint, UIElement? leading = null, bool bold = false, bool italic = false)
	{
		_themeInk = themeInk;
		Background = Chrome;
		CornerRadius = new CornerRadius(6.0);
		Padding = new Thickness(4.0);
		TextElement.SetForeground(this, Brushes.White);
		TextElement.SetFontSize(this, 12.0);
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal
		};
		foreach (string item in InkColors.Palette)
		{
			stackPanel.Children.Add(Swatch(item));
		}
		_boldButton = Button(Styled("B", FontWeights.Bold, FontStyles.Normal), "Bold text (Ctrl+B)", () =>
		{
			PickStyle(!_bold, _italic);
		});
		_italicButton = Button(Styled("I", FontWeights.Normal, FontStyles.Italic), "Italic text (Ctrl+I)", () =>
		{
			PickStyle(_bold, !_italic);
		});
		StackPanel stackPanel2 = Row(Button(Letter(10.0), "Smaller text", () =>
		{
			PickFontSize(InkColors.Step(InkColors.FontSizes, _fontSize, -1));
		}), _fontSizeLabel, Button(Letter(16.0), "Bigger text", () =>
		{
			PickFontSize(InkColors.Step(InkColors.FontSizes, _fontSize, 1));
		}), _boldButton, _italicButton);
		UIElement uIElement = (paint ? Row(Button(Glyph("−"), "Thinner paint", () =>
		{
			PickThickness(InkColors.Step(InkColors.Thicknesses, _thickness, -1));
		}), new Grid
		{
			Width = 22.0,
			Height = 22.0,
			Children = { (UIElement)_thicknessDot }
		}, Button(Glyph("+"), "Thicker paint", () =>
		{
			PickThickness(InkColors.Step(InkColors.Thicknesses, _thickness, 1));
		})) : null);
		if (stacked)
		{
			StackPanel stackPanel3 = new StackPanel();
			if (leading != null)
			{
				stackPanel3.Children.Add(leading);
			}
			stackPanel3.Children.Add(stackPanel);
			if (uIElement != null)
			{
				stackPanel3.Children.Add(Labelled("Paint", uIElement));
			}
			stackPanel3.Children.Add(Labelled("Text", stackPanel2));
			Child = stackPanel3;
		}
		else
		{
			StackPanel stackPanel4 = new StackPanel
			{
				Orientation = Orientation.Horizontal
			};
			if (leading != null)
			{
				stackPanel4.Children.Add(leading);
			}
			stackPanel4.Children.Add(stackPanel);
			stackPanel4.Children.Add(Divider());
			if (uIElement != null)
			{
				stackPanel4.Children.Add(uIElement);
				stackPanel4.Children.Add(Divider());
			}
			stackPanel4.Children.Add(stackPanel2);
			Child = stackPanel4;
		}
		ShowColor(color);
		ShowThickness(thickness);
		ShowFontSize(fontSize);
		ShowStyle(bold, italic);
	}

	public void ShowStyle(bool bold, bool italic)
	{
		bool bold2 = bold;
		bool italic2 = italic;
		_bold = bold2;
		_italic = italic2;
		_boldButton.Background = (bold ? On : Brushes.Transparent);
		_italicButton.Background = (italic ? On : Brushes.Transparent);
	}

	private void PickStyle(bool bold, bool italic)
	{
		ShowStyle(bold, italic);
		StylePicked?.Invoke(bold, italic);
	}

	public void ShowColor(string? color)
	{
		foreach (var (text2, ellipse2) in _rings)
		{
			ellipse2.Visibility = ((!(text2 == SwatchKey(color))) ? Visibility.Hidden : Visibility.Visible);
		}
		_thicknessDot.Fill = new SolidColorBrush(InkColors.Resolve(color, _themeInk));
	}

	public void ShowThickness(double thickness)
	{
		_thickness = thickness;
		Ellipse thicknessDot = _thicknessDot;
		double width = (_thicknessDot.Height = Math.Clamp(thickness, 2.0, 18.0));
		thicknessDot.Width = width;
	}

	public void ShowFontSize(double fontSize)
	{
		_fontSize = fontSize;
		_fontSizeLabel.Text = Math.Round(fontSize).ToString(CultureInfo.CurrentCulture);
	}

	private void PickThickness(double thickness)
	{
		ShowThickness(thickness);
		ThicknessPicked?.Invoke(thickness);
	}

	private void PickFontSize(double fontSize)
	{
		ShowFontSize(fontSize);
		FontSizePicked?.Invoke(fontSize);
	}

	private static string SwatchKey(string? color)
	{
		return color ?? "";
	}

	private FrameworkElement Swatch(string? color)
	{
		Ellipse dot = new Ellipse
		{
			Width = 14.0,
			Height = 14.0,
			Fill = new SolidColorBrush(InkColors.Resolve(color, _themeInk)),
			Stroke = SwatchEdge,
			StrokeThickness = 1.0
		};
		if (color == null)
		{
			dot.StrokeDashArray = new DoubleCollection { 2.0, 1.5 };
		}
		Ellipse ellipse = new Ellipse
		{
			Width = 20.0,
			Height = 20.0,
			Stroke = Brushes.White,
			StrokeThickness = 2.0,
			Visibility = Visibility.Hidden
		};
		_rings[SwatchKey(color)] = ellipse;
		Grid grid = new Grid();
		grid.Width = 22.0;
		grid.Height = 22.0;
		grid.Background = Brushes.Transparent;
		grid.Cursor = Cursors.Hand;
		grid.ToolTip = ((color == null) ? "Theme ink" : color);
		grid.Children.Add(ellipse);
		grid.Children.Add(dot);
		grid.MouseEnter += (object _, MouseEventArgs _) =>
		{
			Ellipse ellipse2 = dot;
			double width = (dot.Height = 16.0);
			ellipse2.Width = width;
		};
		grid.MouseLeave += (object _, MouseEventArgs _) =>
		{
			Ellipse ellipse2 = dot;
			double width = (dot.Height = 14.0);
			ellipse2.Width = width;
		};
		Clickable(grid, () =>
		{
			ShowColor(color);
			ColorPicked?.Invoke(color);
		});
		return grid;
	}

	private static Border Button(UIElement content, string tip, Action click)
	{
		Border button = new Border
		{
			Width = 22.0,
			Height = 22.0,
			CornerRadius = new CornerRadius(4.0),
			Background = Brushes.Transparent,
			Cursor = Cursors.Hand,
			ToolTip = tip,
			Child = content
		};
		button.MouseEnter += (object _, MouseEventArgs _) =>
		{
			if (button.Background != On)
			{
				button.Background = Hover;
			}
		};
		button.MouseLeave += (object _, MouseEventArgs _) =>
		{
			if (button.Background == Hover)
			{
				button.Background = Brushes.Transparent;
			}
		};
		Clickable(button, click);
		return button;
	}

	private static void Clickable(UIElement element, Action click)
	{
		element.MouseLeftButtonDown += (object _, MouseButtonEventArgs e) =>
		{
			e.Handled = true;
		};
		element.MouseLeftButtonUp += (object _, MouseButtonEventArgs e) =>
		{
			e.Handled = true;
			click();
		};
	}

	private static TextBlock Letter(double size)
	{
		return new TextBlock
		{
			Text = "A",
			FontSize = size,
			FontWeight = FontWeights.SemiBold,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
	}

	private static TextBlock Styled(string letter, FontWeight weight, FontStyle style)
	{
		return new TextBlock
		{
			Text = letter,
			FontSize = 14.0,
			FontWeight = weight,
			FontStyle = style,
			FontFamily = new FontFamily("Georgia"),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
	}

	private static TextBlock Glyph(string glyph)
	{
		return new TextBlock
		{
			Text = glyph,
			FontSize = 16.0,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
	}

	private static StackPanel Row(params UIElement[] children)
	{
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal
		};
		foreach (UIElement element in children)
		{
			stackPanel.Children.Add(element);
		}
		return stackPanel;
	}

	private static StackPanel Labelled(string label, UIElement controls)
	{
		return Row(new TextBlock
		{
			Text = label,
			Width = 40.0,
			Margin = new Thickness(4.0, 0.0, 0.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center,
			Opacity = 0.8
		}, controls);
	}

	private static Rectangle Divider()
	{
		return new Rectangle
		{
			Width = 1.0,
			Margin = new Thickness(4.0, 3.0, 4.0, 3.0),
			Fill = SwatchEdge
		};
	}

	private static T Freeze<T>(T freezable) where T : Freezable
	{
		freezable.Freeze();
		return freezable;
	}
}
