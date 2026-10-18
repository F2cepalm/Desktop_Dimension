using System;
using System.Windows;

namespace InfiniteSpace.Shell;

internal sealed class CursorActivity(TimeSpan recent)
{
	private Point? _last;

	private TimeSpan _movedAt;

	public bool Sample(Point cursor, TimeSpan now)
	{
		if (_last != cursor)
		{
			_last = cursor;
			_movedAt = now;
		}
		return now - _movedAt < recent;
	}
}
