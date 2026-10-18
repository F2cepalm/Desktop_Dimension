using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Windows;
using System.Windows.Media;
using InfiniteSpace.Native;
using InfiniteSpace.Spaces;
using Vortice.Direct2D1;
using Vortice.Mathematics;

namespace InfiniteSpace.Rendering;

internal sealed class InkyLook : ISpaceLook, IDisposable
{
	private sealed record BoilDrawing(ID2D1GeometryRealization Body, ID2D1GeometryRealization Rim);

	private readonly record struct Drop(System.Windows.Vector Direction, double Speed, double Radius);

	private sealed record Splash(Point Origin, double Start, Drop[] Drops);

	private readonly struct BoilFrame
	{
		public double Shift1 { get; }

		public double Shift2 { get; }

		public double Width { get; }

		public BoilFrame(long index)
		{
			ulong num = (ulong)(index * -7046029254386353131L);
			num ^= num >> 29;
			num *= 13787848793156543929uL;
			num ^= num >> 32;
			Shift1 = (double)(num & 0xFFFF) / 65536.0 * (Math.PI * 2.0);
			Shift2 = (double)((num >> 16) & 0xFFFF) / 65536.0 * (Math.PI * 2.0);
			Width = (double)((num >> 32) & 0xFFFF) / 65536.0 * (Math.PI * 2.0);
		}
	}

	private sealed class Contour
	{
		private readonly Vector2[] _center;

		private readonly Vector2[] _outer;

		private readonly Vector2[] _inner;

		private Point[] Points { get; }

		private System.Windows.Vector[] Normals { get; }

		private double ShiftRate1 { get; }

		private double ShiftRate2 { get; }

		private double WidthRate1 { get; }

		private double WidthRate2 { get; }

		private double WidthPhase1 { get; }

		private double WidthPhase2 { get; }

		private Contour(Point[] points, System.Windows.Vector[] normals, double perimeter)
		{
			Points = points;
			Normals = normals;
			int n = points.Length;
			_center = new Vector2[n];
			_outer = new Vector2[n];
			_inner = new Vector2[n];
			ShiftRate1 = Rate(60.0);
			ShiftRate2 = Rate(23.0);
			WidthRate1 = Rate(140.0);
			WidthRate2 = Rate(45.0);
			WidthPhase1 = perimeter * 0.618 % (Math.PI * 2.0);
			WidthPhase2 = perimeter * 0.377 % (Math.PI * 2.0);
			double Rate(double wavelength)
			{
				return Math.PI * 2.0 * Math.Max(1.0, Math.Round(perimeter / wavelength)) / (double)n;
			}
		}

		public static Contour? Build(PathFigure figure, System.Windows.Vector offset, double spacing = 3.0)
		{
			List<Point> list = new List<Point> { figure.StartPoint + offset };
			foreach (System.Windows.Media.PathSegment segment in figure.Segments)
			{
				if (segment is LineSegment lineSegment)
				{
					list.Add(lineSegment.Point + offset);
				}
				else
				{
					if (!(segment is PolyLineSegment polyLineSegment))
					{
						continue;
					}
					foreach (Point point3 in polyLineSegment.Points)
					{
						list.Add(point3 + offset);
					}
				}
			}
			if (list.Count > 1)
			{
				if ((list[list.Count - 1] - list[0]).Length < 0.001)
				{
					list.RemoveAt(list.Count - 1);
				}
			}
			if (list.Count < 3)
			{
				return null;
			}
			int count = list.Count;
			double num = 0.0;
			for (int i = 0; i < count; i++)
			{
				num += (list[(i + 1) % count] - list[i]).Length;
			}
			if (num < 1.0)
			{
				return null;
			}
			int num2 = Math.Max(12, (int)Math.Round(num / spacing));
			double num3 = num / (double)num2;
			Point[] array = new Point[num2];
			int num4 = 0;
			double num5 = 0.0;
			double length = (list[1] - list[0]).Length;
			for (int j = 0; j < num2; j++)
			{
				double num6 = (double)j * num3;
				while (num5 + length < num6 && num4 < count - 1)
				{
					num5 += length;
					num4++;
					length = (list[(num4 + 1) % count] - list[num4]).Length;
				}
				Point point = list[num4];
				Point point2 = list[(num4 + 1) % count];
				double num7 = ((length > 1E-09) ? Math.Clamp((num6 - num5) / length, 0.0, 1.0) : 0.0);
				array[j] = point + (point2 - point) * num7;
			}
			System.Windows.Vector[] array2 = new System.Windows.Vector[num2];
			for (int k = 0; k < num2; k++)
			{
				System.Windows.Vector vector = array[(k + 2) % num2] - array[(k - 2 + num2) % num2];
				if (vector.LengthSquared < 1E-12)
				{
					vector = new System.Windows.Vector(1.0, 0.0);
				}
				vector.Normalize();
				array2[k] = new System.Windows.Vector(0.0 - vector.Y, vector.X);
			}
			return new Contour(array, array2, num);
		}

