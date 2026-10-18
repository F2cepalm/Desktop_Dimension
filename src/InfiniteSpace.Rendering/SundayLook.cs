using System;
using System.Numerics;
using System.Windows;
using System.Windows.Media;
using Vortice;
using Vortice.Direct2D1;

namespace InfiniteSpace.Rendering;

internal sealed class SundayLook : SceneLook
{
	public static readonly Color Ink = Color.FromRgb(90, 38, 56);

	private const double FrameRate = 30.0;

	private static readonly FrameStyle Style = new FrameStyle
	{
		Line = SceneLook.Rgb(16775666u),
		LineWidth = 1.5,
		Glow = SceneLook.Rgb(15299198u, 0.6),
		GlowWidth = 3.0,
		GlowBlur = 3.0,
		Shade = SceneLook.Rgb(12868202u, 0.22),
		ShadeBlur = 12.0
	};

	private static readonly Field[] Wisps = new Field[2]
	{
		new Field(130.0, 0.45, new System.Windows.Vector(5.0, 0.0), 0.008),
		new Field(210.0, 0.4, new System.Windows.Vector(9.0, 0.0), 0.018)
	};

	private ID2D1Bitmap1? _wisp;

	private ID2D1Bitmap1? _sun;

	protected override FrameStyle Frame => Style;

	protected override double BurstLife => 1.1;

	private Point Sun => new Point(Area.Left + Area.Width * 0.74, Area.Top + Area.Height * 0.3);

	private double SunRadius => Math.Max(Math.Max(Area.Width, Area.Height) * 0.65, 60.0);

	protected override bool Advance()
	{
		return Stepped(30.0) | LookMoved(Wisps[^1].Parallax);
	}

	protected override void PrepareStills(ISpaceCanvas canvas, float scale)
	{
		if ((object)_wisp == null)
		{
			_wisp = PaintGlow(canvas, scale, 64.0, (0f, SceneLook.Rgb(16774896u, 0.9)), (0.5f, SceneLook.Rgb(16773096u, 0.45)), (1f, SceneLook.Rgb(16773096u, 0.0)));
		}
		if ((object)_sun == null)
		{
			_sun = PaintGlow(canvas, scale, 128.0, (0f, SceneLook.Rgb(16776170u)), (0.15f, SceneLook.Rgb(16773580u, 0.8)), (0.45f, SceneLook.Rgb(16768424u, 0.3)), (1f, SceneLook.Rgb(16768424u, 0.0)));
		}
	}

	protected override void DrawBackdrop(ID2D1DeviceContext context, ID2D1Factory1 factory)
	{
		Rect area = Area;
		RawRectF rect = SceneLook.R(new Rect(0.0, 0.0, World.Width, World.Height));
		using (ID2D1LinearGradientBrush brush = Linear(context, new Point(0.0, area.Top), new Point(0.0, area.Bottom), (0f, SceneLook.Rgb(15635609u)), (0.35f, SceneLook.Rgb(16034975u)), (0.68f, SceneLook.Rgb(16304290u)), (1f, SceneLook.Rgb(16507822u))))
		{
			context.FillRectangle(rect, brush);
		}
		using (ID2D1RadialGradientBrush brush2 = Radial(context, Sun, SunRadius, SunRadius, (0f, SceneLook.Rgb(16775396u, 0.9)), (0.12f, SceneLook.Rgb(16773064u, 0.7)), (0.42f, SceneLook.Rgb(16767400u, 0.28)), (1f, SceneLook.Rgb(16767400u, 0.0))))
		{
			context.FillRectangle(rect, brush2);
		}
		using ID2D1LinearGradientBrush brush3 = Linear(context, new Point(0.0, area.Top + area.Height * 0.62), new Point(0.0, area.Bottom), (0f, SceneLook.Rgb(16773594u, 0.0)), (1f, SceneLook.Rgb(16773594u, 0.5)));
		context.FillRectangle(rect, brush3);
	}

	protected override void DrawMotion(ID2D1DeviceContext context, ID2D1Factory1 factory, Matrix3x2 origin)
	{
		double num = SunRadius * (0.9 + 0.05 * Math.Sin(Time * 0.5));
		SceneLook.DrawSprite(context, origin, _sun, Sun, num, num, 0.0, 0.18 + 0.1 * Math.Sin(Time * 0.3));
		for (int i = 0; i < Wisps.Length; i++)
		{
			foreach (Mote item in Scatter(Wisps[i], i, 160.0))
			{
				uint seed = item.Seed;
				double num2 = (double)((i == 0) ? 90 : 160) + 80.0 * SceneLook.Unit(seed, 0);
				double height = num2 * (0.1 + 0.05 * SceneLook.Unit(seed, 8));
				double y = 3.0 * Math.Sin(Time * 0.15 + SceneLook.Unit(seed, 16) * (Math.PI * 2.0));
				double opacity = ((i == 0) ? 0.32 : 0.26) * (0.7 + 0.3 * Math.Sin(Time * 0.2 + SceneLook.Unit(seed, 24) * (Math.PI * 2.0)));
				SceneLook.DrawSprite(context, origin, _wisp, item.At + new System.Windows.Vector(0.0, y), num2, height, 0.0, opacity);
			}
		}
		foreach (Burst burst in Bursts)
		{
			double num3 = (Time - burst.Start) / BurstLife;
			double num4 = 20.0 + 140.0 * Math.Sqrt(num3);
			SceneLook.DrawSprite(context, origin, _sun, burst.Origin, num4, num4, 0.0, 0.75 * Math.Pow(1.0 - num3, 1.5));
		}
	}

	protected override void DisposeResources()
	{
		_wisp?.Dispose();
		_sun?.Dispose();
	}
}
