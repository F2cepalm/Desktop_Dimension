using System;
using System.Numerics;
using InfiniteSpace.Rendering;
using Vortice;
using Vortice.DXGI;
using Vortice.Direct2D1;
using Vortice.DirectComposition;
using Vortice.Mathematics;

namespace InfiniteSpace.Shell;

internal static class SurfaceDrawing
{
	public static void Draw(GpuCompositor gpu, IDCompositionSurface surface, RawRect update, float scale, Action<ID2D1DeviceContext> draw)
	{
		ID2D1DeviceContext d2DContext = gpu.D2DContext;
		float num = 96f * scale;
		Int2 updateOffset;
		using (IDXGISurface surface2 = surface.BeginDraw<IDXGISurface>(update, out updateOffset))
		{
			using ID2D1Bitmap1 target = d2DContext.CreateBitmapFromDxgiSurface(surface2, new BitmapProperties1(GpuCompositor.Bgra, num, num, BitmapOptions.Target | BitmapOptions.CannotDraw));
			d2DContext.Target = target;
			d2DContext.SetDpi(num, num);
			d2DContext.BeginDraw();
			d2DContext.AntialiasMode = AntialiasMode.PerPrimitive;
			d2DContext.TextAntialiasMode = TextAntialiasMode.Grayscale;
			Matrix3x2 transform = (d2DContext.Transform = Matrix3x2.CreateTranslation((float)(updateOffset.X - update.Left) / scale, (float)(updateOffset.Y - update.Top) / scale));
			d2DContext.PushAxisAlignedClip(new RawRectF((float)update.Left / scale, (float)update.Top / scale, (float)update.Right / scale, (float)update.Bottom / scale), AntialiasMode.Aliased);
			d2DContext.Clear(new Color4(0f, 0f, 0f, 0f));
			draw(d2DContext);
			d2DContext.Transform = transform;
			d2DContext.PopAxisAlignedClip();
			d2DContext.Transform = Matrix3x2.Identity;
			d2DContext.EndDraw().CheckError();
			d2DContext.Target = null;
		}
		surface.EndDraw();
		gpu.Invalidate();
	}
}
