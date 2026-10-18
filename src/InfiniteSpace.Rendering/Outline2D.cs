using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows;
using System.Windows.Media;
using InfiniteSpace.Spaces;
using Vortice.Direct2D1;

namespace InfiniteSpace.Rendering;

internal readonly record struct Outline2D(Vector2[][] Figures, FillMode FillMode)
{
	public static Outline2D Empty { get; } = new Outline2D(Array.Empty<Vector2[]>(), FillMode.Winding);

	public static Outline2D From(Geometry outline, Point origin, double tolerance = 0.25)
	{
		PathGeometry pathGeometry = (tolerance > 0.25)
			? outline.GetFlattenedPathGeometry(tolerance, ToleranceType.Absolute)
			: SpaceGeometry.Flat(outline);
		List<Vector2[]> list = new List<Vector2[]>(pathGeometry.Figures.Count);
		foreach (PathFigure figure in pathGeometry.Figures)
		{
			Vector2[] array = Figure(figure, origin);
			if (array.Length >= 3)
			{
				list.Add(array);
			}
		}
		return new Outline2D(list.ToArray(), (pathGeometry.FillRule != FillRule.EvenOdd) ? FillMode.Winding : FillMode.Alternate);
	}

	public Outline2D Offset(System.Windows.Vector by)
	{
		Vector2 vector = new Vector2((float)by.X, (float)by.Y);
		Vector2[][] array = new Vector2[Figures.Length][];
		for (int i = 0; i < array.Length; i++)
		{
			Vector2[] array2 = Figures[i];
			Vector2[] array3 = new Vector2[array2.Length];
			for (int j = 0; j < array3.Length; j++)
			{
				array3[j] = array2[j] + vector;
			}
			array[i] = array3;
		}
		return new Outline2D(array, FillMode);
	}

	public ID2D1PathGeometry ToPath(ID2D1Factory1 factory)
	{
		ID2D1PathGeometry1 iD2D1PathGeometry = factory.CreatePathGeometry();
		using ID2D1GeometrySink iD2D1GeometrySink = iD2D1PathGeometry.Open();
		iD2D1GeometrySink.SetFillMode(FillMode);
		Vector2[][] figures = Figures;
		foreach (Vector2[] array in figures)
		{
			iD2D1GeometrySink.BeginFigure(array[0], FigureBegin.Filled);
			iD2D1GeometrySink.AddLines(array);
			iD2D1GeometrySink.EndFigure(FigureEnd.Closed);
		}
		iD2D1GeometrySink.Close();
		return iD2D1PathGeometry;
	}

	private static Vector2[] Figure(PathFigure figure, Point origin)
	{
		int num = 1;
		foreach (System.Windows.Media.PathSegment segment in figure.Segments)
		{
			num += ((!(segment is PolyLineSegment polyLineSegment)) ? 1 : polyLineSegment.Points.Count);
		}
		Vector2[] array = new Vector2[num];
		int num2 = 0;
		array[num2++] = V(figure.StartPoint, origin);
		foreach (System.Windows.Media.PathSegment segment2 in figure.Segments)
		{
			if (segment2 is LineSegment lineSegment)
			{
				array[num2++] = V(lineSegment.Point, origin);
			}
			else
			{
				if (!(segment2 is PolyLineSegment polyLineSegment2))
				{
					continue;
				}
				foreach (Point point in polyLineSegment2.Points)
				{
					array[num2++] = V(point, origin);
				}
			}
		}
		if (num2 != num)
		{
			return array[..num2];
		}
		return array;
	}

	private static Vector2 V(Point p, Point origin)
	{
		return new Vector2((float)(p.X - origin.X), (float)(p.Y - origin.Y));
	}
}
