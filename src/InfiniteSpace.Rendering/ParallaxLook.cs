using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Windows;
using System.Windows.Media;
using Vortice;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;

namespace InfiniteSpace.Rendering;

internal sealed class ParallaxLook : SceneLook
{
	private sealed record Band(Point From, Point To, double Parallax, int Seed, (float At, Color Color)[] Colors);

	public static readonly Color Ink = Color.FromRgb(31, 42, 31);

	private const double Margin = 36.0;

	private static readonly FrameStyle Style = new FrameStyle
	{
		Line = SceneLook.Rgb(3092524u),
		LineWidth = 2.2,
		Cast = SceneLook.Rgb(0u, 0.35),
		CastOffset = new System.Windows.Vector(0.0, 3.0),
		CastBlur = 4.0,
		Shade = SceneLook.Rgb(1976858u, 0.3),
		ShadeBlur = 7.0
	};

	private static readonly Band[] Bands = new Band[2]
	{
		new Band(new Point(1.08, -0.08), new Point(-0.08, 0.78), 0.012, 1, new (float, Color)[3]
		{
			(0f, SceneLook.Rgb(11389332u)),
			(0.3f, SceneLook.Rgb(9416824u)),
			(1f, SceneLook.Rgb(8101734u))
		}),
		new Band(new Point(1.08, 0.36), new Point(0.12, 1.08), 0.026, 2, new (float, Color)[3]
		{
			(0f, SceneLook.Rgb(5799248u)),
			(0.35f, SceneLook.Rgb(4350782u)),
			(1f, SceneLook.Rgb(3362608u))
		})
	};

	private readonly ID2D1Bitmap1?[] _layers = new ID2D1Bitmap1[Bands.Length];

	private ID2D1Bitmap1? _flat;

	private Rect _bandArea = Rect.Empty;

	private Shadow? _bandShadow;

	private ID2D1Bitmap1? _plaster;

	private ID2D1Bitmap1? _moss;

	private ID2D1SolidColorBrush? _ring;

	protected override FrameStyle Frame => Style;

	protected override double BurstLife => 0.8;

	protected override bool Advance()
	{
		return LookMoved(Bands[^1].Parallax);
	}

	protected override void PrepareStills(ISpaceCanvas canvas, float scale)
	{
		if ((object)_plaster == null)
		{
			_plaster = PaintTile(canvas, scale, 180.0, PlasterStrokes);
		}
		if ((object)_moss == null)
		{
			_moss = PaintTile(canvas, scale, 120.0, MossSpeckles);
		}
		if (!Draft || !_layers.All((ID2D1Bitmap1 l) => (object)l != null))
		{
			for (int num = 0; num < Bands.Length; num++)
			{
				PaintBand(canvas, scale, Bands[num], ref _layers[num]);
			}
			_bandArea = Area;
		}
	}

	protected override void DrawBackdrop(ID2D1DeviceContext context, ID2D1Factory1 factory)
	{
		Rect area = Area;
		RawRectF rect = SceneLook.R(new Rect(0.0, 0.0, World.Width, World.Height));
		using (ID2D1LinearGradientBrush brush = Linear(context, area.TopLeft, area.BottomRight, (0f, SceneLook.Rgb(15000284u)), (1f, SceneLook.Rgb(13158079u))))
		{
			context.FillRectangle(rect, brush);
		}
		using ID2D1BitmapBrush1 brush2 = Tiled(context, _plaster);
		context.FillRectangle(rect, brush2);
	}

