using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using InfiniteSpace.Rendering;
using InfiniteSpace.Spaces;
using Vortice.Direct2D1;
using Vortice.DirectComposition;
using Vortice.Mathematics;

namespace InfiniteSpace.Shell;

internal sealed class CanvasOverlay : GpuOverlay
{
	private const double EraserRadius = 22.0;

	private const double MinPointSpacing = 1.0;

	private const double LineWidth = 2.0;

	private const double FillRate = 60.0;

	private static readonly Color4 PreviewFill = new Color4(0f, 0f, 0f, 0.6f);

	private static readonly Color4 EraserInk = new Color4(1f, 109f / 255f, 168f / 255f);

	private static readonly Color4 OutlineInk = new Color4(1f, 1f, 1f, 0.8f);

	private static readonly Color4 OutlineFill = new Color4(1f, 1f, 1f, 2f / 15f);

	private readonly SpaceManager _spaces;

	private readonly nint _host;

	private readonly List<Point> _points = new List<Point>();

	private readonly OverlayLayer? _existing;

	private readonly OverlayLayer? _fill;

	private readonly OverlayLayer _stroke;

	private readonly IDCompositionEffectGroup? _trailOpacity;

	private readonly OverlaySprite? _brush;

	private readonly OverlayPicture? _brushPicture;

	private ID2D1StrokeStyle? _round;

	private ID2D1StrokeStyle? _dashed;

	private int _drawn;

	private System.Windows.Rect _filled = System.Windows.Rect.Empty;

	private bool _fillStale;

	private double _lastFill = double.NegativeInfinity;

	private System.Windows.Rect _existingArea = System.Windows.Rect.Empty;

	private bool _drawing;

	public OverlayMode Mode { get; }

	public override bool WantsFrame
	{
		get
		{
			if (_drawn >= _points.Count)
			{
				return _fillStale;
			}
			return true;
		}
	}

	internal IReadOnlyList<Point> Points => _points;

	private double Width
	{
		get
		{
			if (Mode != OverlayMode.Draw)
			{
				return 44.0;
			}
			return 2.0;
		}
	}

	public CanvasOverlay(OverlayMode mode, SpaceManager spaces, Universe universe, nint host, System.Windows.Rect? bounds = null)
		: base(universe, (mode == OverlayMode.Draw) ? "Draw any shape to open a space   ·   Esc to cancel" : "Drag over spaces to erase them   ·   Esc or right-click when done", bounds)
	{
		Mode = mode;
		_spaces = spaces;
		_host = host;
		Cursor = ((mode == OverlayMode.Draw) ? SystemCursors.Cross : IntPtr.Zero);
		if (mode == OverlayMode.Draw)
		{
			_fill = new OverlayLayer(Gpu, Layers, PixelWidth, PixelHeight);
			_stroke = new OverlayLayer(Gpu, Layers, PixelWidth, PixelHeight);
			return;
		}
		_existing = new OverlayLayer(Gpu, Layers, PixelWidth, PixelHeight);
		_stroke = new OverlayLayer(Gpu, Layers, PixelWidth, PixelHeight);
		Gpu.Composition.CreateEffectGroup(out _trailOpacity).CheckError();
		_trailOpacity.SetOpacity(0.4f);
		_stroke.Visual.SetEffect(_trailOpacity);
		double size = 46.0;
		_brushPicture = OverlayPicture.Paint(Gpu, size, size, (ID2D1DeviceContext context) =>
		{
			using ID2D1SolidColorBrush brush = context.CreateSolidColorBrush(new Color4(1f, 1f, 1f));
			context.DrawEllipse(new Ellipse(new Vector2((float)(size / 2.0), (float)(size / 2.0)), 21.25f, 21.25f), brush, 1.5f);
		});
		_brush = new OverlaySprite(Gpu, Layers, _brushPicture);
		RefreshExisting();
	}

