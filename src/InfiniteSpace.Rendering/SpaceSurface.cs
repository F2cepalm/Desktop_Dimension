using System;
using System.Numerics;
using System.Windows;
using Vortice;
using Vortice.DCommon;
using Vortice.DXGI;
using Vortice.Direct2D1;
using Vortice.Direct3D11;
using Vortice.DirectComposition;
using Vortice.Mathematics;

namespace InfiniteSpace.Rendering;

internal sealed class SpaceSurface : ISpaceCanvas, IDisposable
{
	private sealed class Layer(IDCompositionVisual visual) : IDisposable
	{
		private const int Step = 256;

		private (int Width, int Height) _size;

		private (int Width, int Height) _capacity;

		public IDCompositionVisual Visual { get; } = visual;

		public IDCompositionSurface? Surface { get; private set; }

		public bool Ensure(GpuCompositor gpu, int width, int height)
		{
			bool num = (object)Surface != null && width <= _capacity.Width && height <= _capacity.Height;
			bool flag = (long)_capacity.Width * (long)_capacity.Height > 4 * Math.Max(65536L, (long)width * (long)height);
			if (num && !flag)
			{
				(int, int) size = _size;
				if (size.Item1 != width || size.Item2 != height)
				{
					_size = (Width: width, Height: height);
					Visual.SetClip(new RawRectF(0f, 0f, width, height));
					gpu.Invalidate();
				}
				return false;
			}
			Surface?.Dispose();
			_capacity = (Width: Headroom.Size(width, _capacity.Width), Height: Headroom.Size(height, _capacity.Height));
			gpu.Composition.CreateSurface((uint)_capacity.Width, (uint)_capacity.Height, Format.B8G8R8A8_UNorm, Vortice.DXGI.AlphaMode.Premultiplied, out var surface).CheckError();
			Surface = surface;
			Visual.SetContent(surface);
			Visual.SetClip(new RawRectF(0f, 0f, width, height));
			_size = (Width: width, Height: height);
			gpu.Invalidate();
			return true;
		}

		public bool Clear()
		{
			if ((object)Surface == null)
			{
				return false;
			}
			Visual.SetContent(null);
			Surface.Dispose();
			Surface = null;
			_size = default;
			_capacity = default;
			return true;
		}

		public void Dispose()
		{
			Surface?.Dispose();
			Visual.Dispose();
		}
	}

	private static readonly PixelFormat Bgra = GpuCompositor.Bgra;

	private readonly GpuCompositor _gpu;

	private readonly IDCompositionTarget _target;

	private readonly IDCompositionVisual _root;

	private readonly Layer _backdrop;

	private readonly Layer _art;

	private readonly Layer _frame;

	private readonly Layer _ink;

	private float _artScale = float.NaN;

	private bool _artIsVector;

	public SpaceSurface(GpuCompositor gpu, nint hwnd)
	{
		_gpu = gpu;
		gpu.Composition.CreateTargetForHwnd(hwnd, true, out _target).CheckError();
		_root = gpu.Composition.CreateVisual();
		_backdrop = new Layer(gpu.Composition.CreateVisual());
		_art = new Layer(gpu.Composition.CreateVisual());
		_frame = new Layer(gpu.Composition.CreateVisual());
		_ink = new Layer(gpu.Composition.CreateVisual());
		_art.Visual.SetBitmapInterpolationMode(Vortice.DirectComposition.BitmapInterpolationMode.NearestNeighbor);
		_root.AddVisual(_backdrop.Visual, false, null);
		_root.AddVisual(_art.Visual, true, _backdrop.Visual);
		_root.AddVisual(_frame.Visual, true, _art.Visual);
		_root.AddVisual(_ink.Visual, true, _frame.Visual);
		_target.SetRoot(_root);
		gpu.Invalidate();
	}

	public void ShowArt(int[] pixels, int width, int height, float scale)
	{
		if (_artIsVector)
		{
			_art.Clear();
			_art.Visual.SetBitmapInterpolationMode(Vortice.DirectComposition.BitmapInterpolationMode.NearestNeighbor);
			_artScale = float.NaN;
			_artIsVector = false;
		}
		_art.Ensure(_gpu, width, height);
		if (scale != _artScale)
		{
			_art.Visual.SetTransform(Matrix3x2.CreateScale(scale));
			_artScale = scale;
		}
		Upload(_art.Surface, pixels, width, new Int32Rect(0, 0, width, height));
	}

	public void DrawVector(int width, int height, float scale, Action<ID2D1DeviceContext, ID2D1Factory1> draw)
	{
		if (!_artIsVector)
		{
			_art.Clear();
			_art.Visual.SetBitmapInterpolationMode(Vortice.DirectComposition.BitmapInterpolationMode.Linear);
			_art.Visual.SetTransform(Matrix3x2.Identity);
			_artScale = float.NaN;
			_artIsVector = true;
		}
		Draw(_art, width, height, scale, draw);
	}

	public void DrawBackdrop(int width, int height, float scale, Action<ID2D1DeviceContext, ID2D1Factory1> draw)
	{
		Draw(_backdrop, width, height, scale, draw);
	}

	public void DrawFrame(int width, int height, float scale, Action<ID2D1DeviceContext, ID2D1Factory1> draw)
	{
		Draw(_frame, width, height, scale, draw);
	}

	public void SetPlacement(float scaleX, float scaleY, float offsetX, float offsetY)
	{
		_root.SetTransform(Matrix3x2.CreateScale(scaleX, scaleY) * Matrix3x2.CreateTranslation(offsetX, offsetY));
		_gpu.Invalidate();
	}

