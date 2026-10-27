using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using InfiniteSpace.Native;
using InfiniteSpace.Rendering;
using InfiniteSpace.Spaces;
using Vortice;
using Vortice.Direct2D1;
using Vortice.DirectComposition;
using Vortice.Mathematics;

namespace InfiniteSpace.Shell;

internal sealed class MoveOverlay : GpuOverlay
{
	private const double Step = 10.0;

	private const double FineStep = 2.0;

	private const double MinSize = 24.0;

	private const double GripMargin = 10.0;

	private static readonly BlockGrip[] Grips = new BlockGrip[8]
	{
		BlockGrip.Left | BlockGrip.Top,
		BlockGrip.Top,
		BlockGrip.Top | BlockGrip.Right,
		BlockGrip.Left,
		BlockGrip.Right,
		BlockGrip.Left | BlockGrip.Bottom,
		BlockGrip.Bottom,
		BlockGrip.Right | BlockGrip.Bottom
	};

	private static readonly Color4 FrameInk = new Color4(1f, 1f, 1f, 179f / 255f);

	private readonly SpaceManager _spaces;

	private readonly Dictionary<double, OverlayPicture> _arrowPictures = new Dictionary<double, OverlayPicture>();

	private readonly OverlaySprite[] _arrows;

	private readonly IDCompositionVisual _arrowLayer;

	private readonly IDCompositionEffectGroup _arrowOpacity;

	private readonly OverlayPicture _across;

	private readonly OverlayPicture _down;

	private readonly OverlaySprite[] _sides;

	private readonly OverlaySprite _size;

	private OverlayPicture? _sizePicture;

	private string _sizeText = "";

	private SpaceManager.MovingSpace? _moving;

	private System.Windows.Rect _rect;

	private bool _placed;

	private BlockGrip _drag;

	private Point _last;

	private Vector _carried;

	private System.Windows.Threading.DispatcherTimer? _settleTimer;

	private static string PickHint => "Click a space to pick it up   ·   Esc to cancel";

	private static string FollowHint => "Click to set it down   ·   arrow keys reshape (Shift shrinks, Ctrl fine), Alt+arrows nudge, wheel scales   ·   double-click or Enter drops it   ·   Esc puts it back";

	private static string PlacedHint => "Drag the arrows to reshape, the body to move   ·   arrow keys reshape, Alt+arrows nudge   ·   double-click or Enter drops it   ·   Esc puts it back";

	internal SpaceManager.MovingSpace? Moving => _moving;

	public MoveOverlay(SpaceManager spaces, Universe universe, System.Windows.Rect? bounds = null)
		: base(universe, PickHint, bounds)
	{
		_spaces = spaces;
		_last = CursorOnScreen();
		Cursor = SystemCursors.Hand;
		double longest = (double)Math.Max(PixelWidth, PixelHeight) / Dpi.Scale + 8.0;
		_across = OverlayPicture.Paint(Gpu, longest, 1.0, (ID2D1DeviceContext context) =>
		{
			DashedLine(context, longest, horizontal: true);
		});
		_down = OverlayPicture.Paint(Gpu, 1.0, longest, (ID2D1DeviceContext context) =>
		{
			DashedLine(context, longest, horizontal: false);
		});
		_sides = new OverlaySprite[4]
		{
			new OverlaySprite(Gpu, Layers, _across),
			new OverlaySprite(Gpu, Layers, _across),
			new OverlaySprite(Gpu, Layers, _down),
			new OverlaySprite(Gpu, Layers, _down)
		};
		_arrowLayer = Gpu.Composition.CreateVisual();
		Layers.AddVisual(_arrowLayer, true, null);
		Gpu.Composition.CreateEffectGroup(out _arrowOpacity).CheckError();
		_arrowLayer.SetEffect(_arrowOpacity);
		_arrows = Grips.Select((BlockGrip grip) => new OverlaySprite(Gpu, _arrowLayer, Arrow(ArrowHandle.AngleFor(grip)))).ToArray();
		_size = new OverlaySprite(Gpu, Layers);
	}

	protected override void OnShown()
	{
		PickUp(_last);
	}

	protected override void OnClosing()
	{
		if (_moving != null)
		{
			_spaces.PutBack(_moving);
			_moving = null;
		}
	}



