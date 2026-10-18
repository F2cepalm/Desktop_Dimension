using System;
using System.Runtime.ExceptionServices;
using System.Windows.Threading;

namespace InfiniteSpace.Native;

internal sealed class WindowEvents : IDisposable
{
	private readonly Win32.WinEventProc _callback;

	private readonly nint[] _hooks;

	public WindowEvents(Action<uint, nint> changed)
	{
		Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
		_callback = (nint _, uint eventType, nint hwnd, int idObject, int idChild, uint _, uint _) =>
		{
			if (idObject != 0 || idChild != 0 || hwnd == IntPtr.Zero)
			{
				return;
			}
			try
			{
				changed(eventType, hwnd);
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				Exception ex3 = ex2;
				dispatcher.BeginInvoke((Action)(() =>
				{
					ExceptionDispatchInfo.Throw(ex3);
				}));
			}
		};
		_hooks = new nint[3]
		{
			Win32.SetWinEventHook(3u, 3u, IntPtr.Zero, _callback, 0u, 0u, 2u),
			Win32.SetWinEventHook(22u, 23u, IntPtr.Zero, _callback, 0u, 0u, 2u),
			Win32.SetWinEventHook(32770u, 32770u, IntPtr.Zero, _callback, 0u, 0u, 2u)
		};
	}

	public void Dispose()
	{
		nint[] hooks = _hooks;
		foreach (nint num in hooks)
		{
			if (num != IntPtr.Zero)
			{
				Win32.UnhookWinEvent(num);
			}
		}
	}
}
