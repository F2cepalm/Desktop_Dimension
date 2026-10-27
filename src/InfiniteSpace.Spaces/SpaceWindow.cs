using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using InfiniteSpace.Native;
using InfiniteSpace.Rendering;

namespace InfiniteSpace.Spaces;

internal sealed class SpaceWindow : IDisposable
{
	private enum Gesture
	{
		None,
		Pressed,
		Painting,
		MovingNote
	}

	[ComVisible(true)]
	internal sealed class DropTarget(SpaceWindow window) : IOleDropTarget
	{
		private const int DROPEFFECT_NONE = 0;

		private const int DROPEFFECT_COPY = 1;

		private bool _takes;

		public int DragEnter(System.Runtime.InteropServices.ComTypes.IDataObject data, int keyState, Win32.POINT point, ref int effect)
		{
			try
			{
				_takes = CanTake(new DataObject(data));
			}
			catch (COMException)
			{
				_takes = false;
			}
			effect = Allowed(effect);
			return 0;
		}

		public int DragOver(int keyState, Win32.POINT point, ref int effect)
		{
			effect = Allowed(effect);
			return 0;
		}

		public int DragLeave()
		{
			_takes = false;
			return 0;
		}

		public int Drop(System.Runtime.InteropServices.ComTypes.IDataObject data, int keyState, Win32.POINT point, ref int effect)
		{
			effect = Allowed(effect);
			_takes = false;
			if (effect == 0)
			{
				return 0;
			}
			try
			{
				string[] paths;
				string text;
				(paths, text) = Read(new DataObject(data));
				window._source.Dispatcher.BeginInvoke((Action)(() =>
				{
					window.OnDrop(paths, text, point);
				}));
			}
			catch (COMException)
			{
				effect = 0;
			}
			return 0;
		}

		private int Allowed(int effects)
		{
			if (!_takes || (effects & 1) == 0)
			{
				return 0;
			}
			return 1;
		}
	}

