using System;
using System.Numerics;
using System.Windows;
using System.Windows.Media;
using Vortice;
using Vortice.Direct2D1;

namespace InfiniteSpace.Rendering;

internal sealed class GloryLook : SceneLook
{
	public static readonly Color Ink = Color.FromRgb(byte.MaxValue, 241, 214);

	private static readonly FrameStyle Style = new FrameStyle
	{
		Line = SceneLook.Rgb(16773839u),
		LineWidth = 1.3,
		Glow = SceneLook.Rgb(16762460u, 0.95),
		Shade = SceneLook.Rgb(0u, 0.8),
		ShadeBlur = 16.0
	};

	private static readonly Field[] Motes = new Field[3]
	{
		new Field(42.0, 0.42, new System.Windows.Vector(1.5, -5.0), 0.01),
		new Field(76.0, 0.4, new System.Windows.Vector(2.5, -9.0), 0.02),
		new Field(150.0, 0.32, new System.Windows.Vector(4.0, -14.0), 0.035)
	};

	private ID2D1Bitmap1? _mote;

	private ID2D1Bitmap1? _bokeh;

	private ID2D1Bitmap1? _beam;

	protected override FrameStyle Frame => Style;

	protected override double BurstLife => 1.4;

	protected override bool Advance()
	{
		return true;
	}

	protected override void PrepareStills(ISpaceCanvas canvas, float scale)
	{
		if ((object)_mote == null)
		{
			_mote = PaintGlow(canvas, scale, 16.0, (0f, SceneLook.Rgb(16775392u)), (0.18f, SceneLook.Rgb(16769946u, 0.95)), (0.45f, SceneLook.Rgb(16761421u, 0.35)), (1f, SceneLook.Rgb(16756784u, 0.0)));
		}
		if ((object)_bokeh == null)
		{
			_bokeh = PaintGlow(canvas, scale, 64.0, (0f, SceneLook.Rgb(16770982u, 0.55)), (0.55f, SceneLook.Rgb(16766074u, 0.45)), (0.8f, SceneLook.Rgb(15906128u, 0.22)), (1f, SceneLook.Rgb(15906128u, 0.0)));
		}
		if ((object)_beam != null)
		{
			return;
		}
		_beam = canvas.Paint((int)Math.Ceiling(64f * scale), (int)Math.Ceiling(512f * scale), scale, (ID2D1DeviceContext context, ID2D1Factory1 _) =>
		{
			using ID2D1RadialGradientBrush brush = Radial(context, new Point(32.0, 0.0), 32.0, 512.0, (0f, SceneLook.Rgb(16773320u)), (0.45f, SceneLook.Rgb(16767118u, 0.45)), (1f, SceneLook.Rgb(16767118u, 0.0)));
			context.FillRectangle(new RawRectF(0f, 0f, 64f, 512f), brush);
		});
	}

	protected override void DrawBackdrop(ID2D1DeviceContext context, ID2D1Factory1 factory)
	{
		Rect area = Area;
		RawRectF rect = SceneLook.R(new Rect(0.0, 0.0, World.Width, World.Height));
		using (ID2D1LinearGradientBrush brush = Linear(context, new Point(0.0, area.Top), new Point(0.0, area.Bottom), (0f, SceneLook.Rgb(7690554u)), (0.45f, SceneLook.Rgb(3877658u)), (1f, SceneLook.Rgb(1117191u))))
		{
			context.FillRectangle(rect, brush);
		}
		using (ID2D1LinearGradientBrush brush2 = Linear(context, new Point(area.Left, 0.0), new Point(area.Left + 29.0, 0.0), ExtendMode.Mirror, (0f, SceneLook.Rgb(0u, 0.22)), (1f, SceneLook.Rgb(16769696u, 0.05))))
		{
			context.FillRectangle(rect, brush2);
		}
		using (ID2D1LinearGradientBrush brush3 = Linear(context, new Point(area.Left + 7.0, 0.0), new Point(area.Left + 54.0, 0.0), ExtendMode.Mirror, (0f, SceneLook.Rgb(0u, 0.0)), (1f, SceneLook.Rgb(0u, 0.18))))
		{
			context.FillRectangle(rect, brush3);
		}
		using ID2D1RadialGradientBrush brush4 = Radial(context, new Point(area.Left + area.Width / 2.0, area.Top - area.Height * 0.05), Math.Max(area.Width * 0.8, 80.0), Math.Max(area.Height, 80.0), (0f, SceneLook.Rgb(16767374u, 0.55)), (0.5f, SceneLook.Rgb(14919248u, 0.18)), (1f, SceneLook.Rgb(14919248u, 0.0)));
		context.FillRectangle(rect, brush4);
	}

