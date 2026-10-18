using System;
using System.Runtime.InteropServices;
using System.Text;

namespace InfiniteSpace.Native;

internal static class Win32
{
	public struct WINDOWPOS
	{
		public nint hwnd;

		public nint hwndInsertAfter;

		public int x;

		public int y;

		public int cx;

		public int cy;

		public uint flags;
	}

	public struct POINT
	{
		public int X;

		public int Y;
	}

	public delegate void WinEventProc(nint hook, uint eventType, nint hwnd, int idObject, int idChild, uint eventThread, uint eventTime);

	private delegate nint SubclassProc(nint hWnd, int msg, nint wParam, nint lParam, nuint id, nint data);

	public struct SIZE
	{
		public int Width;

		public int Height;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct BLENDFUNCTION
	{
		public byte BlendOp;

		public byte BlendFlags;

		public byte SourceConstantAlpha;

		public byte AlphaFormat;
	}

	public struct BITMAPINFOHEADER
	{
		public int biSize;

		public int biWidth;

		public int biHeight;

		public short biPlanes;

		public short biBitCount;

		public int biCompression;

		public int biSizeImage;

		public int biXPelsPerMeter;

		public int biYPelsPerMeter;

		public int biClrUsed;

		public int biClrImportant;
	}

	public struct RECT
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	public delegate bool EnumWindowsProc(nint hwnd, nint lParam);

	public const int WM_WINDOWPOSCHANGING = 70;

	public const int WM_MOUSEACTIVATE = 33;

	public const int WM_HOTKEY = 786;

	public const int MA_NOACTIVATE = 3;

	public const int GWL_EXSTYLE = -20;

	public const long WS_EX_TOOLWINDOW = 128L;

	public const long WS_EX_APPWINDOW = 262144L;

	public const long WS_EX_NOACTIVATE = 134217728L;

	public const uint SWP_NOSIZE = 1u;

	public const uint SWP_NOMOVE = 2u;

	public const uint SWP_NOZORDER = 4u;

	public const uint SWP_NOACTIVATE = 16u;

	public const uint MOD_ALT = 1u;

	public const uint MOD_CONTROL = 2u;

	public const uint MOD_SHIFT = 4u;

	public const uint MOD_WIN = 8u;

	public const uint MOD_NOREPEAT = 16384u;

	public static readonly nint HWND_BOTTOM = new IntPtr(1);

	public static readonly nint HWND_MESSAGE = new IntPtr(-3);

	public const int WM_SETCURSOR = 32;

	public const int WM_MOUSEMOVE = 512;

	public const int WM_LBUTTONDOWN = 513;

	public const int WM_LBUTTONUP = 514;

	public const int WM_RBUTTONDOWN = 516;

	public const int WM_RBUTTONUP = 517;

	public const int WM_CAPTURECHANGED = 533;

	public const int IDC_CROSS = 32515;

	public const int SW_HIDE = 0;

	public const long WS_EX_TOPMOST = 8L;

	public const long WS_EX_TRANSPARENT = 32L;

	public const long WS_EX_NOREDIRECTIONBITMAP = 2097152L;

	public static readonly nint HWND_TOPMOST = new IntPtr(-1);

	public const int WINDING = 2;

	public const uint EVENT_SYSTEM_FOREGROUND = 3u;

	public const uint EVENT_SYSTEM_MINIMIZESTART = 22u;

	public const uint EVENT_SYSTEM_MINIMIZEEND = 23u;

	public const uint EVENT_OBJECT_DESTROY = 32769u;

	public const uint EVENT_OBJECT_HIDE = 32771u;

	public const uint WINEVENT_OUTOFCONTEXT = 0u;

	public const uint WINEVENT_SKIPOWNPROCESS = 2u;

	public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;

	public const int DWMWCP_DONOTROUND = 1;

	public const uint GW_HWNDPREV = 3u;

	public static readonly nint HWND_TOP = IntPtr.Zero;

	public const int VK_LBUTTON = 1;

	public const int VK_RBUTTON = 2;

	public const int SW_SHOW = 5;

	public const int IDC_SIZENWSE = 32642;

	public const int IDC_SIZENESW = 32643;

	public const int IDC_SIZEWE = 32644;

	public const int IDC_SIZENS = 32645;

