using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Windows;
using System.Windows.Media;
using InfiniteSpace.Spaces;
using Vortice;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace InfiniteSpace.Rendering;

internal sealed class InkLayer : IDisposable
{
	private readonly record struct Segment(Vector2 From, Vector2 To, float Thickness, System.Windows.Media.Color Color);

	private sealed class CachedDocument(NoteDocument document, System.Windows.Media.Color accent)
	{
		public NoteDocument Document { get; } = document;

		public System.Windows.Media.Color Accent { get; } = accent;

		public int Generation { get; set; }
	}

	private sealed class CachedStroke(ID2D1PathGeometry geometry, double[] points, int revision)
	{
		public ID2D1PathGeometry Geometry { get; } = geometry;

		public double[] Points { get; } = points;

		public int Revision { get; } = revision;

		public int Generation { get; set; }
	}

	private sealed class CachedNote(IDWriteTextLayout layout, string text, double fontSize, double? width, bool bold, bool italic)
	{
		public IDWriteTextLayout Layout { get; } = layout;

		public int Generation { get; set; }

		public bool Matches(TextNote note)
		{
			if (note.Text == text && note.FontSize == fontSize && note.Width == width && note.Bold == bold)
			{
				return note.Italic == italic;
			}
			return false;
		}
	}

	private const int Bleed = 2;

	private const int Step = 256;

	private const float Fade = 0.07f;

	private readonly GpuCompositor _gpu;

	private readonly Dictionary<System.Windows.Media.Color, ID2D1SolidColorBrush> _brushes = new Dictionary<System.Windows.Media.Color, ID2D1SolidColorBrush>();

	private readonly Dictionary<InkStroke, CachedStroke> _strokes = new Dictionary<InkStroke, CachedStroke>();

	private readonly Dictionary<TextNote, CachedNote> _notes = new Dictionary<TextNote, CachedNote>();

	private readonly Dictionary<TextNote, CachedDocument> _documents = new Dictionary<TextNote, CachedDocument>();

	private readonly Dictionary<(bool Top, bool Bottom), ID2D1GradientStopCollection> _fades = new Dictionary<(bool, bool), ID2D1GradientStopCollection>();

	private readonly List<InkStroke> _unusedStrokes = new List<InkStroke>();

	private readonly List<TextNote> _unusedNotes = new List<TextNote>();

	private readonly List<Segment> _pending = new List<Segment>();

	private ID2D1Bitmap1? _ink;

	private ID2D1Bitmap1? _mask;

	private ID2D1StrokeStyle? _round;

	private int _capacityWidth;

	private int _capacityHeight;

	private Point _origin;

	private double _scale = 1.0;

	private Outline2D _outline = Outline2D.Empty;

	private bool _maskStale = true;

	private int _generation;

	private bool _disposed;

	public System.Windows.Media.Color ThemeInk { get; set; }

	public System.Windows.Media.Color ThemeAccent { get; set; } = Themes.AccentOf("Dreamy");

	public int Width { get; private set; }

	public int Height { get; private set; }

	public Int32Rect Bounds => new Int32Rect(0, 0, Width, Height);

	public InkLayer(GpuCompositor gpu, System.Windows.Media.Color ink)
	{
		_gpu = gpu;
		ThemeInk = ink;
	}

	public InkLayer(GpuCompositor gpu, int width, int height, double scale, Point origin, Geometry outline, System.Windows.Media.Color ink)
		: this(gpu, ink)
	{
		SetShape(width, height, scale, origin, outline);
	}

	public void SetShape(int width, int height, double scale, Point origin, Geometry outline, bool draft = false)
	{
		Width = Math.Max(1, width);
		Height = Math.Max(1, height);
		_scale = scale;
		_origin = origin;
		_outline = Outline2D.From(outline, origin, draft ? 1.5 : 0.25);
		_maskStale = true;
		_pending.Clear();
		EnsureCapacity();
	}

	public void MoveOrigin(System.Windows.Vector offset)
	{
		_origin += offset;
	}

