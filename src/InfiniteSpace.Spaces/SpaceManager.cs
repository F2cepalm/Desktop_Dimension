using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using InfiniteSpace.Native;
using InfiniteSpace.Rendering;
using Microsoft.Extensions.Logging;
using Vortice.DirectWrite;

namespace InfiniteSpace.Spaces;

internal sealed class SpaceManager(SpaceStore store, Universe universe, ILogger<SpaceManager> logger) : IDisposable
{
	internal sealed record SpacePart(SpaceRecord Record, PathGeometry Outline);

	internal enum Hover
	{
		None,
		Space,
		Note
	}

	internal sealed class MovingSpace
	{
		internal Space Space { get; }

		internal Matrix Before { get; }

		internal Rect Bounds { get; }

		internal Rect Now { get; set; }

		internal Vector Carried { get; set; }

		internal MovingSpace(Space space, Matrix before, Rect bounds)
		{
			Space = space;
			Before = before;
			Bounds = (Now = bounds);
		}

		internal Point PlaceNote(TextNote note)
		{
			return SpaceManager.PlaceNote(NoteText.Bounds(note, Before), Bounds, Now, Carried);
		}

		internal Matrix PlaceStroke(InkStroke stroke)
		{
			Matrix result = stroke.Placed(Before, Space?.Record.Anchor);
			result.Translate(Carried.X, Carried.Y);
			return result;
		}
	}

	internal sealed class Space(SpaceRecord record, PathGeometry shape, SpaceWindow window, Matrix toScreen)
	{
		private PathGeometry? _outline;

		private Matrix _outlineFor;

		public SpaceRecord Record { get; } = record;

		public PathGeometry Shape
		{
			get;
			set
			{
				field = value;
				_outline = null;
			}
		} = shape;

		public PathGeometry Outline
		{
			get
			{
				if (_outline == null || _outlineFor != ToScreen)
				{
					_outline = SpaceGeometry.Transform(Shape, ToScreen);
					_outlineFor = ToScreen;
				}
				return _outline;
			}
		}

		public Matrix ToScreen { get; set; } = toScreen;

		public Matrix Shown { get; set; } = toScreen;

		public bool Stretched { get; set; }

		public DateTime LastResize { get; set; }

		public bool Following { get; set; }

		public DateTime LastChange { get; set; }

		public bool Moving { get; set; }

		public Func<TextNote, Point>? NotePlacement { get; set; }

		public Func<InkStroke, Matrix>? StrokePlacement { get; set; }

		public SpaceWindow Window { get; } = window;

		public Stack<IInk> Redo { get; } = new Stack<IInk>();
	}

	private static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(150L);

	private static readonly TimeSpan LiveResizeGap = TimeSpan.FromMilliseconds(300L);

	private static readonly TimeSpan LookoutInterval = TimeSpan.FromSeconds(2L);

	private const double NoteReach = 4.0;

	private readonly List<Space> _spaces = new List<Space>();

	private readonly List<SpaceRecord> _kept = new List<SpaceRecord>();

	private readonly Dictionary<nint, Guid> _windowIds = new Dictionary<nint, Guid>();

	private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;

	private DispatcherTimer? _settle;

	private DispatcherTimer? _lookout;

	private DispatcherTimer? _lookSoon;

	private TextEntry? _textEntry;

	private const double SmallestFileType = 11.0;

	private const double LargestFileType = 20.0;

	private const double FileStep = 28.0;

	private const int MaxDroppedText = 1048576;

	private const double DropMargin = 12.0;

	internal const double SmallestDroppedType = 10.0;

	private const double DroppedLineEms = 26.0;

	public int Count => _spaces.Count + _kept.Count;

	public int KeptCount => _kept.Count;

	public string Theme { get; private set; } = "Dreamy";

	public InkPen Pen { get; private set; } = InkPen.Default;

	internal IEnumerable<SpaceWindow> Windows => _spaces.Select((Space s) => s.Window);

	internal IEnumerable<SpaceRecord> Records => _spaces.Select((Space s) => s.Record);

	public IEnumerable<Geometry> Outlines => ((IEnumerable<Space>)_spaces).Select((Func<Space, Geometry>)((Space s) => s.Outline));

	public bool IsTyping => _textEntry != null;

	public event Action<InkPen>? PenChanged;

	public event Action<Point>? PaletteRequested;

	public event Action? PaintingStarted;

	public event Action<string, string>? Notice;

	public void ApplyTheme(string theme)
	{
		Theme = Themes.Resolve(theme);
		foreach (Space space in _spaces)
		{
			space.Window.SetTheme(Theme);
		}
		universe.Gpu.Commit();
	}

	public void SetPen(InkPen pen)
	{
		if (pen == Pen)
		{
			return;
		}
		Pen = pen;
		foreach (Space space in _spaces)
		{
			space.Window.Pen = pen;
		}
		PenChanged?.Invoke(pen);
	}

	public void RestoreAll()
	{
		foreach (SpaceRecord item in store.Load())
		{
			if (SpaceGeometry.Decode(item).Figures.Count == 0)
			{
				continue;
			}
			HostInfo host = item.Host;
			if (host != null)
			{
				string app = host.App;
				if (app != null && app.Length > 0)
				{
					_kept.Add(item);
					continue;
				}
			}
			Open(item, IntPtr.Zero, Matrix.Identity);
		}
		ReturnKept();
		logger.LogInformation("Restored {Count} space(s) from {File}; {Kept} wait for their app's window", Count, store.FilePath, _kept.Count);
		_lookout = new DispatcherTimer(LookoutInterval, DispatcherPriority.Background, (object? _, EventArgs _) =>
		{
			Lookout();
		}, _dispatcher);
		_lookout.Start();
	}