	public const int IDC_SIZEALL = 32646;

	public const int IDC_HAND = 32649;

	public const uint LWA_ALPHA = 2u;

	private const int WM_STYLECHANGING = 124;

	private const int WM_NCDESTROY = 130;

	private static readonly SubclassProc KeepLayeredProc = KeepLayered;

	public const uint MONITOR_DEFAULTTOPRIMARY = 1u;

	public const uint WS_POPUP = 2147483648u;

	public const long WS_EX_LAYERED = 524288L;

	public const int SW_SHOWNOACTIVATE = 4;

	public const uint ULW_ALPHA = 2u;

	public const byte AC_SRC_OVER = 0;

	public const byte AC_SRC_ALPHA = 1;

	public const int IDC_ARROW = 32512;

	public const uint EVENT_OBJECT_SHOW = 32770u;

	public const int RGN_COPY = 5;

	public const uint SWP_NOREDRAW = 8u;

	public const uint SWP_NOCOPYBITS = 256u;

	public const uint SWP_DEFERERASE = 8192u;

	public static readonly nint HWND_NOTOPMOST = new IntPtr(-2);

	public const uint GA_ROOT = 2u;

	public const int DWMWA_CLOAKED = 14;

	public const uint PROCESS_QUERY_LIMITED_INFORMATION = 4096u;

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool UnregisterHotKey(nint hWnd, int id);

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool GetCursorPos(out POINT point);

