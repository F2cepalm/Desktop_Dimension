using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace InfiniteSpace.Spaces;

internal static class InkColors
{
	private static readonly ConcurrentDictionary<string, Color?> Parsed = new ConcurrentDictionary<string, Color?>();

	public static IReadOnlyList<string?> Palette { get; } = new _003C_003Ez__ReadOnlyArray<string>(new string[10] { null, "#FFFFFF", "#151515", "#FF5A5F", "#FF9F1C", "#FFD93D", "#4CD964", "#3FA7FF", "#9B6DFF", "#FF6DA8" });

	public static IReadOnlyList<double> Thicknesses { get; } = new _003C_003Ez__ReadOnlyArray<double>(new double[6] { 1.5, 3.0, 5.0, 8.0, 12.0, 20.0 });

	public static IReadOnlyList<double> FontSizes { get; } = new _003C_003Ez__ReadOnlyArray<double>(new double[11]
	{
		10.0, 12.0, 14.0, 16.0, 20.0, 24.0, 32.0, 40.0, 56.0, 72.0,
		96.0
	});

	public static Color? Parse(string? color)
	{
		if (string.IsNullOrWhiteSpace(color))
		{
			return null;
		}
		if (Parsed.TryGetValue(color, out var value))
		{
			return value;
		}
		Color? color2;
		try
		{
			color2 = ((ColorConverter.ConvertFromString(color) is Color color3) ? new Color?(Color.FromRgb(color3.R, color3.G, color3.B)) : ((Color?)null));
		}
		catch (FormatException)
		{
			color2 = null;
		}
		if (Parsed.Count < 1024)
		{
			Parsed[color] = color2;
		}
		return color2;
	}

	public static string Format(Color color)
	{
		return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
	}

	public static Color Resolve(string? color, Color themeInk)
	{
		return Parse(color) ?? themeInk;
	}

	public static bool IsDark(Color color)
	{
		return 0.2126 * (double)(int)color.R + 0.7152 * (double)(int)color.G + 0.0722 * (double)(int)color.B < 128.0;
	}

	public static double Step(IReadOnlyList<double> steps, double current, int direction)
	{
		if (direction > 0)
		{
			return steps.FirstOrDefault((double s) => s > current + 1E-06, steps[steps.Count - 1]);
		}
		return steps.LastOrDefault((double s) => s < current - 1E-06, steps[0]);
	}
}
