using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace InfiniteSpace.Spaces;

internal static class SpaceGeometry
{
	public const double Tolerance = 0.25;

	public const double MinArea = 300.0;

	public static PathGeometry FromStroke(IReadOnlyList<Point> points, Vector offset)
	{
		PathGeometry pathGeometry = new PathGeometry
		{
			FillRule = FillRule.Nonzero
		};
		if (points.Count < 3)
		{
			return pathGeometry;
		}
		PointCollection pointCollection = new PointCollection(points.Count - 1);
		for (int i = 1; i < points.Count; i++)
		{
			pointCollection.Add(points[i] + offset);
		}
		pathGeometry.Figures.Add(new PathFigure(points[0] + offset, new _003C_003Ez__ReadOnlySingleElementList<PathSegment>(new PolyLineSegment(pointCollection, isStroked: true)), closed: true));
		return pathGeometry;
	}

	public static Geometry EraserStroke(IReadOnlyList<Point> points, Vector offset, double radius)
	{
		if (points.Count == 0)
		{
			return Geometry.Empty;
		}
		if (points.Count == 1)
		{
			return new EllipseGeometry(points[0] + offset, radius, radius);
		}
		PointCollection pointCollection = new PointCollection(points.Count - 1);
		for (int i = 1; i < points.Count; i++)
		{
			pointCollection.Add(points[i] + offset);
		}
		PathGeometry pathGeometry = new PathGeometry(new _003C_003Ez__ReadOnlySingleElementList<PathFigure>(new PathFigure(points[0] + offset, new _003C_003Ez__ReadOnlySingleElementList<PathSegment>(new PolyLineSegment(pointCollection, isStroked: true)), closed: false)));
		Pen pen = new Pen(Brushes.Black, radius * 2.0)
		{
			StartLineCap = PenLineCap.Round,
			EndLineCap = PenLineCap.Round,
			LineJoin = PenLineJoin.Round
		};
		PathGeometry widenedPathGeometry = pathGeometry.GetWidenedPathGeometry(pen, 0.25, ToleranceType.Absolute);
		widenedPathGeometry.FillRule = FillRule.Nonzero;
		return widenedPathGeometry;
	}

	public static double Area(Geometry geometry)
	{
		return geometry.GetArea(0.25, ToleranceType.Absolute);
	}

	public static PathGeometry? Subtract(Geometry shape, Geometry eraser)
	{
		PathGeometry pathGeometry = Geometry.Combine(shape, eraser, GeometryCombineMode.Exclude, null, 0.25, ToleranceType.Absolute);
		if (!(Area(pathGeometry) < 300.0))
		{
			return pathGeometry;
		}
		return null;
	}

	public static bool Overlap(Geometry a, Geometry b)
	{
		if (!a.Bounds.IntersectsWith(b.Bounds))
		{
			return false;
		}
		return Area(Geometry.Combine(a, b, GeometryCombineMode.Intersect, null, 0.25, ToleranceType.Absolute)) > 1.0;
	}

	public static Geometry Union(IReadOnlyList<Geometry> outlines)
	{
		Geometry geometry = outlines[0];
		for (int i = 1; i < outlines.Count; i++)
		{
			geometry = Geometry.Combine(geometry, outlines[i], GeometryCombineMode.Union, null, 0.25, ToleranceType.Absolute);
		}
		return geometry;
	}

	public static PathGeometry Flat(Geometry outline)
	{
		if (outline is PathGeometry pathGeometry && (pathGeometry.Transform == null || pathGeometry.Transform.Value.IsIdentity) && IsFlat(pathGeometry))
		{
			return pathGeometry;
		}
		return outline.GetFlattenedPathGeometry(0.25, ToleranceType.Absolute);
	}

	private static bool IsFlat(PathGeometry path)
	{
		foreach (PathFigure figure in path.Figures)
		{
			foreach (PathSegment segment in figure.Segments)
			{
				if ((!(segment is LineSegment) && !(segment is PolyLineSegment)) || 1 == 0)
				{
					return false;
				}
			}
		}
		return true;
	}

	public static PathGeometry Transform(PathGeometry outline, Matrix m)
	{
		if (m.IsIdentity)
		{
			return outline;
		}
		PathGeometry pathGeometry = new PathGeometry
		{
			FillRule = outline.FillRule
		};
		foreach (PathFigure figure in outline.Figures)
		{
			PointCollection pointCollection = new PointCollection();
			foreach (PathSegment segment in figure.Segments)
			{
				if (segment is LineSegment lineSegment)
				{
					pointCollection.Add(m.Transform(lineSegment.Point));
				}
				else
				{
					if (!(segment is PolyLineSegment polyLineSegment))
					{
						continue;
					}
					foreach (Point point in polyLineSegment.Points)
					{
						pointCollection.Add(m.Transform(point));
					}
				}
			}
			pathGeometry.Figures.Add(new PathFigure(m.Transform(figure.StartPoint), new _003C_003Ez__ReadOnlySingleElementList<PathSegment>(new PolyLineSegment(pointCollection, isStroked: true)), figure.IsClosed));
		}
		pathGeometry.Freeze();
		return pathGeometry;
	}

