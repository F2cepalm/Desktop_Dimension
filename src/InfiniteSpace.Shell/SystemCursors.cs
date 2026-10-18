using System;
using InfiniteSpace.Native;

namespace InfiniteSpace.Shell;

internal static class SystemCursors
{
	public static readonly nint Arrow = Load(32512);

	public static readonly nint Cross = Load(32515);

	public static readonly nint Hand = Load(32649);

	public static readonly nint SizeAll = Load(32646);

	public static readonly nint SizeNWSE = Load(32642);

	public static readonly nint SizeNESW = Load(32643);

	public static readonly nint SizeWE = Load(32644);

	public static readonly nint SizeNS = Load(32645);

	public static nint For(BlockGrip grip)
	{
		switch (grip)
		{
		case BlockGrip.Left | BlockGrip.Top:
		case BlockGrip.Right | BlockGrip.Bottom:
			return SizeNWSE;
		case BlockGrip.Top | BlockGrip.Right:
		case BlockGrip.Left | BlockGrip.Bottom:
			return SizeNESW;
		case BlockGrip.Left:
		case BlockGrip.Right:
			return SizeWE;
		case BlockGrip.Top:
		case BlockGrip.Bottom:
			return SizeNS;
		case BlockGrip.Move:
			return SizeAll;
		default:
			return Arrow;
		}
	}

	private static nint Load(int id)
	{
		return Win32.LoadCursor(IntPtr.Zero, id);
	}
}