	[DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
	private static extern nint GetWindowLongPtr64(nint hWnd, int index);

	[DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
	private static extern int GetWindowLong32(nint hWnd, int index);

	[DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
	private static extern nint SetWindowLongPtr64(nint hWnd, int index, nint value);

	[DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
	private static extern int SetWindowLong32(nint hWnd, int index, int value);

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool DestroyIcon(nint hIcon);

	[DllImport("gdi32.dll", SetLastError = true)]
	public static extern nint CreatePolyPolygonRgn(POINT[] points, int[] counts, int polygonCount, int fillMode);

	[DllImport("user32.dll", SetLastError = true)]
	public static extern int SetWindowRgn(nint hWnd, nint hRgn, [MarshalAs(UnmanagedType.Bool)] bool redraw);

	[DllImport("user32.dll")]
	public static extern nint SetWinEventHook(uint eventMin, uint eventMax, nint module, WinEventProc callback, uint idProcess, uint idThread, uint flags);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool UnhookWinEvent(nint hook);

	[DllImport("dwmapi.dll")]
	public static extern int DwmSetWindowAttribute(nint hWnd, int attribute, ref int value, int size);

	[DllImport("dwmapi.dll")]
	public static extern int DwmFlush();

	[DllImport("user32.dll")]
	public static extern nint GetForegroundWindow();

	[DllImport("user32.dll")]
	public static extern short GetAsyncKeyState(int vKey);

	public static bool IsMouseButtonDown()
	{
		if ((GetAsyncKeyState(VK_LBUTTON) & 0x8000) == 0)
		{
			return (GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0;
		}
		return true;
	}

	[DllImport("dcomp.dll", ExactSpelling = true)]
	public static extern uint DCompositionWaitForCompositorClock(uint count, nint handles, uint timeoutInMs);

	[DllImport("user32.dll")]
	public static extern nint GetCapture();

	[DllImport("user32.dll")]
	public static extern short GetKeyState(int virtualKey);

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool SetLayeredWindowAttributes(nint hWnd, uint colorKey, byte alpha, uint flags);

	[DllImport("comctl32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool SetWindowSubclass(nint hWnd, SubclassProc proc, nuint id, nint data);

	[DllImport("comctl32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool RemoveWindowSubclass(nint hWnd, SubclassProc proc, nuint id);

	[DllImport("comctl32.dll")]
	private static extern nint DefSubclassProc(nint hWnd, int msg, nint wParam, nint lParam);

	private static nint KeepLayered(nint hWnd, int msg, nint wParam, nint lParam, nuint id, nint keep)
	{
		if (msg == 130)
		{
			RemoveWindowSubclass(hWnd, KeepLayeredProc, id);
		}
		nint result = DefSubclassProc(hWnd, msg, wParam, lParam);
		if (msg == 124 && ((IntPtr)wParam).ToInt64() == -20 && keep != IntPtr.Zero)
		{
			int num = Marshal.ReadInt32(lParam, 4);
			Marshal.WriteInt32(lParam, 4, num | 0x80000);
		}
		return result;
	}

	public static void SetClickThrough(nint hWnd, bool clickThrough)
	{
		SetWindowSubclass(hWnd, KeepLayeredProc, 1u, clickThrough ? 1 : 0);
		long windowLong = GetWindowLong(hWnd, -20);
		long num = (clickThrough ? (windowLong | 0x80000 | 0x20) : (windowLong & -524321));
		if (num != windowLong)
		{
			SetWindowLong(hWnd, -20, num);
			if (clickThrough)
			{
				SetLayeredWindowAttributes(hWnd, 0u, byte.MaxValue, 2u);
			}
		}
	}

	[DllImport("user32.dll")]
	public static extern nint MonitorFromPoint(POINT point, uint flags);

	[DllImport("user32.dll")]
	public static extern nint WindowFromPoint(POINT point);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool SetForegroundWindow(nint hWnd);

	[DllImport("user32.dll")]
	public static extern nint GetWindow(nint hWnd, uint cmd);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool IsWindow(nint hWnd);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool IsWindowVisible(nint hWnd);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool IsIconic(nint hWnd);

	[DllImport("user32.dll")]
	public static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	public static extern int GetClassName(nint hWnd, StringBuilder className, int maxCount);

	[DllImport("user32.dll")]
	public static extern nint SetCapture(nint hWnd);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool ReleaseCapture();

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool UpdateLayeredWindow(nint hWnd, nint hdcDst, ref POINT pptDst, ref SIZE psize, nint hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, uint dwFlags);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool ShowWindow(nint hWnd, int nCmdShow);

	[DllImport("user32.dll")]
	public static extern nint LoadCursor(nint hInstance, nint cursorName);

	[DllImport("user32.dll")]
	public static extern nint SetCursor(nint hCursor);

	[DllImport("user32.dll")]
	public static extern uint GetDpiForSystem();

	[DllImport("gdi32.dll", SetLastError = true)]
	public static extern nint CreateCompatibleDC(nint hdc);

	[DllImport("gdi32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool DeleteDC(nint hdc);

	[DllImport("gdi32.dll")]
	public static extern nint SelectObject(nint hdc, nint hObject);

	[DllImport("gdi32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool DeleteObject(nint hObject);

	[DllImport("gdi32.dll", SetLastError = true)]
	public static extern nint CreateDIBSection(nint hdc, ref BITMAPINFOHEADER bitmapInfo, uint usage, out nint bits, nint section, uint offset);

	[DllImport("gdi32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool GdiFlush();

	public static long GetWindowLong(nint hWnd, int index)
	{
		if (IntPtr.Size != 8)
		{
			return GetWindowLong32(hWnd, index);
		}
		return ((IntPtr)GetWindowLongPtr64(hWnd, index)).ToInt64();
	}

	public static void SetWindowLong(nint hWnd, int index, long value)
	{
		if (IntPtr.Size == 8)
		{
			SetWindowLongPtr64(hWnd, index, new IntPtr(value));
		}
		else
		{
			SetWindowLong32(hWnd, index, (int)value);
		}
	}

	[DllImport("gdi32.dll")]
	public static extern nint CreateRectRgn(int left, int top, int right, int bottom);

	[DllImport("gdi32.dll")]
	public static extern int CombineRgn(nint destination, nint source1, nint source2, int mode);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool EnumWindows(EnumWindowsProc callback, nint lParam);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool GetWindowRect(nint hWnd, out RECT rect);

	[DllImport("user32.dll")]
	public static extern nint GetAncestor(nint hWnd, uint flags);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	public static extern int GetWindowTextLength(nint hWnd);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	public static extern int GetWindowText(nint hWnd, StringBuilder text, int maxCount);

	[DllImport("dwmapi.dll")]
	public static extern int DwmGetWindowAttribute(nint hWnd, int attribute, out int value, int size);

	[DllImport("kernel32.dll", SetLastError = true)]
	public static extern nint OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool QueryFullProcessImageName(nint process, uint flags, StringBuilder name, ref uint size);

	[DllImport("kernel32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool CloseHandle(nint handle);
}
