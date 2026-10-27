using System;

namespace InfiniteSpace.Rendering;

internal static class Headroom
{
	public const int Step = 128;

	public static int Size(int needed, int had)
	{
		int growth = (had > 0) ? Math.Clamp(had / 4, 128, 512) : 128;
		return RoundUp((had > 0 && needed > had) ? Math.Max(needed, had + growth) : needed);
	}

	private static int RoundUp(int pixels)
	{
		return Math.Max(128, (pixels + 128 - 1) / 128 * 128);
	}
}