	protected override void OnMouseDown(Point at, int clickCount)
	{
		_drawing = true;
		ResetStroke();
		AddPoint(at);
		CaptureMouse();
	}

	protected override void OnMouseMove(Point at)
	{
		if (_brush != null)
		{
			_brush.CentreAt(at);
			_brush.Shown = true;
		}
		if (_drawing)
		{
			AddPoint(at);
		}
	}

	protected override void OnMouseUp(Point at)
	{
		if (_drawing)
		{
			_drawing = false;
			ReleaseMouseCapture();
			Commit();
		}
	}

	protected override void OnCaptureLost()
	{
		if (_drawing)
		{
			_drawing = false;
			ResetStroke();
		}
	}

	protected override bool OnKeyDown(Key key, ModifierKeys modifiers)
	{
		if (key == Key.Return && Mode == OverlayMode.Erase)
		{
			Dismiss();
			return true;
		}
		return base.OnKeyDown(key, modifiers);
	}

	public override void RenderFrame(FrameState frame)
	{
		if (_drawn < _points.Count)
		{
			DrawNewPieces();
		}
		if (_fillStale && _fill != null && (frame.Time - _lastFill >= 0.0125 || !_drawing))
		{
			_lastFill = frame.Time;
			DrawFill();
		}
	}

	protected override void DisposeParts()
	{
		_round?.Dispose();
		_dashed?.Dispose();
		_existing?.Dispose();
		_fill?.Dispose();
		_stroke.Dispose();
		_trailOpacity?.Dispose();
		_brush?.Dispose();
		_brushPicture?.Dispose();
	}

	private void AddPoint(Point p)
	{
		if (_points.Count > 0)
		{
			List<Point> points = _points;
			if ((p - points[points.Count - 1]).Length < 1.0)
			{
				return;
			}
		}
		_points.Add(p);
		_fillStale = Mode == OverlayMode.Draw;
	}

	private void DrawNewPieces()
	{
		int num = Math.Max(0, _drawn - 1);
		System.Windows.Rect area = System.Windows.Rect.Empty;
		for (int i = num; i < _points.Count; i++)
		{
			area.Union(_points[i]);
		}
		double reach = Width / 2.0 + 1.0;
		area.Inflate(reach, reach);
		_drawn = _points.Count;
		_stroke.Draw(area, (ID2D1DeviceContext context) =>
		{
			using ID2D1SolidColorBrush brush = context.CreateSolidColorBrush((Mode == OverlayMode.Draw) ? new Color4(1f, 1f, 1f) : EraserInk);
			ID2D1StrokeStyle strokeStyle = Round();
			float num2 = (float)Width;
			if (_points.Count == 1)
			{
				context.FillEllipse(new Ellipse(V(_points[0]), num2 / 2f, num2 / 2f), brush);
			}
			else
			{
				for (int j = 1; j < _points.Count; j++)
				{
					Point p = _points[j - 1];
					Point p2 = _points[j];
					if (!(Math.Max(p.X, p2.X) + reach < area.Left) && !(Math.Min(p.X, p2.X) - reach > area.Right) && !(Math.Max(p.Y, p2.Y) + reach < area.Top) && !(Math.Min(p.Y, p2.Y) - reach > area.Bottom))
					{
						context.DrawLine(V(p), V(p2), brush, num2, strokeStyle);
					}
				}
			}
		});
	}