	[ComImport]
	[Guid("00000122-0000-0000-C000-000000000046")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IOleDropTarget
	{
		[PreserveSig]
		int DragEnter([MarshalAs(UnmanagedType.Interface)] System.Runtime.InteropServices.ComTypes.IDataObject data, int keyState, Win32.POINT point, ref int effect);

		[PreserveSig]
		int DragOver(int keyState, Win32.POINT point, ref int effect);

		[PreserveSig]
		int DragLeave();

		[PreserveSig]
		int Drop([MarshalAs(UnmanagedType.Interface)] System.Runtime.InteropServices.ComTypes.IDataObject data, int keyState, Win32.POINT point, ref int effect);
	}

	private const double RimRoom = 14.0;

	private const double HitMargin = 10.0;

	private const double DragThreshold = 3.0;

	private const double JellyStep = 16.0;

	private const uint PlaceFlags = 8476u;

	private static readonly nint CrossCursor = Win32.LoadCursor(IntPtr.Zero, 32515);

	private static readonly nint MoveCursor = Win32.LoadCursor(IntPtr.Zero, 32646);

	private static readonly Pen RegionPen = Freeze(new Pen(Brushes.Black, 20.0) { LineJoin = PenLineJoin.Round });

	private static T Freeze<T>(T freezable) where T : Freezable
	{
		freezable.Freeze();
		return freezable;
	}

	private readonly HwndSource _source;

	private SpaceSurface _surface;

	private readonly GpuCompositor _gpu;

	private readonly SpaceRecord _record;

	private readonly nint _previewUnder;

	private nint _host;

	private Geometry _outline = Geometry.Empty;

	private ISpaceLook _look;

	private string _theme;

	private InkLayer? _ink;

	private Int32Rect _inkDirty = Int32Rect.Empty;

	private Rect _world;

	private Win32.POINT _position;

	private Win32.SIZE _size;

	private bool _placementChanged;

	private bool _needsPresent;

	private bool _lookDirty = true;

	private Gesture _gesture;

	private Point _pressedAt;

	private List<Point> _stroke = new List<Point>();

	private InkPen _strokePen = InkPen.Default;

	private Color _strokeColor;

	private TextNote? _editing;

	private bool _shown;

	private bool _hidden;

	private bool _disposed;

	private DateTime? _minimizingSince;

	private Matrix _toScreen = Matrix.Identity;

	private Rect _hostFrame = Rect.Empty;

	private bool _following = true;

	private bool _floating;

	private Win32.POINT _floatOrigin;

	private nint _region;

	private bool _regionStale;

	private bool _hostGone;

	private int _pollTick;

	private bool _cachedCloaked;

	private nint _liftedUnder;

	private (Geometry Outline, Matrix ToScreen)? _live;

	private bool _liveChanged;

	private readonly Jelly _jelly = new Jelly();

	private bool _restWhenStill;

	private bool _draft;

	private double _lastLive = double.NaN;

	private const double DraftPause = 0.15;

	private double _jellyTime = double.NaN;

	private FrameState _lastFrame;

	private const double ScrollGlide = 0.07;

	private const double ArrivalFade = 0.3;

	private const int WM_DROPFILES = 563;

	private const int WM_MOUSEWHEEL = 522;

	private const int WM_LBUTTONDBLCLK = 515;

	private const int WM_COPYDATA = 74;

	private const int WM_COPYGLOBALDATA = 73;

	private const int CS_DBLCLKS = 8;

	private readonly Dictionary<TextNote, double> _scrolls = new Dictionary<TextNote, double>();

	private readonly Dictionary<TextNote, double> _arrivals = new Dictionary<TextNote, double>();

	private double _filesTime = double.NaN;

	private TextNote? _pressedNote;

	private TextNote? _movingNote;

	private Vector _noteDrag;

	private Func<TextNote, Point>? _placeMoving;

	private const double DropWobble = 7.0;

	private DropTarget? _dropTarget;

	private const uint MSGFLT_ALLOW = 1u;

	public nint Handle => _source.Handle;

	public nint Host => _host;

	public InkPen Pen { get; set; } = InkPen.Default;

	public Func<TextNote, Point>? NotePlacement { get; set; }

	public Func<InkStroke, Matrix>? StrokePlacement { get; set; }

	public bool IsReshaping
	{
		get
		{
			if (!_liveChanged)
			{
				return _jelly.IsMoving;
			}
			return true;
		}
	}

	public TextNote? Editing
	{
		get
		{
			return _editing;
		}
		set
		{
			if (_editing != value)
			{
				_editing = value;
				RedrawInk();
			}
		}
	}

	public bool WantsEveryFrame
	{
		get
		{
			if (!_disposed)
			{
				if (!_needsPresent && _shown && !_look.HasBursts && !_jelly.IsMoving && !FilesMoving)
				{
					if (_draft)
					{
						(Geometry, Matrix)? live = _live;
						return live.HasValue;
					}
					return false;
				}
				return true;
			}
			return false;
		}
	}

	public bool IsShowing
	{
		get
		{
			if (!_disposed && _shown)
			{
				return !_hidden;
			}
			return false;
		}
	}

	public Func<SpaceWindow, Point, NoteClick, bool>? NoteClicked { get; set; }

	public Func<SpaceWindow, Point, TextNote?>? NoteAt { get; set; }

	private Func<TextNote, Point>? PlaceNotes
	{
		get
		{
			Func<TextNote, Point>? func;
			if (_movingNote != null)
			{
				func = _placeMoving;
				if (func == null)
				{
					return _placeMoving = (TextNote note) =>
					{
						Point point = NotePlacement?.Invoke(note) ?? _toScreen.Transform(new Point(note.X, note.Y));
						return (note != _movingNote) ? point : (point + _noteDrag);
					};
				}
			}
			else
			{
				func = NotePlacement;
			}
			return func;
		}
	}

	private bool FilesMoving
	{
		get
		{
			if (_scrolls.Count <= 0)
			{
				return _arrivals.Count > 0;
			}
			return true;
		}
	}

	internal DropTarget? Drops => _dropTarget;

	public event Action<SpaceWindow>? InkChanged;

	public event Action<SpaceWindow, Point>? PaletteRequested;

	public event Action<SpaceWindow>? PaintingStarted;

	public event Action<SpaceWindow, Rect>? HostMoved;

	public event Action<SpaceWindow>? HostClosed;

	public event Action<SpaceWindow, string[], Point>? FilesDropped;

	public event Action<SpaceWindow, Point, int>? Wheel;

	public event Action<SpaceWindow>? ScrollSettled;

	public event Action<SpaceWindow, TextNote, Vector>? NoteMoved;

	public event Action<SpaceWindow, string, Point>? TextDropped;

	public SpaceWindow(SpaceRecord record, Geometry outline, nint host, GpuCompositor gpu, string theme, nint previewUnder = 0, Matrix toScreen = default(Matrix))
	{
		_record = record;
		_host = host;
		_previewUnder = previewUnder;
		_theme = theme;
		_toScreen = toScreen;
		_look = Themes.CreateLook(theme);
		_source = new HwndSource(new HwndSourceParameters("Infinite Space")
		{
			WindowStyle = int.MinValue,
			ExtendedWindowStyle = 136315008,
			WindowClassStyle = 8,
			Width = 1,
			Height = 1
		});
		_source.AddHook(WndProc);
		if (previewUnder == IntPtr.Zero)
		{
			AcceptFiles();
		}
		int value = 1;
		Win32.DwmSetWindowAttribute(_source.Handle, 33, ref value, 4);
		_gpu = gpu;
		_surface = new SpaceSurface(gpu, _source.Handle);
		SetShape(outline);
	}

	public bool Contains(Point screen)
	{
		return _look.IsInside(screen);
	}

	public void SetShape(Geometry outline, Matrix? toScreen = null)
	{
		if (toScreen.HasValue)
		{
			Matrix valueOrDefault = toScreen.GetValueOrDefault();
			_toScreen = valueOrDefault;
		}
		EndLive();
		bool floating = _floating;
		EndFloat();
		Lay(outline);
		_placementChanged = true;
		ApplyRegion();
		_look.Render(_lastFrame);
		_needsPresent = true;
		if (_shown && !_hidden)
		{
			Present();
			if (floating)
			{
				_surface.CommitNow();
			}
		}
	}

	public void ReshapeLive(Geometry outline, Matrix toScreen)
	{
		if (!_disposed)
		{
			_live = (outline, toScreen);
			_liveChanged = true;
			_jelly.Retarget(outline.Bounds, _theme == "Dreamy" && SystemParameters.ClientAreaAnimation && !_gpu.IsSoftware);
			_needsPresent = true;
		}
	}

	public void FinishReshape()
	{
		if (_disposed)
		{
			return;
		}
		(Geometry, Matrix)? live = _live;
		if (live.HasValue)
		{
			bool isReshaping = IsReshaping;
			_jelly.Stop();
			if (isReshaping)
			{
				DrawLive();
			}
		}
	}

	private void DrawLive(bool draft = true)
	{
		_lastLive = _lastFrame.Time;
		(Geometry Outline, Matrix ToScreen) value = _live.Value;
		Geometry outline = value.Outline;
		Matrix item = value.ToScreen;
		_liveChanged = false;
		if (_jelly.IsMoving)
		{
			Rect shown = _jelly.Shown;
			item.Append(HostFrames.Map(_jelly.Rest, shown));
			outline = SpaceGeometry.Warp(outline, (Point p) => _jelly.Map(p, shown), 6.0);
		}
		else
		{
			_jellyTime = double.NaN;
		}
		_toScreen = item;
		Lay(outline, draft);
		_regionStale = true;
		_look.Render(_lastFrame);
		_needsPresent = true;
		if (!_shown || _hidden)
		{
			_placementChanged = true;
			return;
		}
		Present();
		ShowAt(_world);
	}

	private void EndLive()
	{
		_live = null;
		_liveChanged = false;
		_jelly.Reset();
		_jellyTime = double.NaN;
	}

	private void Lay(Geometry outline, bool draft = false)
	{
		_outline = outline;
		Rect bounds = outline.Bounds;
		bounds.Inflate(14.0, 14.0);
		double num = Math.Floor(bounds.Left / 2.0) * 2.0;
		double num2 = Math.Floor(bounds.Top / 2.0) * 2.0;
		_world = new Rect(num, num2, Math.Ceiling(bounds.Right / 2.0) * 2.0 - num, Math.Ceiling(bounds.Bottom / 2.0) * 2.0 - num2);
		_look.SetShape(outline, _world, draft);
		_draft = draft;
		_lookDirty = true;
		double scale = Dpi.Scale;
		_position = new Win32.POINT
		{
			X = (int)Math.Round(_world.X * scale),
			Y = (int)Math.Round(_world.Y * scale)
		};
		_size = new Win32.SIZE
		{
			Width = Math.Max(1, (int)Math.Round(_world.Width * scale)),
			Height = Math.Max(1, (int)Math.Round(_world.Height * scale))
		};
		if (_record.Strokes.Count == 0 && _record.Notes.Count == 0)
		{
			DropInk();
			return;
		}
		if (_ink == null)
		{
			EnsureInk();
			return;
		}
		_ink.SetShape(_size.Width, _size.Height, Dpi.Scale, _world.TopLeft, outline, draft);
		DrawInk();
	}

	private void DropInk()
	{
		if (_ink != null)
		{
			_ink.Dispose();
			_ink = null;
			_inkDirty = Int32Rect.Empty;
			_surface.HideInk();
		}
	}

	public void SetTheme(string theme)
	{
		if (!_disposed && !(theme == _theme))
		{
			_theme = theme;
			_look.Dispose();
			_look = Themes.CreateLook(theme);
			if (_ink != null)
			{
				_ink.ThemeInk = Themes.InkOf(theme);
				_ink.ThemeAccent = Themes.AccentOf(theme);
			}
			_surface.HideStills();
			SetShape(_outline);
		}
	}

	public void RecreateGraphics()
	{
		if (!_disposed)
		{
			_ink?.Dispose();
			_ink = null;
			_inkDirty = Int32Rect.Empty;
			_surface.Dispose();
			_surface = new SpaceSurface(_gpu, _source.Handle);
			_look.Dispose();
			_look = Themes.CreateLook(_theme);
			EndLive();
			_restWhenStill = false;
			if (_floating)
			{
				EndFloat();
				_placementChanged = true;
			}
			SetShape(_outline);
		}
	}

	public void RedrawInk(bool present = true)
	{
		if (_disposed)
		{
			return;
		}
		if (_record.Strokes.Count == 0 && _record.Notes.Count == 0)
		{
			DropInk();
			return;
		}
		if (_ink == null)
		{
			EnsureInk();
		}
		else
		{
			DrawInk();
		}
		if (present && _shown && !_hidden)
		{
			Present();
		}
	}

	private void DrawInk()
	{
		_inkDirty = _ink.Redraw(_record, _toScreen, _editing, PlaceNotes, (_arrivals.Count > 0) ? new Func<TextNote, float>(NoteOpacity) : null, StrokePlacement);
	}

	public void Move(Geometry outline, Vector offset, Matrix toScreen)
	{
		if (!_disposed)
		{
			_outline = outline;
			_toScreen = toScreen;
			_world.Offset(offset);
			_look.Move(outline, _world);
			_ink?.MoveOrigin(offset);
			_jelly.Shift(offset);
			double scale = Dpi.Scale;
			_position = new Win32.POINT
			{
				X = (int)Math.Round(_world.X * scale),
				Y = (int)Math.Round(_world.Y * scale)
			};
			ShowAt(_world);
		}
	}

	public void Stretch(Matrix relative, Matrix toScreen)
	{
		if (!_disposed)
		{
			_toScreen = toScreen;
			ShowAt(Rect.Transform(_world, relative));
			_surface.CommitNow();
		}
	}

	public void Rest()
	{
		if (_disposed)
		{
			return;
		}
		FinishReshape();
		if (_draft)
		{
			(Geometry, Matrix)? live = _live;
			if (live.HasValue)
			{
				DrawLive(draft: false);
			}
		}
		EndLive();
		if (_floating)
		{
			EndFloat();
			Win32.SetWindowPos(_source.Handle, IntPtr.Zero, _position.X, _position.Y, _size.Width, _size.Height, 8476u);
			_surface.CommitNow();
			RestoreRegion();
		}
	}

	private void ShowAt(Rect target)
	{
		if (!_shown || _hidden)
		{
			_placementChanged = true;
			return;
		}
		bool flag = !_floating;
		if (flag)
		{
			Float();
		}
		double scale = Dpi.Scale;
		double num = target.Width * scale / (double)_size.Width;
		double num2 = target.Height * scale / (double)_size.Height;
		if (Math.Abs(num - 1.0) < 0.001 && Math.Abs(num2 - 1.0) < 0.001)
		{
			num = (num2 = 1.0);
		}
		_surface.SetPlacement((float)num, (float)num2, (float)Math.Round(target.X * scale - (double)_floatOrigin.X), (float)Math.Round(target.Y * scale - (double)_floatOrigin.Y));
		if (flag)
		{
			_surface.CommitNow();
		}
	}

	private void Float()
	{
		Win32.RECT rECT = new Win32.RECT
		{
			Left = (int)Math.Floor(SystemParameters.VirtualScreenLeft * Dpi.Scale),
			Top = (int)Math.Floor(SystemParameters.VirtualScreenTop * Dpi.Scale),
			Right = (int)Math.Ceiling((SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth) * Dpi.Scale),
			Bottom = (int)Math.Ceiling((SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight) * Dpi.Scale)
		};
		_floatOrigin = new Win32.POINT
		{
			X = rECT.Left,
			Y = rECT.Top
		};
		_floating = true;
		Win32.SetClickThrough(_source.Handle, clickThrough: true);
		Win32.SetWindowRgn(_source.Handle, IntPtr.Zero, redraw: false);
		Win32.SetWindowPos(_source.Handle, IntPtr.Zero, rECT.Left, rECT.Top, rECT.Right - rECT.Left, rECT.Bottom - rECT.Top, 8476u);
	}

	private void EndFloat()
	{
		if (_floating)
		{
			_floating = false;
			_surface.SetPlacement(1f, 1f, 0f, 0f);
			Win32.SetClickThrough(_source.Handle, clickThrough: false);
		}
	}

	public void Rebase(Matrix toScreen)
	{
		_toScreen = toScreen;
		(Geometry, Matrix)? live = _live;
		if (live.HasValue)
		{
			_live = (live.GetValueOrDefault().Item1, toScreen);
		}
	}

	public void SetHost(nint host)
	{
		if (_disposed)
		{
			return;
		}
		_host = host;
		_hostGone = false;
		_hostFrame = Rect.Empty;
		_minimizingSince = null;
		_pollTick = 0;
		_cachedCloaked = false;
		if (_shown)
		{
			if (_hidden && host == IntPtr.Zero)
			{
				Win32.ShowWindow(_source.Handle, 4);
				_hidden = false;
			}
			if (_liftedUnder == IntPtr.Zero)
			{
				Settle();
			}
		}
	}

	public void Lift(nint under)
	{
		if (!_disposed)
		{
			_liftedUnder = under;
			Win32.SetWindowPos(_source.Handle, Win32.HWND_TOPMOST, 0, 0, 0, 0, 19u);
			Win32.SetWindowPos(_source.Handle, under, 0, 0, 0, 0, 19u);
		}
	}

	public void Land()
	{
		if (!_disposed && _liftedUnder != IntPtr.Zero)
		{
			_liftedUnder = IntPtr.Zero;
			Win32.SetWindowPos(_source.Handle, Win32.HWND_NOTOPMOST, 0, 0, 0, 0, 19u);
			Settle();
		}
	}

	public void Track()
	{
		if (!_disposed)
		{
			_following = FollowHost();
		}
	}

	public void RenderFrame(FrameState frame)
	{
		_lastFrame = frame;
		if (_disposed || !_following)
		{
			return;
		}
		bool flag = AdvanceFiles(frame);
		(Geometry, Matrix)? live = _live;
		if (live.HasValue && IsReshaping)
		{
			_jelly.Step(double.IsNaN(_jellyTime) ? 0.0 : (frame.Time - _jellyTime));
			_jellyTime = frame.Time;
			DrawLive();
			return;
		}
		if (_restWhenStill)
		{
			_restWhenStill = false;
			Rest();
		}
		else if (_draft)
		{
			live = _live;
			if (live.HasValue && frame.Time - _lastLive >= 0.15)
			{
				DrawLive(draft: false);
				return;
			}
		}
		if (flag)
		{
			RedrawInk(present: false);
		}
		if ((frame.AmbientDue || _look.HasBursts || !_shown) && _look.Render(frame))
		{
			_lookDirty = true;
		}
		if (_lookDirty || _needsPresent || !_shown)
		{
			Present();
		}
	}

	public void Restack()
	{
		_minimizingSince = null;
		_pollTick = 0;
		Track();
	}

	public void HideWithHost()
	{
		if (!_disposed && _shown && !_hidden)
		{
			Win32.ShowWindow(_source.Handle, 0);
			_hidden = true;
			_minimizingSince = DateTime.UtcNow;
		}
	}

	private void Present()
	{
		bool flag = false;
		if (_placementChanged && !_floating)
		{
			Win32.SetWindowPos(_source.Handle, IntPtr.Zero, _position.X, _position.Y, _size.Width, _size.Height, 8476u);
			RestoreRegion();
			_placementChanged = false;
		}
		if (_lookDirty)
		{
			_look.Present(_surface);
			_lookDirty = false;
		}
		if (_ink != null && !_inkDirty.IsEmpty)
		{
			_surface.ShowInk(_ink, _inkDirty);
			_inkDirty = Int32Rect.Empty;
		}
		_needsPresent = false;
		if (flag)
		{
			_surface.CommitNow();
		}
		if (!_shown)
		{
			Win32.ShowWindow(_source.Handle, 4);
			if (_previewUnder != IntPtr.Zero)
			{
				Win32.SetWindowPos(_source.Handle, Win32.HWND_TOPMOST, 0, 0, 0, 0, 19u);
				Win32.SetWindowPos(_source.Handle, _previewUnder, 0, 0, 0, 0, 19u);
			}
			else
			{
				Settle();
			}
			_shown = true;
		}
	}

	private bool FollowHost()
	{
		if (_host == IntPtr.Zero)
		{
			return true;
		}
		if (_hostGone)
		{
			return false;
		}
		if (!Win32.IsWindow(_host))
		{
			_hostGone = true;
			Hide();
			HostClosed?.Invoke(this);
			return false;
		}
		DateTime? minimizingSince = _minimizingSince;
		int num;
		if (minimizingSince.HasValue)
		{
			DateTime valueOrDefault = minimizingSince.GetValueOrDefault();
			num = ((DateTime.UtcNow - valueOrDefault < TimeSpan.FromMilliseconds(500L)) ? 1 : 0);
		}
		else
		{
			num = 0;
		}
		bool flag = (byte)num != 0;
		bool poll = _pollTick++ % 15 == 0;
		if (poll)
		{
			_cachedCloaked = AppWindows.IsCloaked(_host);
		}
		if (((!Win32.IsWindowVisible(_host) || Win32.IsIconic(_host)) | flag) || _cachedCloaked)
		{
			Hide();
			return false;
		}
		if (AppWindows.TryGetFrame(_host, out var frame) && frame != _hostFrame)
		{
			_hostFrame = frame;
			HostMoved?.Invoke(this, frame);
			if (_disposed)
			{
				return false;
			}
		}
		if (_hidden)
		{
			Win32.ShowWindow(_source.Handle, 4);
			_hidden = false;
		}
		if (poll && _shown && _liftedUnder == IntPtr.Zero && Win32.GetWindow(_host, 3u) != _source.Handle)
		{
			PlaceAboveHost();
		}
		return true;
	}

	private void Hide()
	{
		if (_shown && !_hidden)
		{
			Win32.ShowWindow(_source.Handle, 0);
			_hidden = true;
		}
	}

	private void Settle()
	{
		if (_host != IntPtr.Zero)
		{
			PlaceAboveHost();
		}
		else
		{
			Win32.SetWindowPos(_source.Handle, Win32.HWND_BOTTOM, 0, 0, 0, 0, 19u);
		}
	}

	private void PlaceAboveHost()
	{
		nint window = Win32.GetWindow(_host, 3u);
		bool flag = window != IntPtr.Zero && (Win32.GetWindowLong(window, -20) & 8) != 0;
		nint hWndInsertAfter = (((window == IntPtr.Zero) | flag) ? Win32.HWND_TOP : window);
		Win32.SetWindowPos(_source.Handle, hWndInsertAfter, 0, 0, 0, 0, 19u);
	}

	private void ApplyRegion()
	{
		_regionStale = false;
		if (_previewUnder != IntPtr.Zero)
		{
			return;
		}
		PathGeometry widenedPathGeometry = _outline.GetWidenedPathGeometry(RegionPen, 2.5, ToleranceType.Absolute);
		PathGeometry flattenedPathGeometry = Geometry.Combine(_outline, widenedPathGeometry, GeometryCombineMode.Union, null, 2.5, ToleranceType.Absolute).GetFlattenedPathGeometry(2.5, ToleranceType.Absolute);
		List<Win32.POINT> points = new List<Win32.POINT>();
		List<int> list = new List<int>();
		foreach (PathFigure figure in flattenedPathGeometry.Figures)
		{
			int count = points.Count;
			Add(figure.StartPoint);
			foreach (PathSegment segment in figure.Segments)
			{
				if (segment is LineSegment lineSegment)
				{
					Add(lineSegment.Point);
				}
				else
				{
					if (!(segment is PolyLineSegment polyLineSegment))
					{
						continue;
					}
					foreach (Point point in polyLineSegment.Points)
					{
						Add(point);
					}
				}
			}
			if (points.Count - count >= 3)
			{
				list.Add(points.Count - count);
			}
			else
			{
				points.RemoveRange(count, points.Count - count);
			}
		}
		if (list.Count == 0)
		{
			return;
		}
		nint num = Win32.CreatePolyPolygonRgn(points.ToArray(), list.ToArray(), list.Count, 2);
		if (num != IntPtr.Zero)
		{
			if (_region != IntPtr.Zero)
			{
				Win32.DeleteObject(_region);
			}
			_region = Copy(num);
			SetRegion(num);
		}
		void Add(Point p)
		{
			points.Add(new Win32.POINT
			{
				X = (int)Math.Round((p.X - _world.X) * Dpi.Scale),
				Y = (int)Math.Round((p.Y - _world.Y) * Dpi.Scale)
			});
		}
	}

	private void RestoreRegion()
	{
		if (_regionStale)
		{
			ApplyRegion();
		}
		else if (_region != IntPtr.Zero)
		{
			SetRegion(Copy(_region));
		}
	}

	private void SetRegion(nint region)
	{
		if (region != IntPtr.Zero && Win32.SetWindowRgn(_source.Handle, region, redraw: true) == 0)
		{
			Win32.DeleteObject(region);
		}
	}

	private static nint Copy(nint region)
	{
		nint num = Win32.CreateRectRgn(0, 0, 0, 0);
		if (num != IntPtr.Zero)
		{
			Win32.CombineRgn(num, region, IntPtr.Zero, 5);
		}
		return num;
	}

	private InkLayer EnsureInk()
	{
		if (_ink == null)
		{
			_ink = new InkLayer(_gpu, _size.Width, _size.Height, Dpi.Scale, _world.TopLeft, _outline, Themes.InkOf(_theme))
			{
				ThemeAccent = Themes.AccentOf(_theme)
			};
			DrawInk();
		}
		return _ink;
	}

	private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
	{
		switch (msg)
		{
		case 70:
			if (_host == IntPtr.Zero && _previewUnder == IntPtr.Zero && _liftedUnder == IntPtr.Zero)
			{
				Win32.WINDOWPOS structure = Marshal.PtrToStructure<Win32.WINDOWPOS>(lParam);
				structure.hwndInsertAfter = Win32.HWND_BOTTOM;
				structure.flags &= 4294967291u;
				Marshal.StructureToPtr(structure, lParam, fDeleteOld: false);
			}
			break;
		case 33:
			handled = true;
			return new IntPtr(3);
		case 32:
			Win32.SetCursor((NoteUnderCursor() == null) ? CrossCursor : MoveCursor);
			handled = true;
			return new IntPtr(1);
		case 513:
		case 515:
		{
			Point point = ToScreen(lParam);
			handled = true;
			if (msg == 515)
			{
				Func<SpaceWindow, Point, NoteClick, bool>? noteClicked = NoteClicked;
				if (noteClicked != null && noteClicked(this, point, NoteClick.DoubleClick))
				{
					break;
				}
			}
			if (Contains(point))
			{
				_gesture = Gesture.Pressed;
				_pressedAt = point;
				_pressedNote = NoteUnder(point);
				Win32.SetCapture(hwnd);
			}
			break;
		}
		case 512:
			if (_gesture != Gesture.None)
			{
				Drag(ToScreen(lParam));
				handled = true;
			}
			break;
		case 514:
		{
			Gesture gesture = _gesture;
			_gesture = Gesture.None;
			Win32.ReleaseCapture();
			switch (gesture)
			{
			case Gesture.Painting:
				FinishStroke();
				break;
			case Gesture.MovingNote:
				FinishNoteMove();
				break;
			case Gesture.Pressed:
				if (IsCtrlDown())
				{
					Func<SpaceWindow, Point, NoteClick, bool>? noteClicked2 = NoteClicked;
					if (noteClicked2 != null && noteClicked2(this, _pressedAt, NoteClick.CtrlClick))
					{
						break;
					}
				}
				_look.AddBurst(_pressedAt);
				break;
			}
			handled = true;
			break;
		}
		case 522:
			Wheel?.Invoke(this, ScreenAt(lParam), (short)((((IntPtr)wParam).ToInt64() >> 16) & 0xFFFF));
			handled = true;
			break;
		case 563:
			OnDropFiles(wParam);
			handled = true;
			break;
		case 516:
			handled = true;
			break;
		case 517:
		{
			Point point2 = ToScreen(lParam);
			if (_gesture == Gesture.None && Contains(point2))
			{
				PaletteRequested?.Invoke(this, point2);
			}
			handled = true;
			break;
		}
		case 533:
			if (_gesture == Gesture.Painting)
			{
				FinishStroke();
			}
			else if (_gesture == Gesture.MovingNote)
			{
				FinishNoteMove();
			}
			_gesture = Gesture.None;
			break;
		}
		return IntPtr.Zero;
	}

	private void Drag(Point at)
	{
		if (_gesture == Gesture.Pressed)
		{
			if ((at - _pressedAt).Length < 3.0)
			{
				return;
			}
			TextNote pressedNote = _pressedNote;
			if (pressedNote != null && _record.Notes.Contains(pressedNote))
			{
				_gesture = Gesture.MovingNote;
				_movingNote = pressedNote;
				PaintingStarted?.Invoke(this);
			}
			else
			{
				_gesture = Gesture.Painting;
			}
		}
		if (_gesture == Gesture.MovingNote)
		{
			_noteDrag = at - _pressedAt;
			RedrawInk();
			return;
		}
		if (_stroke.Count == 0)
		{
			_strokePen = Pen;
			_strokeColor = InkColors.Resolve(_strokePen.Color, Themes.InkOf(_theme));
			int num = 1;
			List<Point> list = new List<Point>(num);
			CollectionsMarshal.SetCount(list, num);
			Span<Point> span = CollectionsMarshal.AsSpan(list);
			int index = 0;
			span[index] = _pressedAt;
			_stroke = list;
			_inkDirty = InkLayer.Union(_inkDirty, EnsureInk().AddSegment(_pressedAt, _pressedAt, _strokePen.Thickness, _strokeColor));
			PaintingStarted?.Invoke(this);
		}
		List<Point> stroke = _stroke;
		Point point = stroke[stroke.Count - 1];
		if (!((at - point).Length < 1.0))
		{
			_stroke.Add(at);
			_inkDirty = InkLayer.Union(_inkDirty, EnsureInk().AddSegment(point, at, _strokePen.Thickness, _strokeColor));
			_needsPresent = true;
		}
	}

	private void FinishStroke()
	{
		if (_stroke.Count != 0)
		{
			Matrix toScreen = _toScreen;
			if (toScreen.HasInverse)
			{
				toScreen.Invert();
			}
			Point point = new Point(_stroke.Min((Point p) => p.X), _stroke.Min((Point p) => p.Y));
			Vector vector = toScreen.Transform(point) - point;
			double[] array = new double[_stroke.Count * 2];
			for (int num = 0; num < _stroke.Count; num++)
			{
				Point point2 = _stroke[num] + vector;
				array[num * 2] = Math.Round(point2.X, 2);
				array[num * 2 + 1] = Math.Round(point2.Y, 2);
			}
			_stroke = new List<Point>();
			_record.Strokes.Add(new InkStroke
			{
				Points = array,
				Thickness = _strokePen.Thickness,
				Color = _strokePen.Color,
				Order = InkOrder.Next()
			});
			InkChanged?.Invoke(this);
		}
	}

	private Point ToScreen(nint lParam)
	{
		long num = ((IntPtr)lParam).ToInt64();
		int num2 = (short)(num & 0xFFFF);
		int num3 = (short)((num >> 16) & 0xFFFF);
		return new Point(_world.X + (double)num2 / Dpi.Scale, _world.Y + (double)num3 / Dpi.Scale);
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
			_look.Dispose();
			_ink?.Dispose();
			_surface.Dispose();
			RefuseDrops();
			_source.RemoveHook(WndProc);
			_source.Dispose();
			if (_region != IntPtr.Zero)
			{
				Win32.DeleteObject(_region);
			}
			_region = IntPtr.Zero;
		}
	}