	public Int32Rect Redraw(SpaceRecord record, Matrix toScreen = default(Matrix), TextNote? skip = null, Func<TextNote, Point>? placeNote = null, Func<TextNote, float>? opacity = null, Func<InkStroke, Matrix>? placeStroke = null)
	{
		_pending.Clear();
		_generation++;
		ID2D1DeviceContext iD2D1DeviceContext = Begin(_ink);
		iD2D1DeviceContext.PushAxisAlignedClip(new RawRectF(0f, 0f, Width, Height), AntialiasMode.Aliased);
		iD2D1DeviceContext.Clear(new Color4(0f, 0f, 0f, 0f));
		Matrix3x2 world = World();
		foreach (InkStroke stroke in record.Strokes)
		{
			DrawStroke(iD2D1DeviceContext, stroke, placeStroke?.Invoke(stroke) ?? stroke.Placed(toScreen, record.Anchor), world);
		}
		foreach (TextNote note in record.Notes)
		{
			if (note == skip || note.Text.Length == 0)
			{
				continue;
			}
			float num = opacity?.Invoke(note) ?? 1f;
			if (!(num <= 0f))
			{
				if (num < 1f)
				{
					iD2D1DeviceContext.Transform = Matrix3x2.Identity;
					iD2D1DeviceContext.PushLayer(new LayerParameters1
					{
						ContentBounds = new RawRectF(0f, 0f, Width, Height),
						MaskTransform = Matrix3x2.Identity,
						Opacity = num
					}, null);
				}
				DrawNote(iD2D1DeviceContext, note, placeNote?.Invoke(note) ?? toScreen.Transform(new Point(note.X, note.Y)), world);
				if (num < 1f)
				{
					iD2D1DeviceContext.PopLayer();
				}
			}
		}
		iD2D1DeviceContext.PopAxisAlignedClip();
		End(iD2D1DeviceContext);
		ForgetUnused();
		return Bounds;
	}

	public Int32Rect AddSegment(Point from, Point to, double thickness, System.Windows.Media.Color? color = null)
	{
		Vector2 vector = new Vector2((float)(from.X - _origin.X), (float)(from.Y - _origin.Y));
		Vector2 to2 = new Vector2((float)(to.X - _origin.X), (float)(to.Y - _origin.Y));
		double num = thickness / 2.0;
		int num2 = Math.Clamp((int)Math.Floor(((double)Math.Min(vector.X, to2.X) - num) * _scale) - 2, 0, Width);
		int num3 = Math.Clamp((int)Math.Floor(((double)Math.Min(vector.Y, to2.Y) - num) * _scale) - 2, 0, Height);
		int num4 = Math.Clamp((int)Math.Ceiling(((double)Math.Max(vector.X, to2.X) + num) * _scale) + 2, 0, Width);
		int num5 = Math.Clamp((int)Math.Ceiling(((double)Math.Max(vector.Y, to2.Y) + num) * _scale) + 2, 0, Height);
		if (num4 <= num2 || num5 <= num3)
		{
			return Int32Rect.Empty;
		}
		_pending.Add(new Segment(vector, to2, (float)thickness, color ?? ThemeInk));
		return new Int32Rect(num2, num3, num4 - num2, num5 - num3);
	}

	public void Prepare()
	{
		EnsureMask();
		if (_pending.Count == 0)
		{
			return;
		}
		ID2D1DeviceContext iD2D1DeviceContext = Begin(_ink);
		iD2D1DeviceContext.Transform = Matrix3x2.CreateScale((float)_scale);
		foreach (Segment item in _pending)
		{
			ID2D1SolidColorBrush brush = Brush(iD2D1DeviceContext, item.Color);
			if (item.From == item.To)
			{
				iD2D1DeviceContext.FillEllipse(new Ellipse(item.From, item.Thickness / 2f, item.Thickness / 2f), brush);
			}
			else
			{
				iD2D1DeviceContext.DrawLine(item.From, item.To, brush, item.Thickness, Round());
			}
		}
		End(iD2D1DeviceContext);
		_pending.Clear();
	}

