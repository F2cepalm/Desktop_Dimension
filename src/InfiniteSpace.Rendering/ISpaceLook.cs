using System;
using System.Windows;
using System.Windows.Media;

namespace InfiniteSpace.Rendering;

internal interface ISpaceLook : IDisposable
{
	bool HasBursts { get; }

	void SetShape(Geometry outline, Rect world);

	void SetShape(Geometry outline, Rect world, bool draft)
	{
		SetShape(outline, world);
	}

	void Move(Geometry outline, Rect world);

	bool Render(FrameState frame);

	void Present(ISpaceCanvas canvas);

	bool IsInside(Point screen);

	void AddBurst(Point screen);
}