	private TextNote? NoteUnder(Point at)
	{
		TextNote textNote = NoteAt?.Invoke(this, at);
		if (textNote == null || textNote == _editing)
		{
			return null;
		}
		return textNote;
	}

	private TextNote? NoteUnderCursor()
	{
		if (NoteAt == null || !Win32.GetCursorPos(out var point))
		{
			return null;
		}
		return NoteUnder(new Point((double)point.X / Dpi.Scale, (double)point.Y / Dpi.Scale));
	}

	public void Adopt(TextNote from, TextNote to)
	{
		NoteText.Adopt(from, to);
		_ink?.Adopt(from, to);
		if (_scrolls.Remove(from, out var value))
		{
			_scrolls[to] = value;
		}
	}

	private void FinishNoteMove()
	{
		TextNote movingNote = _movingNote;
		Vector noteDrag = _noteDrag;
		_movingNote = (_pressedNote = null);
		_noteDrag = default;
		if (movingNote != null && noteDrag.Length >= 0.01 && NoteMoved != null)
		{
			NoteMoved(this, movingNote, noteDrag);
		}
		else
		{
			RedrawInk();
		}
	}

	public double ScrollTarget(TextNote note)
	{
		if (!_scrolls.TryGetValue(note, out var value))
		{
			return note.File?.Scroll ?? 0.0;
		}
		return value;
	}

