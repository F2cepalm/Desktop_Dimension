using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using InfiniteSpace.Native;
using InfiniteSpace.Rendering;
using Vortice.DirectComposition;
using Vortice.Mathematics;

namespace InfiniteSpace.Shell;

internal abstract class GpuOverlay : IFramePart
{
	private const int CS_DBLCLKS = 8;

	private static readonly Color4 HintBacking = new Color4(0f, 0f, 0f, 0.8f);

	private readonly HwndSource _source;

	private readonly IDCompositionTarget _target;

	private readonly OverlaySprite _hint;

	private OverlayPicture? _hintPicture;

	private string _hintText = "";

	private nint _cursor = Win32.LoadCursor(IntPtr.Zero, 32512);

	private bool _shown;

	private bool _closed;

	private const int HTCLIENT = 1;

	private const int WM_ACTIVATE = 6;

	private const int WA_INACTIVE = 0;

	private const int WM_CLOSE = 16;

	private const int WM_KEYDOWN = 256;

	private const int WM_SYSKEYDOWN = 260;

	private const int WM_SYSCHAR = 262;

	private const int WM_LBUTTONDBLCLK = 515;

	private const int WM_MOUSEWHEEL = 522;

	private const int VK_SHIFT = 16;

	private const int VK_CONTROL = 17;

	private const int VK_MENU = 18;

	public nint Handle => _source.Handle;

	protected System.Windows.Vector Origin { get; }

	protected System.Windows.Rect Area { get; }

	protected Universe Universe { get; }

	protected GpuCompositor Gpu { get; }

	protected IDCompositionVisual Root { get; }

	protected IDCompositionVisual Layers { get; }

	protected int PixelWidth { get; }

	protected int PixelHeight { get; }

	protected bool IsClosed => _closed;

	protected string Hint
	{
		get
		{
			return _hintText;
		}
		set
		{
			if (!(value == _hintText) || _hintPicture == null)
			{
				_hintText = value;
				OverlayPicture? hintPicture = _hintPicture;
				_hintPicture = OverlayText.Paint(Gpu, value, 14f, HintBacking, new Vector2(16f, 8f), 8f);
				_hint.Picture = _hintPicture;
				_hint.Shown = true;
				_hint.MoveTo(new Point(SystemParameters.PrimaryScreenWidth / 2.0 - _hintPicture.Size.Width / 2.0 - Origin.X, 28.0 - Origin.Y));
				hintPicture?.Dispose();
			}
		}
	}

	protected nint Cursor
	{
		get
		{
			return _cursor;
		}
		set
		{
			if (_cursor != value)
			{
				_cursor = value;
				if (Win32.GetCursorPos(out var point) && Win32.WindowFromPoint(point) == Handle)
				{
					Win32.SetCursor(value);
				}
			}
		}
	}

	public virtual bool WantsFrame => false;

	protected static ModifierKeys Modifiers => (ModifierKeys)((Down(16) ? 4 : 0) | (Down(17) ? 2 : 0) | (Down(18) ? 1 : 0));

	public event EventHandler? Closed;

