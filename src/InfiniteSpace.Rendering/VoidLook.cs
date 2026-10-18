using System;
using System.Numerics;
using System.Windows;
using System.Windows.Media;
using Vortice.Direct2D1;

namespace InfiniteSpace.Rendering;

internal sealed class VoidLook : SceneLook
{
	public static readonly Color Ink = Colors.White;

	private static readonly FrameStyle Style = new FrameStyle
	{
		Line = Colors.White,
		LineWidth = 2.0
	};

	private ID2D1SolidColorBrush? _ring;

	protected override FrameStyle Frame => Style;

	protected override bool HasMotionLayer => false;

	protected override double BurstLife => 0.7;

	protected override bool Advance()
	{
		return false;
	}

	protected override void DrawBackdrop(ID2D1DeviceContext context, ID2D1Factory1 factory)
	{
		using ID2D1SolidColorBrush brush = SceneLook.Solid(context, Colors.Black);
		context.FillRectangle(SceneLook.R(new Rect(0.0, 0.0, World.Width, World.Height)), brush);
	}

	protected override void DrawMotion(ID2D1DeviceContext context, ID2D1Factory1 factory, Matrix3x2 origin)
	{
		if ((object)_ring == null)
		{
			_ring = SceneLook.Solid(context, Colors.White);
		}
		foreach (Burst burst in Bursts)
		{
			double num = Time - burst.Start;
			double num2 = num / BurstLife;
			float num3 = (float)(4.0 + 50.0 * (1.0 - Math.Exp((0.0 - num) * 4.0)));
			_ring.Opacity = (float)(0.9 * (1.0 - num2) * (1.0 - num2));
			context.DrawEllipse(new Ellipse(SceneLook.V(burst.Origin), num3, num3), _ring, 1.5f);
		}
	}

	protected override void DisposeResources()
	{
		_ring?.Dispose();
	}
}