	protected override void DrawMotion(ID2D1DeviceContext context, ID2D1Factory1 factory, Matrix3x2 origin)
	{
		double num = 32.4;
		Rect area = Area;
		context.Transform = ((area == _bandArea || _bandArea.IsEmpty) ? origin : (Matrix3x2.CreateTranslation((float)(0.0 - _bandArea.X), (float)(0.0 - _bandArea.Y)) * Matrix3x2.CreateScale((float)(area.Width / _bandArea.Width), (float)(area.Height / _bandArea.Height)) * Matrix3x2.CreateTranslation((float)area.X, (float)area.Y) * origin));
		for (int i = 0; i < Bands.Length; i++)
		{
			System.Windows.Vector vector = -Look * Bands[i].Parallax;
			context.DrawImage(targetOffset: new Vector2((float)(Math.Clamp(vector.X, 0.0 - num, num) - 36.0), (float)(Math.Clamp(vector.Y, 0.0 - num, num) - 36.0)), image: _layers[i], imageRectangle: null, interpolationMode: InterpolationMode.Linear, compositeMode: CompositeMode.SourceOver);
		}
		context.Transform = origin;
		if ((object)_ring == null)
		{
			_ring = SceneLook.Solid(context, Colors.White);
		}
		foreach (Burst burst in Bursts)
		{
			double num2 = (Time - burst.Start) / BurstLife;
			float num3 = (float)(4.0 + 44.0 * Math.Sqrt(num2));
			_ring.Opacity = (float)(0.55 * (1.0 - num2));
			context.DrawEllipse(new Ellipse(SceneLook.V(burst.Origin), num3, num3), _ring, 1.6f);
		}
	}

	protected override void DisposeResources()
	{
		ID2D1Bitmap1[] layers = _layers;
		for (int i = 0; i < layers.Length; i++)
		{
			layers[i]?.Dispose();
		}
		_flat?.Dispose();
		_bandShadow?.Dispose();
		_plaster?.Dispose();
		_moss?.Dispose();
		_ring?.Dispose();
	}