	public bool Create(Geometry outline, nint host)
	{
		if (SpaceGeometry.Area(outline) < 300.0)
		{
			return false;
		}
		SpaceRecord spaceRecord = new SpaceRecord
		{
			Figures = SpaceGeometry.Encode(outline, out var fillRule),
			Host = Describe(host)
		};
		spaceRecord.FillRule = fillRule;
		Place(spaceRecord, host);
		Persist();
		return true;
	}

	internal static bool SameLayer(nint hostA, nint hostB)
	{
		return hostA == hostB;
	}

	internal static SpaceRecord Merge(IReadOnlyList<SpacePart> parts)
	{
		if (parts.Count == 1)
		{
			return parts[0].Record;
		}
		SpaceRecord record = parts[0].Record;
		List<Geometry> list = new List<Geometry>();
		list.AddRange(((IEnumerable<SpacePart>)parts).Select((Func<SpacePart, Geometry>)((SpacePart p) => p.Outline)));
		Geometry geometry = SpaceGeometry.Union(new _003C_003Ez__ReadOnlyList<Geometry>(list));
		FillRule fillRule;
		return new SpaceRecord
		{
			Id = record.Id,
			CreatedAt = record.CreatedAt,
			Figures = SpaceGeometry.Encode(geometry, out fillRule),
			Strokes = parts.SelectMany((SpacePart p) => p.Record.Strokes).ToList(),
			Notes = parts.SelectMany((SpacePart p) => p.Record.Notes).ToList(),
			FillRule = fillRule
		};
	}

	public bool RemoveAt(Point point)
	{
		List<Space> list = _spaces.Where((Space s) => s.Window.IsShowing).ToList();
		List<Geometry> list2 = new List<Geometry>();
		list2.AddRange(((IEnumerable<Space>)list).Select((Func<Space, Geometry>)((Space s) => s.Outline)));
		int num = IndexToRemove(new _003C_003Ez__ReadOnlyList<Geometry>(list2), point);
		if (num < 0)
		{
			return false;
		}
		Space space = list[num];
		CloseWindow(space);
		_spaces.Remove(space);
		Persist();
		return true;
	}

	internal static int IndexToRemove(IReadOnlyList<Geometry> outlines, Point point)
	{
		for (int num = outlines.Count - 1; num >= 0; num--)
		{
			if (outlines[num].FillContains(point))
			{
				return num;
			}
		}
		return outlines.Count - 1;
	}

	public int Erase(Geometry eraser)
	{
		Rect bounds = eraser.Bounds;
		int num = 0;
		foreach (Space item in _spaces.ToList())
		{
			if (item.Window.IsShowing && item.Outline.Bounds.IntersectsWith(bounds))
			{
				double num2 = SpaceGeometry.Area(item.Outline);
				PathGeometry pathGeometry = SpaceGeometry.Subtract(item.Outline, eraser);
				if (pathGeometry == null)
				{
					CloseWindow(item);
					_spaces.Remove(item);
					num++;
				}
				else if (!(num2 - SpaceGeometry.Area(pathGeometry) < 1.0))
				{
					Bake(item);
					item.Record.Figures = SpaceGeometry.Encode(pathGeometry, out var fillRule);
					item.Record.FillRule = fillRule;
					item.Shape = SpaceGeometry.Decode(item.Record);
					item.Window.SetShape(item.Outline);
					num++;
				}
			}
		}
		if (num > 0)
		{
			Persist();
		}
		return num;
	}

	public void EraseAll()
	{
		foreach (Space space in _spaces)
		{
			CloseWindow(space);
		}
		_spaces.Clear();
		_kept.Clear();
		Persist();
	}

	public void Dispose()
	{
		_textEntry?.Commit();
		_settle?.Stop();
		_lookout?.Stop();
		_lookSoon?.Stop();
		Persist();
		foreach (Space space in _spaces)
		{
			CloseWindow(space);
		}
		_spaces.Clear();
	}

	public void CommitTyping(bool fade = false)
	{
		if (fade)
		{
			_textEntry?.CommitAndFade();
		}
		else
		{
			_textEntry?.Commit();
		}
	}

	public nint SpaceUnderCursor()
	{
		Point at;
		return HoveredSpace(out at)?.Window.Handle ?? IntPtr.Zero;
	}

	public Hover HoverAtCursor()
	{
		Space space = HoveredSpace(out var at);
		if (space != null)
		{
			if (NoteAt(space, at) != null)
			{
				return Hover.Note;
			}
			return Hover.Space;
		}
		return Hover.None;
	}

	public bool TypeAtCursor()
	{
		Space space = HoveredSpace(out var at);
		if (space == null)
		{
			return false;
		}
		BeginText(space, at, null);
		return true;
	}

	public bool EditAtCursor()
	{
		Space space = HoveredSpace(out var at);
		if (space != null)
		{
			TextNote textNote = NoteAt(space, at);
			if (textNote != null)
			{
				BeginText(space, at, textNote);
				return true;
			}
		}
		return false;
	}

	private Space? HoveredSpace(out Point at)
	{
		at = default;
		if (!Win32.GetCursorPos(out var point))
		{
			return null;
		}
		at = new Point((double)point.X / Dpi.Scale, (double)point.Y / Dpi.Scale);
		Space space = Find(Win32.WindowFromPoint(point));
		if (space == null || !space.Window.Contains(at))
		{
			return null;
		}
		return space;
	}

