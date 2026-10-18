using System;
using System.Windows;

namespace InfiniteSpace.Shell;

internal static class BlockEdit
{
	public const double Spill = 40.0;

	public static Rect Drag(Rect rect, BlockGrip grip, Vector delta, double min)
	{
		double num = rect.Left;
		double num2 = rect.Top;
		double num3 = rect.Right;
		double num4 = rect.Bottom;
		if (grip.HasFlag(BlockGrip.Left))
		{
			num += delta.X;
		}
		if (grip.HasFlag(BlockGrip.Right))
		{
			num3 += delta.X;
		}
		if (grip.HasFlag(BlockGrip.Top))
		{
			num2 += delta.Y;
		}
		if (grip.HasFlag(BlockGrip.Bottom))
		{
			num4 += delta.Y;
		}
		if (num3 - num < min)
		{
			if (grip.HasFlag(BlockGrip.Left))
			{
				num = num3 - min;
			}
			else
			{
				num3 = num + min;
			}
		}
		if (num4 - num2 < min)
		{
			if (grip.HasFlag(BlockGrip.Top))
			{
				num2 = num4 - min;
			}
			else
			{
				num4 = num2 + min;
			}
		}
		return new Rect(new Point(num, num2), new Point(num3, num4));
	}

	public static Rect Grow(Rect rect, BlockGrip side, double amount, double min)
	{
		return Drag(rect, side, side switch
		{
			BlockGrip.Left => new Vector(0.0 - amount, 0.0), 
			BlockGrip.Right => new Vector(amount, 0.0), 
			BlockGrip.Top => new Vector(0.0, 0.0 - amount), 
			BlockGrip.Bottom => new Vector(0.0, amount), 
			_ => new Vector(0.0, 0.0), 
		}, min);
	}

	public static Rect Scale(Rect rect, double factor, double min)
	{
		double num = Math.Max(min, rect.Width * factor);
		double num2 = Math.Max(min, rect.Height * factor);
		Point point = new Point(rect.X + rect.Width / 2.0, rect.Y + rect.Height / 2.0);
		return new Rect(point.X - num / 2.0, point.Y - num2 / 2.0, num, num2);
	}

	public static Rect Contain(Rect before, Rect after, Rect area)
	{
		double num = after.Left;
		double num2 = after.Top;
		double num3 = after.Right;
		double num4 = after.Bottom;
		if (num < before.Left)
		{
			num = Math.Max(num, Math.Min(before.Left, area.Left));
		}
		if (num2 < before.Top)
		{
			num2 = Math.Max(num2, Math.Min(before.Top, area.Top));
		}
		if (num3 > before.Right)
		{
			num3 = Math.Min(num3, Math.Max(before.Right, area.Right));
		}
		if (num4 > before.Bottom)
		{
			num4 = Math.Min(num4, Math.Max(before.Bottom, area.Bottom));
		}
		return new Rect(new Point(num, num2), new Point(num3, num4));
	}

	public static Rect Snap(Rect rect, double grid)
	{
		double num = Math.Round(rect.Left / grid) * grid;
		double num2 = Math.Round(rect.Top / grid) * grid;
		double x = Math.Max(num + grid, Math.Round(rect.Right / grid) * grid);
		return new Rect(point2: new Point(x, Math.Max(num2 + grid, Math.Round(rect.Bottom / grid) * grid)), point1: new Point(num, num2));
	}

	public static BlockGrip GripAt(Rect rect, Point point, double margin)
	{
		Rect rect2 = rect;
		rect2.Inflate(margin, margin);
		if (!rect2.Contains(point))
		{
			return BlockGrip.None;
		}
		BlockGrip blockGrip = BlockGrip.None;
		if (Math.Abs(point.X - rect.Left) <= margin)
		{
			blockGrip |= BlockGrip.Left;
		}
		else if (Math.Abs(point.X - rect.Right) <= margin)
		{
			blockGrip |= BlockGrip.Right;
		}
		if (Math.Abs(point.Y - rect.Top) <= margin)
		{
			blockGrip |= BlockGrip.Top;
		}
		else if (Math.Abs(point.Y - rect.Bottom) <= margin)
		{
			blockGrip |= BlockGrip.Bottom;
		}
		if (blockGrip == BlockGrip.None)
		{
			if (!rect.Contains(point))
			{
				return BlockGrip.None;
			}
			return BlockGrip.Move;
		}
		return blockGrip;
	}
}
