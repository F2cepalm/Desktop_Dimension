using System;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using InfiniteSpace.Native;
using InfiniteSpace.Rendering;
using Vortice;
using Vortice.DXGI;
using Vortice.Direct2D1;
using Vortice.DirectComposition;
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace InfiniteSpace.Shell;

internal sealed class FrameStatsWindow : IDisposable
{
	private const double Width = 260.0;

	private const double Height = 92.0;

	private const double Top = 8.4;

	private readonly Universe _universe;

	private readonly HwndSource _source;

	private IDCompositionTarget _target;

	private IDCompositionVisual _visual;

	private IDCompositionSurface _surface;

	private int _generation;

	private readonly DispatcherTimer _timer;

	private readonly IDWriteTextFormat _format;

	private readonly double[] _times = new double[240];

	private readonly bool[] _interactive = new bool[240];

	private readonly double[] _intervals = new double[240];

	private readonly int _pixelWidth;

	private readonly int _pixelHeight;

	private long _shown = -1L;

	public nint Handle => _source.Handle;

	public FrameStatsWindow(Universe universe, Point? at = null)
	{
		_universe = universe;
		_ = universe.Gpu;
		double scale = Dpi.Scale;
		_pixelWidth = (int)Math.Ceiling(260.0 * scale);
		_pixelHeight = (int)Math.Ceiling(92.0 * scale);
		System.Windows.Rect workArea = SystemParameters.WorkArea;
		_source = new HwndSource(new HwndSourceParameters("Infinite Space frame stats")
		{
			WindowStyle = int.MinValue,
			ExtendedWindowStyle = 136315016,
			PositionX = (int)Math.Round((at?.X ?? (workArea.Right - 260.0 - 12.0)) * scale),
			PositionY = (int)Math.Round((at?.Y ?? (workArea.Bottom - 92.0 - 12.0)) * scale),
			Width = _pixelWidth,
			Height = _pixelHeight
		});
		Build();
		_format = NoteText.Factory.CreateTextFormat(NoteText.Typeface.FontFamily.Source, null, Vortice.DirectWrite.FontWeight.Normal, Vortice.DirectWrite.FontStyle.Normal, Vortice.DirectWrite.FontStretch.Normal, 11f, "");
		Draw();
		Win32.SetClickThrough(_source.Handle, clickThrough: true);
		Win32.ShowWindow(_source.Handle, 4);
		_timer = new DispatcherTimer(TimeSpan.FromMilliseconds(100L), DispatcherPriority.Background, (object? _, EventArgs _) =>
		{
			Draw();
		}, Dispatcher.CurrentDispatcher);
		_timer.Start();
	}

	private void Build()
	{
		GpuCompositor gpu = _universe.Gpu;
		_generation = gpu.Generation;
		gpu.Composition.CreateTargetForHwnd(_source.Handle, true, out _target).CheckError();
		_visual = gpu.Composition.CreateVisual();
		gpu.Composition.CreateSurface((uint)_pixelWidth, (uint)_pixelHeight, Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied, out _surface).CheckError();
		_visual.SetContent(_surface);
		_target.SetRoot(_visual);
	}

	private void Draw()
	{
		if (_universe.Gpu.Generation != _generation)
		{
			_surface.Dispose();
			_visual.Dispose();
			_target.Dispose();
			Build();
			_shown = -1L;
		}
		if (FrameStats.Count == _shown && _shown >= 0)
		{
			return;
		}
		_shown = FrameStats.Count;
		int count = FrameStats.Copy(_times, _interactive, _intervals);
		GpuCompositor gpu = _universe.Gpu;
		float scale = (float)Dpi.Scale;
		SurfaceDrawing.Draw(gpu, _surface, new RawRect(0, 0, _pixelWidth, _pixelHeight), scale, (ID2D1DeviceContext context) =>
		{
			using ID2D1SolidColorBrush brush = context.CreateSolidColorBrush(new Color4(0f, 0f, 0f, 0.72f));
			using ID2D1SolidColorBrush iD2D1SolidColorBrush = context.CreateSolidColorBrush(new Color4(0.55f, 0.55f, 0.6f));
			using ID2D1SolidColorBrush iD2D1SolidColorBrush2 = context.CreateSolidColorBrush(new Color4(0.37f, 0.8f, 0.89f));
			using ID2D1SolidColorBrush iD2D1SolidColorBrush3 = context.CreateSolidColorBrush(new Color4(1f, 0.35f, 0.37f));
			using ID2D1SolidColorBrush brush2 = context.CreateSolidColorBrush(new Color4(1f, 1f, 1f, 0.35f));
			using ID2D1SolidColorBrush defaultFillBrush = context.CreateSolidColorBrush(new Color4(1f, 1f, 1f, 0.9f));
			context.FillRoundedRectangle(new RoundedRectangle(new RawRectF(0f, 0f, 260f, 92f), 6f, 6f), brush);
			double[] array = new double[2] { 4.1, 1.7 };
			foreach (double ms in array)
			{
				context.FillRectangle(new RawRectF(8f, Y(ms), 252f, Y(ms) + 1f), brush2);
			}
			float num = 1.0166667f;
			for (int j = 0; j < count; j++)
			{
				float num2 = 8f + (float)(240 - count + j) * num;
				ID2D1SolidColorBrush brush3 = ((_times[j] > 1.7) ? iD2D1SolidColorBrush3 : (_interactive[j] ? iD2D1SolidColorBrush2 : iD2D1SolidColorBrush));
				context.FillRectangle(new RawRectF(num2, Y(_times[j]), num2 + Math.Max(1f / scale, num - 0.2f), 84f), brush3);
			}
			double[] array2 = _times.AsSpan(0, count).ToArray();
			Array.Sort(array2);
			double value = ((count > 0) ? array2[count / 2] : 0.0);
			double value2 = ((count > 0) ? array2[^1] : 0.0);
			double num3 = 0.0;
			for (int k = 1; k < count; k++)
			{
				num3 += _intervals[k];
			}
			double value3 = ((num3 > 0.0) ? ((double)(count - 1) / (num3 / 1000.0)) : 0.0);
			IFormatProvider invariantCulture = CultureInfo.InvariantCulture;
			DefaultInterpolatedStringHandler handler = new DefaultInterpolatedStringHandler(36, 3, invariantCulture);
			handler.AppendFormatted(value, "0.00");
			handler.AppendLiteral(" ms median · ");
			handler.AppendFormatted(value2, "0.0");
			handler.AppendLiteral(" ms slowest · ");
			handler.AppendFormatted(value3, "0");
			handler.AppendLiteral(" frames/s");
			string text = string.Create(invariantCulture, ref handler);
			using IDWriteTextLayout textLayout = NoteText.Factory.CreateTextLayout(text, _format, 244f, 20f);
			context.DrawTextLayout(new Vector2(8f, 5f), textLayout, defaultFillBrush, DrawTextOptions.None);
		});
		gpu.Commit();
		static float Y(double ms)
		{
			return 84f - (float)(Math.Min(ms, 8.4) / 8.4 * 56.0);
		}
	}

	public void Dispose()
	{
		_timer.Stop();
		_target.SetRoot(null);
		_universe.Gpu.Commit();
		_surface.Dispose();
		_visual.Dispose();
		_target.Dispose();
		_source.Dispose();
	}
}
