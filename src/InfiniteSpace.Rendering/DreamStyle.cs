using System.Windows;
using System.Windows.Media;

namespace InfiniteSpace.Rendering;

internal static class DreamStyle
{
	public const double ArtPixel = 2.0;

	public static readonly Color Void = Color.FromRgb(0, 0, 0);

	public static readonly Color Rim = Colors.White;

	public const double WobbleAmplitude = 1.3;

	public const double RippleAmplitude = 2.5;

	public const double LookParallax = 0.035;

	public static readonly Vector Drift = new Vector(4.0, -2.0);

	public static readonly StarLayer[] Layers = new StarLayer[3]
	{
		new StarLayer(26.0, 0.55, 0.3, new int[4] { 0, 0, 1, 0 }, 2.0, new Color[3]
		{
			Hex(5992161u),
			Hex(13384507u),
			Hex(8235620u)
		}),
		new StarLayer(40.0, 0.6, 0.6, new int[4] { 0, 1, 2, 1 }, 2.6, new Color[3]
		{
			Hex(6278628u),
			Hex(8368734u),
			Hex(14702156u)
		}),
		new StarLayer(64.0, 0.55, 1.0, new int[4] { 1, 2, 3, 2 }, 3.2, new Color[3]
		{
			Hex(16772881u),
			Hex(16711888u),
			Hex(566032u)
		})
	};

	private static Color Hex(uint rgb)
	{
		return Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
	}
}
