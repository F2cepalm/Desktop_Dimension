using System;
using System.Windows;

namespace InfiniteSpace.Rendering;

internal sealed class Jelly
{
	public const double Stiffness = 30.0;

	public const double Damping = 0.3;

	public const double Recoil = 0.4;

	public const double Threshold = 2.5;

	public const double Squash = 0.6;

	public const double Bulge = 1.5;

	public const double Longest = 0.5;

	private const double MostSwing = 0.25;

	private const double MostSwingDips = 40.0;

	private static readonly double SwingPerSpeed = FirstSwing();

	private readonly double[] _offset = new double[4];

	private readonly double[] _speed = new double[4];

	private Rect _rest;

	private Rect _kicked;

	private bool _hasRest;

	private double _sinceKick;

	public bool IsMoving { get; private set; }

	public Rect Rest => _rest;

	public Rect Shown
	{
		get
		{
			if (!IsMoving)
			{
				return _rest;
			}
			double num = _rest.Left + _offset[0];
			double num2 = _rest.Top + _offset[1];
			double num3 = _rest.Right + _offset[2];
			double num4 = _rest.Bottom + _offset[3];
			double num5 = Math.Max(1.0, num3 - num);
			double num6 = Math.Max(1.0, num4 - num2);
			double num7 = num5 / _rest.Width - 1.0;
			double num8 = num6 / _rest.Height - 1.0;
			double num9 = Math.Max(1.0, num5 * (1.0 - 0.6 * num8));
			double num10 = Math.Max(1.0, num6 * (1.0 - 0.6 * num7));
			double num11 = (num + num3) / 2.0;
			return new Rect(y: (num2 + num4) / 2.0 - num10 / 2.0, x: num11 - num9 / 2.0, width: num9, height: num10);
		}
	}

	public void Retarget(Rect rest, bool wobble = true)
	{
		if (!_hasRest || !wobble)
		{
			Stop();
			_rest = (_kicked = rest);
			_hasRest = true;
			return;
		}
		bool flag = Resized(_kicked.Left, _kicked.Right, rest.Left, rest.Right, out var carried, out var lowMoved, out var highMoved);
		bool flag2 = Resized(_kicked.Top, _kicked.Bottom, rest.Top, rest.Bottom, out var carried2, out var lowMoved2, out var highMoved2);
		double num = _kicked.Left + carried;
		double val = _kicked.Right + carried;
		double num2 = _kicked.Top + carried2;
		double val2 = _kicked.Bottom + carried2;
		if (flag)
		{
			Kick(0, lowMoved);
			Kick(2, highMoved);
			double left = rest.Left;
			val = rest.Right;
			num = left;
		}
		if (flag2)
		{
			Kick(1, lowMoved2);
			Kick(3, highMoved2);
			double top = rest.Top;
			val2 = rest.Bottom;
			num2 = top;
		}
		_kicked = new Rect(new Point(num, num2), new Point(Math.Max(num, val), Math.Max(num2, val2)));
		_rest = rest;
		if (flag | flag2)
		{
			_sinceKick = 0.0;
			IsMoving = true;
		}
		Limit();
	}

	public void Poke(double dips)
	{
		if (_hasRest)
		{
			double num = dips / SwingPerSpeed;
			_speed[0] -= num;
			_speed[1] -= num;
			_speed[2] += num;
			_speed[3] += num;
			_sinceKick = 0.0;
			IsMoving = true;
			Limit();
		}
	}

	public void Shift(Vector by)
	{
		_rest.Offset(by);
		_kicked.Offset(by);
	}

	public void Step(double seconds)
	{
		if (!IsMoving)
		{
			return;
		}
		seconds = Math.Clamp(seconds, 0.0, 0.05);
		_sinceKick += seconds;
		if (_sinceKick >= 0.5)
		{
			Stop();
			return;
		}
		double num = seconds / 4.0;
		for (int i = 0; i < 4; i++)
		{
			for (int j = 0; j < 4; j++)
			{
				_speed[j] += (-900.0 * _offset[j] - 18.0 * _speed[j]) * num;
				_offset[j] += _speed[j] * num;
			}
		}
		bool flag = true;
		for (int k = 0; k < 4; k++)
		{
			flag &= Math.Abs(_offset[k]) < 0.05 && Math.Abs(_speed[k]) < 1.0;
		}
		if (flag)
		{
			Stop();
		}
	}

	public void Stop()
	{
		Array.Clear(_offset);
		Array.Clear(_speed);
		IsMoving = false;
	}

	public void Reset()
	{
		Stop();
		_hasRest = false;
	}

	public Point Map(Point atRest, Rect shown)
	{
		double num = (atRest.X - _rest.X) / _rest.Width;
		double num2 = (atRest.Y - _rest.Y) / _rest.Height;
		double num3 = shown.X + num * shown.Width;
		double num4 = shown.Y + num2 * shown.Height;
		double num5 = shown.Width / _rest.Width - 1.0;
		double num6 = shown.Height / _rest.Height - 1.0;
		double num7 = Math.Clamp(2.0 * num - 1.0, -1.0, 1.0);
		double num8 = Math.Clamp(2.0 * num2 - 1.0, -1.0, 1.0);
		double x = num3 - 1.5 * num6 * shown.Width / 2.0 * num7 * Math.Cos(Math.PI * num8 / 2.0);
		num4 -= 1.5 * num5 * shown.Height / 2.0 * num8 * Math.Cos(Math.PI * num7 / 2.0);
		return new Point(x, num4);
	}

	private static bool Resized(double low, double high, double newLow, double newHigh, out double carried, out double lowMoved, out double highMoved)
	{
		double num = newLow - low;
		double num2 = newHigh - high;
		carried = ((Math.Sign(num) != Math.Sign(num2)) ? 0.0 : ((Math.Abs(num) < Math.Abs(num2)) ? num : num2));
		lowMoved = num - carried;
		highMoved = num2 - carried;
		return Math.Abs(lowMoved) + Math.Abs(highMoved) > 2.5;
	}

	private void Kick(int edge, double moved)
	{
		_speed[edge] -= moved * 0.4 / SwingPerSpeed;
	}

	private void Limit()
	{
		double num = Math.Min(40.0, _rest.Width * 0.25);
		double num2 = Math.Min(40.0, _rest.Height * 0.25);
		for (int i = 0; i < 4; i++)
		{
			double num3 = ((i % 2 == 0) ? num : num2);
			_offset[i] = Math.Clamp(_offset[i], 0.0 - num3, num3);
			_speed[i] = Math.Clamp(_speed[i], (0.0 - num3) / SwingPerSpeed, num3 / SwingPerSpeed);
		}
	}

	private static double FirstSwing()
	{
		double num = Math.Sqrt(0.91);
		double num2 = Math.Atan2(num, 0.3);
		return Math.Exp(-0.3 * num2 / num) * Math.Sin(num2) / (30.0 * num);
	}
}
