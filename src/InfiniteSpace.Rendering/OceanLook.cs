using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows;
using System.Windows.Media;
using Vortice;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;

namespace InfiniteSpace.Rendering;

internal sealed class OceanLook : SceneLook
{
	public static readonly Color Ink = Colors.White;

	private const double Period = 256.0;

	private const double RowPitch = 30.0;

	private const double TileHeight = 76.0;

	private const double CrestAt = 26.0;

	private const int Shapes = 3;

	private static readonly FrameStyle Style = new FrameStyle
	{
		Line = SceneLook.Rgb(15924223u),
		LineWidth = 1.4,
		Glow = SceneLook.Rgb(12577023u, 0.85),
		Shade = SceneLook.Rgb(539258u, 0.5),
		ShadeBlur = 11.0
	};

	private static readonly Field Glints = new Field(64.0, 0.35, new System.Windows.Vector(12.0, 0.0), 0.02);

	private readonly ID2D1Bitmap1?[] _tiles = new ID2D1Bitmap1[3];

	private readonly ID2D1BitmapBrush1?[] _swells = new ID2D1BitmapBrush1[3];

	private ID2D1Bitmap1? _glint;

	private ID2D1SolidColorBrush? _ring;

	protected override FrameStyle Frame => Style;

	protected override double BurstLife => 1.3;

	protected override bool Advance()
	{
		return true;
	}

	protected override void PrepareStills(ISpaceCanvas canvas, float scale)
	{
		for (int i = 0; i < 3; i++)
		{
			ID2D1Bitmap1[] tiles = _tiles;
			int num = i;
			if ((object)tiles[num] == null)
			{
				tiles[num] = PaintSwell(canvas, scale, i);
			}
		}
		if ((object)_glint == null)
		{
			_glint = PaintGlow(canvas, scale, 16.0, (0f, Colors.White), (0.25f, SceneLook.Rgb(15267839u, 0.8)), (1f, SceneLook.Rgb(15267839u, 0.0)));
		}
	}

	protected override void DrawBackdrop(ID2D1DeviceContext context, ID2D1Factory1 factory)
	{
		using ID2D1LinearGradientBrush brush = Linear(context, new Point(0.0, Area.Top), new Point(0.0, Area.Bottom), (0f, SceneLook.Rgb(6140138u)), (1f, SceneLook.Rgb(1994434u)));
		context.FillRectangle(SceneLook.R(new Rect(0.0, 0.0, World.Width, World.Height)), brush);
	}

	protected override void DrawMotion(ID2D1DeviceContext context, ID2D1Factory1 factory, Matrix3x2 origin)
	{
		int num = (int)Math.Floor((World.Top - 76.0) / 30.0) - 1;
		int num2 = (int)Math.Ceiling((World.Bottom + 26.0) / 30.0) + 1;
		float right = (float)World.Width;
		for (int i = num; i <= num2; i++)
		{
			uint num3 = SceneLook.Hash(i, 0, 7);
			int num4 = (int)(num3 % 3);
			double num5 = 9.0 + 9.0 * SceneLook.Unit(num3, 8);
			double num6 = 3.2 * Math.Sin(Time * 0.9 + (double)i * 0.83) + 1.5 * Math.Sin(Time * 1.7 + (double)i * 2.1);
			double num7 = (double)i * 30.0 + num6 - Look.Y * 0.012 - 26.0 - World.Top;
			if (!(num7 > World.Height) && !(num7 + 76.0 < 0.0))
			{
				double num8 = (SceneLook.Unit(num3, 16) * 256.0 + Time * num5 - Look.X * 0.02 - World.Left) % 256.0;
				ID2D1BitmapBrush1[] swells = _swells;
				int num9 = num4;
				ID2D1BitmapBrush1 iD2D1BitmapBrush = swells[num9] ?? (swells[num9] = context.CreateBitmapBrush(_tiles[num4], new BitmapBrushProperties1(ExtendMode.Wrap, ExtendMode.Clamp, InterpolationMode.Linear)));
				iD2D1BitmapBrush.Transform = Matrix3x2.CreateTranslation((float)num8, (float)num7);
				context.FillRectangle(new RawRectF(0f, (float)num7, right, (float)(num7 + 76.0)), iD2D1BitmapBrush);
			}
		}
		foreach (Mote item in Scatter(Glints, 0, 10.0))
		{
			uint seed = item.Seed;
			double num10 = 1.6 + 2.0 * SceneLook.Unit(seed, 0);
			double num11 = SceneLook.Frac(Time / num10 + SceneLook.Unit(seed, 8));
			if (!(num11 >= 0.18))
			{
				double num12 = 5.0 + 4.0 * SceneLook.Unit(seed, 16);
				SceneLook.DrawSprite(context, origin, _glint, item.At, num12 * 1.6, num12, 0.0, 0.9 * Math.Sin(Math.PI * num11 / 0.18));
			}
		}
		context.Transform = origin;
		if ((object)_ring == null)
		{
			_ring = SceneLook.Solid(context, Colors.White);
		}
		foreach (Burst burst in Bursts)
		{
			double num13 = Time - burst.Start;
			for (int j = 0; j < 3; j++)
			{
				double num14 = num13 - (double)j * 0.15;
				double num15 = num14 / (BurstLife - (double)j * 0.15);
				if (!(num14 <= 0.0) && !(num15 >= 1.0))
				{
					float num16 = (float)(6.0 + 70.0 * (1.0 - Math.Exp((0.0 - num14) * 2.2)));
					_ring.Opacity = (float)(0.7 * (1.0 - num15));
					context.DrawEllipse(new Ellipse(SceneLook.V(burst.Origin), num16, num16 * 0.8f), _ring, 1.6f);
				}
			}
		}
	}

