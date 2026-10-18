using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using SharpGen.Runtime;
using Vortice;
using Vortice.DCommon;
using Vortice.DXGI;
using Vortice.Direct2D1;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DirectComposition;
using Vortice.Mathematics;

namespace InfiniteSpace.Rendering;

internal sealed class GpuCompositor : IDisposable
{
	private static readonly Vortice.Direct3D.FeatureLevel[] FeatureLevels = new Vortice.Direct3D.FeatureLevel[4]
	{
		Vortice.Direct3D.FeatureLevel.Level_11_1,
		Vortice.Direct3D.FeatureLevel.Level_11_0,
		Vortice.Direct3D.FeatureLevel.Level_10_1,
		Vortice.Direct3D.FeatureLevel.Level_10_0
	};

	private IDXGIDevice _dxgi;

	private ID2D1Factory1? _d2dFactory;

	private ID2D1Device? _d2dDevice;

	private ID2D1DeviceContext? _d2dContext;

	private bool _changed;

	private bool _lost;

	private bool _disposed;

	private int _checks;

	public ID3D11Device Device { get; private set; }

	public ID3D11DeviceContext Context { get; private set; }

	public IDCompositionDevice Composition { get; private set; }

	public bool IsSoftware { get; private set; }

	public int Generation { get; private set; }

	public bool IsLost
	{
		get
		{
			if (_lost || Device.DeviceRemovedReason.Failure)
			{
				return true;
			}
			if (++_checks % 64 != 0)
			{
				return false;
			}
			Composition.CheckDeviceState(out var pfValid);
			return !pfValid;
		}
	}

	public ID2D1Factory1 D2DFactory => _d2dFactory ?? (_d2dFactory = D2D1.D2D1CreateFactory<ID2D1Factory1>());

	public ID2D1DeviceContext D2DContext
	{
		get
		{
			if ((object)_d2dContext == null)
			{
				_d2dDevice = D2DFactory.CreateDevice(_dxgi);
				_d2dContext = _d2dDevice.CreateDeviceContext(DeviceContextOptions.None);
			}
			return _d2dContext;
		}
	}