	protected override void DrawMotion(ID2D1DeviceContext context, ID2D1Factory1 factory, Matrix3x2 origin)
	{
		Rect area = Area;
		for (int i = 0; i < 4; i++)
		{
			double num = ((double)i + 0.5) / 4.0 + 0.05 * Math.Sin(Time * 0.11 + (double)i * 2.3);
			double num2 = area.Height * 1.25 + 40.0;
			double width = 38.0 + (double)(24 * (i * 37 % 5)) / 4.0;
			double angle = (0.5 - num) * 0.5 + 0.04 * Math.Sin(Time * 0.17 + (double)i * 1.3);
			double opacity = 0.35 + 0.25 * (0.5 + 0.5 * Math.Sin(Time * 0.45 + (double)i * 1.9));
			SceneLook.DrawSprite(centre: new Point(area.Left + area.Width * num, area.Top - 12.0) + SceneLook.Rotate(new System.Windows.Vector(0.0, num2 / 2.0), angle), context: context, origin: origin, sprite: _beam, width: width, height: num2, angle: angle, opacity: opacity);
		}
		for (int j = 0; j < Motes.Length; j++)
		{
			foreach (Mote item in Scatter(Motes[j], j, 40.0))
			{
				uint seed = item.Seed;
				double num3 = SceneLook.Unit(seed, 0) * (Math.PI * 2.0);
				Point centre = item.At + new System.Windows.Vector(5.0 * Math.Sin(Time * (0.25 + 0.3 * SceneLook.Unit(seed, 8)) + num3), 0.0);
				double num4 = 0.55 + 0.45 * Math.Sin(Time * (0.8 + 1.2 * SceneLook.Unit(seed, 16)) + num3 * 3.0);
				switch (j)
				{
				case 0:
				{
					double num7 = 8.0 + 4.0 * SceneLook.Unit(seed, 24);
					SceneLook.DrawSprite(context, origin, _mote, centre, num7, num7, 0.0, 0.6 * num4);
					break;
				}
				case 1:
				{
					double num6 = 13.0 + 8.0 * SceneLook.Unit(seed, 24);
					SceneLook.DrawSprite(context, origin, _mote, centre, num6, num6, 0.0, 0.8 * num4);
					break;
				}
				default:
				{
					double num5 = 24.0 + 22.0 * SceneLook.Unit(seed, 24);
					SceneLook.DrawSprite(context, origin, _bokeh, centre, num5, num5, 0.0, 0.2 + 0.2 * num4);
					break;
				}
				}
			}
		}
		foreach (Burst burst in Bursts)
		{
			double num8 = Time - burst.Start;
			double num9 = 1.0 - num8 / BurstLife;
			double num10 = 30.0 + 90.0 * Math.Sqrt(num8 / BurstLife);
			SceneLook.DrawSprite(context, origin, _mote, burst.Origin, num10, num10, 0.0, 0.8 * num9 * num9);
			double num11 = (1.0 - Math.Exp((0.0 - num8) * 3.0)) / 3.0;
			for (int k = 0; k < 16; k++)
			{
				uint seed2 = SceneLook.Mix(burst.Seed + (uint)k);
				double num12 = SceneLook.Unit(seed2, 0) * (Math.PI * 2.0);
				double num13 = 40.0 + 90.0 * SceneLook.Unit(seed2, 8);
				Point centre2 = burst.Origin + new System.Windows.Vector(Math.Cos(num12), Math.Sin(num12)) * (num13 * num11) + new System.Windows.Vector(0.0, -14.0 * num8);
				double num14 = 8.0 + 6.0 * SceneLook.Unit(seed2, 16);
				SceneLook.DrawSprite(context, origin, _mote, centre2, num14, num14, 0.0, num9);
			}
		}
	}

	protected override void DisposeResources()
	{
		_mote?.Dispose();
		_bokeh?.Dispose();
		_beam?.Dispose();
	}
}
