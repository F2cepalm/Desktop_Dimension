using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace InfiniteSpace.Native;

internal static class AppWindows
{
	internal readonly record struct Identity(string App, string WindowClass, string Title);

	private static readonly HashSet<string> ShellClasses = new HashSet<string> { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd" };

	private const double MinSide = 80.0;

	public static bool TryGetFrame(nint hwnd, out Rect frame)
	{
		frame = Rect.Empty;
		if (!Win32.IsWindow(hwnd) || Win32.IsIconic(hwnd) || !Win32.GetWindowRect(hwnd, out var rect))
		{
			return false;
		}
		double scale = Dpi.Scale;
		frame = new Rect((double)rect.Left / scale, (double)rect.Top / scale, (double)Math.Max(0, rect.Right - rect.Left) / scale, (double)Math.Max(0, rect.Bottom - rect.Top) / scale);
		if (frame.Width >= 1.0)
		{
			return frame.Height >= 1.0;
		}
		return false;
	}

	public static Identity? Describe(nint hwnd)
	{
		if (!Win32.IsWindow(hwnd))
		{
			return null;
		}
		string text = ProgramOf(hwnd);
		if (text.Length != 0)
		{
			return new Identity(text, ClassOf(hwnd), TitleOf(hwnd));
		}
		return null;
	}

	public static string TitleOf(nint hwnd)
	{
		int windowTextLength = Win32.GetWindowTextLength(hwnd);
		if (windowTextLength <= 0)
		{
			return "";
		}
		StringBuilder stringBuilder = new StringBuilder(windowTextLength + 1);
		Win32.GetWindowText(hwnd, stringBuilder, stringBuilder.Capacity);
		return stringBuilder.ToString();
	}

	public static string ClassOf(nint hwnd)
	{
		StringBuilder stringBuilder = new StringBuilder(256);
		Win32.GetClassName(hwnd, stringBuilder, stringBuilder.Capacity);
		return stringBuilder.ToString();
	}

	public static bool IsCloaked(nint hwnd)
	{
		if (Win32.DwmGetWindowAttribute(hwnd, 14, out var value, 4) == 0)
		{
			return value != 0;
		}
		return false;
	}

	public static bool IsShell(nint hwnd)
	{
		return ShellClasses.Contains(ClassOf(hwnd));
	}

	public static List<nint> OpenWindows()
	{
		List<nint> windows = new List<nint>();
		Win32.EnumWindows((nint hwnd, nint _) =>
		{
			if (IsCandidate(hwnd))
			{
				windows.Add(hwnd);
			}
			return true;
		}, IntPtr.Zero);
		return windows;
	}

	public static bool IsCandidate(nint hwnd)
	{
		if (!Win32.IsWindowVisible(hwnd) || Win32.IsIconic(hwnd) || Foreground.IsOwn(hwnd))
		{
			return false;
		}
		if (Win32.GetAncestor(hwnd, 2u) != hwnd)
		{
			return false;
		}
		if ((Win32.GetWindowLong(hwnd, -20) & 0x80) != 0L)
		{
			return false;
		}
		if (IsCloaked(hwnd) || IsShell(hwnd))
		{
			return false;
		}
		if (TryGetFrame(hwnd, out var frame) && frame.Width >= 80.0)
		{
			return frame.Height >= 80.0;
		}
		return false;
	}

	public static nint WindowAt(Point screen)
	{
		nint found = IntPtr.Zero;
		Win32.EnumWindows((nint hwnd, nint _) =>
		{
			if (!Win32.IsWindowVisible(hwnd) || Win32.IsIconic(hwnd) || Foreground.IsOwn(hwnd) || IsCloaked(hwnd))
			{
				return true;
			}
			if (!TryGetFrame(hwnd, out var frame) || !frame.Contains(screen))
			{
				return true;
			}
			if ((Win32.GetWindowLong(hwnd, -20) & 0x20) != 0L)
			{
				return true;
			}
			found = (IsShell(hwnd) ? IntPtr.Zero : hwnd);
			return false;
		}, IntPtr.Zero);
		return found;
	}

	private static string ProgramOf(nint hwnd)
	{
		Win32.GetWindowThreadProcessId(hwnd, out var processId);
		nint num = Win32.OpenProcess(4096u, inherit: false, processId);
		if (num == IntPtr.Zero)
		{
			return "";
		}
		try
		{
			StringBuilder stringBuilder = new StringBuilder(1024);
			uint size = (uint)stringBuilder.Capacity;
			return Win32.QueryFullProcessImageName(num, 0u, stringBuilder, ref size) ? stringBuilder.ToString(0, (int)size) : "";
		}
		finally
		{
			Win32.CloseHandle(num);
		}
	}
}
