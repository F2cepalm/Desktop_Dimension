using System;

namespace InfiniteSpace.Spaces;

internal static class InkOrder
{
	private static long _last;

	public static long Next()
	{
		return _last = Math.Max(_last + 1, DateTime.UtcNow.Ticks);
	}
}
