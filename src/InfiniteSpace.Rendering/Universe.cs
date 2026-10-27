using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using InfiniteSpace.Native;
using InfiniteSpace.Spaces;
using SharpGen.Runtime;

namespace InfiniteSpace.Rendering;

internal sealed class Universe : IDisposable
{
	private const int RetireAfterFrames = 3;

	private const double AmbientRate = 60.0;

	private readonly Stopwatch _clock = Stopwatch.StartNew();

	private readonly List<SpaceWindow> _spaces = new List<SpaceWindow>();

	private readonly List<(SpaceWindow Window, int FramesLeft)> _retiring = new List<(SpaceWindow, int)>();

	private readonly List<IFramePart> _parts = new List<IFramePart>();

	private readonly FrameClock _frames;

	private double _lastTick = double.NegativeInfinity;

	private double _lastFrame = double.NegativeInfinity;

	private double _lastInteractive = double.NegativeInfinity;

	private Vector _look;

	private bool _hasLook;

	private bool _disposed;

	public GpuCompositor Gpu { get; }

	internal long FramesRendered { get; private set; }

	internal long Ticks { get; private set; }

	internal string ClockSource => _frames.Source;

	internal long DeviceRecoveries { get; private set; }

	public Universe(GpuCompositor gpu)
	{
		Gpu = gpu;
		_frames = new FrameClock(Dispatcher.CurrentDispatcher, Tick);
	}

	public void Attach(SpaceWindow space)
	{
		if (!_spaces.Contains(space))
		{
			_spaces.Add(space);
			space.Track();
			space.RenderFrame(new FrameState(_clock.Elapsed.TotalSeconds, _look));
			Gpu.Commit();
			_frames.Start();
		}
	}

	public void Detach(SpaceWindow space)
	{
		_spaces.Remove(space);
		StopWhenIdle();
	}

	public void Attach(IFramePart part)
	{
		if (!_parts.Contains(part))
		{
			_parts.Add(part);
			_frames.Start();
		}
	}

	public void Detach(IFramePart part)
	{
		_parts.Remove(part);
		StopWhenIdle();
	}

	public void Retire(SpaceWindow space)
	{
		_spaces.Remove(space);
		_retiring.Add((space, 3));
		_frames.Start();
	}

	internal void Tick()
	{
		if (_disposed)
		{
			return;
		}
		try
		{
			if (Gpu.IsLost)
			{
				Recover();
			}
			Frame();
		}
		catch (SharpGenException ex) when (GpuCompositor.IsDeviceLoss(ex.HResult))
		{
			Gpu.MarkLost();
		}
	}

	private void Recover()
	{
		Gpu.Recreate();
		DeviceRecoveries++;
		foreach (var item in _retiring)
		{
			item.Window.Dispose();
		}
		_retiring.Clear();
		SpaceWindow[] array = _spaces.ToArray();
		for (int i = 0; i < array.Length; i++)
		{
			array[i].RecreateGraphics();
		}
		IFramePart[] array2 = _parts.ToArray();
		for (int i = 0; i < array2.Length; i++)
		{
			array2[i].DeviceLost();
		}
		Gpu.Commit();
	}

	private bool AnySpaceShowing()
	{
		for (int i = 0; i < _spaces.Count; i++)
		{
			if (_spaces[i].IsShowing)
			{
				return true;
			}
		}
		return false;
	}

	private void Frame()
	{
		long timestamp = Stopwatch.GetTimestamp();
		Ticks++;
		double totalSeconds = _clock.Elapsed.TotalSeconds;
		double dt = Math.Min(totalSeconds - _lastTick, 0.1);
		_lastTick = totalSeconds;
		UpdateLook(dt);
		RetireDue();
		bool anyWantsFrame = AnyWantsEveryFrame();
		bool anyShowing = AnySpaceShowing();
		bool idle = !anyShowing && !anyWantsFrame && _retiring.Count == 0 && _parts.Count == 0;
		bool trackDue = !idle || totalSeconds - _lastFrame >= 0.25;
		if (trackDue)
		{
			for (int i = 0; i < _spaces.Count; i++)
			{
				_spaces[i].Track();
			}
			if (idle)
			{
				anyShowing = AnySpaceShowing();
				anyWantsFrame = AnyWantsEveryFrame();
				idle = !anyShowing && !anyWantsFrame && _retiring.Count == 0 && _parts.Count == 0;
			}
		}
		bool flag = anyWantsFrame;
		bool flag2 = totalSeconds - _lastFrame >= (idle ? 0.25 : 0.003);
		bool flag3 = flag2 || flag;
		if (flag3)
		{
			_lastInteractive = totalSeconds;
		}
		if (flag3)
		{
			if (flag2)
			{
				_lastFrame = totalSeconds;
			}
			FramesRendered++;
			FrameState frame = new FrameState(totalSeconds, _look, flag2);
			for (int j = 0; j < _spaces.Count; j++)
			{
				_spaces[j].RenderFrame(frame);
			}
			for (int k = 0; k < _parts.Count; k++)
			{
				_parts[k].RenderFrame(frame);
			}
		}
		Gpu.Commit();
		StopWhenIdle();
		if (flag3)
		{
			FrameEvents.Log.Record(Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds, _spaces.Count, flag);
		}
	}

	private bool AnyWantsEveryFrame()
	{
		for (int i = 0; i < _spaces.Count; i++)
		{
			if (_spaces[i].WantsEveryFrame)
			{
				return true;
			}
		}
		for (int j = 0; j < _parts.Count; j++)
		{
			if (_parts[j].WantsFrame)
			{
				return true;
			}
		}
		return false;
	}

	private void RetireDue()
	{
		for (int num = _retiring.Count - 1; num >= 0; num--)
		{
			var (spaceWindow, num2) = _retiring[num];
			if (num2 > 1)
			{
				_retiring[num] = (spaceWindow, num2 - 1);
			}
			else
			{
				spaceWindow.Dispose();
				_retiring.RemoveAt(num);
			}
		}
	}

	private void StopWhenIdle()
	{
		if (_spaces.Count == 0 && _retiring.Count == 0 && _parts.Count == 0)
		{
			_frames.Stop();
		}
	}

	private void UpdateLook(double dt)
	{
		if (Win32.GetCursorPos(out var point))
		{
			Point point2 = new Point((double)point.X / Dpi.Scale, (double)point.Y / Dpi.Scale);
			Point point3 = new Point(SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth / 2.0, SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight / 2.0);
			Vector vector = point2 - point3;
			if (!_hasLook)
			{
				_look = vector;
				_hasLook = true;
			}
			else if ((vector - _look).LengthSquared < 0.0025)
			{
				_look = vector;
			}
			else
			{
				_look += (vector - _look) * (1.0 - Math.Exp((0.0 - dt) * 2.5));
			}
		}
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}
		_disposed = true;
		_frames.Dispose();
		foreach (var item in _retiring)
		{
			item.Window.Dispose();
		}
		_retiring.Clear();
	}
}
