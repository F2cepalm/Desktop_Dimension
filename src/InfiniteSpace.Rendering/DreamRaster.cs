using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using InfiniteSpace.Spaces;

namespace InfiniteSpace.Rendering;

internal sealed class DreamRaster
{
	private readonly record struct Edge(double YMin, double YMax, double XAtYMin, double Slope, int Winding);

	private readonly record struct Crossing(double X, int Winding);

	private readonly record struct Spark(Vector Direction, double Speed, int Pixel);

	private sealed record Burst(Point Origin, double Start, Spark[] Sparks);

	private sealed class Contour
	{
		private const double Spacing = 1.0;

		[ThreadStatic]
		private static List<Point>? t_raw;

		public Point[] Points { get; private set; } = Array.Empty<Point>();

		public Vector[] Normals { get; private set; } = Array.Empty<Vector>();

		public int Count { get; private set; }

		public double Rate1 { get; private set; }

		public double Rate2 { get; private set; }

		public double Phase { get; private set; }

		public double[] X { get; private set; } = Array.Empty<double>();

		public double[] Y { get; private set; } = Array.Empty<double>();

		public static Contour? Build(PathFigure figure, Matrix toArt, Contour? reuse, double spacing = 1.0)
		{
			List<Point> list = t_raw ?? (t_raw = new List<Point>());
			list.Clear();
			list.Add(toArt.Transform(figure.StartPoint));
			foreach (PathSegment segment in figure.Segments)
			{
				if (segment is LineSegment lineSegment)
				{
					list.Add(toArt.Transform(lineSegment.Point));
				}
				else
				{
					if (!(segment is PolyLineSegment polyLineSegment))
					{
						continue;
					}
					foreach (Point point3 in polyLineSegment.Points)
					{
						list.Add(toArt.Transform(point3));
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
			int num2 = Math.Max(8, (int)Math.Round(num / spacing));
			Contour contour = reuse ?? new Contour();
			contour.Reserve(num2);
			Point[] points = contour.Points;
			double num3 = num / (double)num2;
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
				points[j] = point + (point2 - point) * num7;
			}
			Vector[] normals = contour.Normals;
			for (int k = 0; k < num2; k++)
			{
				Vector vector = points[(k + 3) % num2] - points[(k - 3 + num2) % num2];
				if (vector.LengthSquared < 1E-12)
				{
					vector = new Vector(1.0, 0.0);
				}
				vector.Normalize();
				normals[k] = new Vector(0.0 - vector.Y, vector.X);
			}
			double num8 = Math.Max(1.0, Math.Round(num * 0.18 / (Math.PI * 2.0)));
			double num9 = Math.Max(1.0, Math.Round(num * 0.074 / (Math.PI * 2.0)));
			contour.Count = num2;
			contour.Rate1 = Math.PI * 2.0 * num8 / (double)num2;
			contour.Rate2 = Math.PI * 2.0 * num9 / (double)num2;
			contour.Phase = num * 0.618 % (Math.PI * 2.0);
			return contour;
		}

		private void Reserve(int count)
		{
			if (Points.Length < count)
			{
				int num = ((Points.Length == 0) ? count : (count + count / 2));
				Points = new Point[num];
				Normals = new Vector[num];
				X = new double[num];
				Y = new double[num];
			}
		}
	}

	private const double BurstLife = 1.2;

	private const int BurstSparks = 14;

	private static readonly int VoidPixel = Pack(DreamStyle.Void);

	private static readonly int RimPixel = Pack(DreamStyle.Rim);

	private static readonly int[][] LayerPixels = DreamStyle.Layers.Select((StarLayer l) => l.Colors.Select(Pack).ToArray()).ToArray();

	private static readonly int[] SparkPixels = LayerPixels.SelectMany((int[] p) => p).ToArray();

	private Rect _world;

	private readonly List<Contour> _contours;

	private readonly Edge[] _edges;

	private readonly Crossing[] _crossings;

	private readonly List<int> _active = new List<int>();

	private readonly List<Burst> _bursts = new List<Burst>();

	private int[] _rowStart = Array.Empty<int>();

	private int[] _placed = Array.Empty<int>();

	private Edge[] _sorted = Array.Empty<Edge>();

	private int[] _pixels;

	private int[] _next;

	private double _lastStepTime = double.NaN;

	private Vector _lastLook = new Vector(double.NaN, double.NaN);

	private bool _firstRender = true;

	private double _time;

	public int Width { get; }

	public int Height { get; }

	public int[] Pixels => _pixels;

	public bool HasBursts => _bursts.Count > 0;

	public DreamRaster(Geometry outline, Rect world, DreamRaster? recycle = null, bool draft = false)
	{
		_world = world;
		Width = Math.Max(1, (int)Math.Round(world.Width / 2.0));
		Height = Math.Max(1, (int)Math.Round(world.Height / 2.0));
		_pixels = Buffer(recycle?._pixels, Width * Height);
		_next = Buffer(recycle?._next, Width * Height);
		if (recycle != null)
		{
			int[] rowStart = recycle._rowStart;
			int[] placed = recycle._placed;
			Edge[] sorted = recycle._sorted;
			_rowStart = rowStart;
			_placed = placed;
			_sorted = sorted;
		}
		double num = 0.5;
		Matrix toArt = new Matrix(num, 0.0, 0.0, num, (0.0 - world.X) * num, (0.0 - world.Y) * num);
		PathGeometry pathGeometry = SpaceGeometry.Flat(outline);
		_contours = recycle?._contours ?? new List<Contour>();
		int num2 = 0;
		int num3 = 0;
		double spacing = draft ? 2.5 : 1.0;
		foreach (PathFigure figure in pathGeometry.Figures)
		{
			Contour contour = Contour.Build(figure, toArt, (num2 < _contours.Count) ? _contours[num2] : null, spacing);
			if (contour != null)
			{
				if (num2 < _contours.Count)
				{
					_contours[num2] = contour;
				}
				else
				{
					_contours.Add(contour);
				}
				num2++;
				num3 += contour.Count;
			}
		}
		_contours.RemoveRange(num2, _contours.Count - num2);
		Edge[] array = recycle?._edges;
		_edges = ((array != null && array.Length >= num3) ? array : new Edge[num3 + num3 / 2]);
		Crossing[] array2 = recycle?._crossings;
		_crossings = ((array2 != null && array2.Length >= num3) ? array2 : new Crossing[num3 + num3 / 2]);
	}

	private static int[] Buffer(int[]? old, int count)
	{
		if (old == null)
		{
			return new int[count];
		}
		if (old.Length < count)
		{
			return new int[count + count / 2];
		}
		Array.Clear(old, 0, count);
		return old;
	}

	public void Move(Rect world)
	{
		_world = world;
	}

	public bool IsInside(Point art)
	{
		int num = (int)Math.Floor(art.X);
		int num2 = (int)Math.Floor(art.Y);
		if ((uint)num < (uint)Width && (uint)num2 < (uint)Height)
		{
			return _pixels[num2 * Width + num] != 0;
		}
		return false;
	}

	public void AddBurst(Point art)
	{
		Random shared = Random.Shared;
		Spark[] array = new Spark[14];
		for (int i = 0; i < array.Length; i++)
		{
			double num = shared.NextDouble() * (Math.PI * 2.0);
			array[i] = new Spark(new Vector(Math.Cos(num), Math.Sin(num)), 30.0 + shared.NextDouble() * 70.0, SparkPixels[shared.Next(SparkPixels.Length)]);
		}
		_bursts.Add(new Burst(art, _time, array));
	}

	public bool Render(FrameState frame)
	{
		for (int num = _bursts.Count - 1; num >= 0; num--)
		{
			if (frame.Time - _bursts[num].Start > 1.2)
			{
				_bursts.RemoveAt(num);
			}
		}
		double animTime = (_bursts.Count > 0) ? frame.Time : (Math.Floor(frame.Time * 30.0) / 30.0);
		if (!_firstRender && _bursts.Count == 0 && animTime == _lastStepTime && !double.IsNaN(_lastLook.X) && (frame.Look - _lastLook).LengthSquared < 0.25)
		{
			return false;
		}
		_firstRender = false;
		_lastStepTime = animTime;
		_lastLook = frame.Look;
		_time = animTime;
		foreach (Contour contour in _contours)
		{
			Wobble(contour);
		}
		int length = Width * Height;
		Array.Clear(_next, 0, length);
		FillVoid();
		DrawStars(frame);
		DrawSparks();
		DrawRim();
		if (((ReadOnlySpan<int>)_next.AsSpan(0, length)).SequenceEqual((ReadOnlySpan<int>)_pixels.AsSpan(0, length)))
		{
			return false;
		}
		int[] next = _next;
		int[] pixels = _pixels;
		_pixels = next;
		_next = pixels;
		return true;
	}

	private void Wobble(Contour contour)
	{
		double time = _time;
		for (int i = 0; i < contour.Count; i++)
		{
			Point point = contour.Points[i];
			double num = 1.3 * (0.65 * Math.Sin((double)i * contour.Rate1 + time * 2.4 + contour.Phase) + 0.35 * Math.Sin((double)i * contour.Rate2 - time * 1.6 + contour.Phase * 1.7));
			foreach (Burst burst in _bursts)
			{
				double num2 = time - burst.Start;
				double length = (point - burst.Origin).Length;
				num += 2.5 * Math.Exp((0.0 - num2) * 3.0) * Math.Exp((0.0 - length) / 70.0) * Math.Sin(length * 0.18 - num2 * 16.0);
			}
			Vector vector = contour.Normals[i];
			contour.X[i] = point.X + vector.X * num;
			contour.Y[i] = point.Y + vector.Y * num;
		}
	}

	private void FillVoid()
	{
		if (_rowStart.Length < Height + 2)
		{
			_rowStart = new int[Height + 2];
		}
		Span<int> span = _rowStart.AsSpan(0, Height + 2);
		span.Clear();
		int num = 0;
		foreach (Contour contour in _contours)
		{
			double[] x = contour.X;
			double[] y = contour.Y;
			for (int i = 0; i < contour.Count; i++)
			{
				int num2 = ((i + 1 != contour.Count) ? (i + 1) : 0);
				double num3 = y[i];
				double num4 = y[num2];
				if (num3 != num4)
				{
					Edge edge = ((num3 < num4) ? new Edge(num3, num4, x[i], (x[num2] - x[i]) / (num4 - num3), 1) : new Edge(num4, num3, x[num2], (x[i] - x[num2]) / (num3 - num4), -1));
					_edges[num++] = edge;
					span[FirstRow(edge.YMin) + 1]++;
				}
			}
		}
		for (int j = 1; j < span.Length; j++)
		{
			span[j] += span[j - 1];
		}
		if (_sorted.Length < num)
		{
			_sorted = new Edge[_edges.Length];
		}
		if (_placed.Length < span.Length)
		{
			_placed = new int[span.Length];
		}
		span.CopyTo(_placed);
		for (int k = 0; k < num; k++)
		{
			_sorted[_placed[FirstRow(_edges[k].YMin)]++] = _edges[k];
		}
		_active.Clear();
		for (int l = 0; l < Height; l++)
		{
			double num5 = (double)l + 0.5;
			for (int m = span[l]; m < span[l + 1]; m++)
			{
				_active.Add(m);
			}
			int num6 = 0;
			for (int num7 = _active.Count - 1; num7 >= 0; num7--)
			{
				ref Edge reference = ref _sorted[_active[num7]];
				if (reference.YMax <= num5)
				{
					List<int> active = _active;
					int index = num7;
					List<int> active2 = _active;
					active[index] = active2[active2.Count - 1];
					_active.RemoveAt(_active.Count - 1);
				}
				else
				{
					Crossing crossing = new Crossing(reference.XAtYMin + (num5 - reference.YMin) * reference.Slope, reference.Winding);
					int num8 = num6++;
					while (num8 > 0 && _crossings[num8 - 1].X > crossing.X)
					{
						_crossings[num8] = _crossings[num8 - 1];
						num8--;
					}
					_crossings[num8] = crossing;
				}
			}
			int num9 = 0;
			int num10 = l * Width;
			for (int n = 0; n < num6 - 1; n++)
			{
				num9 += _crossings[n].Winding;
				if (num9 != 0)
				{
					int num11 = Math.Max(0, (int)Math.Ceiling(_crossings[n].X - 0.5));
					int num12 = Math.Min(Width, (int)Math.Ceiling(_crossings[n + 1].X - 0.5));
					if (num12 > num11)
					{
						_next.AsSpan(num10 + num11, num12 - num11).Fill(VoidPixel);
					}
				}
			}
		}
	}

	private int FirstRow(double yMin)
	{
		return (int)Math.Clamp(Math.Ceiling(yMin - 0.5), 0.0, Height);
	}

	private void DrawStars(FrameState frame)
	{
		for (int i = 0; i < DreamStyle.Layers.Length; i++)
		{
			StarLayer starLayer = DreamStyle.Layers[i];
			int[] array = LayerPixels[i];
			Vector vector = frame.LayerOffset(starLayer);
			double cellSize = starLayer.CellSize;
			int num = (int)Math.Floor((_world.Left - vector.X) / cellSize) - 1;
			int num2 = (int)Math.Floor((_world.Right - vector.X) / cellSize) + 1;
			int num3 = (int)Math.Floor((_world.Top - vector.Y) / cellSize) - 1;
			int num4 = (int)Math.Floor((_world.Bottom - vector.Y) / cellSize) + 1;
			uint num5 = (uint)(starLayer.Density * 65536.0);
			for (int j = num3; j <= num4; j++)
			{
				for (int k = num; k <= num2; k++)
				{
					uint num6 = Hash(k, j, i);
					if ((num6 & 0xFFFF) < num5)
					{
						uint num7 = Mix(num6);
						double num8 = (double)k * cellSize + (double)((num6 >> 16) & 0xFF) / 256.0 * cellSize + vector.X - _world.Left;
						double num9 = (double)j * cellSize + (double)(num7 & 0xFF) / 256.0 * cellSize + vector.Y - _world.Top;
						int num10 = (int)Math.Floor(num8 / 2.0);
						int num11 = (int)Math.Floor(num9 / 2.0);
						if (num10 >= -3 && num11 >= -3 && num10 <= Width + 3 && num11 <= Height + 3)
						{
							int colour = array[(num7 >> 8) % (uint)array.Length];
							double num12 = (double)((num7 >> 16) & 0xFF) / 64.0;
							double num13 = starLayer.TwinkleSpeed * (0.6 + (double)((num7 >> 24) & 0xFF) / 320.0);
							int num14 = (int)(frame.Time * num13 + num12) & 3;
							DrawSprite(num10, num11, starLayer.Frames[num14], colour);
						}
					}
				}
			}
		}
	}

	private void DrawSparks()
	{
		foreach (Burst burst in _bursts)
		{
			double num = _time - burst.Start;
			double num2 = (1.0 - Math.Exp((0.0 - num) * 3.0)) / 3.0;
			int frame = Math.Max(0, 3 - (int)(num / 1.2 * 4.0));
			Spark[] sparks = burst.Sparks;
			for (int i = 0; i < sparks.Length; i++)
			{
				Spark spark = sparks[i];
				Point point = burst.Origin + spark.Direction * (spark.Speed * num2);
				DrawSprite((int)Math.Floor(point.X), (int)Math.Floor(point.Y), frame, spark.Pixel);
			}
		}
	}

	private void DrawSprite(int x, int y, int frame, int colour)
	{
		PlotOnVoid(x, y, colour);
		if (frame == 0)
		{
			return;
		}
		PlotOnVoid(x - 1, y, colour);
		PlotOnVoid(x + 1, y, colour);
		PlotOnVoid(x, y - 1, colour);
		PlotOnVoid(x, y + 1, colour);
		if (frame != 1)
		{
			PlotOnVoid(x - 2, y, colour);
			PlotOnVoid(x + 2, y, colour);
			PlotOnVoid(x, y - 2, colour);
			PlotOnVoid(x, y + 2, colour);
			if (frame != 2)
			{
				PlotOnVoid(x - 1, y - 1, colour);
				PlotOnVoid(x + 1, y - 1, colour);
				PlotOnVoid(x - 1, y + 1, colour);
				PlotOnVoid(x + 1, y + 1, colour);
			}
		}
	}

	private void PlotOnVoid(int x, int y, int colour)
	{
		if ((uint)x < (uint)Width && (uint)y < (uint)Height)
		{
			ref int reference = ref _next[y * Width + x];
			if (reference != 0)
			{
				reference = colour;
			}
		}
	}

	private void DrawRim()
	{
		foreach (Contour contour in _contours)
		{
			int count = contour.Count;
			int x = (int)Math.Floor(contour.X[count - 1]);
			int y = (int)Math.Floor(contour.Y[count - 1]);
			for (int i = 0; i < count; i++)
			{
				int num = (int)Math.Floor(contour.X[i]);
				int num2 = (int)Math.Floor(contour.Y[i]);
				Line(x, y, num, num2);
				x = num;
				y = num2;
			}
		}
	}

	private void Line(int x0, int y0, int x1, int y1)
	{
		int num = Math.Abs(x1 - x0);
		int num2 = ((x0 < x1) ? 1 : (-1));
		int num3 = -Math.Abs(y1 - y0);
		int num4 = ((y0 < y1) ? 1 : (-1));
		int num5 = num + num3;
		while (true)
		{
			if ((uint)x0 < (uint)Width && (uint)y0 < (uint)Height)
			{
				_next[y0 * Width + x0] = RimPixel;
			}
			if (x0 == x1 && y0 == y1)
			{
				break;
			}
			int num6 = 2 * num5;
			if (num6 >= num3)
			{
				num5 += num3;
				x0 += num2;
			}
			if (num6 <= num)
			{
				num5 += num;
				y0 += num4;
			}
		}
	}

	private static int Pack(Color c)
	{
		int a = c.A;
		int num = c.R * a / 255;
		int num2 = c.G * a / 255;
		int num3 = c.B * a / 255;
		return (a << 24) | (num << 16) | (num2 << 8) | num3;
	}

	private static uint Hash(int x, int y, int z)
	{
		return Mix((uint)((x * -1918454973) ^ (y * -669632447) ^ (z * -887442657)));
	}

	private static uint Mix(uint h)
	{
		h ^= h >> 16;
		h *= 2146121005;
		h ^= h >> 15;
		h *= 2221713035u;
		h ^= h >> 16;
		return h;
	}
}