	public void Compose(ID2D1DeviceContext context, Int32Rect area, Vector2 at)
	{
		context.Transform = Matrix3x2.CreateTranslation(at.X - (float)area.X, at.Y - (float)area.Y);
		RawRectF rawRectF = new RawRectF(area.X, area.Y, area.X + area.Width, area.Y + area.Height);
		context.PushAxisAlignedClip(rawRectF, AntialiasMode.Aliased);
		context.Clear(new Color4(0f, 0f, 0f, 0f));
		Vector2 value = new Vector2(area.X, area.Y);
		context.DrawImage(_ink, value, rawRectF, InterpolationMode.NearestNeighbor, CompositeMode.SourceCopy);
		context.DrawImage(_mask, value, rawRectF, InterpolationMode.NearestNeighbor, CompositeMode.DestinationIn);
		context.PopAxisAlignedClip();
		context.Transform = Matrix3x2.Identity;
	}

	public int[] ReadPixels()
	{
		Prepare();
		using ID2D1Bitmap1 source = _gpu.Paint(Width, Height, 1f, (ID2D1DeviceContext context, ID2D1Factory1 _) =>
		{
			Compose(context, Bounds, Vector2.Zero);
		});
		return _gpu.ReadPixels(source);
	}

	public static Int32Rect Union(Int32Rect a, Int32Rect b)
	{
		if (a.IsEmpty || a.Width == 0 || a.Height == 0)
		{
			return b;
		}
		if (b.IsEmpty || b.Width == 0 || b.Height == 0)
		{
			return a;
		}
		int num = Math.Min(a.X, b.X);
		int num2 = Math.Min(a.Y, b.Y);
		int num3 = Math.Max(a.X + a.Width, b.X + b.Width);
		int num4 = Math.Max(a.Y + a.Height, b.Y + b.Height);
		return new Int32Rect(num, num2, num3 - num, num4 - num2);
	}

