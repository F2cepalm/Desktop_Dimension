using System;
using System.Collections.Generic;
using System.IO;

namespace InfiniteSpace.Spaces;

internal static class HostMatcher
{
	internal readonly record struct Waiting(Guid Group, string App, string WindowClass, string Title);

	internal readonly record struct Open(nint Handle, string App, string WindowClass, string Title);

	private static readonly HashSet<string> SharedHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ApplicationFrameHost.exe" };

	public static List<(Guid Group, nint Window)> Match(IReadOnlyList<Waiting> waiting, IReadOnlyList<Open> windows, ISet<nint> busy)
	{
		List<(Guid, nint)> list = new List<(Guid, nint)>();
		HashSet<nint> hashSet = new HashSet<nint>(busy);
		HashSet<Guid> hashSet2 = new HashSet<Guid>();
		foreach (Waiting item in waiting)
		{
			foreach (Open window in windows)
			{
				if (!hashSet.Contains(window.Handle) && SameApp(item, window) && item.Title.Length != 0 && !(window.Title != item.Title))
				{
					list.Add((item.Group, window.Handle));
					hashSet.Add(window.Handle);
					hashSet2.Add(item.Group);
					break;
				}
			}
		}
		foreach (Waiting item2 in waiting)
		{
			if (hashSet2.Contains(item2.Group) || SharedHosts.Contains(Path.GetFileName(item2.App)))
			{
				continue;
			}
			foreach (Open window2 in windows)
			{
				if (!hashSet.Contains(window2.Handle) && SameApp(item2, window2))
				{
					list.Add((item2.Group, window2.Handle));
					hashSet.Add(window2.Handle);
					hashSet2.Add(item2.Group);
					break;
				}
			}
		}
		return list;
	}

	private static bool SameApp(Waiting group, Open window)
	{
		if (string.Equals(group.App, window.App, StringComparison.OrdinalIgnoreCase))
		{
			return string.Equals(group.WindowClass, window.WindowClass, StringComparison.Ordinal);
		}
		return false;
	}
}
