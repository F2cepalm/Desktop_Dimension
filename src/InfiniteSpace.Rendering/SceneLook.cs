using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Windows;
using System.Windows.Media;
using InfiniteSpace.Native;
using InfiniteSpace.Spaces;
using Vortice;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using Vortice.Mathematics;

namespace InfiniteSpace.Rendering;

internal abstract class SceneLook : ISpaceLook, IDisposable
{
	private sealed record CachedGradient(ExtendMode Extend, (float At, System.Windows.Media.Color Color)[] Stops, ID2D1GradientStopCollection Collection)
	{
		public bool Matches(ExtendMode extend, (float At, System.Windows.Media.Color Color)[] stops)
		{
			if (extend == Extend)
			{
				return ((ReadOnlySpan<(float, System.Windows.Media.Color)>)stops.AsSpan()).SequenceEqual((ReadOnlySpan<(float, System.Windows.Media.Color)>)Stops);
			}
			return false;
		}
	}

	protected readonly record struct Field(double Cell, double Density, System.Windows.Vector Drift, double Parallax);

	protected readonly record struct Mote(Point At, uint Seed);

	protected sealed record Burst(Point Origin, double Start, uint Seed);

	private readonly List<Burst> _bursts = new List<Burst>();

	private readonly List<Mote> _motes = new List<Mote>();

	private Geometry _outline = Geometry.Empty;

	private System.Windows.Rect _bounds;

	private FillMode _fillMode = FillMode.Winding;

	private Vector2[][] _figures = Array.Empty<Vector2[]>();

	private readonly List<CachedGradient> _gradients = new List<CachedGradient>();

	private ID2D1PathGeometry? _shape;

	private ID2D1Bitmap1? _mask;

	private ID2D1Bitmap1? _outside;

	private ID2D1Bitmap1? _cast;

	private ID2D1Bitmap1? _rim;

	private Shadow? _shadeEffect;

	private Shadow? _castEffect;

	private Shadow? _glowEffect;

	private ID2D1SolidColorBrush? _white;

	private ID2D1StrokeStyle? _roundStroke;

	private bool _stillsDirty = true;

	private bool _dirty = true;

	private bool _artShown = true;

	private long _step = long.MinValue;

	private System.Windows.Vector _drawnLook = new System.Windows.Vector(double.NaN, double.NaN);

	public bool HasBursts => _bursts.Count > 0;

	protected System.Windows.Rect World { get; private set; }

	protected System.Windows.Rect Area { get; private set; }

	protected double Time { get; private set; }

	protected System.Windows.Vector Look { get; private set; }

	protected IReadOnlyList<Burst> Bursts => _bursts;

	protected virtual double BurstLife => 1.0;

	protected virtual bool HasMotionLayer => true;

	protected abstract FrameStyle Frame { get; }

	protected bool Draft { get; private set; }

	public void SetShape(Geometry outline, System.Windows.Rect world)
	{
		SetShape(outline, world, draft: false);
	}

	public void SetShape(Geometry outline, System.Windows.Rect world, bool draft)
	{
		Draft = draft;
		_outline = outline;
		World = world;
		_bounds = outline.Bounds;
		Area = new System.Windows.Rect(_bounds.X - world.X, _bounds.Y - world.Y, _bounds.Width, _bounds.Height);
		PathGeometry pathGeometry = SpaceGeometry.Flat(outline);
		_fillMode = ((pathGeometry.FillRule != FillRule.EvenOdd) ? FillMode.Winding : FillMode.Alternate);
		_figures = (from f in pathGeometry.Figures
			select Figure(f, world.TopLeft) into f
			where f.Length >= 3
			select f).ToArray();
		_shape?.Dispose();
		_shape = null;
		_stillsDirty = true;
		_dirty = true;
	}

	public void Move(Geometry outline, System.Windows.Rect world)
	{
		_outline = outline;
		World = world;
		_bounds = outline.Bounds;
		_dirty = true;
	}

	public bool Render(FrameState frame)
	{
		Time = frame.Time;
		Look = frame.Look;
		bool flag = _bursts.Count > 0;
		for (int num = _bursts.Count - 1; num >= 0; num--)
		{
			if (Time - _bursts[num].Start > BurstLife)
			{
				_bursts.RemoveAt(num);
			}
		}
		bool result = Advance() | _dirty | flag;
		_dirty = false;
		return result;
	}

