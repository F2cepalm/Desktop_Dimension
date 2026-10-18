using System;
using System.Diagnostics.Tracing;

namespace InfiniteSpace.Rendering;

[EventSource(Name = "InfiniteSpace-Frames")]
internal sealed class FrameEvents : EventSource
{
	public static readonly FrameEvents Log = new FrameEvents();

	private EventCounter? _frameTime;

	private IncrementingEventCounter? _frames;

	private FrameEvents()
	{
	}

	[Event(1, Level = EventLevel.Verbose, Message = "Frame {0} ms")]
	public void Frame(double milliseconds, int spaces, bool interactive)
	{
		WriteEvent(1, milliseconds, spaces, interactive);
	}

	[NonEvent]
	public void Record(double milliseconds, int spaces, bool interactive)
	{
		FrameStats.Add(milliseconds, interactive);
		if (IsEnabled())
		{
			_frameTime?.WriteMetric(milliseconds);
			_frames?.Increment();
			if (IsEnabled(EventLevel.Verbose, EventKeywords.All))
			{
				Frame(milliseconds, spaces, interactive);
			}
		}
	}

	protected override void OnEventCommand(EventCommandEventArgs command)
	{
		if (command.Command == EventCommand.Enable)
		{
			if (_frameTime == null)
			{
				_frameTime = new EventCounter("frame-time", this)
				{
					DisplayName = "Frame time",
					DisplayUnits = "ms"
				};
			}
			if (_frames == null)
			{
				_frames = new IncrementingEventCounter("frames", this)
				{
					DisplayName = "Frames",
					DisplayRateTimeScale = TimeSpan.FromSeconds(1L)
				};
			}
		}
	}
}
