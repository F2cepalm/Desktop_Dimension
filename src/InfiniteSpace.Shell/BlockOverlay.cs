using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using InfiniteSpace.Rendering;
using InfiniteSpace.Spaces;
using Vortice;
using Vortice.Direct2D1;
using Vortice.Mathematics;

namespace InfiniteSpace.Shell;

internal sealed class BlockOverlay : GpuOverlay
{
	private const double DefaultWidth = 240.0;

	private const double DefaultHeight = 160.0;

	private const double MinSize = 24.0;

	private const double Step = 10.0;

	private const double FineStep = 2.0;

	private const double GripMargin = 8.0;

	private const double HandleSize = 8.0;

	private readonly SpaceManager _spaces;

	private readonly nint _host;

	private readonly OverlayPicture _handle;

	private readonly OverlaySprite[] _handles;

	private readonly OverlaySprite _size;

	private OverlayPicture? _sizePicture;

	private string _sizeText = "";

	private SpaceWindow? _preview;

	private System.Windows.Rect _rect;

	private bool _dropped;

	private BlockGrip _drag;

	private Point _last;

	internal System.Windows.Rect Block => new System.Windows.Rect(_rect.X + Origin.X, _rect.Y + Origin.Y, _rect.Width, _rect.Height);

	public BlockOverlay(SpaceManager spaces, Universe universe, nint host, System.Windows.Rect? bounds = null)
		: base(universe, "Move to place, click to drop   ·   arrows grow a side (Shift shrinks, Ctrl fine), wheel scales   ·   drag edges   ·   Enter to create, Esc to cancel", bounds)
	{
		_spaces = spaces;
		_host = host;
		Cursor = SystemCursors.SizeAll;
		_handle = OverlayPicture.Paint(Gpu, 8.0, 8.0, (ID2D1DeviceContext context) =>
		{
			using ID2D1SolidColorBrush brush = context.CreateSolidColorBrush(new Color4(1f, 1f, 1f));
			context.FillRectangle(new RawRectF(0f, 0f, 8f, 8f), brush);
		});
		_handles = (from _ in Enumerable.Range(0, 8)
			select new OverlaySprite(Gpu, Layers, _handle)).ToArray();
		_size = new OverlaySprite(Gpu, Layers);
		_last = CursorOnSurface();
		_rect = new System.Windows.Rect(_last.X - 120.0, _last.Y - 80.0, 240.0, 160.0);
		Layout();
	}

	protected override void OnShown()
	{
		_preview = new SpaceWindow(new SpaceRecord(), Outline(), IntPtr.Zero, Gpu, _spaces.Theme, Handle);
		Universe.Attach(_preview);
	}

	protected override void OnClosing()
	{
		if (_preview != null)
		{
			Universe.Retire(_preview);
			_preview = null;
		}
	}

	protected override void DisposeParts()
	{
		OverlaySprite[] handles = _handles;
		for (int i = 0; i < handles.Length; i++)
		{
			handles[i].Dispose();
		}
		_handle.Dispose();
		_size.Dispose();
		_sizePicture?.Dispose();
	}

	protected override void OnMouseMove(Point p)
	{
		if (!_dropped)
		{
			_rect.Offset(p - _last);
			Layout();
		}
		else if (_drag != BlockGrip.None)
		{
			_rect = Contained(_rect, BlockEdit.Drag(_rect, _drag, p - _last, 24.0), onSurface: true);
			Layout();
		}
		else
		{
			Cursor = SystemCursors.For(BlockEdit.GripAt(_rect, p, 8.0));
		}
		_last = p;
	}

	protected override void OnMouseDown(Point p, int clickCount)
	{
		_last = p;
		if (clickCount == 2 && _rect.Contains(p))
		{
			Create();
			return;
		}
		if (!_dropped)
		{
			_dropped = true;
			Layout();
			Cursor = SystemCursors.For(BlockEdit.GripAt(_rect, p, 8.0));
			return;
		}
		_drag = BlockEdit.GripAt(_rect, p, 8.0);
		if (_drag == BlockGrip.None)
		{
			_rect.Offset(p - new Point(_rect.X + _rect.Width / 2.0, _rect.Y + _rect.Height / 2.0));
			Layout();
		}
		else
		{
			CaptureMouse();
		}
	}

	protected override void OnMouseUp(Point p)
	{
		if (_drag != BlockGrip.None)
		{
			_drag = BlockGrip.None;
			ReleaseMouseCapture();
		}
	}

	protected override void OnCaptureLost()
	{
		_drag = BlockGrip.None;
	}

	protected override void OnWheel(int delta)
	{
		_rect = Contained(_rect, BlockEdit.Scale(_rect, (delta > 0) ? 1.1 : (930.0 / 1023.0), 24.0), onSurface: true);
		Layout();
	}

	protected override bool OnKeyDown(Key key, ModifierKeys modifiers)
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
			if (modifiers.HasFlag(ModifierKeys.Shift))
			{
				num = 0.0 - num;
			}
			_rect = Contained(_rect, BlockEdit.Grow(_rect, blockGrip, num, 24.0), onSurface: true);
			Layout();
			return true;
		}
		if (key == Key.Return)
		{
			Create();
			return true;
		}
		return base.OnKeyDown(key, modifiers);
	}

	private void Create()
	{
		_preview?.FinishReshape();
		if (_spaces.Create(Outline(), _host))
		{
			Dismiss();
		}
	}

	private PathGeometry Outline()
	{
		System.Windows.Rect rect = BlockEdit.Snap(Block, 2.0);
		return SpaceGeometry.FromStroke(new _003C_003Ez__ReadOnlyArray<Point>(new Point[4] { rect.TopLeft, rect.TopRight, rect.BottomRight, rect.BottomLeft }), new Vector(0.0, 0.0));
	}

	private void Layout()
	{
		_preview?.ReshapeLive(Outline(), Matrix.Identity);
		double[] array = new double[3]
		{
			_rect.Left,
			_rect.Left + _rect.Width / 2.0,
			_rect.Right
		};
		double[] array2 = new double[3]
		{
			_rect.Top,
			_rect.Top + _rect.Height / 2.0,
			_rect.Bottom
		};
		int num = 0;
		for (int i = 0; i < 3; i++)
		{
			for (int j = 0; j < 3; j++)
			{
				if (i != 1 || j != 1)
				{
					OverlaySprite obj = _handles[num++];
					obj.CentreAt(new Point(array[j], array2[i]));
					obj.Shown = _dropped;
				}
			}
		}
		string text = $"{Math.Round(_rect.Width)} × {Math.Round(_rect.Height)}";
		if (text != _sizeText)
		{
			_sizeText = text;
			OverlayText.Repaint(Gpu, ref _sizePicture, _size, text, 12f);
		}
		_size.MoveTo(new Point(_rect.X, _rect.Bottom + 12.0));
		_size.Shown = true;
	}
}