	protected GpuOverlay(Universe universe, string hint, System.Windows.Rect? bounds = null)
	{
		Universe = universe;
		Gpu = universe.Gpu;
		System.Windows.Rect rect = (Area = bounds ?? new System.Windows.Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight));
		double scale = Dpi.Scale;
		int num = (int)Math.Floor(rect.Left * scale);
		int num2 = (int)Math.Floor(rect.Top * scale);
		PixelWidth = Math.Max(1, (int)Math.Ceiling(rect.Right * scale) - num);
		PixelHeight = Math.Max(1, (int)Math.Ceiling(rect.Bottom * scale) - num2);
		Origin = new System.Windows.Vector((double)num / scale, (double)num2 / scale);
		_source = new HwndSource(new HwndSourceParameters("Infinite Space")
		{
			WindowStyle = int.MinValue,
			ExtendedWindowStyle = 2097288,
			WindowClassStyle = 8,
			PositionX = num,
			PositionY = num2,
			Width = PixelWidth,
			Height = PixelHeight
		});
		_source.AddHook(WndProc);
		Win32.SetWindowPos(Handle, Win32.HWND_TOPMOST, num, num2, PixelWidth, PixelHeight, 16u);
		Gpu.Composition.CreateTargetForHwnd(Handle, true, out _target).CheckError();
		Root = Gpu.Composition.CreateVisual();
		_target.SetRoot(Root);
		Layers = Gpu.Composition.CreateVisual();
		Root.AddVisual(Layers, false, null);
		_hint = new OverlaySprite(Gpu, Root);
		Hint = hint;
	}

	protected System.Windows.Rect Contained(System.Windows.Rect before, System.Windows.Rect after, bool onSurface = false)
	{
		System.Windows.Rect area = (onSurface ? new System.Windows.Rect(Area.X - Origin.X, Area.Y - Origin.Y, Area.Width, Area.Height) : Area);
		area.Inflate(40.0, 40.0);
		return BlockEdit.Contain(before, after, area);
	}

	public void Show()
	{
		Show(activate: true);
	}

	internal void Show(bool activate)
	{
		if (!_closed && !_shown)
		{
			_shown = true;
			Universe.Attach(this);
			Gpu.Commit();
			Win32.ShowWindow(Handle, activate ? 5 : 4);
			Win32.SetWindowPos(Handle, Win32.HWND_TOPMOST, 0, 0, 0, 0, (uint)(3 | ((!activate) ? 16 : 0)));
			if (activate)
			{
				Win32.SetForegroundWindow(Handle);
				SetFocus(Handle);
			}
			OnShown();
			Gpu.Commit();
		}
	}

	public void Dismiss()
	{
		if (_closed)
		{
			return;
		}
		_closed = true;
		if (Win32.GetCapture() == Handle)
		{
			Win32.ReleaseCapture();
		}
		try
		{
			OnClosing();
		}
		finally
		{
			Universe.Detach(this);
			_target.SetRoot(null);
			Gpu.Invalidate();
			Gpu.Commit();
			DisposeParts();
			_hint.Dispose();
			_hintPicture?.Dispose();
			Layers.Dispose();
			Root.Dispose();
			_target.Dispose();
			_source.RemoveHook(WndProc);
			_source.Dispose();
			Closed?.Invoke(this, EventArgs.Empty);
		}
	}

	public virtual void RenderFrame(FrameState frame)
	{
	}

	public void DeviceLost()
	{
		Later(Dismiss);
	}

	protected virtual void OnShown()
	{
	}

	protected virtual void OnClosing()
	{
	}

	protected virtual void DisposeParts()
	{
	}

	protected virtual void OnMouseMove(Point at)
	{
	}

	protected virtual void OnMouseDown(Point at, int clickCount)
	{
	}

	protected virtual void OnMouseUp(Point at)
	{
	}

	protected virtual void OnWheel(int delta)
	{
	}

	protected virtual void OnCaptureLost()
	{
	}

	protected virtual bool OnKeyDown(Key key, ModifierKeys modifiers)
	{
		if (key != Key.Escape)
		{
			return false;
		}
		Dismiss();
		return true;
	}

	protected void CaptureMouse()
	{
		Win32.SetCapture(Handle);
	}

	protected void ReleaseMouseCapture()
	{
		if (Win32.GetCapture() == Handle)
		{
			Win32.ReleaseCapture();
		}
	}

	protected Point CursorOnSurface()
	{
		System.Windows.Size size = new System.Windows.Size((double)PixelWidth / Dpi.Scale, (double)PixelHeight / Dpi.Scale);
		if (Win32.GetCursorPos(out var point))
		{
			Point point2 = new Point((double)point.X / Dpi.Scale - Origin.X, (double)point.Y / Dpi.Scale - Origin.Y);
			if (new System.Windows.Rect(size).Contains(point2))
			{
				return point2;
			}
		}
		return new Point(size.Width / 2.0, size.Height / 2.0);
	}

	private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
	{
		if (_closed)
		{
			return IntPtr.Zero;
		}
		switch (msg)
		{
		case 32:
			if ((((IntPtr)lParam).ToInt64() & 0xFFFF) == 1)
			{
				Win32.SetCursor(_cursor);
				handled = true;
				return new IntPtr(1);
			}
			break;
		case 512:
			OnMouseMove(At(lParam));
			handled = true;
			break;
		case 513:
		case 515:
			OnMouseDown(At(lParam), (msg != 515) ? 1 : 2);
			handled = true;
			break;
		case 514:
			OnMouseUp(At(lParam));
			handled = true;
			break;
		case 516:
			handled = true;
			break;
		case 517:
			handled = true;
			Later(Dismiss);
			break;
		case 522:
			OnWheel((short)((((IntPtr)wParam).ToInt64() >> 16) & 0xFFFF));
			handled = true;
			break;
		case 533:
			if (lParam != Handle)
			{
				OnCaptureLost();
			}
			break;
		case 256:
		case 260:
		{
			Key key = KeyInterop.KeyFromVirtualKey((int)((IntPtr)wParam).ToInt64());
			if ((uint)(key - 116) <= 5u)
			{
				handled = msg == 260;
			}
			else
			{
				handled = OnKeyDown(key, Modifiers) || msg == 260;
			}
			break;
		}
		case 262:
			handled = true;
			break;
		case 6:
			if ((((IntPtr)wParam).ToInt64() & 0xFFFF) == 0L)
			{
				Later(Dismiss);
			}
			break;
		case 16:
			handled = true;
			Later(Dismiss);
			break;
		}
		return IntPtr.Zero;
	}

	private static Point At(nint lParam)
	{
		long num = ((IntPtr)lParam).ToInt64();
		int num2 = (short)(num & 0xFFFF);
		int num3 = (short)((num >> 16) & 0xFFFF);
		return new Point((double)num2 / Dpi.Scale, (double)num3 / Dpi.Scale);
	}

	private void Later(Action action)
	{
		Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Normal, action);
	}

	private static bool Down(int key)
	{
		return (Win32.GetKeyState(key) & 0x8000) != 0;
	}

	[DllImport("user32.dll")]
	private static extern nint SetFocus(nint window);
}
