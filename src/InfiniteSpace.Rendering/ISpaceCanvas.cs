using System;
using Vortice.Direct2D1;

namespace InfiniteSpace.Rendering;

internal interface ISpaceCanvas
{
	void ShowArt(int[] pixels, int width, int height, float scale);

	void DrawVector(int width, int height, float scale, Action<ID2D1DeviceContext, ID2D1Factory1> draw);

	void DrawBackdrop(int width, int height, float scale, Action<ID2D1DeviceContext, ID2D1Factory1> draw);

	void DrawFrame(int width, int height, float scale, Action<ID2D1DeviceContext, ID2D1Factory1> draw);

	void HideArt();

	ID2D1Bitmap1 Paint(int width, int height, float scale, Action<ID2D1DeviceContext, ID2D1Factory1> draw);

	void Repaint(ref ID2D1Bitmap1? bitmap, int width, int height, float scale, Action<ID2D1DeviceContext, ID2D1Factory1> draw)
	{
		bitmap?.Dispose();
		bitmap = Paint(width, height, scale, draw);
	}
}
