using System;
using System.Collections.Generic;
using System.Windows.Input;
using System.Windows.Interop;

namespace InfiniteSpace.Native;

internal sealed class GlobalHotkeys : IDisposable
{
	private readonly HwndSource _source;

	private readonly Dictionary<int, Action> _handlers = new Dictionary<int, Action>();

	private int _nextId = 45056;

	public GlobalHotkeys()
	{
		HwndSourceParameters parameters = new HwndSourceParameters("InfiniteSpace.Hotkeys")
		{
			WindowStyle = 0,
			ParentWindow = Win32.HWND_MESSAGE
		};
		_source = new HwndSource(parameters);
		_source.AddHook(WndProc);
	}

	public int? Register(ModifierKeys modifiers, Key key, Action handler, bool repeat = false)
	{
		uint num = ((!repeat) ? 16384u : 0u);
		if (modifiers.HasFlag(ModifierKeys.Alt))
		{
			num |= 1;
		}
		if (modifiers.HasFlag(ModifierKeys.Control))
		{
			num |= 2;
		}
		if (modifiers.HasFlag(ModifierKeys.Shift))
		{
			num |= 4;
		}
		if (modifiers.HasFlag(ModifierKeys.Windows))
		{
			num |= 8;
		}
		int num2 = _nextId++;
		uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
		if (!Win32.RegisterHotKey(_source.Handle, num2, num, vk))
		{
			return null;
		}
		_handlers[num2] = handler;
		return num2;
	}

	public void Unregister(int id)
	{
		if (_handlers.Remove(id))
		{
			Win32.UnregisterHotKey(_source.Handle, id);
		}
	}

	private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
	{
		if (msg == 786 && _handlers.TryGetValue(((IntPtr)wParam).ToInt32(), out Action value))
		{
			handled = true;
			value();
		}
		return IntPtr.Zero;
	}

	public void Dispose()
	{
		foreach (int key in _handlers.Keys)
		{
			Win32.UnregisterHotKey(_source.Handle, key);
		}
		_handlers.Clear();
		_source.RemoveHook(WndProc);
		_source.Dispose();
	}
}
