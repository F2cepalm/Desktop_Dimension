using System;
using System.Numerics;
using System.Windows;
using System.Windows.Media;
using Vortice;
using Vortice.Direct2D1;

namespace InfiniteSpace.Rendering;

internal sealed class BurnLook : SceneLook
{
	public static readonly Color Ink = Color.FromRgb(byte.MaxValue, 219, 190);

	private static readonly FrameStyle Style = new FrameStyle
	{
		Line = SceneLook.Rgb(16751196u),
		LineWidth = 1.2,
		Glow = SceneLook.Rgb(16730642u, 0.9),
		GlowBlur = 3.0,
		Shade = SceneLook.Rgb(0u, 0.85),
		ShadeBlur = 18.0
	};

	private static readonly Field[] Embers = new Field[3]
	{
		new Field(34.0, 0.4, new System.Windows.Vector(8.0, -26.0), 0.012),
		new Field(60.0, 0.36, new System.Windows.Vector(14.0, -44.0), 0.022),
		new Field(112.0, 0.3, new System.Windows.Vector(22.0, -70.0), 0.036)
	};

	private static readonly (double Size, double Spread, double Streak)[] EmberSizes = new (double, double, double)[3]
	{
		(1.6, 0.8, 1.6),
		(2.4, 1.2, 2.2),
		(3.6, 1.6, 3.0)
	};

	private ID2D1Bitmap1? _ember;

	private ID2D1Bitmap1? _fire;

	protected override FrameStyle Frame => Style;

	protected override double BurstLife => 1.2;

	protected override bool Advance()
	{
		return true;
	}

	protected override void PrepareStills(ISpaceCanvas canvas, float scale)
	{
		if ((object)_ember == null)
		{
			_ember = PaintGlow(canvas, scale, 16.0, (0f, SceneLook.Rgb(16774336u)), (0.2f, SceneLook.Rgb(16761946u)), (0.5f, SceneLook.Rgb(16738850u, 0.55)), (1f, SceneLook.Rgb(13115402u, 0.0)));
		}
		if ((object)_fire == null)
		{
			_fire = PaintGlow(canvas, scale, 256.0, (0f, SceneLook.Rgb(16742958u, 0.55)), (0.5f, SceneLook.Rgb(14696474u, 0.2)), (1f, SceneLook.Rgb(14696474u, 0.0)));
		}
	}

	protected override void DrawBackdrop(ID2D1DeviceContext context, ID2D1Factory1 factory)
	{
		Rect area = Area;
		RawRectF rect = SceneLook.R(new Rect(0.0, 0.0, World.Width, World.Height));
		using (ID2D1LinearGradientBrush brush = Linear(context, new Point(0.0, area.Top), new Point(0.0, area.Bottom), (0f, SceneLook.Rgb(1377542u)), (0.5f, SceneLook.Rgb(3148298u)), (0.85f, SceneLook.Rgb(5707280u)), (1f, SceneLook.Rgb(7217170u))))
		{
			context.FillRectangle(rect, brush);
		}
		using ID2D1RadialGradientBrush brush2 = Radial(context, new Point(area.Left + area.Width / 2.0, area.Bottom + area.Height * 0.1), Math.Max(area.Width * 0.85, 60.0), Math.Max(area.Height * 0.6, 60.0), (0f, SceneLook.Rgb(16738858u, 0.35)), (0.55f, SceneLook.Rgb(13120026u, 0.12)), (1f, SceneLook.Rgb(13120026u, 0.0)));
		context.FillRectangle(rect, brush2);
	}

	protected override void DrawMotion(ID2D1DeviceContext context, ID2D1Factory1 factory, Matrix3x2 origin)
	{
		Rect area = Area;
		double num = 0.55 + 0.2 * Math.Sin(Time * 2.3) + 0.15 * Math.Sin(Time * 5.9 + 1.3) + 0.1 * Math.Sin(Time * 12.7 + 0.4);
		SceneLook.DrawSprite(context, origin, _fire, new Point(area.Left + area.Width / 2.0, area.Bottom + area.Height * 0.05), area.Width * 1.3 + 40.0, area.Height * 0.9 + 40.0, 0.0, 0.45 * num);
		for (int i = 0; i < Embers.Length; i++)
		{
			Field field = Embers[i];
			(double Size, double Spread, double Streak) tuple = EmberSizes[i];
			double item = tuple.Size;
			double item2 = tuple.Spread;
			double item3 = tuple.Streak;
			double num2 = Math.Atan2(field.Drift.Y, field.Drift.X);
			foreach (Mote item4 in Scatter(field, i, 24.0))
			{
				uint seed = item4.Seed;
				double num3 = 2.5 + 3.5 * SceneLook.Unit(seed, 0);
				double num4 = SceneLook.Unit(seed, 8);
				double x = SceneLook.Frac(Time / num3 + num4);
				double num5 = SceneLook.Smooth(0.0, 0.12, x) * (1.0 - SceneLook.Smooth(0.55, 1.0, x));
				if (!(num5 <= 0.0))
				{
					double opacity = num5 * (0.7 + 0.3 * Math.Sin(Time * (8.0 + 7.0 * SceneLook.Unit(seed, 16)) + num4 * 20.0));
					double num6 = Math.Sin(Time * (0.7 + SceneLook.Unit(seed, 24)) + num4 * 6.0);
					Point centre = item4.At + new System.Windows.Vector(5.0 * num6, 0.0);
					double num7 = item + item2 * SceneLook.Unit(seed, 16);
					SceneLook.DrawSprite(context, origin, _ember, centre, 4.0 * num7 * item3, 4.0 * num7, num2 + 0.15 * num6, opacity);
				}
			}
		}
		foreach (Burst burst in Bursts)
		{
			double num8 = Time - burst.Start;
			double num9 = 1.0 - num8 / BurstLife;
			double num10 = 40.0 + 60.0 * Math.Sqrt(num8 / BurstLife);
			SceneLook.DrawSprite(context, origin, _fire, burst.Origin, num10, num10, 0.0, 0.6 * num9 * num9);
			double num11 = (1.0 - Math.Exp((0.0 - num8) * 3.0)) / 3.0;
			for (int j = 0; j < 18; j++)
			{
				uint seed2 = SceneLook.Mix(burst.Seed + (uint)j);
				double num12 = SceneLook.Unit(seed2, 0) * (Math.PI * 2.0);
				double num13 = 50.0 + 90.0 * SceneLook.Unit(seed2, 8);
				Point centre2 = burst.Origin + new System.Windows.Vector(Math.Cos(num12), Math.Sin(num12)) * (num13 * num11) + new System.Windows.Vector(0.0, -25.0 * num8 * num8);
				double num14 = 2.5 + 1.5 * SceneLook.Unit(seed2, 16);
				double opacity2 = num9 * (0.75 + 0.25 * Math.Sin(Time * 14.0 + (double)j));
				SceneLook.DrawSprite(context, origin, _ember, centre2, 4.0 * num14 * 2.0, 4.0 * num14, num12, opacity2);
			}
		}
	}

	protected override void DisposeResources()
	{
		_ember?.Dispose();
		_fire?.Dispose();
	}
}