	private void DrawFill()
	{
		_fillStale = false;
		System.Windows.Rect empty = System.Windows.Rect.Empty;
		foreach (Point point in _points)
		{
			empty.Union(point);
		}
		empty.Inflate(1.0, 1.0);
		System.Windows.Rect filled = _filled;
		filled.Union(empty);
		_filled = empty;
		if (_points.Count < 3)
		{
			_fill.Draw(filled, (ID2D1DeviceContext _) =>
			{
			});
			return;
		}
		_fill.Draw(filled, (ID2D1DeviceContext context) =>
		{
			using ID2D1PathGeometry1 iD2D1PathGeometry = Gpu.D2DFactory.CreatePathGeometry();
			using (ID2D1GeometrySink iD2D1GeometrySink = iD2D1PathGeometry.Open())
			{
				iD2D1GeometrySink.SetFillMode(FillMode.Winding);
				iD2D1GeometrySink.BeginFigure(V(_points[0]), FigureBegin.Filled);
				for (int i = 1; i < _points.Count; i++)
				{
					iD2D1GeometrySink.AddLine(V(_points[i]));
				}
				iD2D1GeometrySink.EndFigure(FigureEnd.Closed);
				iD2D1GeometrySink.Close();
			}
			using ID2D1SolidColorBrush brush = context.CreateSolidColorBrush(PreviewFill);
			context.FillGeometry(iD2D1PathGeometry, brush);
		});
	}

	private void Commit()
	{
		if (Mode == OverlayMode.Draw)
		{
			if (_points.Count >= 3 && _spaces.Create(SpaceGeometry.FromStroke(_points, Origin), _host))
			{
				Dismiss();
				return;
			}
			Hint = "Too small to hold a space, draw a bigger shape   ·   Esc to cancel";
			ResetStroke();
			return;
		}
		if (_points.Count > 0)
		{
			_spaces.Erase(SpaceGeometry.EraserStroke(_points, Origin, 22.0));
		}
		ResetStroke();
		RefreshExisting();
	}

	private void ResetStroke()
	{
		_points.Clear();
		_drawn = 0;
		_stroke.Clear();
		if (_fill != null && !_filled.IsEmpty)
		{
			_fill.Clear();
			_filled = System.Windows.Rect.Empty;
		}
		_fillStale = false;
	}

	private void RefreshExisting()
	{
		if (_existing == null)
		{
			return;
		}
		List<(Geometry Geometry, System.Windows.Rect Bounds)> outlines = _spaces.Outlines.Select((Geometry o) => (Geometry: o, Bounds: o.Bounds)).ToList();
		System.Windows.Rect empty = System.Windows.Rect.Empty;
		foreach (var item2 in outlines)
		{
			System.Windows.Rect item = item2.Bounds;
			empty.Union(new System.Windows.Rect(item.X - Origin.X, item.Y - Origin.Y, item.Width, item.Height));
		}
		empty.Inflate(2.0, 2.0);
		System.Windows.Rect existingArea = _existingArea;
		existingArea.Union(empty);
		_existingArea = empty;
		_existing.Draw(existingArea, (ID2D1DeviceContext context) =>
		{
			using ID2D1SolidColorBrush brush = context.CreateSolidColorBrush(OutlineInk);
			using ID2D1SolidColorBrush brush2 = context.CreateSolidColorBrush(OutlineFill);
			ID2D1Factory1 d2DFactory = Gpu.D2DFactory;
			if ((object)_dashed == null)
			{
				_dashed = d2DFactory.CreateStrokeStyle(new StrokeStyleProperties
				{
					DashStyle = Vortice.Direct2D1.DashStyle.Custom,
					LineJoin = LineJoin.Round
				}, new float[2] { 4f, 3f });
			}
			foreach (var item3 in outlines)
			{
				using ID2D1PathGeometry geometry = Outline2D.From(item3.Geometry, (Point)Origin).ToPath(d2DFactory);
				context.FillGeometry(geometry, brush2);
				context.DrawGeometry(geometry, brush, 1.5f, _dashed);
			}
		});
	}

	private ID2D1StrokeStyle Round()
	{
		return _round ?? (_round = Gpu.D2DFactory.CreateStrokeStyle(new StrokeStyleProperties
		{
			StartCap = CapStyle.Round,
			EndCap = CapStyle.Round,
			LineJoin = LineJoin.Round
		}));
	}

	private static Vector2 V(Point p)
	{
		return new Vector2((float)p.X, (float)p.Y);
	}
}