	private void PaintBand(ISpaceCanvas canvas, float scale, Band band, ref ID2D1Bitmap1? layer)
	{
		Rect area = Area;
		Point from = new Point(area.Left + band.From.X * area.Width, area.Top + band.From.Y * area.Height);
		Point point = new Point(area.Left + band.To.X * area.Width, area.Top + band.To.Y * area.Height);
		System.Windows.Vector along = point - from;
		double length = along.Length;
		along /= length;
		System.Windows.Vector outward = new System.Windows.Vector(0.0 - along.Y, along.X);
		double reach = new System.Windows.Vector(World.Width, World.Height).Length + 72.0;
		uint seed = SceneLook.Mix((uint)(band.Seed * -1640531527));
		double p1 = SceneLook.Unit(seed, 0) * (Math.PI * 2.0);
		double p2 = SceneLook.Unit(seed, 8) * (Math.PI * 2.0);
		double num = SceneLook.Unit(seed, 16) * (Math.PI * 2.0);
		double start = -92.0;
		double end = length + 72.0 + 20.0;
		List<Vector2> list = new List<Vector2>();
		for (double num2 = start; num2 <= end; num2 += 4.0)
		{
			list.Add(SceneLook.V(At(num2, Swell(num2) + 1.2 * Math.Sin(num2 / 31.0 * (Math.PI * 2.0) + num))));
		}
		List<Vector2> region = new List<Vector2>(list)
		{
			SceneLook.V(At(end, 0.0) - outward * reach),
			SceneLook.V(At(start, 0.0) - outward * reach)
		};
		int wide = (int)Math.Round((World.Width + 72.0) * (double)scale);
		int high = (int)Math.Round((World.Height + 72.0) * (double)scale);
		Matrix3x2 shift = Matrix3x2.CreateTranslation(36f, 36f);
		canvas.Repaint(ref _flat, wide, high, scale, (ID2D1DeviceContext context, ID2D1Factory1 factory) =>
		{
			context.Transform = shift;
			using ID2D1PathGeometry iD2D1PathGeometry = SceneLook.Path(factory, region, closed: true);
			Point point2 = from + along * (length / 2.0);
			using (ID2D1LinearGradientBrush brush = Linear(context, point2, point2 - outward * (reach * 0.35), band.Colors))
			{
				context.FillGeometry(iD2D1PathGeometry, brush);
			}
			context.PushLayer(new LayerParameters1
			{
				ContentBounds = new RawRectF(float.MinValue, float.MinValue, float.MaxValue, float.MaxValue),
				GeometricMask = iD2D1PathGeometry,
				MaskAntialiasMode = AntialiasMode.PerPrimitive,
				MaskTransform = Matrix3x2.Identity,
				Opacity = 1f
			}, null);
			if (band.Seed == 1)
			{
				using ID2D1SolidColorBrush brush2 = SceneLook.Solid(context, SceneLook.Rgb(14084802u, 0.4));
				using ID2D1SolidColorBrush brush3 = SceneLook.Solid(context, SceneLook.Rgb(5930828u, 0.28));
				ID2D1StrokeStyle strokeStyle = RoundStroke(factory);
				double num3 = 6.0;
				for (int i = 0; i < 22; i++)
				{
					uint seed2 = SceneLook.Hash(i, band.Seed, 5);
					num3 += 9.0 + 6.0 * SceneLook.Unit(seed2, 0);
					double num4 = Math.Max(0.2, 1.0 - num3 / 260.0);
					double num5 = 2.0 + 3.0 * SceneLook.Unit(seed2, 8);
					double num6 = 80.0 + 60.0 * SceneLook.Unit(seed2, 24);
					double num7 = SceneLook.Unit(seed2, 16) * (Math.PI * 2.0);
					float num8 = (float)(1.2 + 1.8 * SceneLook.Unit(seed2, 4));
					List<Vector2> list2 = new List<Vector2>();
					List<Vector2> list3 = new List<Vector2>();
					for (double num9 = start; num9 <= end; num9 += 9.0)
					{
						double num10 = Swell(num9) * num4 - num3 + num5 * Math.Sin(num9 / num6 * (Math.PI * 2.0) + num7);
						list2.Add(SceneLook.V(At(num9, num10)));
						list3.Add(SceneLook.V(At(num9, num10 - (double)num8 * 0.9)));
					}
					using ID2D1PathGeometry geometry = SceneLook.Path(factory, list3, closed: false);
					using ID2D1PathGeometry geometry2 = SceneLook.Path(factory, list2, closed: false);
					context.DrawGeometry(geometry, brush3, num8 * 1.3f, strokeStyle);
					context.DrawGeometry(geometry2, brush2, num8, strokeStyle);
				}
			}
			else
			{
				context.Transform = Matrix3x2.Identity;
				using ID2D1BitmapBrush1 brush4 = Tiled(context, _moss);
				context.FillRectangle(new RawRectF(0f, 0f, (float)wide / scale, (float)high / scale), brush4);
				context.Transform = shift;
			}
			context.PopLayer();
		});
		ID2D1Bitmap1 flat = _flat;
		canvas.Repaint(ref layer, wide, high, scale, (ID2D1DeviceContext context, ID2D1Factory1 _) =>
		{
			if ((object)_bandShadow == null)
			{
				_bandShadow = new Shadow(context)
				{
					BlurStandardDeviation = 5f,
					Color = SceneLook.ToVector4(SceneLook.Rgb(0u, 0.38))
				};
			}
			_bandShadow.SetInput(0u, flat, true);
			SceneLook.DrawEffect(context, _bandShadow, SceneLook.V((Point)(outward * 3.5)));
			context.DrawImage(flat, InterpolationMode.NearestNeighbor);
		});
		Point At(double s, double offset)
		{
			return from + along * s + outward * offset;
		}
		double Swell(double s)
		{
			return 9.0 * Math.Sin(s / 170.0 * (Math.PI * 2.0) + p1) + 4.0 * Math.Sin(s / 67.0 * (Math.PI * 2.0) + p2);
		}
	}

