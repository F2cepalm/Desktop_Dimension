using System;
using System.Windows;
using System.Windows.Media;

namespace InfiniteSpace.Spaces;

internal static class HostFrames
{
	public static Matrix Map(Rect from, Rect to)
	{
		double num = ((from.Width >= 1.0) ? (to.Width / from.Width) : 1.0);
		double num2 = ((from.Height >= 1.0) ? (to.Height / from.Height) : 1.0);
		return new Matrix(num, 0.0, 0.0, num2, to.X - from.X * num, to.Y - from.Y * num2);
	}

	public static bool SameScale(Matrix a, Matrix b)
	{
		if (Math.Abs(a.M11 - b.M11) < 1E-09)
		{
			return Math.Abs(a.M22 - b.M22) < 1E-09;
		}
		return false;
	}
}
