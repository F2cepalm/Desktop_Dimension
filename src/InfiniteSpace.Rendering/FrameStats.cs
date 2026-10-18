using System;
using System.Diagnostics;

namespace InfiniteSpace.Rendering;

internal static class FrameStats
{
	public const int Capacity = 240;

	private static readonly double[] Times = new double[240];

	private static readonly bool[] Interactive = new bool[240];

	private static readonly long[] Stamps = new long[240];

	private static int _next;

	public static long Count { get; private set; }

	public static void Add(double milliseconds, bool interactive)
	{
		Times[_next] = milliseconds;
		Interactive[_next] = interactive;
		Stamps[_next] = Stopwatch.GetTimestamp();
		_next = (_next + 1) % 240;
		Count++;
	}

	public static int Copy(Span<double> times, Span<bool> interactive, Span<double> intervals)
	{
		int num = (int)Math.Min(Math.Min(Count, 240L), times.Length);
		for (int i = 0; i < num; i++)
		{
			int num2 = ((_next - num + i) % 240 + 240) % 240;
			int num3 = (num2 - 1 + 240) % 240;
			times[i] = Times[num2];
			interactive[i] = Interactive[num2];
			intervals[i] = ((i == 0 && Count <= num) ? 0.0 : Stopwatch.GetElapsedTime(Stamps[num3], Stamps[num2]).TotalMilliseconds);
		}
		return num;
	}
}
