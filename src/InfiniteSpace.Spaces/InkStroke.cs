using System;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;

namespace InfiniteSpace.Spaces;

internal sealed class InkStroke : IInk
{
	public const double DefaultThickness = 3.0;

	private (double[] Points, int Revision, Point Corner)? _corner;

	public long Order { get; set; }

	public double Thickness { get; set; } = 3.0;

	public string? Color { get; set; }

	public double[] Points { get; set; } = Array.Empty<double>();

	[JsonIgnore]
	public int Revision { get; set; }

	[JsonIgnore]
	public Point Corner
	{
		get
		{
			(double[], int, Point)? corner = _corner;
			if (corner.HasValue)
			{
				(double[], int, Point) valueOrDefault = corner.GetValueOrDefault();
				if (valueOrDefault.Item1 == Points && valueOrDefault.Item2 == Revision)
				{
					return valueOrDefault.Item3;
				}
			}
			double num = double.MaxValue;
			double num2 = double.MaxValue;
			for (int i = 0; i + 1 < Points.Length; i += 2)
			{
				num = Math.Min(num, Points[i]);
				num2 = Math.Min(num2, Points[i + 1]);
			}
			Point point = ((Points.Length < 2) ? default(Point) : new Point(num, num2));
			_corner = (Points, Revision, point);
			return point;
		}
	}

	public Matrix Placed(Matrix toScreen, Point? anchor = null)
	{
		Point point = anchor ?? Corner;
		Point point2 = toScreen.Transform(point);
		return new Matrix(1.0, 0.0, 0.0, 1.0, point2.X - point.X, point2.Y - point.Y);
	}
}
