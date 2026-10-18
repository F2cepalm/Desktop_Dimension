using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace InfiniteSpace.Rendering;

internal static class Themes
{
	public const string Dreamy = "Dreamy";

	public const string Claudish = "Claudish";

	public const string Glory = "Glory";

	public const string Burn = "Burn";

	public const string Sunday = "Sunday";

	public const string Parallax = "Parallax";

	public const string Ocean = "Ocean";

	public const string Void = "Void";

	public static IReadOnlyList<string> All { get; } = new _003C_003Ez__ReadOnlyArray<string>(new string[8] { "Dreamy", "Claudish", "Glory", "Burn", "Sunday", "Parallax", "Ocean", "Void" });

	public static string Resolve(string? name)
	{
		if (name == null || !All.Contains(name))
		{
			return "Dreamy";
		}
		return name;
	}

	public static Color InkOf(string theme)
	{
		return theme switch
		{
			"Claudish" => ClaudishStyle.Ink, 
			"Glory" => GloryLook.Ink, 
			"Burn" => BurnLook.Ink, 
			"Sunday" => SundayLook.Ink, 
			"Parallax" => ParallaxLook.Ink, 
			"Ocean" => OceanLook.Ink, 
			"Void" => VoidLook.Ink, 
			_ => Colors.White, 
		};
	}

	public static Color AccentOf(string theme)
	{
		return theme switch
		{
			"Claudish" => Color.FromRgb(92, 29, 11), 
			"Glory" => Color.FromRgb(byte.MaxValue, 198, 92), 
			"Burn" => Color.FromRgb(byte.MaxValue, 154, 60), 
			"Sunday" => Color.FromRgb(184, 59, 91), 
			"Parallax" => Color.FromRgb(46, 107, 58), 
			"Ocean" => Color.FromRgb(byte.MaxValue, 233, 138), 
			"Void" => Color.FromRgb(155, 211, byte.MaxValue), 
			_ => Color.FromRgb(95, 205, 228), 
		};
	}

	public static ISpaceLook CreateLook(string theme)
	{
		return theme switch
		{
			"Claudish" => (ISpaceLook)new InkyLook(), 
			"Glory" => new GloryLook(), 
			"Burn" => new BurnLook(), 
			"Sunday" => new SundayLook(), 
			"Parallax" => new ParallaxLook(), 
			"Ocean" => new OceanLook(), 
			"Void" => new VoidLook(), 
			_ => new DreamyLook(), 
		};
	}
}