		public void Emit(ID2D1GeometrySink body, ID2D1GeometrySink rim, BoilFrame boil, double time, List<Splash> splashes)
		{
			int num = Points.Length;
			for (int i = 0; i < num; i++)
			{
				Point point = Points[i];
				System.Windows.Vector vector = Normals[i];
				double num2 = 0.7 * (0.6 * Math.Sin((double)i * ShiftRate1 + boil.Shift1) + 0.4 * Math.Sin((double)i * ShiftRate2 + boil.Shift2)) + Ripple(point, time, splashes);
				double num3 = 5.5 * (1.0 + 0.35 * Math.Sin((double)i * WidthRate1 + WidthPhase1) + 0.15 * Math.Sin((double)i * WidthRate2 + WidthPhase2)) * (1.0 + 0.08 * Math.Sin((double)i * ShiftRate1 + boil.Width));
				Point point2 = point + vector * num2;
				System.Windows.Vector vector2 = vector * (num3 / 2.0);
				_center[i] = new Vector2((float)point2.X, (float)point2.Y);
				_outer[i] = new Vector2((float)(point2.X + vector2.X), (float)(point2.Y + vector2.Y));
				_inner[num - 1 - i] = new Vector2((float)(point2.X - vector2.X), (float)(point2.Y - vector2.Y));
			}
			AddFigure(body, _center);
			AddFigure(rim, _outer);
			AddFigure(rim, _inner);
		}

		private static void AddFigure(ID2D1GeometrySink sink, Vector2[] points)
		{
			sink.BeginFigure(points[0], FigureBegin.Filled);
			sink.AddLines(points);
			sink.EndFigure(FigureEnd.Closed);
		}
	}

	private const double SampleSpacing = 3.0;

	private const double SplashLife = 0.9;

	private const int SplashDrops = 12;

	private const int BoilDrawings = 8;

	private readonly BoilDrawing?[] _drawings = new BoilDrawing[8];

	private ID2D1DeviceContext1? _context1;

	private nint _context1Of;

	private readonly List<Splash> _splashes = new List<Splash>();

	private Geometry _outline = Geometry.Empty;

	private System.Windows.Rect _world;

	private System.Windows.Rect _bounds;

	private FillMode _fillMode = FillMode.Winding;

	private Contour[] _contours = Array.Empty<Contour>();

	private long _boil = long.MinValue;

	private double _time;

	private bool _draft;

	private bool _dirty = true;

	private ID2D1SolidColorBrush? _clayBrush;

	private ID2D1SolidColorBrush? _inkBrush;

	public bool HasBursts => _splashes.Count > 0;

	public void SetShape(Geometry outline, System.Windows.Rect world)
	{
		SetShape(outline, world, draft: false);
	}

