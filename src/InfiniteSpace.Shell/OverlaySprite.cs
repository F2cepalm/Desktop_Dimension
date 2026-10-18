using System;
using System.Windows;
using InfiniteSpace.Native;
using InfiniteSpace.Rendering;
using Vortice.DirectComposition;

namespace InfiniteSpace.Shell;

internal sealed class OverlaySprite : IDisposable
{
	private readonly GpuCompositor _gpu;

	private OverlayPicture? _picture;

	private bool _shown;

	private (float X, float Y) _at = (X: float.NaN, Y: float.NaN);

	public IDCompositionVisual Visual { get; }

	public OverlayPicture? Picture
	{
		get
		{
			return _picture;
		}
		set
		{
			if (_picture != value)
			{
				_picture = value;
				if (_shown)
				{
					Visual.SetContent(value?.Surface);
				}
				_gpu.Invalidate();
			}
		}
	}

	public bool Shown
	{
		get
		{
			return _shown;
		}
		set
		{
			if (_shown != value)
			{
				_shown = value;
				Visual.SetContent((!value) ? null : _picture?.Surface);
				_gpu.Invalidate();
			}
		}
	}

	public OverlaySprite(GpuCompositor gpu, IDCompositionVisual parent, OverlayPicture? picture = null)
	{
		_gpu = gpu;
		Visual = gpu.Composition.CreateVisual();
		parent.AddVisual(Visual, true, null);
		_picture = picture;
	}

	public void MoveTo(Point at)
	{
		double scale = Dpi.Scale;
		(float, float) tuple = ((float)Math.Round(at.X * scale), (float)Math.Round(at.Y * scale));
		(float, float) tuple2 = tuple;
		(float, float) at2 = _at;
		if (tuple2.Item1 != at2.Item1 || tuple2.Item2 != at2.Item2)
		{
			_at = tuple;
			Visual.SetOffsetX(tuple.Item1);
			Visual.SetOffsetY(tuple.Item2);
			_gpu.Invalidate();
		}
	}

	public void CentreAt(Point centre)
	{
		Size size = _picture?.Size ?? default(Size);
		MoveTo(new Point(centre.X - size.Width / 2.0, centre.Y - size.Height / 2.0));
	}

	public void Dispose()
	{
		Visual.Dispose();
	}
}