	private void ScheduleSettle()
	{
		_settleTimer?.Stop();
		if (_settleTimer == null)
		{
			_settleTimer = new System.Windows.Threading.DispatcherTimer(
				TimeSpan.FromMilliseconds(150),
				System.Windows.Threading.DispatcherPriority.Background,
				(s, e) =>
				{
					_settleTimer?.Stop();
					if (_moving != null && _drag == BlockGrip.None)
					{
						_spaces.SettleMoving(_moving);
					}
				},
				System.Windows.Threading.Dispatcher.CurrentDispatcher);
		}
		_settleTimer.Start();
	}
	protected override void DisposeParts()
	{
		OverlaySprite[] arrows = _arrows;
		for (int i = 0; i < arrows.Length; i++)
		{
			arrows[i].Dispose();
		}
		foreach (OverlayPicture value in _arrowPictures.Values)
		{
			value.Dispose();
		}
		arrows = _sides;
		for (int i = 0; i < arrows.Length; i++)
		{
			arrows[i].Dispose();
		}
		_across.Dispose();
		_down.Dispose();
		_arrowOpacity.Dispose();
		_arrowLayer.Dispose();
		_size.Dispose();
		_sizePicture?.Dispose();
		_settleTimer?.Stop();
	}

	protected override void OnMouseMove(Point at)
	{
		Point point = ToScreen(at);
		if (_moving != null)
		{
			if (!_placed)
			{
				MoveBy(point - _last);
			}
			else if (_drag == BlockGrip.Move)
			{
				MoveBy(point - _last);
			}
			else if (_drag != BlockGrip.None)
			{
				Fit(Contained(_rect, BlockEdit.Drag(_rect, _drag, point - _last, 24.0)));
			}
			else
			{
				Cursor = SystemCursors.For(BlockEdit.GripAt(_rect, point, 10.0));
			}
		}
		_last = point;
	}

	protected override void OnMouseDown(Point at, int clickCount)
	{
		Point point = (_last = ToScreen(at));
		if (_moving == null)
		{
			PickUp(point);
		}
		else if (clickCount == 2 && BlockEdit.GripAt(_rect, point, 10.0) != BlockGrip.None)
		{
			DropAt(point);
		}
		else if (!_placed)
		{
			_placed = true;
			Hint = PlacedHint;
			Layout();
			Cursor = SystemCursors.For(BlockEdit.GripAt(_rect, point, 10.0));
		}
		else
		{
			_drag = BlockEdit.GripAt(_rect, point, 10.0);
			if (_drag == BlockGrip.None)
			{
				MoveBy(point - new Point(_rect.X + _rect.Width / 2.0, _rect.Y + _rect.Height / 2.0));
			}
			else
			{
				CaptureMouse();
			}
		}
	}

	protected override void OnMouseUp(Point at)
	{
		if (_drag != BlockGrip.None)
		{
			_drag = BlockGrip.None;
			ReleaseMouseCapture();
			_settleTimer?.Stop();
			if (_moving != null)
			{
				_spaces.SettleMoving(_moving);
			}
		}
	}

	protected override void OnCaptureLost()
	{
		_drag = BlockGrip.None;
		_settleTimer?.Stop();
		if (_moving != null)
		{
			_spaces.SettleMoving(_moving);
		}
	}

	protected override void OnWheel(int delta)
	{
		if (_moving != null)
		{
			Fit(Contained(_rect, BlockEdit.Scale(_rect, (delta > 0) ? 1.1 : (930.0 / 1023.0), 24.0)));
			ScheduleSettle();
		}
	}

	protected override bool OnKeyDown(Key key, ModifierKeys modifiers)
	{
		if (_moving != null)
		{
			BlockGrip blockGrip = key switch
			{
				Key.Left => BlockGrip.Left, 
				Key.Right => BlockGrip.Right, 
				Key.Up => BlockGrip.Top, 
				Key.Down => BlockGrip.Bottom, 
				_ => BlockGrip.None, 
			};
			if (blockGrip != BlockGrip.None)
			{
				double num = (modifiers.HasFlag(ModifierKeys.Control) ? 2.0 : 10.0);
				if (modifiers.HasFlag(ModifierKeys.Alt))
				{
					MoveBy(blockGrip switch
					{
						BlockGrip.Left => new Vector(0.0 - num, 0.0), 
						BlockGrip.Right => new Vector(num, 0.0), 
						BlockGrip.Top => new Vector(0.0, 0.0 - num), 
						_ => new Vector(0.0, num), 
					});
				}
				else
				{
					Fit(Contained(_rect, BlockEdit.Grow(_rect, blockGrip, modifiers.HasFlag(ModifierKeys.Shift) ? (0.0 - num) : num, 24.0)));
					ScheduleSettle();
				}
				return true;
			}
			if (key == Key.Return)
			{
				DropAt(DropPoint());
				return true;
			}
		}
		return base.OnKeyDown(key, modifiers);
	}

	private void PickUp(Point at)
	{
		if (_moving == null)
		{
			_moving = _spaces.PickUp(at, Handle);
			if (_moving == null)
			{
				Hint = PickHint;
				Cursor = SystemCursors.Hand;
				return;
			}
			_rect = _moving.Bounds;
			Hint = FollowHint;
			Cursor = SystemCursors.SizeAll;
			Layout();
		}
	}

