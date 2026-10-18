using System;
using System.Windows;
using InfiniteSpace.Native;
using InfiniteSpace.Rendering;
using Vortice;
using Vortice.DXGI;
using Vortice.Direct2D1;
using Vortice.DirectComposition;

namespace InfiniteSpace.Shell;

internal sealed class OverlayLayer : IDisposable
{
	private readonly GpuCompositor _gpu;

	private readonly IDCompositionVirtualSurface _surface;

	private readonly int _width;

	private readonly int _height;

	public IDCompositionVisual Visual { get; }

	public OverlayLayer(GpuCompositor gpu, IDCompositionVisual parent, int width, int height)
	{
		_gpu = gpu;
		_width = width;
		_height = height;
		Visual = gpu.Composition.CreateVisual();
		gpu.Composition.CreateVirtualSurface((uint)width, (uint)height, Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied, out _surface).CheckError();
		Visual.SetContent(_surface);
		parent.AddVisual(Visual, true, null);
	}

	public void Draw(Rect area, Action<ID2D1DeviceContext> draw)
	{
		if (!area.IsEmpty)
		{
			float num = (float)Dpi.Scale;
			int num2 = Math.Clamp((int)Math.Floor(area.Left * (double)num), 0, _width);
			int num3 = Math.Clamp((int)Math.Floor(area.Top * (double)num), 0, _height);
			int num4 = Math.Clamp((int)Math.Ceiling(area.Right * (double)num), 0, _width);
			int num5 = Math.Clamp((int)Math.Ceiling(area.Bottom * (double)num), 0, _height);
			if (num4 > num2 && num5 > num3)
			{
				SurfaceDrawing.Draw(_gpu, _surface, new RawRect(num2, num3, num4, num5), num, draw);
			}
		}
	}

	public void Clear()
	{
		_surface.Trim(Array.Empty<RawRect>(), 0u);
		_gpu.Invalidate();
	}

	public void Dispose()
	{
		Visual.SetContent(null);
		_surface.Dispose();
		Visual.Dispose();
	}
}