	public void ScrollTo(TextNote note, double target)
	{
		if (note.File != null && !_disposed)
		{
			_scrolls[note] = target;
		}
	}

	public void Arrive(TextNote note, Point at)
	{
		if (!_disposed)
		{
			_look.AddBurst(at);
			_arrivals[note] = double.NaN;
			Wobble();
		}
	}

	private float NoteOpacity(TextNote note)
	{
		if (!_arrivals.TryGetValue(note, out var value) || double.IsNaN(value) || double.IsNaN(_filesTime))
		{
			return (!_arrivals.ContainsKey(note)) ? 1 : 0;
		}
		double num = Math.Clamp((_filesTime - value) / 0.3, 0.0, 1.0);
		return (float)(num * num * (3.0 - 2.0 * num));
	}

	private bool AdvanceFiles(FrameState frame)
	{
		if (!FilesMoving)
		{
			_filesTime = double.NaN;
			return false;
		}
		double num = (double.IsNaN(_filesTime) ? 0.0 : Math.Clamp(frame.Time - _filesTime, 0.0, 0.1));
		_filesTime = frame.Time;
		bool result = false;
		bool flag = false;
		KeyValuePair<TextNote, double>[] array = _scrolls.ToArray();
		TextNote key;
		double value;
		foreach (KeyValuePair<TextNote, double> keyValuePair in array)
		{
			keyValuePair.Deconstruct(out key, out value);
			TextNote textNote = key;
			double num2 = value;
			NoteFile file = textNote.File;
			double num3 = num2 - file.Scroll;
			if (Math.Abs(num3) < 0.25)
			{
				file.Scroll = num2;
				_scrolls.Remove(textNote);
				flag = true;
			}
			else
			{
				file.Scroll += num3 * (1.0 - Math.Exp((0.0 - num) / 0.07));
			}
			result = true;
		}
		array = _arrivals.ToArray();
		foreach (KeyValuePair<TextNote, double> keyValuePair in array)
		{
			keyValuePair.Deconstruct(out key, out value);
			TextNote key2 = key;
			double num4 = value;
			if (double.IsNaN(num4))
			{
				_arrivals[key2] = frame.Time;
			}
			else if (frame.Time - num4 >= 0.3)
			{
				_arrivals.Remove(key2);
			}
			result = true;
		}
		if (flag && _scrolls.Count == 0)
		{
			ScrollSettled?.Invoke(this);
		}
		return result;
	}