	public void Present(ISpaceCanvas canvas)
	{
		float num = (float)Dpi.Scale;
		int width = Math.Max(1, (int)Math.Round(World.Width * (double)num));
		int height = Math.Max(1, (int)Math.Round(World.Height * (double)num));
		if (_stillsDirty)
		{
			_stillsDirty = false;
			canvas.Repaint(ref _mask, width, height, num, (ID2D1DeviceContext context, ID2D1Factory1 factory) =>
			{
				context.FillGeometry(Shape(factory), White(context));
			});
			PrepareStills(canvas, num);
			canvas.DrawBackdrop(width, height, num, (ID2D1DeviceContext context, ID2D1Factory1 factory) =>
			{
				DrawBackdrop(context, factory);
				Cut(context);
			});
			PaintFrame(canvas, width, height, num);
		}
		if (HasMotionLayer || _bursts.Count > 0)
		{
			canvas.DrawVector(width, height, num, (ID2D1DeviceContext context, ID2D1Factory1 factory) =>
			{
				Matrix3x2 transform = context.Transform;
				DrawMotion(context, factory, transform);
				context.Transform = transform;
				Cut(context);
			});
			_artShown = true;
		}
		else if (_artShown)
		{
			canvas.HideArt();
			_artShown = false;
		}
	}

	public bool IsInside(Point screen)
	{
		if (_bounds.Contains(screen))
		{
			return _outline.FillContains(screen);
		}
		return false;
	}

	public void AddBurst(Point screen)
	{
		_bursts.Add(new Burst(new Point(screen.X - World.X, screen.Y - World.Y), Time, (uint)Random.Shared.Next()));
	}

	public void Dispose()
	{
		DisposeResources();
		_shape?.Dispose();
		_mask?.Dispose();
		_outside?.Dispose();
		_cast?.Dispose();
		_rim?.Dispose();
		_shadeEffect?.Dispose();
		_castEffect?.Dispose();
		_glowEffect?.Dispose();
		_white?.Dispose();
		_roundStroke?.Dispose();
		foreach (CachedGradient gradient in _gradients)
		{
			gradient.Collection.Dispose();
		}
		_gradients.Clear();
	}

	protected ID2D1SolidColorBrush White(ID2D1DeviceContext context)
	{
		return _white ?? (_white = Solid(context, System.Windows.Media.Colors.White));
	}

	protected abstract bool Advance();

	protected virtual void PrepareStills(ISpaceCanvas canvas, float scale)
	{
	}

	protected abstract void DrawBackdrop(ID2D1DeviceContext context, ID2D1Factory1 factory);

	protected abstract void DrawMotion(ID2D1DeviceContext context, ID2D1Factory1 factory, Matrix3x2 origin);

	protected virtual void DisposeResources()
	{
	}

	protected bool Stepped(double rate)
	{
		long num = (long)Math.Floor(Time * rate);
		if (num == _step)
		{
			return false;
		}
		_step = num;
		return true;
	}

	protected bool LookMoved(double parallax)
	{
		if (!double.IsNaN(_drawnLook.X) && (Look - _drawnLook).Length * parallax < 0.04)
		{
			return false;
		}
		_drawnLook = Look;
		return true;
	}

	protected ID2D1PathGeometry Shape(ID2D1Factory1 factory)
	{
		if ((object)_shape != null)
		{
			return _shape;
		}
		_shape = factory.CreatePathGeometry();
		using (ID2D1GeometrySink iD2D1GeometrySink = _shape.Open())
		{
			iD2D1GeometrySink.SetFillMode(_fillMode);
			Vector2[][] figures = _figures;
			foreach (Vector2[] array in figures)
			{
				iD2D1GeometrySink.BeginFigure(array[0], FigureBegin.Filled);
				iD2D1GeometrySink.AddLines(array);
				iD2D1GeometrySink.EndFigure(FigureEnd.Closed);
			}
			iD2D1GeometrySink.Close();
		}
		return _shape;
	}

	protected ID2D1StrokeStyle RoundStroke(ID2D1Factory1 factory)
	{
		return _roundStroke ?? (_roundStroke = factory.CreateStrokeStyle(new StrokeStyleProperties
		{
			StartCap = CapStyle.Round,
			EndCap = CapStyle.Round,
			DashCap = CapStyle.Round,
			LineJoin = LineJoin.Round,
			MiterLimit = 1f
		}));
	}

