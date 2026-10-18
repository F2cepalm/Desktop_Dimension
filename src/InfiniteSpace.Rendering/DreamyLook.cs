using System;
using System.Windows;
using System.Windows.Media;
using InfiniteSpace.Native;

namespace InfiniteSpace.Rendering;

internal sealed class DreamyLook : ISpaceLook, IDisposable
{
	private DreamRaster _raster;

	private Rect _world;

	public bool HasBursts => _raster.HasBursts;

	public void SetShape(Geometry outline, Rect world)
	{
		SetShape(outline, world, draft: false);
	}

	public void SetShape(Geometry outline, Rect world, bool draft)
	{
		_world = world;
		_raster = new DreamRaster(outline, world, _raster, draft);
	}

	public void Move(Geometry outline, Rect world)
	{
		_world = world;
		_raster.Move(world);
	}

	public bool Render(FrameState frame)
	{
		return _raster.Render(frame);
	}

	public void Present(ISpaceCanvas canvas)
	{
		canvas.ShowArt(_raster.Pixels, _raster.Width, _raster.Height, (float)(2.0 * Dpi.Scale));
	}

	public bool IsInside(Point screen)
	{
		return _raster.IsInside(ToArt(screen));
	}

	public void AddBurst(Point screen)
	{
		_raster.AddBurst(ToArt(screen));
	}

	public void Dispose()
	{
	}

	private Point ToArt(Point screen)
	{
		return new Point((screen.X - _world.X) / 2.0, (screen.Y - _world.Y) / 2.0);
	}
}
