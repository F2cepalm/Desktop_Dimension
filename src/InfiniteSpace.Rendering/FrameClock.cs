using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Threading;
using InfiniteSpace.Native;
using Vortice.DXGI;

namespace InfiniteSpace.Rendering;

internal sealed class FrameClock : IDisposable
{
	private static readonly TimeSpan TooFast = TimeSpan.FromMilliseconds(1L);

	private const int TooFastInARow = 4;

	private const int WAIT_TIMEOUT = 258;

	private readonly Dispatcher _dispatcher;

	private readonly Action _tick;

	private readonly ManualResetEventSlim _running = new ManualResetEventSlim(initialState: false);

	private readonly Thread _thread;

	private readonly Stopwatch _sinceTick = new Stopwatch();

	private volatile bool _disposed;

	private int _queued;

	private int _tooFast;

	private bool? _compositorClock = (PreferCompositorClock ? ((bool?)null) : new bool?(false));

	private IDXGIFactory1? _factory;

	private readonly List<IDXGIOutput> _outputs = new List<IDXGIOutput>();

	private IDXGIOutput? _output;

	private nint _outputMonitor;

	private long _nextOutputCheck;

	public string Source
	{
		get
		{
			if (_compositorClock != true)
			{
				if ((object)_output == null)
				{
					return "compositor frames";
				}
				return "vertical blank";
			}
			return "compositor clock";
		}
	}

	internal static bool PreferCompositorClock { get; set; } = true;

	public FrameClock(Dispatcher dispatcher, Action tick)
	{
		_dispatcher = dispatcher;
		_tick = tick;
		_thread = new Thread(Run)
		{
			IsBackground = true,
			Name = "Infinite Space frame clock",
			Priority = ThreadPriority.AboveNormal
		};
		_thread.Start();
	}

	public void Start()
	{
		_running.Set();
	}

	public void Stop()
	{
		_running.Reset();
	}

	private void Run()
	{
		_sinceTick.Start();
		while (!_disposed)
		{
			_running.Wait();
			if (_disposed)
			{
				break;
			}
			WaitForRefresh();
			TimeSpan elapsed = _sinceTick.Elapsed;
			_tooFast = ((elapsed < TooFast) ? (_tooFast + 1) : 0);
			if (_tooFast >= 4 && elapsed < TimeSpan.FromMilliseconds(16L))
			{
				Thread.Sleep(TimeSpan.FromMilliseconds(16L) - elapsed);
			}
			_sinceTick.Restart();
			if (Interlocked.Exchange(ref _queued, 1) == 0)
			{
				_dispatcher.BeginInvoke(DispatcherPriority.Render, (Action)(() =>
				{
					Volatile.Write(ref _queued, 0);
					_tick();
				}));
			}
		}
	}

	private void WaitForRefresh()
	{
		try
		{
			if (_compositorClock != false && WaitForCompositorClock())
			{
				return;
			}
			IDXGIOutput iDXGIOutput = CursorOutput();
			if ((object)iDXGIOutput != null)
			{
				iDXGIOutput.WaitForVBlank();
				return;
			}
		}
		catch (Exception ex) when (!(ex is ThreadAbortException))
		{
			ForgetOutputs();
		}
		Win32.DwmFlush();
	}

	private bool WaitForCompositorClock()
	{
		try
		{
			uint num = Win32.DCompositionWaitForCompositorClock(0u, IntPtr.Zero, 100u);
			if (num == 0)
			{
				_compositorClock = true;
				return true;
			}
			if (num == 258)
			{
				return false;
			}
			_compositorClock = false;
			return false;
		}
		catch (EntryPointNotFoundException)
		{
			_compositorClock = false;
			return false;
		}
	}

	private IDXGIOutput? CursorOutput()
	{
		long timestamp = Stopwatch.GetTimestamp();
		if ((object)_output != null && timestamp < _nextOutputCheck)
		{
			return _output;
		}
		_nextOutputCheck = timestamp + Stopwatch.Frequency / 4;
		nint monitor = (Win32.GetCursorPos(out var point) ? Win32.MonitorFromPoint(point, 1u) : IntPtr.Zero);
		if ((object)_output != null && monitor == _outputMonitor)
		{
			return _output;
		}
		if (_outputs.Count == 0 || ((object)_factory != null && !_factory.IsCurrent))
		{
			FindOutputs();
		}
		_output = _outputs.FirstOrDefault((IDXGIOutput o) => o.Description.Monitor == monitor) ?? _outputs.FirstOrDefault();
		_outputMonitor = monitor;
		return _output;
	}

	private void FindOutputs()
	{
		ForgetOutputs();
		_factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
		IDXGIAdapter1 adapterOut;
		for (uint num = 0u; _factory.EnumAdapters1(num, out adapterOut).Success; num++)
		{
			using (adapterOut)
			{
				IDXGIOutput outputOut;
				for (uint num2 = 0u; adapterOut.EnumOutputs(num2, out outputOut).Success; num2++)
				{
					_outputs.Add(outputOut);
				}
			}
		}
	}

	private void ForgetOutputs()
	{
		foreach (IDXGIOutput output in _outputs)
		{
			output.Dispose();
		}
		_outputs.Clear();
		_output = null;
		_outputMonitor = IntPtr.Zero;
		_factory?.Dispose();
		_factory = null;
	}

	public void Dispose()
	{
		_disposed = true;
		_running.Set();
		_thread.Join(TimeSpan.FromSeconds(1L));
		_running.Dispose();
		if (!_thread.IsAlive)
		{
			ForgetOutputs();
		}
	}
}
