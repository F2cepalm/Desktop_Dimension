namespace InfiniteSpace.Native;

internal static class Dpi
{
	public static double Scale { get; } = (double)Win32.GetDpiForSystem() / 96.0;
}