	private static TextNote? NoteAt(Space space, Point at)
	{
		TextNote textNote = null;
		foreach (TextNote note in space.Record.Notes)
		{
			if (note.Text.Length != 0 && (textNote == null || note.Order >= textNote.Order))
			{
				Rect rect = NoteText.Bounds(note, space.ToScreen);
				rect.Inflate(4.0, 4.0);
				if (rect.Contains(at))
				{
					textNote = note;
				}
			}
		}
		return textNote;
	}

	public void OnWindowEvent(uint eventType, nint window)
	{
		if (eventType == 32770)
		{
			if (_kept.Count > 0 && Win32.GetAncestor(window, 2u) == window)
			{
				LookSoon();
			}
			return;
		}
		foreach (Space space in _spaces)
		{
			if (eventType == 22 && space.Window.Host == window)
			{
				space.Window.HideWithHost();
			}
			else
			{
				space.Window.Restack();
			}
		}
		if (_kept.Count > 0 && eventType != 22)
		{
			LookSoon();
		}
	}

	public void Restack()
	{
		foreach (Space space in _spaces)
		{
			space.Window.Restack();
		}
	}

	public bool Undo(nint window)
	{
		Space space = Find(window);
		IInk ink = space?.Record.TakeNewestInk();
		if (space == null || ink == null)
		{
			return false;
		}
		space.Redo.Push(ink);
		space.Window.RedrawInk();
		Persist();
		return true;
	}

	public bool Redo(nint window)
	{
		Space space = Find(window);
		if (space == null || !space.Redo.TryPop(out IInk result))
		{
			return false;
		}
		space.Record.PutBack(result);
		space.Window.RedrawInk();
		Persist();
		return true;
	}

	public MovingSpace? PickUp(Point at, nint overlay)
	{
		Space space = _spaces.LastOrDefault((Space s) => s.Window.IsShowing && s.Window.Contains(at));
		if (space == null)
		{
			return null;
		}
		if (space.Stretched)
		{
			Reshape(space);
		}
		space.Following = false;
		space.Window.Lift(overlay);
		space.Moving = true;
		MovingSpace movingSpace = new MovingSpace(space, space.ToScreen, space.Outline.Bounds);
		Func<TextNote, Point> notePlacement = (space.Window.NotePlacement = movingSpace.PlaceNote);
		space.NotePlacement = notePlacement;
		Func<InkStroke, Matrix> strokePlacement = (space.Window.StrokePlacement = movingSpace.PlaceStroke);
		space.StrokePlacement = strokePlacement;
		return movingSpace;
	}

	public void Fit(MovingSpace moving, Rect bounds, Vector carried)
	{
		Space space = moving.Space;
		if (!_spaces.Contains(space) || bounds.Width < 1.0 || bounds.Height < 1.0)
		{
			return;
		}
		moving.Now = bounds;
		moving.Carried = carried;
		Matrix matrix = Reshaped(moving.Before, moving.Bounds, bounds);
		if (!space.Stretched && HostFrames.SameScale(space.Shown, matrix) && !space.Window.IsReshaping)
		{
			if (!(matrix == space.Shown))
			{
				Vector offset = new Vector(matrix.OffsetX - space.Shown.OffsetX, matrix.OffsetY - space.Shown.OffsetY);
				Matrix toScreen = (space.Shown = matrix);
				space.ToScreen = toScreen;
				space.Window.Move(SpaceGeometry.Mapped(space.Shape, matrix), offset, matrix);
			}
		}
		else
		{
			space.ToScreen = matrix;
			Matrix shown = space.Shown;
			shown.Invert();
			shown.Append(matrix);
			space.Window.Stretch(shown, matrix);
			space.Stretched = true;
		}
	}

	public void SettleMoving(MovingSpace moving)
	{
		Space space = moving.Space;
		if (_spaces.Contains(space) && space.Stretched)
		{
			Reshape(space);
		}
	}

	internal static Point PlaceNote(Rect note, Rect from, Rect to, Vector carried)
	{
		note.Offset(carried);
		from.Offset(carried);
		return new Point(Push(note.X, note.Width, from.Left, from.Right, to.Left, to.Right), Push(note.Y, note.Height, from.Top, from.Bottom, to.Top, to.Bottom));
		static double Push(double start, double size, double fromLow, double fromHigh, double low, double high)
		{
			double num = Math.Max(0.0, fromLow - start);
			double num2 = Math.Max(0.0, start + size - fromHigh);
			if (start + size > high + num2)
			{
				start = high + num2 - size;
			}
			if (start < low - num)
			{
				start = low - num;
			}
			return start;
		}
	}

	internal static Matrix Reshaped(Matrix before, Rect from, Rect to)
	{
		Matrix result = before;
		result.Append(HostFrames.Map(from, to));
		return result;
	}

	public void Drop(MovingSpace moving, Point at)
	{
		Space space = moving.Space;
		space.Moving = false;
		if (_spaces.Contains(space))
		{
			space.Window.FinishReshape();
			nint host = AppWindows.WindowAt(at);
			Bake(space);
			space.Record.Host = Describe(host);
			List<Space> list = _spaces.Where((Space s) => s != space && SameLayer(s.Window.Host, host) && SpaceGeometry.Overlap(s.Outline, space.Outline)).ToList();
			if (list.Count == 0)
			{
				Rest(space);
				space.Window.SetHost(host);
				space.Window.Land();
			}
			else
			{
				_spaces.Remove(space);
				Place(space.Record, host, list);
				universe.Retire(space.Window);
			}
			Persist();
		}
	}