	private void Wobble()
	{
		if (!(_theme != "Dreamy") && SystemParameters.ClientAreaAnimation && !_gpu.IsSoftware && _shown && !_hidden && !_floating)
		{
			(Geometry, Matrix)? live = _live;
			if (!live.HasValue && _liftedUnder == IntPtr.Zero)
			{
				_live = (_outline, _toScreen);
				_restWhenStill = true;
				_jelly.Retarget(_outline.Bounds, wobble: false);
				_jelly.Poke(7.0);
				_needsPresent = true;
			}
		}
	}

	private void AcceptFiles()
	{
		if (OleInitialize(IntPtr.Zero) >= 0)
		{
			RevokeDragDrop(Handle);
			_dropTarget = new DropTarget(this);
			if (RegisterDragDrop(Handle, _dropTarget) < 0)
			{
				_dropTarget = null;
			}
		}
		DragAcceptFiles(Handle, accept: true);
		ChangeWindowMessageFilterEx(Handle, 563, 1u, IntPtr.Zero);
		ChangeWindowMessageFilterEx(Handle, 74, 1u, IntPtr.Zero);
		ChangeWindowMessageFilterEx(Handle, 73, 1u, IntPtr.Zero);
	}

	private void OnDropFiles(nint drop)
	{
		try
		{
			uint num = DragQueryFile(drop, uint.MaxValue, null, 0u);
			List<string> list = new List<string>();
			for (uint num2 = 0u; num2 < num; num2++)
			{
				StringBuilder stringBuilder = new StringBuilder((int)(DragQueryFile(drop, num2, null, 0u) + 1));
				if (DragQueryFile(drop, num2, stringBuilder, (uint)stringBuilder.Capacity) != 0)
				{
					list.Add(stringBuilder.ToString());
				}
			}
			DragQueryPoint(drop, out var point);
			ClientToScreen(Handle, ref point);
			Point arg = new Point((double)point.X / Dpi.Scale, (double)point.Y / Dpi.Scale);
			if (list.Count > 0)
			{
				FilesDropped?.Invoke(this, list.ToArray(), arg);
			}
		}
		finally
		{
			DragFinish(drop);
		}
	}