	[ThreadStatic]
	private static List<Point>? t_warpPoints;

	public static PathGeometry Warp(Geometry outline, Func<Point, Point> map, double step)
	{
		PathGeometry pathGeometry = (outline as PathGeometry) ?? outline.GetFlattenedPathGeometry(0.25, ToleranceType.Absolute);
		PathGeometry pathGeometry2 = new PathGeometry
		{
			FillRule = pathGeometry.FillRule
		};
		List<Point> list = (t_warpPoints ??= new List<Point>());
		foreach (PathFigure figure in pathGeometry.Figures)
		{
			list.Clear();
			list.Add(figure.StartPoint);
			foreach (PathSegment segment in figure.Segments)
			{
				if (segment is LineSegment lineSegment)
				{
					list.Add(lineSegment.Point);
					continue;
				}
				if (segment is PolyLineSegment polyLineSegment)
				{
					list.AddRange(polyLineSegment.Points);
					continue;
				}
				return Warp(outline.GetFlattenedPathGeometry(0.25, ToleranceType.Absolute), map, step);
			}
			int estCapacity = list.Count * Math.Max(1, (int)(32.0 / step));
			PointCollection pointCollection = new PointCollection(estCapacity);
			for (int i = 0; i < list.Count; i++)
			{
				Point point = list[i];
				Point point2 = list[(i + 1) % list.Count];
				if (i > 0)
				{
					pointCollection.Add(map(point));
				}
				int num = (int)Math.Ceiling((point2 - point).Length / step);
				for (int j = 1; j < num; j++)
				{
					pointCollection.Add(map(point + (point2 - point) * ((double)j / (double)num)));
				}
			}
			pathGeometry2.Figures.Add(new PathFigure(map(figure.StartPoint), new _003C_003Ez__ReadOnlySingleElementList<PathSegment>(new PolyLineSegment(pointCollection, isStroked: true)), closed: true));
		}
		pathGeometry2.Freeze();
		return pathGeometry2;
	}

	public static Geometry Mapped(PathGeometry outline, Matrix m)
	{
		if (m.IsIdentity)
		{
			return outline;
		}
		PathGeometry pathGeometry = new PathGeometry(outline.Figures, outline.FillRule, new MatrixTransform(m));
		pathGeometry.Freeze();
		return pathGeometry;
	}

	public static List<double[]> Encode(Geometry geometry, out FillRule fillRule)
	{
		PathGeometry flattenedPathGeometry = geometry.GetFlattenedPathGeometry(0.25, ToleranceType.Absolute);
		fillRule = flattenedPathGeometry.FillRule;
		List<double[]> list = new List<double[]>(flattenedPathGeometry.Figures.Count);
		List<double> coords = new List<double>();
		foreach (PathFigure figure in flattenedPathGeometry.Figures)
		{
			coords.Clear();
			Point? last = null;
			Add(figure.StartPoint);
			foreach (PathSegment segment in figure.Segments)
			{
				if (!(segment is LineSegment lineSegment))
				{
					if (!(segment is PolyLineSegment polyLineSegment))
					{
						continue;
					}
					foreach (Point point in polyLineSegment.Points)
					{
						Add(point);
					}
				}
				else
				{
					Add(lineSegment.Point);
				}
			}
			if (coords.Count >= 4)
			{
				List<double> list2 = coords;
				if (Math.Abs(list2[list2.Count - 2] - coords[0]) < 0.01)
				{
					List<double> list3 = coords;
					if (Math.Abs(list3[list3.Count - 1] - coords[1]) < 0.01)
					{
						coords.RemoveRange(coords.Count - 2, 2);
					}
				}
			}
			if (coords.Count >= 6)
			{
				list.Add(coords.ToArray());
			}
			void Add(Point p)
			{
				if (last.HasValue)
				{
					Point valueOrDefault = last.GetValueOrDefault();
					if (Math.Abs(valueOrDefault.X - p.X) < 0.01 && Math.Abs(valueOrDefault.Y - p.Y) < 0.01)
					{
						return;
					}
				}
				coords.Add(Math.Round(p.X, 2));
				coords.Add(Math.Round(p.Y, 2));
				last = p;
			}
		}
		return list;
	}

	public static PathGeometry Decode(SpaceRecord record)
	{
		PathGeometry pathGeometry = new PathGeometry
		{
			FillRule = record.FillRule
		};
		foreach (double[] figure in record.Figures)
		{
			if (figure.Length >= 6)
			{
				PointCollection pointCollection = new PointCollection(figure.Length / 2 - 1);
				for (int i = 2; i + 1 < figure.Length; i += 2)
				{
					pointCollection.Add(new Point(figure[i], figure[i + 1]));
				}
				pathGeometry.Figures.Add(new PathFigure(new Point(figure[0], figure[1]), new _003C_003Ez__ReadOnlySingleElementList<PathSegment>(new PolyLineSegment(pointCollection, isStroked: true)), closed: true));
			}
		}
		pathGeometry.Freeze();
		return pathGeometry;
	}
}
