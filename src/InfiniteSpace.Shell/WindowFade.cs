using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using InfiniteSpace.Native;

namespace InfiniteSpace.Shell;

internal static class WindowFade
{
	public static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(500L);

	public static void Out(Window window, Action close)
	{
		nint handle = new WindowInteropHelper(window).Handle;
		if (handle != IntPtr.Zero)
		{
			Win32.SetWindowLong(handle, -20, Win32.GetWindowLong(handle, -20) | 0x20 | 0x8000000);
		}
		window.IsHitTestVisible = false;
		DoubleAnimation doubleAnimation = new DoubleAnimation(0.0, new Duration(Duration))
		{
			EasingFunction = new QuadraticEase
			{
				EasingMode = EasingMode.EaseIn
			}
		};
		doubleAnimation.Completed += (object? _, EventArgs _) =>
		{
			close();
		};
		window.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
	}
}