	public void CommitNow()
	{
		_gpu.Commit();
	}

	public void HideArt()
	{
		if (_art.Clear())
		{
			_gpu.Invalidate();
		}
	}

	public void HideStills()
	{
		if (_backdrop.Clear() | _frame.Clear())
		{
			_gpu.Invalidate();
		}
	}

	public ID2D1Bitmap1 Paint(int width, int height, float scale, Action<ID2D1DeviceContext, ID2D1Factory1> draw)
	{
		return _gpu.Paint(width, height, scale, draw);
	}

	public void Repaint(ref ID2D1Bitmap1? bitmap, int width, int height, float scale, Action<ID2D1DeviceContext, ID2D1Factory1> draw)
	{
		_gpu.Repaint(ref bitmap, width, height, scale, draw);
	}

	public void ShowInk(InkLayer ink, Int32Rect dirty)
	{
		if (_ink.Ensure(_gpu, ink.Width, ink.Height))
		{
			dirty = ink.Bounds;
		}
		int num = Math.Max(0, dirty.X);
		int num2 = Math.Max(0, dirty.Y);
		int num3 = Math.Min(ink.Width, dirty.X + dirty.Width);
		int num4 = Math.Min(ink.Height, dirty.Y + dirty.Height);
		if (num3 <= num || num4 <= num2)
		{
			return;
		}
		Int32Rect int32Rect = new Int32Rect(num, num2, num3 - num, num4 - num2);
		ink.Prepare();
		ID2D1DeviceContext d2DContext = _gpu.D2DContext;
		bool flag = int32Rect == ink.Bounds;
		Int2 updateOffset;
		using (IDXGISurface surface = _ink.Surface.BeginDraw<IDXGISurface>(flag ? ((RawRect?)null) : new RawRect?(new RawRect(num, num2, num3, num4)), out updateOffset))
		{
			using ID2D1Bitmap1 target = d2DContext.CreateBitmapFromDxgiSurface(surface, new BitmapProperties1(Bgra, 96f, 96f, BitmapOptions.Target | BitmapOptions.CannotDraw));
			d2DContext.Target = target;
			d2DContext.SetDpi(96f, 96f);
			d2DContext.BeginDraw();
			ink.Compose(d2DContext, int32Rect, new Vector2(updateOffset.X, updateOffset.Y));
			d2DContext.EndDraw().CheckError();
			d2DContext.Target = null;
		}
		_ink.Surface.EndDraw();
		_gpu.Invalidate();
	}

	public void HideInk()
	{
		if (_ink.Clear())
		{
			_gpu.Invalidate();
		}
	}

	private void Draw(Layer layer, int width, int height, float scale, Action<ID2D1DeviceContext, ID2D1Factory1> draw)
	{
		layer.Ensure(_gpu, width, height);
		ID2D1DeviceContext d2DContext = _gpu.D2DContext;
		float num = 96f * scale;
		Int2 updateOffset;
		using (IDXGISurface surface = layer.Surface.BeginDraw<IDXGISurface>(new RawRect(0, 0, Math.Max(1, width), Math.Max(1, height)), out updateOffset))
		{
			using ID2D1Bitmap1 target = d2DContext.CreateBitmapFromDxgiSurface(surface, new BitmapProperties1(Bgra, num, num, BitmapOptions.Target | BitmapOptions.CannotDraw));
			d2DContext.Target = target;
			d2DContext.SetDpi(num, num);
			d2DContext.BeginDraw();
			Vector2 position = new Vector2((float)updateOffset.X / scale, (float)updateOffset.Y / scale);
			d2DContext.Transform = Matrix3x2.Identity;
			d2DContext.PushAxisAlignedClip(new RawRectF(position.X, position.Y, position.X + (float)width / scale, position.Y + (float)height / scale), AntialiasMode.Aliased);
			d2DContext.Clear(new Color4(0f, 0f, 0f, 0f));
			d2DContext.Transform = Matrix3x2.CreateTranslation(position);
			draw(d2DContext, _gpu.D2DFactory);
			d2DContext.Transform = Matrix3x2.Identity;
			d2DContext.PopAxisAlignedClip();
			d2DContext.EndDraw().CheckError();
			d2DContext.Target = null;
		}
		layer.Surface.EndDraw();
		_gpu.Invalidate();
	}

	private void Upload(IDCompositionSurface surface, int[] pixels, int stride, Int32Rect rect)
	{
		if (rect.Width > 0 && rect.Height > 0)
		{
			Int2 updateOffset;
			using (ID3D11Texture2D resource = surface.BeginDraw<ID3D11Texture2D>(new RawRect(rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height), out updateOffset))
			{
				Box value = new Box(updateOffset.X, updateOffset.Y, 0, updateOffset.X + rect.Width, updateOffset.Y + rect.Height, 1);
				ReadOnlySpan<int> data = pixels.AsSpan(rect.Y * stride + rect.X, (rect.Height - 1) * stride + rect.Width);
				_gpu.Context.UpdateSubresource(data, resource, 0u, (uint)(stride * 4), 0u, value);
			}
			surface.EndDraw();
			_gpu.Invalidate();
		}
	}

	public void Dispose()
	{
		_target.SetRoot(null);
		_gpu.Invalidate();
		_ink.Dispose();
		_frame.Dispose();
		_art.Dispose();
		_backdrop.Dispose();
		_root.Dispose();
		_target.Dispose();
	}
}
