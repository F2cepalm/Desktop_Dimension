using System;
using System.Windows.Media;

namespace InfiniteSpace.Spaces;

internal sealed record InkPen(string? Color, double Thickness, double FontSize, bool Bold = false, bool Italic = false)
{
	public static InkPen Default { get; } = new InkPen(null, 3.0, 16.0);

	public const double MinThickness = 1.0;

	public const double MaxThickness = 40.0;

	public const double MinFontSize = 8.0;

	public const double MaxFontSize = 200.0;

	public static InkPen Create(string? color, double thickness, double fontSize, bool bold = false, bool italic = false)
	{
		Color? color2 = InkColors.Parse(color);
		object color3;
		if (color2.HasValue)
		{
			Color valueOrDefault = color2.GetValueOrDefault();
			color3 = InkColors.Format(valueOrDefault);
		}
		else
		{
			color3 = null;
		}
		return new InkPen((string?)color3, double.IsFinite(thickness) ? Math.Clamp(thickness, 1.0, 40.0) : 3.0, double.IsFinite(fontSize) ? Math.Clamp(fontSize, 8.0, 200.0) : 16.0, bold, italic);
	}
}