	protected List<Mote> Scatter(Field field, int layer, double margin)
	{
		_motes.Clear();
		System.Windows.Vector vector = field.Drift * Time - Look * field.Parallax;
		double cell = field.Cell;
		int num = (int)Math.Floor((World.Left - margin - vector.X) / cell);
		int num2 = (int)Math.Floor((World.Right + margin - vector.X) / cell);
		int num3 = (int)Math.Floor((World.Top - margin - vector.Y) / cell);
		int num4 = (int)Math.Floor((World.Bottom + margin - vector.Y) / cell);
		uint num5 = (uint)(field.Density * 65536.0);
		for (int i = num3; i <= num4; i++)
		{
			for (int j = num; j <= num2; j++)
			{
				uint num6 = Hash(j, i, layer);
				if ((num6 & 0xFFFF) < num5)
				{
					uint num7 = Mix(num6);
					double x = ((double)j + (double)((num6 >> 16) & 0xFF) / 256.0) * cell + vector.X - World.Left;
					double y = ((double)i + (double)(num7 & 0xFF) / 256.0) * cell + vector.Y - World.Top;
					_motes.Add(new Mote(new Point(x, y), Mix(num7 ^ 0x5BD1E995)));
				}
			}
		}
		return _motes;
	}

	protected static double Unit(uint seed, int shift)
	{
		return (double)((seed >> shift) & 0xFF) / 255.0;
	}

	protected static uint Hash(int x, int y, int z)
	{
		return Mix((uint)((x * -1918454973) ^ (y * -669632447) ^ (z * -887442657)));
	}

	protected static uint Mix(uint h)
	{
		h ^= h >> 16;
		h *= 2146121005;
		h ^= h >> 15;
		h *= 2221713035u;
		h ^= h >> 16;
		return h;
	}

	protected static double Frac(double x)
	{
		return x - Math.Floor(x);
	}

	protected static System.Windows.Vector Rotate(System.Windows.Vector v, double angle)
	{
		double num = Math.Cos(angle);
		double num2 = Math.Sin(angle);
		return new System.Windows.Vector(v.X * num - v.Y * num2, v.X * num2 + v.Y * num);
	}

	protected static double Smooth(double edge0, double edge1, double x)
	{
		double num = Math.Clamp((x - edge0) / (edge1 - edge0), 0.0, 1.0);
		return num * num * (3.0 - 2.0 * num);
	}