	private void MoveBy(Vector offset)
	{
		if (_moving != null)
		{
			_carried += offset;
			Fit(Offset(_rect, offset));
		}
	}

	private void Fit(System.Windows.Rect rect)
	{
		if (_moving != null)
		{
			_rect = rect;
			_spaces.Fit(_moving, rect, _carried);
			Layout();
		}
	}

	private void Layout()
	{
		bool shown = _moving != null;
		System.Windows.Rect rect = new System.Windows.Rect(_rect.X - Origin.X, _rect.Y - Origin.Y, _rect.Width, _rect.Height);
		double scale = Dpi.Scale;
		float num = (float)Math.Round(rect.Width * scale);
		float num2 = (float)Math.Round(rect.Height * scale);
		Place(_sides[0], new Point(rect.Left - 0.5, rect.Top - 0.5), new RawRectF(0f, 0f, num + (float)scale, (float)scale));
		Place(_sides[1], new Point(rect.Left - 0.5, rect.Bottom - 0.5), new RawRectF(0f, 0f, num + (float)scale, (float)scale));
		Place(_sides[2], new Point(rect.Left - 0.5, rect.Top - 0.5), new RawRectF(0f, 0f, (float)scale, num2 + (float)scale));
		Place(_sides[3], new Point(rect.Right - 0.5, rect.Top - 0.5), new RawRectF(0f, 0f, (float)scale, num2 + (float)scale));
		OverlaySprite[] sides = _sides;
		for (int i = 0; i < sides.Length; i++)
		{
			sides[i].Shown = shown;
		}
		for (int j = 0; j < Grips.Length; j++)
		{
			BlockGrip blockGrip = Grips[j];
			double x;
			if (blockGrip.HasFlag(BlockGrip.Left))
			{
				x = rect.Left;
			}
			else
			{
				x = (blockGrip.HasFlag(BlockGrip.Right) ? rect.Right : (rect.Left + rect.Width / 2.0));
			}
			double y;
			if (blockGrip.HasFlag(BlockGrip.Top))
			{
				y = rect.Top;
			}
			else
			{
				y = (blockGrip.HasFlag(BlockGrip.Bottom) ? rect.Bottom : (rect.Top + rect.Height / 2.0));
			}
			_arrows[j].CentreAt(new Point(x, y));
			_arrows[j].Shown = shown;
		}
		_arrowOpacity.SetOpacity(_placed ? 1f : 0.75f);
		Gpu.Invalidate();
		string text = $"{Math.Round(_rect.Width)} × {Math.Round(_rect.Height)}";
		if (text != _sizeText)
		{
			_sizeText = text;
			OverlayText.Repaint(Gpu, ref _sizePicture, _size, text, 12f);
		}
		_size.MoveTo(new Point(rect.X, rect.Bottom + 9.0 + 4.0));
		_size.Shown = shown;
	}

	private void Place(OverlaySprite side, Point at, RawRectF clip)
	{
		side.MoveTo(at);
		side.Visual.SetClip(clip);
	}

	private OverlayPicture Arrow(double angle)
	{
		if (!_arrowPictures.TryGetValue(angle, out OverlayPicture value))
		{
			value = (_arrowPictures[angle] = ArrowHandle.Picture(Gpu, angle));
		}
		return value;
	}

	private static void DashedLine(ID2D1DeviceContext context, double length, bool horizontal)
	{
		using ID2D1SolidColorBrush brush = context.CreateSolidColorBrush(FrameInk);
		for (double num = 0.0; num < length; num += 7.0)
		{
			float num2 = (float)num;
			float num3 = (float)Math.Min(length, num + 4.0);
			context.FillRectangle(horizontal ? new RawRectF(num2, 0f, num3, 1f) : new RawRectF(0f, num2, 1f, num3), brush);
		}
	}

	private Point DropPoint()
	{
		Point point = CursorOnScreen();
		if (!_rect.Contains(point))
		{
			return new Point(_rect.X + _rect.Width / 2.0, _rect.Y + _rect.Height / 2.0);
		}
		return point;
	}

	private void DropAt(Point at)
	{
		SpaceManager.MovingSpace moving = _moving;
		_moving = null;
		_spaces.Drop(moving, at);
		Dismiss();
	}

	private static System.Windows.Rect Offset(System.Windows.Rect rect, Vector by)
	{
		rect.Offset(by);
		return rect;
	}

	private Point ToScreen(Point onSurface)
	{
		return onSurface + Origin;
	}

	private Point CursorOnScreen()
	{
		return CursorOnSurface() + Origin;
	}
}