	public void Adopt(TextNote from, TextNote to)
	{
		if (_notes.Remove(from, out CachedNote value))
		{
			_notes[to] = value;
		}
		if (_documents.Remove(from, out CachedDocument value2))
		{
			_documents[to] = value2;
		}
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}
		_disposed = true;
		foreach (ID2D1SolidColorBrush value in _brushes.Values)
		{
			value.Dispose();
		}
		_brushes.Clear();
		foreach (CachedStroke value2 in _strokes.Values)
		{
			value2.Geometry.Dispose();
		}
		_strokes.Clear();
		foreach (CachedNote value3 in _notes.Values)
		{
			value3.Layout.Dispose();
		}
		_notes.Clear();
		foreach (CachedDocument value4 in _documents.Values)
		{
			value4.Document.Dispose();
		}
		_documents.Clear();
		foreach (ID2D1GradientStopCollection value5 in _fades.Values)
		{
			value5.Dispose();
		}
		_fades.Clear();
		_round?.Dispose();
		_ink?.Dispose();
		_mask?.Dispose();
	}

	private Matrix3x2 World()
	{
		return Matrix3x2.CreateTranslation((float)(0.0 - _origin.X), (float)(0.0 - _origin.Y)) * Matrix3x2.CreateScale((float)_scale);
	}

	private void EnsureCapacity()
	{
		bool num = (object)_ink != null && _capacityWidth >= Width && _capacityHeight >= Height;
		bool flag = (long)_capacityWidth * (long)_capacityHeight > 2 * Math.Max(65536L, (long)Width * (long)Height);
		if (!num || flag)
		{
			int val = (int)Math.Min(16384u, _gpu.D2DContext.MaximumBitmapSize);
			_capacityWidth = Math.Min(val, Headroom.Size(Width, _capacityWidth));
			_capacityHeight = Math.Min(val, Headroom.Size(Height, _capacityHeight));
			Width = Math.Min(Width, _capacityWidth);
			Height = Math.Min(Height, _capacityHeight);
			_ink?.Dispose();
			_mask?.Dispose();
			SizeI size = new SizeI(_capacityWidth, _capacityHeight);
			_ink = _gpu.D2DContext.CreateBitmap(size, IntPtr.Zero, 0u, new BitmapProperties1(GpuCompositor.Bgra, 96f, 96f, BitmapOptions.Target));
			_mask = _gpu.D2DContext.CreateBitmap(size, IntPtr.Zero, 0u, new BitmapProperties1(GpuCompositor.Bgra, 96f, 96f, BitmapOptions.Target));
			_maskStale = true;
		}
	}

	private void EnsureMask()
	{
		if (!_maskStale)
		{
			return;
		}
		_maskStale = false;
		ID2D1DeviceContext iD2D1DeviceContext = Begin(_mask);
		iD2D1DeviceContext.Clear(new Color4(0f, 0f, 0f, 0f));
		if (_outline.Figures.Length != 0)
		{
			using ID2D1PathGeometry geometry = _outline.ToPath(_gpu.D2DFactory);
			iD2D1DeviceContext.Transform = Matrix3x2.CreateScale((float)_scale);
			iD2D1DeviceContext.FillGeometry(geometry, Brush(iD2D1DeviceContext, System.Windows.Media.Colors.White));
		}
		End(iD2D1DeviceContext);
	}

	private ID2D1DeviceContext Begin(ID2D1Bitmap1 target)
	{
		ID2D1DeviceContext d2DContext = _gpu.D2DContext;
		d2DContext.Target = target;
		d2DContext.SetDpi(96f, 96f);
		d2DContext.BeginDraw();
		d2DContext.Transform = Matrix3x2.Identity;
		d2DContext.AntialiasMode = AntialiasMode.PerPrimitive;
		d2DContext.TextAntialiasMode = Vortice.Direct2D1.TextAntialiasMode.Grayscale;
		return d2DContext;
	}

	private static void End(ID2D1DeviceContext context)
	{
		context.Transform = Matrix3x2.Identity;
		context.EndDraw().CheckError();
		context.Target = null;
	}

	private void DrawStroke(ID2D1DeviceContext context, InkStroke stroke, Matrix toScreen, Matrix3x2 world)
	{
		double[] points = stroke.Points;
		if (points.Length < 2)
		{
			return;
		}
		ID2D1SolidColorBrush brush = Brush(context, InkColors.Resolve(stroke.Color, ThemeInk));
		float num = (float)stroke.Thickness;
		if (points.Length < 4)
		{
			Point point = toScreen.Transform(new Point(points[0], points[1]));
			context.Transform = world;
			context.FillEllipse(new Ellipse(new Vector2((float)point.X, (float)point.Y), num / 2f, num / 2f), brush);
			return;
		}
		ID2D1PathGeometry iD2D1PathGeometry = Geometry(stroke);
		if (toScreen.M11 == 1.0 && toScreen.M22 == 1.0 && toScreen.M12 == 0.0 && toScreen.M21 == 0.0)
		{
			context.Transform = Matrix3x2.CreateTranslation((float)toScreen.OffsetX, (float)toScreen.OffsetY) * world;
			context.DrawGeometry(iD2D1PathGeometry, brush, num, Round());
			return;
		}
		Matrix3x2 transform = new Matrix3x2((float)toScreen.M11, (float)toScreen.M12, (float)toScreen.M21, (float)toScreen.M22, (float)toScreen.OffsetX, (float)toScreen.OffsetY);
		using ID2D1TransformedGeometry geometry = _gpu.D2DFactory.CreateTransformedGeometry(iD2D1PathGeometry, transform);
		context.Transform = world;
		context.DrawGeometry(geometry, brush, num, Round());
	}

	private void DrawNote(ID2D1DeviceContext context, TextNote note, Point at, Matrix3x2 world)
	{
		if (note.File != null)
		{
			DrawFile(context, note, at, world);
			return;
		}
		if (!_notes.TryGetValue(note, out CachedNote value) || !value.Matches(note))
		{
			value?.Layout.Dispose();
			value = new CachedNote(NoteText.Layout(note), note.Text, note.FontSize, note.Width, note.Bold, note.Italic);
			_notes[note] = value;
		}
		value.Generation = _generation;
		context.Transform = Matrix3x2.CreateTranslation((float)at.X, (float)at.Y) * world;
		context.DrawTextLayout(Vector2.Zero, value.Layout, Brush(context, InkColors.Resolve(note.Color, ThemeInk)), DrawTextOptions.EnableColorFont);
	}

	private void DrawFile(ID2D1DeviceContext context, TextNote note, Point at, Matrix3x2 world)
	{
		NoteFile file = note.File;
		NoteDocument noteDocument = Document(note);
		System.Windows.Media.Color color = InkColors.Resolve(note.Color, ThemeInk);
		ID2D1SolidColorBrush ink = Brush(context, color);
		ID2D1SolidColorBrush defaultFillBrush = Brush(context, System.Windows.Media.Color.FromArgb(160, color.R, color.G, color.B));
		Matrix3x2 matrix3x = Matrix3x2.CreateTranslation((float)at.X, (float)at.Y) * world;
		float num = (float)noteDocument.Width;
		float num2 = (float)Math.Max(noteDocument.HeaderHeight, file.Height);
		context.Transform = matrix3x;
		float num3 = (float)(note.FontSize * 0.6);
		System.Windows.Media.Color color2 = (InkColors.IsDark(color) ? System.Windows.Media.Color.FromArgb(112, byte.MaxValue, byte.MaxValue, byte.MaxValue) : System.Windows.Media.Color.FromArgb(128, 0, 0, 0));
		context.FillRoundedRectangle(new RoundedRectangle(new RawRectF(0f - num3, (0f - num3) * 0.8f, num + num3, num2 + num3 * 0.6f), num3, num3), Brush(context, color2));
		context.PushAxisAlignedClip(new RawRectF(0f, 0f, num, (float)noteDocument.HeaderHeight), AntialiasMode.Aliased);
		context.DrawTextLayout(Vector2.Zero, noteDocument.Header, defaultFillBrush, DrawTextOptions.None);
		context.PopAxisAlignedClip();
		float num4 = (float)noteDocument.RuleY;
		context.FillRectangle(new RawRectF(0f, num4, num, num4 + 1f), Brush(context, System.Windows.Media.Color.FromArgb(80, color.R, color.G, color.B)));
		float num5 = (float)noteDocument.HeaderHeight;
		double num6 = num2 - num5;
		double num7 = Math.Clamp(file.Scroll, 0.0, noteDocument.MaxScroll(num2));
		if (num6 <= 0.0)
		{
			return;
		}
		context.PushAxisAlignedClip(new RawRectF(0f, num5, num, num2), AntialiasMode.Aliased);
		bool flag = num7 > 0.5;
		bool flag2 = num7 < noteDocument.MaxScroll(num2) - 0.5;
		ID2D1LinearGradientBrush iD2D1LinearGradientBrush = null;
		if (flag | flag2)
		{
			iD2D1LinearGradientBrush = context.CreateLinearGradientBrush(new LinearGradientBrushProperties(new Vector2(0f, num5), new Vector2(0f, num2)), Fades(context, flag, flag2));
			context.PushLayer(new LayerParameters1
			{
				ContentBounds = new RawRectF(0f, num5, num, num2),
				MaskTransform = Matrix3x2.Identity,
				Opacity = 1f,
				OpacityBrush = iD2D1LinearGradientBrush
			}, null);
		}
		context.Transform = Matrix3x2.CreateTranslation(0f, (float)((double)num5 - num7)) * matrix3x;
		ID2D1SolidColorBrush accent = Brush(context, ThemeAccent);
		foreach (LaidParagraph paragraph in noteDocument.Paragraphs)
		{
			if (!(paragraph.Top + paragraph.Height < num7))
			{
				if (paragraph.Top > num7 + num6)
				{
					break;
				}
				DrawParagraph(context, paragraph, note.FontSize, ink, accent, color);
			}
		}
		context.Transform = matrix3x;
		if ((object)iD2D1LinearGradientBrush != null)
		{
			context.PopLayer();
			iD2D1LinearGradientBrush.Dispose();
		}
		context.PopAxisAlignedClip();
	}

	private void DrawParagraph(ID2D1DeviceContext context, LaidParagraph paragraph, double fontSize, ID2D1Brush ink, ID2D1Brush accent, System.Windows.Media.Color inkColor)
	{
		float num = (float)paragraph.Left;
		float num2 = (float)paragraph.Top;
		float num3 = (float)fontSize;
		NoteParagraph source = paragraph.Source;
		for (int i = 0; i < source.Quote; i++)
		{
			float num4 = num - (float)(source.Quote - i) * num3 * 1.1f + num3 * 0.25f;
			context.FillRectangle(new RawRectF(num4, num2 - num3 * 0.15f, num4 + Math.Max(2f, num3 * 0.16f), num2 + (float)paragraph.Height + num3 * 0.15f), Brush(context, System.Windows.Media.Color.FromArgb(153, ThemeAccent.R, ThemeAccent.G, ThemeAccent.B)));
		}
		switch (source.Kind)
		{
		case ParagraphKind.Rule:
		{
			float num5 = num2 + (float)paragraph.Height / 2f;
			context.FillRectangle(new RawRectF(num, num5, num + (float)paragraph.Width, num5 + 1f), Brush(context, System.Windows.Media.Color.FromArgb(96, inkColor.R, inkColor.G, inkColor.B)));
			return;
		}
		case ParagraphKind.Code:
			context.FillRoundedRectangle(new RoundedRectangle(new RawRectF(num, num2, num + (float)paragraph.Width, num2 + (float)paragraph.Height), num3 * 0.3f, num3 * 0.3f), Brush(context, System.Windows.Media.Color.FromArgb(36, inkColor.R, inkColor.G, inkColor.B)));
			break;
		}
		if ((object)paragraph.Layout == null)
		{
			return;
		}
		float num6 = num + (float)paragraph.Padding;
		float num7 = (float)paragraph.TextTop;
		if (source.Kind == ParagraphKind.Text && source.Runs.Any((StyledRun r) => r.Style.HasFlag(TextStyle.Code)))
		{
			if (paragraph.CodeBackings == null)
			{
				List<System.Windows.Rect> list = (paragraph.CodeBackings = CodeBackings(paragraph));
			}
			ID2D1SolidColorBrush brush = Brush(context, System.Windows.Media.Color.FromArgb(36, inkColor.R, inkColor.G, inkColor.B));
			foreach (System.Windows.Rect codeBacking in paragraph.CodeBackings)
			{
				context.FillRoundedRectangle(new RoundedRectangle(new RawRectF(num6 + (float)codeBacking.Left - 2f, num7 + (float)codeBacking.Top, num6 + (float)codeBacking.Right + 2f, num7 + (float)codeBacking.Bottom), 3f, 3f), brush);
			}
		}
		if ((object)paragraph.Marker != null)
		{
			context.DrawTextLayout(new Vector2(num - num3 * 1.6f - num3 * 0.35f, num7), paragraph.Marker, ink, DrawTextOptions.None);
		}
		context.DrawTextLayout(new Vector2(num6, num7), paragraph.Layout, (source.Kind == ParagraphKind.Heading) ? accent : ink, DrawTextOptions.EnableColorFont);
	}

	private static List<System.Windows.Rect> CodeBackings(LaidParagraph paragraph)
	{
		List<System.Windows.Rect> list = new List<System.Windows.Rect>();
		HitTestMetrics[] array = new HitTestMetrics[32];
		foreach (StyledRun run in paragraph.Source.Runs)
		{
			if (run.Style.HasFlag(TextStyle.Code) && run.Length != 0)
			{
				paragraph.Layout.HitTestTextRange((uint)run.Start, (uint)run.Length, 0f, 0f, array, out var actualHitTestMetricsCount);
				for (int i = 0; i < Math.Min(actualHitTestMetricsCount, (uint)array.Length); i++)
				{
					list.Add(new System.Windows.Rect(array[i].Left, array[i].Top, array[i].Width, array[i].Height));
				}
			}
		}
		return list;
	}

	private NoteDocument Document(TextNote note)
	{
		if (_documents.TryGetValue(note, out CachedDocument value) && value.Document.Matches(note) && value.Accent == ThemeAccent)
		{
			value.Generation = _generation;
			return value.Document;
		}
		value?.Document.Dispose();
		ID2D1DeviceContext d2DContext = _gpu.D2DContext;
		NoteDocument noteDocument = NoteDocument.Build(note, Brush(d2DContext, ThemeAccent));
		_documents[note] = new CachedDocument(noteDocument, ThemeAccent)
		{
			Generation = _generation
		};
		return noteDocument;
	}

	private ID2D1GradientStopCollection Fades(ID2D1DeviceContext context, bool top, bool bottom)
	{
		if (!_fades.TryGetValue((top, bottom), out ID2D1GradientStopCollection value))
		{
			value = context.CreateGradientStopCollection(new Vortice.Direct2D1.GradientStop[4]
			{
				new Vortice.Direct2D1.GradientStop(0f, new Color4(0f, 0f, 0f, (!top) ? 1 : 0)),
				new Vortice.Direct2D1.GradientStop(0.07f, new Color4(0f, 0f, 0f)),
				new Vortice.Direct2D1.GradientStop(0.93f, new Color4(0f, 0f, 0f)),
				new Vortice.Direct2D1.GradientStop(1f, new Color4(0f, 0f, 0f, (!bottom) ? 1 : 0))
			}, ExtendMode.Clamp);
			_fades[(top, bottom)] = value;
		}
		return value;
	}

	private ID2D1PathGeometry Geometry(InkStroke stroke)
	{
		if (_strokes.TryGetValue(stroke, out CachedStroke value) && value.Points == stroke.Points && value.Revision == stroke.Revision)
		{
			value.Generation = _generation;
			return value.Geometry;
		}
		value?.Geometry.Dispose();
		double[] points = stroke.Points;
		ID2D1PathGeometry1 iD2D1PathGeometry = _gpu.D2DFactory.CreatePathGeometry();
		using (ID2D1GeometrySink iD2D1GeometrySink = iD2D1PathGeometry.Open())
		{
			iD2D1GeometrySink.BeginFigure(new Vector2((float)points[0], (float)points[1]), FigureBegin.Hollow);
			for (int i = 2; i + 1 < points.Length; i += 2)
			{
				iD2D1GeometrySink.AddLine(new Vector2((float)points[i], (float)points[i + 1]));
			}
			iD2D1GeometrySink.EndFigure(FigureEnd.Open);
			iD2D1GeometrySink.Close();
		}
		_strokes[stroke] = new CachedStroke(iD2D1PathGeometry, stroke.Points, stroke.Revision)
		{
			Generation = _generation
		};
		return iD2D1PathGeometry;
	}

	private void ForgetUnused()
	{
		foreach (var (item, cachedStroke2) in _strokes)
		{
			if (cachedStroke2.Generation != _generation)
			{
				_unusedStrokes.Add(item);
			}
		}
		foreach (InkStroke unusedStroke in _unusedStrokes)
		{
			_strokes[unusedStroke].Geometry.Dispose();
			_strokes.Remove(unusedStroke);
		}
		_unusedStrokes.Clear();
		TextNote key;
		foreach (KeyValuePair<TextNote, CachedNote> note in _notes)
		{
			note.Deconstruct(out key, out var value);
			TextNote item2 = key;
			if (value.Generation != _generation)
			{
				_unusedNotes.Add(item2);
			}
		}
		foreach (TextNote unusedNote in _unusedNotes)
		{
			_notes[unusedNote].Layout.Dispose();
			_notes.Remove(unusedNote);
		}
		_unusedNotes.Clear();
		foreach (KeyValuePair<TextNote, CachedDocument> document in _documents)
		{
			document.Deconstruct(out key, out var value2);
			TextNote item3 = key;
			if (value2.Generation != _generation)
			{
				_unusedNotes.Add(item3);
			}
		}
		foreach (TextNote unusedNote2 in _unusedNotes)
		{
			_documents[unusedNote2].Document.Dispose();
			_documents.Remove(unusedNote2);
		}
		_unusedNotes.Clear();
	}

	private ID2D1SolidColorBrush Brush(ID2D1DeviceContext context, System.Windows.Media.Color color)
	{
		if (!_brushes.TryGetValue(color, out ID2D1SolidColorBrush value))
		{
			value = context.CreateSolidColorBrush(new Color4((float)(int)color.R / 255f, (float)(int)color.G / 255f, (float)(int)color.B / 255f, (float)(int)color.A / 255f));
			_brushes[color] = value;
		}
		return value;
	}

	private ID2D1StrokeStyle Round()
	{
		return _round ?? (_round = _gpu.D2DFactory.CreateStrokeStyle(new StrokeStyleProperties
		{
			StartCap = CapStyle.Round,
			EndCap = CapStyle.Round,
			DashCap = CapStyle.Round,
			LineJoin = LineJoin.Round
		}));
	}
}
