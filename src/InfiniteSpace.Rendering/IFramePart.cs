namespace InfiniteSpace.Rendering;

internal interface IFramePart
{
	bool WantsFrame { get; }

	void RenderFrame(FrameState frame);

	void DeviceLost();
}