	public static PixelFormat Bgra { get; } = new PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied);

	public TimeSpan CommitTime { get; private set; }

	public GpuCompositor()
	{
		CreateDevices();
	}

	public void MarkLost()
	{
		_lost = true;
	}

	public static bool IsDeviceLoss(int hresult)
	{
		if ((uint)(hresult - -2005270523) <= 2u || hresult == -2005270496 || hresult == -2003238900)
		{
			return true;
		}
		return false;
	}

	public void Recreate()
	{
		ReleaseDevices();
		CreateDevices();
		_lost = false;
		_changed = true;
		Generation++;
	}

	private void CreateDevices()
	{
		(ID3D11Device, bool) tuple = CreateDevice();
		Device = tuple.Item1;
		IsSoftware = tuple.Item2;
		Context = Device.ImmediateContext;
		_dxgi = Device.QueryInterface<IDXGIDevice>();
		Composition = DComp.DCompositionCreateDevice<IDCompositionDevice>(_dxgi);
	}

	private void ReleaseDevices()
	{
		_d2dContext?.Dispose();
		_d2dContext = null;
		_d2dDevice?.Dispose();
		_d2dDevice = null;
		Composition.Dispose();
		_dxgi.Dispose();
		Context.Dispose();
		Device.Dispose();
	}

	public ID2D1Bitmap1 Paint(int width, int height, float scale, Action<ID2D1DeviceContext, ID2D1Factory1> draw)
	{
		ID2D1DeviceContext d2DContext = D2DContext;
		float num = 96f * scale;
		ID2D1Bitmap1 result = (ID2D1Bitmap1)(d2DContext.Target = d2DContext.CreateBitmap(new SizeI(Math.Max(1, width), Math.Max(1, height)), IntPtr.Zero, 0u, new BitmapProperties1(Bgra, num, num, BitmapOptions.Target)));
		d2DContext.SetDpi(num, num);
		d2DContext.BeginDraw();
		d2DContext.Transform = Matrix3x2.Identity;
		d2DContext.Clear(new Color4(0f, 0f, 0f, 0f));
		draw(d2DContext, D2DFactory);
		d2DContext.Transform = Matrix3x2.Identity;
		d2DContext.EndDraw().CheckError();
		d2DContext.Target = null;
		return result;
	}

	public void Repaint(ref ID2D1Bitmap1? bitmap, int width, int height, float scale, Action<ID2D1DeviceContext, ID2D1Factory1> draw)
	{
		width = Math.Max(1, width);
		height = Math.Max(1, height);
		float num = 96f * scale;
		ID2D1DeviceContext d2DContext = D2DContext;
		SizeI sizeI = bitmap?.PixelSize ?? default(SizeI);
		bool num2 = (object)bitmap != null && sizeI.Width >= width && sizeI.Height >= height && bitmap.Dpi.Width == num;
		bool flag = (long)sizeI.Width * (long)sizeI.Height > 4 * Math.Max(65536L, (long)width * (long)height);
		if (!num2 | flag)
		{
			bitmap?.Dispose();
			bitmap = d2DContext.CreateBitmap(new SizeI(Headroom.Size(width, sizeI.Width), Headroom.Size(height, sizeI.Height)), IntPtr.Zero, 0u, new BitmapProperties1(Bgra, num, num, BitmapOptions.Target));
		}
		d2DContext.Target = bitmap;
		d2DContext.SetDpi(num, num);
		d2DContext.BeginDraw();
		d2DContext.Transform = Matrix3x2.Identity;
		d2DContext.Clear(new Color4(0f, 0f, 0f, 0f));
		d2DContext.PushAxisAlignedClip(new RawRectF(0f, 0f, (float)width / scale, (float)height / scale), AntialiasMode.Aliased);
		draw(d2DContext, D2DFactory);
		d2DContext.Transform = Matrix3x2.Identity;
		d2DContext.PopAxisAlignedClip();
		d2DContext.EndDraw().CheckError();
		d2DContext.Target = null;
	}

	public int[] ReadPixels(ID2D1Bitmap1 source)
	{
		SizeI pixelSize = source.PixelSize;
		using ID2D1Bitmap1 iD2D1Bitmap = D2DContext.CreateBitmap(pixelSize, IntPtr.Zero, 0u, new BitmapProperties1(Bgra, 96f, 96f, BitmapOptions.CannotDraw | BitmapOptions.CpuRead));
		iD2D1Bitmap.CopyFromBitmap(source).CheckError();
		MappedRectangle mappedRectangle = iD2D1Bitmap.Map(MapOptions.Read);
		int[] array = new int[pixelSize.Width * pixelSize.Height];
		try
		{
			for (int i = 0; i < pixelSize.Height; i++)
			{
				Marshal.Copy(mappedRectangle.Bits + i * (int)mappedRectangle.Pitch, array, i * pixelSize.Width, pixelSize.Width);
			}
		}
		finally
		{
			iD2D1Bitmap.Unmap();
		}
		return array;
	}

	public void Invalidate()
	{
		_changed = true;
	}

	public void Commit()
	{
		if (_changed)
		{
			_changed = false;
			long timestamp = Stopwatch.GetTimestamp();
			Result result = Composition.Commit();
			CommitTime += Stopwatch.GetElapsedTime(timestamp);
			if (result.Failure)
			{
				_lost = true;
			}
		}
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
			ReleaseDevices();
			_d2dFactory?.Dispose();
		}
	}

	private static (ID3D11Device Device, bool Software) CreateDevice()
	{
		DriverType[] array = new DriverType[2]
		{
			DriverType.Hardware,
			DriverType.Warp
		};
		foreach (DriverType driverType in array)
		{
			if (D3D11.D3D11CreateDevice(IntPtr.Zero, driverType, DeviceCreationFlags.BgraSupport, FeatureLevels, out ID3D11Device device).Success && (object)device != null)
			{
				return (Device: device, Software: driverType == DriverType.Warp || IsBasicRenderDriver(device));
			}
		}
		throw new InvalidOperationException("Could not create a Direct3D 11 device.");
	}

	private static bool IsBasicRenderDriver(ID3D11Device device)
	{
		using IDXGIDevice iDXGIDevice = device.QueryInterface<IDXGIDevice>();
		using IDXGIAdapter iDXGIAdapter = iDXGIDevice.GetAdapter();
		AdapterDescription description = iDXGIAdapter.Description;
		return description.VendorId == 5140 && description.DeviceId == 140;
	}
}