	private void RefuseDrops()
	{
		if (_dropTarget != null)
		{
			RevokeDragDrop(Handle);
			_dropTarget = null;
		}
	}

	private void OnDrop(string[]? paths, string? text, Win32.POINT point)
	{
		Point point2 = new Point((double)point.X / Dpi.Scale, (double)point.Y / Dpi.Scale);
		if (!_disposed && Contains(point2))
		{
			if (paths != null && paths.Length > 0)
			{
				FilesDropped?.Invoke(this, paths, point2);
			}
			else if (!string.IsNullOrEmpty(text))
			{
				TextDropped?.Invoke(this, text, point2);
			}
		}
	}

	private static (string[]? Paths, string? Text) Read(System.Windows.IDataObject data)
	{
		if (!data.GetDataPresent(DataFormats.FileDrop) || !(data.GetData(DataFormats.FileDrop) is string[] array) || array.Length <= 0)
		{
			return (Paths: null, Text: data.GetData(DataFormats.UnicodeText, autoConvert: true) as string);
		}
		return (Paths: array, Text: null);
	}

	private static bool CanTake(System.Windows.IDataObject data)
	{
		if (!data.GetDataPresent(DataFormats.FileDrop))
		{
			return data.GetDataPresent(DataFormats.UnicodeText, autoConvert: true);
		}
		return true;
	}

