using System;
using System.Windows;
using InfiniteSpace.Native;
using InfiniteSpace.Rendering;
using Vortice;
using Vortice.DXGI;
using Vortice.Direct2D1;
using Vortice.DirectComposition;

namespace InfiniteSpace.Shell;

internal sealed class OverlayPicture : IDisposable
{
	private int _capWidth;

	private int _capHeight;

	public IDCompositionSurface Surface { get; }

	public Size Size { get; private set; }

	private OverlayPicture(IDCompositionSurface surface, Size size, int capWidth, int capHeight)
	{
		Surface = surface;
		Size = size;
		_capWidth = capWidth;
		_capHeight = capHeight;
	}

	public static OverlayPicture Paint(GpuCompositor gpu, double width, double height, Action<ID2D1DeviceContext> draw)
	{
		float num = (float)Dpi.Scale;
		int num2 = Math.Max(1, (int)Math.Ceiling(width * (double)num));
		int num3 = Math.Max(1, (int)Math.Ceiling(height * (double)num));
		gpu.Composition.CreateSurface((uint)num2, (uint)num3, Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied, out var surface).CheckError();
		OverlayPicture result = new OverlayPicture(surface, new Size((float)num2 / num, (float)num3 / num), num2, num3);
		SurfaceDrawing.Draw(gpu, surface, new RawRect(0, 0, num2, num3), num, draw);
		return result;
	}

	public static void Repaint(GpuCompositor gpu, ref OverlayPicture? picture, double width, double height, Action<ID2D1DeviceContext> draw)
	{
		float scale = (float)Dpi.Scale;
		int neededWidth = Math.Max(1, (int)Math.Ceiling(width * (double)scale));
		int neededHeight = Math.Max(1, (int)Math.Ceiling(height * (double)scale));
		if (picture != null && neededWidth <= picture._capWidth && neededHeight <= picture._capHeight)
		{
			picture.Size = new Size((float)neededWidth / scale, (float)neededHeight / scale);
			SurfaceDrawing.Draw(gpu, picture.Surface, new RawRect(0, 0, picture._capWidth, picture._capHeight), scale, draw);
			return;
		}
		int minCapWidth = (int)Math.Ceiling(160.0 * (double)scale);
		int minCapHeight = (int)Math.Ceiling(36.0 * (double)scale);
		int capWidth = Math.Max(neededWidth, minCapWidth);
		int capHeight = Math.Max(neededHeight, minCapHeight);
		if (picture != null)
		{
			capWidth = Math.Max(capWidth, picture._capWidth + picture._capWidth / 2);
			capHeight = Math.Max(capHeight, picture._capHeight + picture._capHeight / 2);
		}
		gpu.Composition.CreateSurface((uint)capWidth, (uint)capHeight, Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied, out var surface).CheckError();
		picture = new OverlayPicture(surface, new Size((float)neededWidth / scale, (float)neededHeight / scale), capWidth, capHeight);
		SurfaceDrawing.Draw(gpu, picture.Surface, new RawRect(0, 0, capWidth, capHeight), scale, draw);
	}

	public void Dispose()
	{
		Surface.Dispose();
	}
}