	protected static System.Windows.Media.Color Rgb(uint rgb, double alpha = 1.0)
	{
		return System.Windows.Media.Color.FromArgb((byte)Math.Round(Math.Clamp(alpha, 0.0, 1.0) * 255.0), (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
	}

	protected static Color4 ToColor4(System.Windows.Media.Color c)
	{
		return new Color4((float)(int)c.R / 255f, (float)(int)c.G / 255f, (float)(int)c.B / 255f, (float)(int)c.A / 255f);
	}

	protected static Vector2 V(Point p)
	{
		return new Vector2((float)p.X, (float)p.Y);
	}

	protected static RawRectF R(System.Windows.Rect r)
	{
		return new RawRectF((float)r.Left, (float)r.Top, (float)r.Right, (float)r.Bottom);
	}

	protected static ID2D1SolidColorBrush Solid(ID2D1DeviceContext context, System.Windows.Media.Color color)
	{
		return context.CreateSolidColorBrush(ToColor4(color));
	}

	protected ID2D1LinearGradientBrush Linear(ID2D1DeviceContext context, Point from, Point to, params (float At, System.Windows.Media.Color Color)[] stops)
	{
		return Linear(context, from, to, ExtendMode.Clamp, stops);
	}

	protected ID2D1LinearGradientBrush Linear(ID2D1DeviceContext context, Point from, Point to, ExtendMode extend, params (float At, System.Windows.Media.Color Color)[] stops)
	{
		return context.CreateLinearGradientBrush(new LinearGradientBrushProperties(V(from), V(to)), Gradient(context, extend, stops));
	}

	protected ID2D1RadialGradientBrush Radial(ID2D1DeviceContext context, Point centre, double radiusX, double radiusY, params (float At, System.Windows.Media.Color Color)[] stops)
	{
		return context.CreateRadialGradientBrush(new RadialGradientBrushProperties(V(centre), Vector2.Zero, (float)radiusX, (float)radiusY), Gradient(context, ExtendMode.Clamp, stops));
	}

	private ID2D1GradientStopCollection Gradient(ID2D1DeviceContext context, ExtendMode extend, (float At, System.Windows.Media.Color Color)[] stops)
	{
		foreach (CachedGradient gradient in _gradients)
		{
			if (gradient.Matches(extend, stops))
			{
				return gradient.Collection;
			}
		}
		Vortice.Direct2D1.GradientStop[] array = new Vortice.Direct2D1.GradientStop[stops.Length];
		for (int i = 0; i < stops.Length; i++)
		{
			array[i] = new Vortice.Direct2D1.GradientStop(stops[i].At, ToColor4(stops[i].Color));
		}
		ID2D1GradientStopCollection iD2D1GradientStopCollection = context.CreateGradientStopCollection(array, extend);
		_gradients.Add(new CachedGradient(extend, stops.ToArray(), iD2D1GradientStopCollection));
		return iD2D1GradientStopCollection;
	}

	protected ID2D1Bitmap1 PaintGlow(ISpaceCanvas canvas, float scale, double diameter, params (float At, System.Windows.Media.Color Color)[] stops)
	{
		int num = (int)Math.Ceiling(diameter * (double)scale);
		double d = (float)num / scale;
		return canvas.Paint(num, num, scale, (ID2D1DeviceContext context, ID2D1Factory1 _) =>
		{
			using ID2D1RadialGradientBrush brush = Radial(context, new Point(d / 2.0, d / 2.0), d / 2.0, d / 2.0, stops);
			context.FillRectangle(new RawRectF(0f, 0f, (float)d, (float)d), brush);
		});
	}

	protected static void DrawSprite(ID2D1DeviceContext context, Matrix3x2 origin, ID2D1Bitmap1 sprite, Point centre, double width, double height, double angle, double opacity)
	{
		if (!(opacity < 0.004) && !(width <= 0.0) && !(height <= 0.0))
		{
			context.Transform = Matrix3x2.CreateRotation((float)angle) * Matrix3x2.CreateTranslation((float)centre.X, (float)centre.Y) * origin;
			float num = (float)(width / 2.0);
			float num2 = (float)(height / 2.0);
			context.DrawBitmap(sprite, new RawRectF(0f - num, 0f - num2, num, num2), (float)Math.Min(1.0, opacity), InterpolationMode.Linear, null, null);
		}
	}

	protected static void DrawEffect(ID2D1DeviceContext context, ID2D1Effect effect, Vector2 offset)
	{
		using ID2D1Image image = effect.Output;
		context.DrawImage(image, offset, null, InterpolationMode.Linear, CompositeMode.SourceOver);
	}

	protected static Vector4 ToVector4(System.Windows.Media.Color c)
	{
		return new Vector4((float)(int)c.R / 255f, (float)(int)c.G / 255f, (float)(int)c.B / 255f, (float)(int)c.A / 255f);
	}

	protected static ID2D1PathGeometry Path(ID2D1Factory1 factory, IReadOnlyList<Vector2> points, bool closed)
	{
		ID2D1PathGeometry1 iD2D1PathGeometry = factory.CreatePathGeometry();
		using ID2D1GeometrySink iD2D1GeometrySink = iD2D1PathGeometry.Open();
		iD2D1GeometrySink.BeginFigure(points[0], (!closed) ? FigureBegin.Hollow : FigureBegin.Filled);
		iD2D1GeometrySink.AddLines(points.Skip(1).ToArray());
		iD2D1GeometrySink.EndFigure(closed ? FigureEnd.Closed : FigureEnd.Open);
		iD2D1GeometrySink.Close();
		return iD2D1PathGeometry;
	}

	private static Vector2[] Figure(PathFigure figure, Point origin)
	{
		List<Vector2> list = new List<Vector2> { V(figure.StartPoint - (System.Windows.Vector)origin) };
		foreach (System.Windows.Media.PathSegment segment in figure.Segments)
		{
			if (segment is LineSegment lineSegment)
			{
				list.Add(V(lineSegment.Point - (System.Windows.Vector)origin));
			}
			else
			{
				if (!(segment is PolyLineSegment polyLineSegment))
				{
					continue;
				}
				foreach (Point point in polyLineSegment.Points)
				{
					list.Add(V(point - (System.Windows.Vector)origin));
				}
			}
		}
		return list.ToArray();
	}

	private void Cut(ID2D1DeviceContext context)
	{
		context.DrawImage(_mask, InterpolationMode.NearestNeighbor, CompositeMode.DestinationIn);
	}

	private void PaintFrame(ISpaceCanvas canvas, int width, int height, float scale)
	{
		FrameStyle style = Frame;
		float pad = 0f;
		if (!Draft)
		{
			if (style.Shade.HasValue)
			{
				int num = (int)Math.Ceiling(style.ShadeBlur * 3.0 * (double)scale);
				pad = (float)num / scale;
				float w = (float)(width + 2 * num) / scale;
				float h = (float)(height + 2 * num) / scale;
				canvas.Repaint(ref _outside, width + 2 * num, height + 2 * num, scale, (ID2D1DeviceContext context, ID2D1Factory1 _) =>
				{
					context.FillRectangle(new RawRectF(0f, 0f, w, h), White(context));
					context.DrawImage(_mask, new Vector2(pad, pad), null, InterpolationMode.NearestNeighbor, CompositeMode.DestinationOut);
				});
			}
			System.Windows.Media.Color? cast = style.Cast;
			if (cast.HasValue)
			{
				System.Windows.Media.Color castColor = cast.GetValueOrDefault();
				canvas.Repaint(ref _cast, width, height, scale, (ID2D1DeviceContext context, ID2D1Factory1 _) =>
				{
					if ((object)_castEffect == null)
					{
						_castEffect = new Shadow(context);
					}
					_castEffect.BlurStandardDeviation = (float)style.CastBlur;
					_castEffect.Color = ToVector4(castColor);
					_castEffect.SetInput(0u, _mask, true);
					DrawEffect(context, _castEffect, new Vector2((float)style.CastOffset.X, (float)style.CastOffset.Y));
					context.DrawImage(_mask, InterpolationMode.NearestNeighbor, CompositeMode.DestinationOut);
				});
			}
			if (style.Glow.HasValue)
			{
				canvas.Repaint(ref _rim, width, height, scale, (ID2D1DeviceContext context, ID2D1Factory1 factory) =>
				{
					context.DrawGeometry(Shape(factory), White(context), (float)style.GlowWidth, RoundStroke(factory));
				});
			}
		}
		canvas.DrawFrame(width, height, scale, (ID2D1DeviceContext context, ID2D1Factory1 factory) =>
		{
			if (!Draft)
			{
				System.Windows.Media.Color? shade = style.Shade;
				if (shade.HasValue)
				{
					System.Windows.Media.Color valueOrDefault = shade.GetValueOrDefault();
					if ((object)_shadeEffect == null)
					{
						_shadeEffect = new Shadow(context);
					}
					_shadeEffect.BlurStandardDeviation = (float)style.ShadeBlur;
					_shadeEffect.Color = ToVector4(valueOrDefault);
					_shadeEffect.SetInput(0u, _outside, true);
					DrawEffect(context, _shadeEffect, new Vector2(0f - pad, 0f - pad));
					Cut(context);
				}
				if (style.Cast.HasValue)
				{
					context.DrawImage(_cast, InterpolationMode.NearestNeighbor);
				}
				shade = style.Glow;
				if (shade.HasValue)
				{
					System.Windows.Media.Color valueOrDefault2 = shade.GetValueOrDefault();
					if ((object)_glowEffect == null)
					{
						_glowEffect = new Shadow(context);
					}
					_glowEffect.BlurStandardDeviation = (float)style.GlowBlur;
					_glowEffect.Color = ToVector4(valueOrDefault2);
					_glowEffect.SetInput(0u, _rim, true);
					DrawEffect(context, _glowEffect, Vector2.Zero);
				}
			}
			using ID2D1SolidColorBrush brush = Solid(context, style.Line);
			context.DrawGeometry(Shape(factory), brush, (float)style.LineWidth, RoundStroke(factory));
		});
	}
}
