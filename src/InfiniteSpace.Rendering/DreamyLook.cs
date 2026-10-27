using System;
using System.Windows;
using System.Windows.Media;
using InfiniteSpace.Native;

namespace InfiniteSpace.Rendering;

internal sealed class DreamyLook : ISpaceLook, IDisposable
{
	private DreamRaster? _raster;

	private Rect _world;

	public bool HasBursts => _raster?.HasBursts ?? false;

	public void SetShape(Geometry outline, Rect world)
	{
		SetShape(outline, world, draft: false);
	}

	public void SetShape(Geometry outline, Rect world, bool draft)
	{
		_world = world;
		DreamRaster? old = _raster;
		_raster = new DreamRaster(outline, world, old, draft);
		old?.Dispose();
	}

	public void Move(Geometry outline, Rect world)
	{
		_world = world;
		_raster?.Move(world);
	}

	public bool Render(FrameState frame)
	{
		return _raster?.Render(frame) ?? false;
	}

	public void Present(ISpaceCanvas canvas)
	{
		if (_raster != null)
		{
			canvas.ShowArt(_raster.Pixels, _raster.Width, _raster.Height, (float)(2.0 * Dpi.Scale));
		}
	}

	public bool IsInside(Point screen)
	{
		return _raster?.IsInside(ToArt(screen)) ?? false;
	}

	public void AddBurst(Point screen)
	{
		_raster?.AddBurst(ToArt(screen));
	}

	public void Dispose()
	{
		_raster?.Dispose();
		_raster = null;
	}

	private Point ToArt(Point screen)
	{
		return new Point((screen.X - _world.X) / 2.0, (screen.Y - _world.Y) / 2.0);
	}
}
