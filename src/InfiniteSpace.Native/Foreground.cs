using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Threading;

namespace InfiniteSpace.Native;

internal static class Foreground
{
	public static nint CaptureHost()
	{
		nint foregroundWindow = Win32.GetForegroundWindow();
		if (foregroundWindow == IntPtr.Zero)
		{
			return IntPtr.Zero;
		}
		StringBuilder stringBuilder = new StringBuilder(64);
		Win32.GetClassName(foregroundWindow, stringBuilder, stringBuilder.Capacity);
		string text = stringBuilder.ToString();
		if ((text == "Progman" || text == "WorkerW") ? true : false)
		{
			return IntPtr.Zero;
		}
		if (AppWindows.IsCandidate(foregroundWindow))
		{
			return foregroundWindow;
		}
		List<nint> list = AppWindows.OpenWindows();
		if (list.Count <= 0)
		{
			return IntPtr.Zero;
		}
		return list[0];
	}

	public static void RestoreTo(nint window)
	{
		if (window != IntPtr.Zero && Win32.IsWindow(window))
		{
			nint foregroundWindow = Win32.GetForegroundWindow();
			if (foregroundWindow == IntPtr.Zero || IsOwn(foregroundWindow))
			{
				Win32.SetForegroundWindow(window);
			}
		}
	}

	public static void WhenMouseUp(Action action)
	{
		if (!Win32.IsMouseButtonDown())
		{
			action();
			return;
		}
		DispatcherTimer wait = new DispatcherTimer(DispatcherPriority.Background)
		{
			Interval = TimeSpan.FromMilliseconds(30L)
		};
		wait.Tick += (object? _, EventArgs _) =>
		{
			if (!Win32.IsMouseButtonDown())
			{
				wait.Stop();
				action();
			}
		};
		wait.Start();
	}

	public static bool IsOwn(nint hwnd)
	{
		Win32.GetWindowThreadProcessId(hwnd, out var processId);
		return processId == Environment.ProcessId;
	}
}
