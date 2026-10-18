using System.Windows;

namespace InfiniteSpace.Rendering;

internal readonly record struct FrameState(double Time, Vector Look, bool AmbientDue = true)
{
	public Vector LayerOffset(StarLayer layer)
	{
		return (DreamStyle.Drift * Time - Look * 0.035) * layer.Depth;
	}
}
