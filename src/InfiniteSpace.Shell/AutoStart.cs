using System;
using Microsoft.Win32;

namespace InfiniteSpace.Shell;

internal static class AutoStart
{
	private const string RunKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";

	private const string ValueName = "InfiniteSpace";

	public static bool IsEnabled
	{
		get
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run");
			return registryKey?.GetValue("InfiniteSpace") is string;
		}
	}

	public static void Set(bool enabled)
	{
		using RegistryKey registryKey = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", writable: true);
		if (enabled)
		{
			registryKey.SetValue("InfiniteSpace", "\"" + Environment.ProcessPath + "\"");
		}
		else
		{
			registryKey.DeleteValue("InfiniteSpace", throwOnMissingValue: false);
		}
	}
}