	public void PutBack(MovingSpace moving)
	{
		Space space = moving.Space;
		space.Moving = false;
		Func<TextNote, Point> notePlacement = (space.Window.NotePlacement = null);
		space.NotePlacement = notePlacement;
		Func<InkStroke, Matrix> strokePlacement = (space.Window.StrokePlacement = null);
		space.StrokePlacement = strokePlacement;
		if (_spaces.Contains(space))
		{
			space.ToScreen = moving.Before;
			HostInfo host = space.Record.Host;
			if (host != null && AppWindows.TryGetFrame(space.Window.Host, out var frame))
			{
				space.ToScreen = HostFrames.Map(host.Frame, frame);
			}
			Reshape(space);
			space.Window.Land();
		}
	}

	private Space? Find(nint window)
	{
		if (window != IntPtr.Zero)
		{
			return _spaces.FirstOrDefault((Space s) => s.Window.Handle == window);
		}
		return null;
	}

	private void Place(SpaceRecord drawn, nint host, List<Space>? crossing = null)
	{
		PathGeometry outline = SpaceGeometry.Decode(drawn);
		List<Space> list = crossing ?? _spaces.Where((Space s) => SameLayer(s.Window.Host, host) && SpaceGeometry.Overlap(s.Outline, outline)).ToList();
		foreach (Space item in list)
		{
			Bake(item);
		}
		SpaceRecord spaceRecord;
		if (list.Count != 0)
		{
			List<SpacePart> list2 = new List<SpacePart>();
			list2.AddRange(list.Select((Space s) => new SpacePart(s.Record, s.Outline)));
			list2.Add(new SpacePart(drawn, outline));
			spaceRecord = Merge(new _003C_003Ez__ReadOnlyList<SpacePart>(list2));
		}
		else
		{
			spaceRecord = drawn;
		}
		SpaceRecord spaceRecord2 = spaceRecord;
		spaceRecord2.Host = drawn.Host;
		Open(spaceRecord2, host, Matrix.Identity);
		foreach (Space item2 in list)
		{
			_spaces.Remove(item2);
			universe.Retire(item2.Window);
		}
	}

