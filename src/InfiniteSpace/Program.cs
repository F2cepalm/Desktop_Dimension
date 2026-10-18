using System;
using System.IO;
using System.Runtime;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using InfiniteSpace.Rendering;
using InfiniteSpace.Shell;
using InfiniteSpace.Spaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharpGen.Runtime;

namespace InfiniteSpace;

internal static class Program
{
	[STAThread]
	private static void Main(string[] args)
	{
		bool createdNew;
		using (new Mutex(initiallyOwned: true, "Local\\InfiniteSpace.SingleInstance", out createdNew))
		{
			if (!createdNew)
			{
				return;
			}
			GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
			HostApplicationBuilder hostApplicationBuilder = Host.CreateApplicationBuilder(args);
			string dataDirectory = ResolveDataDirectory(hostApplicationBuilder.Configuration);
			hostApplicationBuilder.Services.AddSingleton((IServiceProvider sp) => new SpaceStore(dataDirectory, sp.GetRequiredService<ILogger<SpaceStore>>()));
			hostApplicationBuilder.Services.AddSingleton<GpuCompositor>();
			hostApplicationBuilder.Services.AddSingleton<Universe>();
			hostApplicationBuilder.Services.AddSingleton<SpaceManager>();
			hostApplicationBuilder.Services.AddSingleton<ShellController>();
			using IHost host = hostApplicationBuilder.Build();
			host.Start();
			ILogger<Application> logger = host.Services.GetRequiredService<ILogger<Application>>();
			ShellController shell = host.Services.GetRequiredService<ShellController>();
			Application application = new Application();
			application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
			application.Startup += (object _, StartupEventArgs _) =>
			{
				shell.Start();
			};
			application.Exit += (object _, ExitEventArgs _) =>
			{
				shell.Dispose();
			};
			GpuCompositor gpu = host.Services.GetRequiredService<GpuCompositor>();
			application.DispatcherUnhandledException += (object _, DispatcherUnhandledExceptionEventArgs e) =>
			{
				if (e.Exception is SharpGenException ex && GpuCompositor.IsDeviceLoss(ex.HResult))
				{
					logger.LogWarning(e.Exception, "The GPU went away; making everything again");
					gpu.MarkLost();
				}
				else
				{
					logger.LogError(e.Exception, "Unhandled UI exception");
				}
				e.Handled = true;
			};
			application.Run();
			host.StopAsync(TimeSpan.FromSeconds(2L)).GetAwaiter().GetResult();
		}
	}

	private static string ResolveDataDirectory(IConfiguration configuration)
	{
		string text = configuration["InfiniteSpace:DataDirectory"];
		if (text == null || text.Length <= 0)
		{
			return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "InfiniteSpace");
		}
		return text;
	}
}
