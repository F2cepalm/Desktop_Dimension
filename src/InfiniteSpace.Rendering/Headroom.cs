using System;

namespace InfiniteSpace.Rendering;

internal static class Headroom
{
	public const int Step = 256;

	public static int Size(int needed, int had)
	{
		return RoundUp((had > 0 && needed > had) ? Math.Max(needed, had + had / 2) : needed);
	}

	private static int RoundUp(int pixels)
	{
		return Math.Max(256, (pixels + 256 - 1) / 256 * 256);
	}
}