	private static ID2D1Bitmap1 PaintTile(ISpaceCanvas canvas, float scale, double size, Action<ID2D1DeviceContext, ID2D1Factory1, float, Action<Action>> draw)
	{
		int num = (int)Math.Round(size * (double)scale);
		float side = (float)num / scale;
		return canvas.Paint(num, num, scale, (ID2D1DeviceContext context, ID2D1Factory1 factory) =>
		{
			draw(context, factory, side, (Action paint) =>
			{
				for (int i = -1; i <= 1; i++)
				{
					for (int j = -1; j <= 1; j++)
					{
						context.Transform = Matrix3x2.CreateTranslation((float)i * side, (float)j * side);
						paint();
					}
				}
				context.Transform = Matrix3x2.Identity;
			});
		});
	}

	private static ID2D1BitmapBrush1 Tiled(ID2D1DeviceContext context, ID2D1Bitmap1 tile)
	{
		return context.CreateBitmapBrush(tile, new BitmapBrushProperties1(ExtendMode.Wrap, ExtendMode.Wrap, InterpolationMode.Linear));
	}

	private void PlasterStrokes(ID2D1DeviceContext context, ID2D1Factory1 factory, float side, Action<Action> wrapped)
	{
		using ID2D1SolidColorBrush iD2D1SolidColorBrush = SceneLook.Solid(context, SceneLook.Rgb(16777215u, 0.24));
		using ID2D1SolidColorBrush iD2D1SolidColorBrush2 = SceneLook.Solid(context, SceneLook.Rgb(10394515u, 0.14));
		ID2D1StrokeStyle round = RoundStroke(factory);
		for (int i = 0; i < 90; i++)
		{
			uint num = SceneLook.Hash(i, 3, 11);
			uint num2 = SceneLook.Mix(num);
			Point p = new Point(SceneLook.Unit(num, 0) * (double)side, SceneLook.Unit(num, 8) * (double)side);
			double num3 = 20.0 + 40.0 * SceneLook.Unit(num, 16);
			double num4 = -0.45 + 0.25 * (SceneLook.Unit(num, 24) - 0.5);
			System.Windows.Vector d = new System.Windows.Vector(Math.Cos(num4), Math.Sin(num4)) * (num3 / 2.0);
			float width = (float)(0.8 + 1.6 * SceneLook.Unit(num2, 0));
			ID2D1SolidColorBrush brush = (((num2 & 0x100) != 0) ? iD2D1SolidColorBrush : iD2D1SolidColorBrush2);
			wrapped(() =>
			{
				context.DrawLine(SceneLook.V(p - d), SceneLook.V(p + d), brush, width, round);
			});
		}
	}

	private static void MossSpeckles(ID2D1DeviceContext context, ID2D1Factory1 factory, float side, Action<Action> wrapped)
	{
		using ID2D1SolidColorBrush iD2D1SolidColorBrush = SceneLook.Solid(context, SceneLook.Rgb(8366199u, 0.45));
		using ID2D1SolidColorBrush iD2D1SolidColorBrush2 = SceneLook.Solid(context, SceneLook.Rgb(1847322u, 0.4));
		for (int i = 0; i < 260; i++)
		{
			uint num = SceneLook.Hash(i, 5, 13);
			uint num2 = SceneLook.Mix(num);
			Vector2 p = SceneLook.V(new Point(SceneLook.Unit(num, 0) * (double)side, SceneLook.Unit(num, 8) * (double)side));
			float rx = (float)(0.6 + 1.0 * SceneLook.Unit(num, 16));
			float ry = (float)(0.6 + 1.0 * SceneLook.Unit(num, 24));
			ID2D1SolidColorBrush brush = (((num2 & 0x100) != 0) ? iD2D1SolidColorBrush : iD2D1SolidColorBrush2);
			wrapped(() =>
			{
				context.FillEllipse(new Ellipse(in p, rx, ry), brush);
			});
		}
	}
}