	[DllImport("ole32.dll")]
	private static extern int OleInitialize(nint reserved);

	[DllImport("ole32.dll")]
	private static extern int RegisterDragDrop(nint window, IOleDropTarget target);

	[DllImport("ole32.dll")]
	private static extern int RevokeDragDrop(nint window);

	private static bool IsCtrlDown()
	{
		return (Win32.GetKeyState(17) & 0x8000) != 0;
	}

	private static Point ScreenAt(nint lParam)
	{
		long num = ((IntPtr)lParam).ToInt64();
		int num2 = (short)(num & 0xFFFF);
		int num3 = (short)((num >> 16) & 0xFFFF);
		return new Point((double)num2 / Dpi.Scale, (double)num3 / Dpi.Scale);
	}

	[DllImport("shell32.dll")]
	private static extern void DragAcceptFiles(nint window, [MarshalAs(UnmanagedType.Bool)] bool accept);

	[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
	private static extern uint DragQueryFile(nint drop, uint index, StringBuilder? file, uint length);

	[DllImport("shell32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool DragQueryPoint(nint drop, out Win32.POINT point);

	[DllImport("shell32.dll")]
	private static extern void DragFinish(nint drop);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool ClientToScreen(nint window, ref Win32.POINT point);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool ChangeWindowMessageFilterEx(nint window, int message, uint action, nint changeFilterStruct);
}