	public void SetShape(Geometry outline, System.Windows.Rect world, bool draft)
	{
		_draft = draft;
		_outline = outline;
		_world = world;
		_bounds = outline.Bounds;
		PathGeometry pathGeometry = SpaceGeometry.Flat(outline);
		_fillMode = ((pathGeometry.FillRule != FillRule.EvenOdd) ? FillMode.Winding : FillMode.Alternate);
		System.Windows.Vector offset = new System.Windows.Vector(0.0 - world.X, 0.0 - world.Y);
		double spacing = draft ? 6.0 : 3.0;
		_contours = pathGeometry.Figures.Select((PathFigure f) => Contour.Build(f, offset, spacing)).OfType<Contour>().ToArray();
		ForgetDrawings();
		_dirty = true;
	}

	public void Move(Geometry outline, System.Windows.Rect world)
	{
		_outline = outline;
		_world = world;
		_bounds = outline.Bounds;
	}

	public bool Render(FrameState frame)
	{
		_time = frame.Time;
		for (int num = _splashes.Count - 1; num >= 0; num--)
		{
			if (_time - _splashes[num].Start > 0.9)
			{
				_splashes.RemoveAt(num);
			}
		}
		long num2 = (long)Math.Floor(frame.Time * 10.0);
		bool result = _dirty || num2 != _boil || _splashes.Count > 0;
		_boil = num2;
		_dirty = false;
		return result;
	}

	public void Present(ISpaceCanvas canvas)
	{
		double scale = Dpi.Scale;
		int width = Math.Max(1, (int)Math.Round(_world.Width * scale));
		int height = Math.Max(1, (int)Math.Round(_world.Height * scale));
		canvas.DrawVector(width, height, (float)scale, Draw);
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
		Random shared = Random.Shared;
		Drop[] array = new Drop[12];
		for (int i = 0; i < array.Length; i++)
		{
			double num = shared.NextDouble() * (Math.PI * 2.0);
			array[i] = new Drop(new System.Windows.Vector(Math.Cos(num), Math.Sin(num)), 60.0 + shared.NextDouble() * 110.0, 1.8 + shared.NextDouble() * 2.6);
		}
		_splashes.Add(new Splash(new Point(screen.X - _world.X, screen.Y - _world.Y), _time, array));
	}

	private void Draw(ID2D1DeviceContext context, ID2D1Factory1 factory)
	{
		if ((object)_clayBrush == null)
		{
			_clayBrush = context.CreateSolidColorBrush(ToColor4(ClaudishStyle.Clay));
		}
		if ((object)_inkBrush == null)
		{
			_inkBrush = context.CreateSolidColorBrush(ToColor4(ClaudishStyle.Ink));
		}
		int num = (int)((_boil % 8 + 8) % 8);
		BoilFrame boil = new BoilFrame(num);
		if (!_draft && _splashes.Count == 0)
		{
			ID2D1DeviceContext1 iD2D1DeviceContext = Context1(context);
			if ((object)iD2D1DeviceContext != null)
			{
				ref BoilDrawing reference = ref _drawings[num];
				BoilDrawing boilDrawing = reference ?? (reference = Realize(iD2D1DeviceContext, factory, boil));
				iD2D1DeviceContext.DrawGeometryRealization(boilDrawing.Body, _clayBrush);
				iD2D1DeviceContext.DrawGeometryRealization(boilDrawing.Rim, _inkBrush);
				return;
			}
		}
		var (iD2D1PathGeometry, iD2D1PathGeometry2) = Build(factory, boil, _splashes);
		using (iD2D1PathGeometry)
		{
			using (iD2D1PathGeometry2)
			{
				context.FillGeometry(iD2D1PathGeometry, _clayBrush);
				context.FillGeometry(iD2D1PathGeometry2, _inkBrush);
			}
		}
		foreach (Splash splash in _splashes)
		{
			double num2 = _time - splash.Start;
			double num3 = (1.0 - Math.Exp((0.0 - num2) * 3.0)) / 3.0;
			double num4 = Math.Max(0.0, 1.0 - num2 / 0.9);
			Drop[] drops = splash.Drops;
			for (int i = 0; i < drops.Length; i++)
			{
				Drop drop = drops[i];
				Point point = splash.Origin + drop.Direction * (drop.Speed * num3);
				float num5 = (float)(drop.Radius * num4);
				if (num5 > 0.2f)
				{
					context.FillEllipse(new Ellipse(new Vector2((float)point.X, (float)point.Y), num5, num5), _inkBrush);
				}
			}
		}
	}

