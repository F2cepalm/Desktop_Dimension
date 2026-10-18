using System.Windows;
using System.Windows.Media;

namespace InfiniteSpace.Rendering;

internal sealed record FrameStyle
{
	public required Color Line { get; init; }

	public double LineWidth { get; init; } = 1.5;

	public Color? Glow { get; init; }

	public double GlowWidth { get; init; } = 2.5;

	public double GlowBlur { get; init; } = 2.5;

	public Color? Shade { get; init; }

	public double ShadeBlur { get; init; } = 12.0;

	public Color? Cast { get; init; }

	public Vector CastOffset { get; init; }

	public double CastBlur { get; init; } = 4.0;
}