	private Space Open(SpaceRecord record, nint host, Matrix toScreen)
	{
		PathGeometry pathGeometry = SpaceGeometry.Decode(record);
		PathGeometry outline = SpaceGeometry.Transform(pathGeometry, toScreen);
		SpaceWindow spaceWindow = new SpaceWindow(record, outline, host, universe.Gpu, Theme, 0, toScreen)
		{
			Pen = Pen
		};
		Space space = new Space(record, pathGeometry, spaceWindow, toScreen);
		spaceWindow.InkChanged += (SpaceWindow _) =>
		{
			space.Redo.Clear();
			Persist();
		};
		spaceWindow.PaletteRequested += (SpaceWindow _, Point at) =>
		{
			PaletteRequested?.Invoke(at);
		};
		spaceWindow.PaintingStarted += (SpaceWindow _) =>
		{
			CommitTyping(fade: true);
			PaintingStarted?.Invoke();
		};
		spaceWindow.HostMoved += (SpaceWindow _, Rect frame) =>
		{
			OnHostMoved(space, frame);
		};
		spaceWindow.FilesDropped += (SpaceWindow _, string[] paths, Point at) =>
		{
			OpenFiles(space, paths, at);
		};
		spaceWindow.Wheel += (SpaceWindow _, Point at, int delta) =>
		{
			OnWheel(space, at, delta);
		};
		spaceWindow.ScrollSettled += (SpaceWindow _) =>
		{
			Persist();
		};
		spaceWindow.NoteClicked = (SpaceWindow _, Point at, NoteClick click) => OnNoteClicked(space, at, click);
		spaceWindow.NoteAt = (SpaceWindow _, Point at) =>
		{
			TextNote textNote = NoteAt(space, at);
			return (textNote == null || textNote.File != null) ? null : textNote;
		};
		spaceWindow.NoteMoved += (SpaceWindow _, TextNote note, Vector by) =>
		{
			MoveNote(space, note, by);
		};
		spaceWindow.TextDropped += (SpaceWindow _, string text, Point at) =>
		{
			DropText(space, text, at);
		};
		spaceWindow.HostClosed += (SpaceWindow _) =>
		{
			_dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() =>
			{
				OnHostClosed(space);
			}));
		};
		_spaces.Add(space);
		universe.Attach(spaceWindow);
		return space;
	}

	private HostInfo? Describe(nint host)
	{
		if (host == IntPtr.Zero || !AppWindows.TryGetFrame(host, out var frame))
		{
			return null;
		}
		AppWindows.Identity? identity = AppWindows.Describe(host);
		if (!_windowIds.TryGetValue(host, out var value))
		{
			value = (_windowIds[host] = Guid.NewGuid());
		}
		return new HostInfo
		{
			App = (identity?.App ?? ""),
			WindowClass = (identity?.WindowClass ?? ""),
			Title = (identity?.Title ?? ""),
			Window = value,
			Frame = frame
		};
	}

	private void OnHostMoved(Space space, Rect frame)
	{
		HostInfo host = space.Record.Host;
		if (host != null && !space.Moving)
		{
			ShowAt(space, HostFrames.Map(host.Frame, frame));
		}
	}

	private void ShowAt(Space space, Matrix toScreen)
	{
		if (toScreen == space.ToScreen)
		{
			return;
		}
		space.ToScreen = toScreen;
		DateTime utcNow = DateTime.UtcNow;
		if (!space.Stretched && HostFrames.SameScale(space.Shown, toScreen))
		{
			Vector offset = new Vector(toScreen.OffsetX - space.Shown.OffsetX, toScreen.OffsetY - space.Shown.OffsetY);
			space.Shown = toScreen;
			space.Window.Move(SpaceGeometry.Mapped(space.Shape, toScreen), offset, toScreen);
			SettleLater(space, utcNow);
			return;
		}
		space.LastResize = utcNow;
		Matrix shown = space.Shown;
		shown.Invert();
		shown.Append(toScreen);
		space.Window.Stretch(shown, toScreen);
		space.Stretched = true;
		SettleLater(space, utcNow);
	}

	private void SettleLater(Space space, DateTime now)
	{
		space.Following = true;
		space.LastChange = now;
		if (_settle == null)
		{
			_settle = new DispatcherTimer(SettleDelay, DispatcherPriority.Background, (object? _, EventArgs _) =>
			{
				Settle();
			}, _dispatcher);
		}
		if (!_settle.IsEnabled)
		{
			_settle.Start();
		}
	}

	private void Settle()
	{
		DateTime utcNow = DateTime.UtcNow;
		bool flag = false;
		bool mouseDown = (Win32.GetAsyncKeyState(1) & 0x8000) != 0;
		foreach (Space space in _spaces)
		{
			if (space.Following && !space.Moving)
			{
				if (mouseDown)
				{
					space.LastChange = utcNow;
					flag = true;
				}
				else if (utcNow - space.LastChange < SettleDelay)
				{
					flag = true;
				}
				else
				{
					Rest(space);
				}
			}
		}
		if (!flag)
		{
			_settle?.Stop();
		}
	}

	private static void Rest(Space space)
	{
		space.Following = false;
		if (space.Stretched)
		{
			Reshape(space);
		}
		else
		{
			space.Window.Rest();
		}
	}

	private static void Reshape(Space space)
	{
		space.Stretched = false;
		space.Following = false;
		space.Shown = space.ToScreen;
		space.Window.SetShape(space.Outline, space.ToScreen);
	}

	private void OnHostClosed(Space space)
	{
		if (!_spaces.Contains(space))
		{
			return;
		}
		_windowIds.Remove(space.Window.Host);
		HostInfo host = space.Record.Host;
		if (host != null)
		{
			string app = host.App;
			if (app != null && app.Length > 0)
			{
				CloseWindow(space);
				_spaces.Remove(space);
				_kept.Add(space.Record);
				logger.LogInformation("Keeping a space until {App} opens \"{Title}\" again", Path.GetFileName(host.App), host.Title);
				goto IL_00be;
			}
		}
		Bake(space);
		space.Record.Host = null;
		space.Window.SetHost(IntPtr.Zero);
		goto IL_00be;
		IL_00be:
		Persist();
	}

	private void LookSoon()
	{
		if (_lookSoon == null)
		{
			_lookSoon = new DispatcherTimer(TimeSpan.FromMilliseconds(400L), DispatcherPriority.Background, (object? _, EventArgs _) =>
			{
				_lookSoon.Stop();
				ReturnKept();
			}, _dispatcher);
		}
		if (!_lookSoon.IsEnabled)
		{
			_lookSoon.Start();
		}
	}

	private void Lookout()
	{
		foreach (Space space in _spaces)
		{
			HostInfo host = space.Record.Host;
			if (host != null && space.Window.Host != IntPtr.Zero)
			{
				string text = AppWindows.TitleOf(space.Window.Host);
				if (text.Length > 0)
				{
					host.Title = text;
				}
			}
		}
		ReturnKept();
	}

	private void ReturnKept()
	{
		if (_kept.Count == 0)
		{
			return;
		}
		List<HostMatcher.Waiting> list = (from r in _kept
			where r.Host != null
			group r by r.Host.Window into g
			select g.OrderByDescending((SpaceRecord r) => r.CreatedAt).First().Host into h
			select new HostMatcher.Waiting(h.Window, h.App, h.WindowClass, h.Title)).ToList();
		HashSet<string> hashSet = list.Select((HostMatcher.Waiting w) => w.WindowClass).ToHashSet(StringComparer.Ordinal);
		HashSet<nint> hashSet2 = (from s in _spaces
			select s.Window.Host into h
			where h != IntPtr.Zero
			select h).ToHashSet();
		List<HostMatcher.Open> list2 = new List<HostMatcher.Open>();
		foreach (nint item in AppWindows.OpenWindows())
		{
			if (!hashSet2.Contains(item) && hashSet.Contains(AppWindows.ClassOf(item)))
			{
				AppWindows.Identity? identity = AppWindows.Describe(item);
				if (identity.HasValue)
				{
					AppWindows.Identity valueOrDefault = identity.GetValueOrDefault();
					list2.Add(new HostMatcher.Open(item, valueOrDefault.App, valueOrDefault.WindowClass, valueOrDefault.Title));
				}
			}
		}
		foreach (var item2 in HostMatcher.Match(list, list2, hashSet2))
		{
			var (group, num) = item2;
			if (!AppWindows.TryGetFrame(num, out var frame))
			{
				continue;
			}
			_windowIds[num] = group;
			List<SpaceRecord> list3 = _kept.Where((SpaceRecord r) =>
			{
				HostInfo? host = r.Host;
				return host != null && host.Window == group;
			}).ToList();
			foreach (SpaceRecord item3 in list3)
			{
				_kept.Remove(item3);
				Open(item3, num, HostFrames.Map(item3.Host.Frame, frame));
			}
			logger.LogInformation("Brought back {Count} space(s) on {App} \"{Title}\"", list3.Count, Path.GetFileName(list3[0].Host.App), AppWindows.TitleOf(num));
		}
	}

	private static void Bake(Space space)
	{
		if (space.Stretched)
		{
			Reshape(space);
		}
		BakeMapping(space);
	}

	private static void BakeMapping(Space space)
	{
		Matrix toScreen = space.ToScreen;
		Func<TextNote, Point> notePlacement = space.NotePlacement;
		Func<InkStroke, Matrix> strokePlacement = space.StrokePlacement;
		Func<TextNote, Point> notePlacement2 = (space.Window.NotePlacement = null);
		space.NotePlacement = notePlacement2;
		Func<InkStroke, Matrix> strokePlacement2 = (space.Window.StrokePlacement = null);
		space.StrokePlacement = strokePlacement2;
		if (toScreen.IsIdentity && notePlacement == null && strokePlacement == null)
		{
			return;
		}
		space.Record.Transform(toScreen, notePlacement, strokePlacement);
		foreach (IInk item in space.Redo)
		{
			SpaceRecord.TransformInk(item, toScreen, notePlacement, strokePlacement, space.Record.Anchor);
		}
		HostInfo host = space.Record.Host;
		if (host != null)
		{
			host.Frame = Rect.Transform(host.Frame, toScreen);
		}
		space.Shape = SpaceGeometry.Decode(space.Record);
		Matrix toScreen2 = (space.Shown = Matrix.Identity);
		space.ToScreen = toScreen2;
		space.Window.Rebase(Matrix.Identity);
	}

	private void BeginText(Space space, Point at, TextNote? editing)
	{
		_textEntry?.Commit();
		Point origin;
		TextNote draft;
		if (editing == null)
		{
			double num = Pen.FontSize * NoteText.LineSpacing;
			origin = new Point(Math.Round(at.X, 2), Math.Round(at.Y - num / 2.0, 2));
			draft = new TextNote
			{
				FontSize = Pen.FontSize,
				Color = Pen.Color,
				Bold = Pen.Bold,
				Italic = Pen.Italic
			};
		}
		else
		{
			origin = space.ToScreen.Transform(new Point(editing.X, editing.Y));
			draft = editing;
			space.Window.Editing = editing;
		}
		nint previousApp = Win32.GetForegroundWindow();
		TextEntry entry = new TextEntry(origin, draft, Themes.InkOf(Theme));
		entry.ColorPicked += (string? color) =>
		{
			SetPen(Pen with
			{
				Color = color
			});
		};
		entry.FontSizePicked += (double size) =>
		{
			SetPen(Pen with
			{
				FontSize = size
			});
		};
		entry.StylePicked += (bool bold, bool italic) =>
		{
			SetPen(Pen with
			{
				Bold = bold,
				Italic = italic
			});
		};
		entry.Committed += (TextNote typed) =>
		{
			Keep(space, editing, origin, typed);
		};
		entry.Gone += () =>
		{
			if (_textEntry == entry)
			{
				_textEntry = null;
			}
			if (editing != null)
			{
				foreach (Space item in _spaces.Where((Space s) => s.Window.Editing == editing))
				{
					item.Window.Editing = null;
				}
			}
			Foreground.WhenMouseUp(() =>
			{
				Foreground.RestoreTo(previousApp);
				Restack();
			});
		};
		_textEntry = entry;
		entry.Show();
		entry.Activate();
	}

	private void Keep(Space space, TextNote? editing, Point shownAt, TextNote typed)
	{
		Space space2 = ((editing == null) ? space : _spaces.FirstOrDefault((Space s) => s.Record.Notes.Contains(editing)));
		if (space2 == null || !_spaces.Contains(space2) || (editing == null && typed.Text.Length == 0))
		{
			return;
		}
		Point point = new Point(typed.X, typed.Y);
		if (editing != null && (point - shownAt).Length < 0.5)
		{
			typed.X = editing.X;
			typed.Y = editing.Y;
		}
		else
		{
			Matrix toScreen = space2.ToScreen;
			if (toScreen.HasInverse)
			{
				toScreen.Invert();
			}
			Point point2 = toScreen.Transform(point);
			typed.X = Math.Round(point2.X, 2);
			typed.Y = Math.Round(point2.Y, 2);
		}
		if (editing != null && SameNote(editing, typed))
		{
			return;
		}
		typed.Order = InkOrder.Next();
		if (editing == null)
		{
			space2.Record.Notes.Add(typed);
		}
		else
		{
			if (typed.Text == editing.Text)
			{
				space2.Window.Adopt(editing, typed);
			}
			space2.Record.Replace(editing, typed);
		}
		space2.Redo.Clear();
		if (editing != null && space2.Window.Editing == editing)
		{
			space2.Window.Editing = null;
		}
		else
		{
			space2.Window.RedrawInk();
		}
		Persist();
	}

	internal static bool SameNote(TextNote a, TextNote b)
	{
		if (a.Text == b.Text && a.X == b.X && a.Y == b.Y && a.FontSize == b.FontSize && a.Color == b.Color && a.Width == b.Width && a.Bold == b.Bold)
		{
			return a.Italic == b.Italic;
		}
		return false;
	}

	private void CloseWindow(Space space)
	{
		universe.Detach(space.Window);
		space.Window.Dispose();
	}

	private void Persist()
	{
		try
		{
			store.Save(_spaces.Select((Space s) => s.Record).Concat(_kept));
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) ? true : false)
		{
			logger.LogError(ex, "Could not save spaces to {File}", store.FilePath);
		}
	}

	public bool HasSpaceAt(Point at)
	{
		return SpaceAt(at) != null;
	}

	public int OpenFiles(IReadOnlyList<string> paths, Point at)
	{
		Space space = SpaceAt(at);
		if (space == null)
		{
			return 0;
		}
		return OpenFiles(space, paths, at);
	}

	private Space? SpaceAt(Point at)
	{
		return _spaces.LastOrDefault((Space s) => s.Window.IsShowing && s.Window.Contains(at));
	}

	private int OpenFiles(Space space, IReadOnlyList<string> paths, Point at)
	{
		List<string> list = new List<string>();
		List<(TextNote, Point)> list2 = new List<(TextNote, Point)>();
		Rect bounds = space.Outline.Bounds;
		foreach (string path in paths)
		{
			string fileName = Path.GetFileName(path);
			if (!TextFiles.IsSupported(path))
			{
				list.Add(fileName + " is not a text (.txt) or Markdown (.md) file.");
				continue;
			}
			string text;
			try
			{
				text = TextFiles.Read(path).TrimEnd();
			}
			catch (TextFileException ex)
			{
				list.Add(ex.Message);
				continue;
			}
			if (string.IsNullOrWhiteSpace(text))
			{
				list.Add(fileName + " is empty.");
				continue;
			}
			Point point = NextPlace(list2, at, bounds);
			TextNote textNote = new TextNote
			{
				Text = text,
				FontSize = Math.Clamp(Pen.FontSize, 11.0, 20.0),
				Color = Pen.Color,
				Width = Math.Round(Math.Clamp(bounds.Right - point.X - 8.0, 200.0, 420.0), 2),
				File = new NoteFile
				{
					Path = path,
					Markdown = TextFiles.IsMarkdown(path)
				}
			};
			double firstHeight;
			double val;
			using (NoteDocument noteDocument = NoteDocument.Build(textNote))
			{
				firstHeight = noteDocument.FirstHeight;
				val = noteDocument.HeaderHeight + 3.0 * textNote.FontSize * NoteText.LineSpacing;
				NoteText.Remember(textNote, noteDocument);
			}
			textNote.File.Height = Math.Round(Math.Min(firstHeight, Math.Max(val, bounds.Bottom - point.Y - 8.0)), 2);
			Matrix toScreen = space.ToScreen;
			if (toScreen.HasInverse)
			{
				toScreen.Invert();
			}
			Point point2 = toScreen.Transform(point);
			TextNote textNote2 = textNote;
			double x = Math.Round(point2.X, 2);
			double y = Math.Round(point2.Y, 2);
			textNote.X = x;
			textNote2.Y = y;
			textNote.Order = InkOrder.Next();
			space.Record.Notes.Add(textNote);
			list2.Add((textNote, point));
		}
		if (list2.Count > 0)
		{
			space.Redo.Clear();
			space.Window.RedrawInk();
			foreach (var (note, at2) in list2)
			{
				space.Window.Arrive(note, at2);
			}
			Persist();
		}
		if (list.Count > 0)
		{
			Notice?.Invoke((list.Count == 1 && list2.Count == 0) ? "That file wasn't opened" : "Some files weren't opened", string.Join("\n", list));
		}
		return list2.Count;
	}

	private void MoveNote(Space space, TextNote note, Vector by)
	{
		if (!_spaces.Contains(space) || !space.Record.Notes.Contains(note))
		{
			space.Window.RedrawInk();
			return;
		}
		Matrix toScreen = space.ToScreen;
		if (toScreen.HasInverse)
		{
			toScreen.Invert();
		}
		Point point = toScreen.Transform(space.ToScreen.Transform(new Point(note.X, note.Y)) + by);
		TextNote textNote = new TextNote
		{
			X = Math.Round(point.X, 2),
			Y = Math.Round(point.Y, 2),
			Text = note.Text,
			FontSize = note.FontSize,
			Color = note.Color,
			Width = note.Width,
			Bold = note.Bold,
			Italic = note.Italic,
			File = note.File?.Copy(),
			Order = InkOrder.Next()
		};
		space.Window.Adopt(note, textNote);
		space.Record.Replace(note, textNote);
		space.Redo.Clear();
		space.Window.RedrawInk();
		Persist();
	}

	internal static (Point At, double FontSize, double? Width) FitDroppedText(string text, Rect space, Point at, double fontSize, bool bold, bool italic)
	{
		Rect area = space;
		area.Inflate(-12.0, -12.0);
		if (area.Width < 40.0 || area.Height < 20.0)
		{
			area = space;
		}
		double num = Math.Max(10.0, Math.Min(fontSize, 200.0));
		(double?, Size) tuple = Lay(num);
		if (!Fits(tuple) && num > 10.0)
		{
			double num2 = 10.0;
			double num3 = num;
			(double?, Size) tuple2 = Lay(num2);
			if (Fits(tuple2))
			{
				while (num3 - num2 > 0.5)
				{
					double num4 = Math.Round(num2 + num3) / 2.0;
					if (num4 <= num2 || num4 >= num3)
					{
						break;
					}
					(double?, Size) tuple3 = Lay(num4);
					if (Fits(tuple3))
					{
						tuple2 = tuple3;
						num2 = num4;
					}
					else
					{
						num3 = num4;
					}
				}
			}
			double num5 = num2;
			tuple = tuple2;
			num = num5;
		}
		Size item = tuple.Item2;
		double num6 = num * NoteText.LineSpacing;
		double value = Math.Max(area.Left, Math.Min(at.X, area.Right - item.Width));
		Point item2 = new Point(y: Math.Round(Math.Max(area.Top, Math.Min(at.Y - num6 / 2.0, area.Bottom - item.Height)), 2), x: Math.Round(value, 2));
		double item3 = num;
		var (num7, _) = tuple;
		double? item4;
		if (num7.HasValue)
		{
			double valueOrDefault = num7.GetValueOrDefault();
			item4 = Math.Round(valueOrDefault, 2);
		}
		else
		{
			item4 = null;
		}
		return (At: item2, FontSize: item3, Width: item4);
		bool Fits((double? Width, Size Box) laid)
		{
			if (laid.Box.Width <= area.Width + 0.5)
			{
				return laid.Box.Height <= area.Height + 0.5;
			}
			return false;
		}
		(double? Width, Size Box) Lay(double size)
		{
			double num8 = Math.Min(area.Width, Math.Max(420.0, 26.0 * size));
			using (IDWriteTextLayout iDWriteTextLayout = NoteText.Layout(text, size, null, bold, italic))
			{
				TextMetrics metrics = iDWriteTextLayout.Metrics;
				if ((double)metrics.Width <= num8)
				{
					return (Width: null, Box: new Size(metrics.Width, metrics.Height));
				}
			}
			using IDWriteTextLayout iDWriteTextLayout2 = NoteText.Layout(text, size, num8, bold, italic);
			TextMetrics metrics2 = iDWriteTextLayout2.Metrics;
			return (Width: num8, Box: new Size(Math.Max(metrics2.Width, num8), metrics2.Height));
		}
	}

	private void DropText(Space space, string text, Point at)
	{
		if (!_spaces.Contains(space))
		{
			return;
		}
		text = text.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\0", "")
			.TrimEnd();
		if (text.Trim().Length == 0)
		{
			return;
		}
		if (text.Length > 1048576)
		{
			Notice?.Invoke("That text wasn't dropped", $"It is {text.Length / 1024} K characters; up to {1024} K fit on a space.");
			return;
		}
		(Point, double, double?) tuple = FitDroppedText(text, space.Outline.Bounds, at, Pen.FontSize, Pen.Bold, Pen.Italic);
		Point item = tuple.Item1;
		TextNote textNote = new TextNote
		{
			Text = text,
			FontSize = tuple.Item2,
			Color = Pen.Color,
			Width = tuple.Item3,
			Bold = Pen.Bold,
			Italic = Pen.Italic
		};
		Matrix toScreen = space.ToScreen;
		if (toScreen.HasInverse)
		{
			toScreen.Invert();
		}
		Point point = toScreen.Transform(item);
		TextNote textNote2 = textNote;
		double x = Math.Round(point.X, 2);
		double y = Math.Round(point.Y, 2);
		textNote.X = x;
		textNote2.Y = y;
		textNote.Order = InkOrder.Next();
		space.Record.Notes.Add(textNote);
		space.Redo.Clear();
		space.Window.RedrawInk();
		space.Window.Arrive(textNote, at);
		Persist();
	}

	private static Point NextPlace(List<(TextNote Note, Point At)> opened, Point first, Rect bounds)
	{
		if (opened.Count == 0)
		{
			return first;
		}
		(TextNote Note, Point At) tuple = opened[opened.Count - 1];
		TextNote item = tuple.Note;
		Point item2 = tuple.At;
		Point result = new Point(item2.X + (item.Width ?? 420.0) + 56.0, item2.Y);
		if (result.X + 200.0 <= bounds.Right)
		{
			return result;
		}
		return first + new Vector(28.0, 28.0) * opened.Count;
	}

	private void OnWheel(Space space, Point at, int delta)
	{
		TextNote textNote = FileAt(space, at);
		if (textNote != null)
		{
			NoteFile file = textNote.File;
			if (file != null)
			{
				double num = textNote.FontSize * NoteText.LineSpacing;
				int wheelScrollLines = SystemParameters.WheelScrollLines;
				double num2 = ((wheelScrollLines > 0) ? ((double)wheelScrollLines * num) : (file.Height * 0.8));
				double max = NoteText.MaxScroll(textNote);
				double value = Math.Clamp(space.Window.ScrollTarget(textNote) - (double)delta / 120.0 * num2, 0.0, max);
				space.Window.ScrollTo(textNote, Math.Round(value, 2));
			}
		}
	}

	private bool OnNoteClicked(Space space, Point at, NoteClick click)
	{
		TextNote textNote = FileAt(space, at);
		if (textNote != null)
		{
			NoteFile file = textNote.File;
			if (file != null)
			{
				Point point = space.ToScreen.Transform(new Point(textNote.X, textNote.Y));
				Point at2 = new Point(at.X - point.X, at.Y - point.Y);
				using NoteDocument noteDocument = NoteDocument.Build(textNote);
				if (click == NoteClick.DoubleClick)
				{
					if (!noteDocument.IsOnHeader(at2))
					{
						return false;
					}
					if (!File.Exists(file.Path))
					{
						Notice?.Invoke("The file is gone", file.Path + " was moved or deleted. What was opened stays on the space.");
						return true;
					}
					Launch(file.Path);
					return true;
				}
				string text = noteDocument.LinkAt(at2, Math.Clamp(file.Scroll, 0.0, noteDocument.MaxScroll(file.Height)));
				if (text == null)
				{
					return false;
				}
				Launch(text);
				return true;
			}
		}
		return false;
	}

	private static TextNote? FileAt(Space space, Point at)
	{
		TextNote textNote = null;
		foreach (TextNote note in space.Record.Notes)
		{
			if (note.File != null && note.Text.Length != 0 && (textNote == null || note.Order >= textNote.Order) && NoteText.Bounds(note, space.ToScreen).Contains(at))
			{
				textNote = note;
			}
		}
		return textNote;
	}

	private void Launch(string target)
	{
		try
		{
			Process.Start(new ProcessStartInfo(target)
			{
				UseShellExecute = true
			});
		}
		catch (Exception ex) when ((ex is Win32Exception || ex is InvalidOperationException || ex is FileNotFoundException) ? true : false)
		{
			Notice?.Invoke("Couldn't open it", ex.Message);
		}
	}
}