	private (ID2D1PathGeometry Body, ID2D1PathGeometry Rim) Build(ID2D1Factory1 factory, BoilFrame boil, List<Splash> splashes)
	{
		ID2D1PathGeometry1 iD2D1PathGeometry = factory.CreatePathGeometry();
		ID2D1PathGeometry1 iD2D1PathGeometry2 = factory.CreatePathGeometry();
		using (ID2D1GeometrySink iD2D1GeometrySink = iD2D1PathGeometry.Open())
		{
			using ID2D1GeometrySink iD2D1GeometrySink2 = iD2D1PathGeometry2.Open();
			iD2D1GeometrySink.SetFillMode(_fillMode);
			iD2D1GeometrySink2.SetFillMode(FillMode.Winding);
			Contour[] contours = _contours;
			for (int i = 0; i < contours.Length; i++)
			{
				contours[i].Emit(iD2D1GeometrySink, iD2D1GeometrySink2, boil, _time, splashes);
			}
			iD2D1GeometrySink.Close();
			iD2D1GeometrySink2.Close();
		}
		return (Body: iD2D1PathGeometry, Rim: iD2D1PathGeometry2);
	}

	private BoilDrawing Realize(ID2D1DeviceContext1 context, ID2D1Factory1 factory, BoilFrame boil)
	{
		var (iD2D1PathGeometry, iD2D1PathGeometry2) = Build(factory, boil, new List<Splash>());
		using (iD2D1PathGeometry)
		{
			using (iD2D1PathGeometry2)
			{
				float flatteningTolerance = 0.25f / (float)Dpi.Scale;
				return new BoilDrawing(context.CreateFilledGeometryRealization(iD2D1PathGeometry, flatteningTolerance), context.CreateFilledGeometryRealization(iD2D1PathGeometry2, flatteningTolerance));
			}
		}
	}

	private ID2D1DeviceContext1? Context1(ID2D1DeviceContext context)
	{
		if (_context1Of != context.NativePointer)
		{
			_context1?.Dispose();
			_context1 = context.QueryInterfaceOrNull<ID2D1DeviceContext1>();
			_context1Of = context.NativePointer;
			ForgetDrawings();
		}
		return _context1;
	}

	private void ForgetDrawings()
	{
		for (int i = 0; i < _drawings.Length; i++)
		{
			_drawings[i]?.Body.Dispose();
			_drawings[i]?.Rim.Dispose();
			_drawings[i] = null;
		}
	}

	public void Dispose()
	{
		ForgetDrawings();
		_context1?.Dispose();
		_clayBrush?.Dispose();
		_inkBrush?.Dispose();
	}

	private static Color4 ToColor4(System.Windows.Media.Color c)
	{
		return new Color4((float)(int)c.R / 255f, (float)(int)c.G / 255f, (float)(int)c.B / 255f, (float)(int)c.A / 255f);
	}

	private static double Ripple(Point p, double time, List<Splash> splashes)
	{
		double num = 0.0;
		foreach (Splash splash in splashes)
		{
			double num2 = time - splash.Start;
			double length = (p - splash.Origin).Length;
			num += 5.0 * Math.Exp((0.0 - num2) * 3.0) * Math.Exp((0.0 - length) / 140.0) * Math.Sin(length * 0.09 - num2 * 16.0);
		}
		return num;
	}
}