	protected override void DisposeResources()
	{
		ID2D1BitmapBrush1[] swells = _swells;
		for (int i = 0; i < swells.Length; i++)
		{
			swells[i]?.Dispose();
		}
		ID2D1Bitmap1[] tiles = _tiles;
		for (int i = 0; i < tiles.Length; i++)
		{
			tiles[i]?.Dispose();
		}
		_glint?.Dispose();
		_ring?.Dispose();
	}

	private ID2D1Bitmap1 PaintSwell(ISpaceCanvas canvas, float scale, int shape)
	{
		uint seed = SceneLook.Mix((uint)(shape * -1640531527 + 1));
		double p1 = SceneLook.Unit(seed, 0) * (Math.PI * 2.0);
		double p2 = SceneLook.Unit(seed, 8) * (Math.PI * 2.0);
		double p3 = SceneLook.Unit(seed, 16) * (Math.PI * 2.0);
		double a1 = 5.0 + 2.0 * SceneLook.Unit(seed, 24);
		int num = (int)Math.Round(256.0 * (double)scale);
		int height = (int)Math.Ceiling(76.0 * (double)scale);
		float period = (float)num / scale;
		List<Vector2> crest = new List<Vector2>();
		for (int i = 0; i <= 192; i++)
		{
			double num2 = (float)(i * 3) * period / 192f;
			crest.Add(new Vector2((float)num2, (float)Crest(num2)));
		}
		ID2D1Bitmap1 water = canvas.Paint(3 * num, height, scale, (ID2D1DeviceContext context, ID2D1Factory1 factory) =>
		{
			List<Vector2> points = new List<Vector2>(crest)
			{
				new Vector2(3f * period, 77f),
				new Vector2(0f, 77f)
			};
			using ID2D1PathGeometry geometry = SceneLook.Path(factory, points, closed: true);
			using ID2D1PathGeometry geometry2 = SceneLook.Path(factory, crest, closed: false);
			double from = 16.0;
			using (ID2D1LinearGradientBrush brush = Linear(context, new Point(0.0, from), new Point(0.0, 76.0), (0f, SceneLook.Rgb(11066103u)), (At(30.0), SceneLook.Rgb(7190764u)), (At(46.0), SceneLook.Rgb(4364510u)), (1f, SceneLook.Rgb(2916561u))))
			{
				context.FillGeometry(geometry, brush);
			}
			using (ID2D1LinearGradientBrush brush2 = Linear(context, new Point(0.0, 0.0), new Point(period / 6f, 0.0), ExtendMode.Mirror, (0f, SceneLook.Rgb(16055295u, 0.9)), (1f, SceneLook.Rgb(16055295u, 0.12))))
			{
				context.DrawGeometry(geometry2, brush2, 1.3f);
			}
			context.Transform = Matrix3x2.CreateTranslation(0f, 2.5f);
			using (ID2D1SolidColorBrush brush3 = SceneLook.Solid(context, SceneLook.Rgb(14545407u, 0.18)))
			{
				context.DrawGeometry(geometry2, brush3, 5f);
			}
			context.Transform = Matrix3x2.Identity;
			float At(double y)
			{
				return (float)((y - from) / (76.0 - from));
			}
		});
		try
		{
			return canvas.Paint(num, height, scale, (ID2D1DeviceContext context, ID2D1Factory1 _) =>
			{
				using Shadow shadow = new Shadow(context)
				{
					BlurStandardDeviation = 4f,
					Color = SceneLook.ToVector4(SceneLook.Rgb(740501u, 0.55))
				};
				shadow.SetInput(0u, water, true);
				SceneLook.DrawEffect(context, shadow, new Vector2(0f - period, -3f));
				context.DrawImage(water, new Vector2(0f - period, 0f), null, InterpolationMode.NearestNeighbor, CompositeMode.SourceOver);
			});
		}
		finally
		{
			if ((object)water != null)
			{
				((IDisposable)water).Dispose();
			}
		}
		double Crest(double x)
		{
			return 26.0 + a1 * Math.Sin(Math.PI * 2.0 * x / 256.0 + p1) + 3.0 * Math.Sin(Math.PI * 4.0 * x / 256.0 + p2) + 1.3 * Math.Sin(Math.PI * 8.0 * x / 256.0 + p3);
		}
	}
}
