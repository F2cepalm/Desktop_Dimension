using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using InfiniteSpace.Spaces;
using Vortice.DirectWrite;

namespace InfiniteSpace.Rendering;

internal static class NoteText
{
	private sealed record MeasuredFile(string Text, double FontSize, double? Width, bool Markdown, double Header, double Content)
	{
		public bool Matches(TextNote note)
		{
			if (note.Text == Text && note.FontSize == FontSize && note.Width == Width)
			{
				return note.File?.Markdown == Markdown;
			}
			return false;
		}
	}

	private sealed record MeasuredNote(string Text, double FontSize, double? Width, bool Bold, bool Italic, Size Size)
	{
		public bool Matches(TextNote note)
		{
			if (note.Text == Text && note.FontSize == FontSize && note.Width == Width && note.Bold == Bold)
			{
				return note.Italic == Italic;
			}
			return false;
		}
	}

	private static readonly ConditionalWeakTable<TextNote, MeasuredNote> Measured = new ConditionalWeakTable<TextNote, MeasuredNote>();

	private static readonly Dictionary<float, IDWriteTextFormat> Formats = new Dictionary<float, IDWriteTextFormat>();

	private static readonly ConditionalWeakTable<TextNote, MeasuredFile> MeasuredFiles = new ConditionalWeakTable<TextNote, MeasuredFile>();

	internal const string MonospaceFamily = "Consolas";

	public static IDWriteFactory Factory { get; } = DWrite.DWriteCreateFactory<IDWriteFactory>();

	public static Typeface Typeface { get; } = new Typeface(SystemFonts.MessageFontFamily, SystemFonts.MessageFontStyle, SystemFonts.MessageFontWeight, FontStretches.Normal);

	public static double LineSpacing => Typeface.FontFamily.LineSpacing;

	public static IDWriteTextLayout Layout(TextNote note)
	{
		return Layout(note.Text, note.FontSize, note.Width, note.Bold, note.Italic);
	}

	public static IDWriteTextLayout Layout(string text, double fontSize, double? width, bool bold = false, bool italic = false)
	{
		int num;
		if (width.HasValue)
		{
			double valueOrDefault = width.GetValueOrDefault();
			num = ((valueOrDefault > 0.0) ? 1 : 0);
		}
		else
		{
			num = 0;
		}
		bool flag = (byte)num != 0;
		IDWriteTextLayout iDWriteTextLayout = Factory.CreateTextLayout(text, Format((float)fontSize), flag ? ((float)width.Value) : 1000000f, 1000000f);
		iDWriteTextLayout.WordWrapping = ((!flag) ? WordWrapping.NoWrap : WordWrapping.WholeWord);
		TextRange textRange = new TextRange(0u, (uint)text.Length);
		if (bold)
		{
			iDWriteTextLayout.SetFontWeight(Vortice.DirectWrite.FontWeight.Bold, textRange);
		}
		if (italic)
		{
			iDWriteTextLayout.SetFontStyle(Vortice.DirectWrite.FontStyle.Italic, textRange);
		}
		return iDWriteTextLayout;
	}

	public static Size Measure(TextNote note)
	{
		NoteFile file = note.File;
		if (file != null)
		{
			return new Size(note.Width ?? 420.0, file.Height);
		}
		if (Measured.TryGetValue(note, out MeasuredNote value) && value.Matches(note))
		{
			return value.Size;
		}
		Size size;
		using (IDWriteTextLayout iDWriteTextLayout = Layout(note))
		{
			TextMetrics metrics = iDWriteTextLayout.Metrics;
			size = new Size(Math.Max(metrics.Width, note.Width.GetValueOrDefault()), metrics.Height);
		}
		Measured.AddOrUpdate(note, new MeasuredNote(note.Text, note.FontSize, note.Width, note.Bold, note.Italic, size));
		return size;
	}

	public static double MaxScroll(TextNote note)
	{
		NoteFile file = note.File;
		if (file == null)
		{
			return 0.0;
		}
		if (!MeasuredFiles.TryGetValue(note, out MeasuredFile value) || !value.Matches(note))
		{
			using NoteDocument noteDocument = NoteDocument.Build(note);
			value = new MeasuredFile(note.Text, note.FontSize, note.Width, file.Markdown, noteDocument.HeaderHeight, noteDocument.ContentHeight);
			MeasuredFiles.AddOrUpdate(note, value);
		}
		return Math.Max(0.0, value.Content - (file.Height - value.Header));
	}

	public static void Remember(TextNote note, NoteDocument document)
	{
		MeasuredFiles.AddOrUpdate(note, new MeasuredFile(note.Text, note.FontSize, note.Width, note.File?.Markdown ?? false, document.HeaderHeight, document.ContentHeight));
	}

	public static void Adopt(TextNote from, TextNote to)
	{
		if (Measured.TryGetValue(from, out MeasuredNote value))
		{
			Measured.AddOrUpdate(to, value);
			Measured.Remove(from);
		}
		if (MeasuredFiles.TryGetValue(from, out MeasuredFile value2))
		{
			MeasuredFiles.AddOrUpdate(to, value2);
			MeasuredFiles.Remove(from);
		}
	}

	public static Rect Bounds(TextNote note, Matrix toScreen)
	{
		return new Rect(toScreen.Transform(new Point(note.X, note.Y)), Measure(note));
	}

	public static int[] LineLengths(TextNote note)
	{
		using IDWriteTextLayout iDWriteTextLayout = Layout(note);
		LineMetrics[] array = new LineMetrics[iDWriteTextLayout.Metrics.LineCount];
		iDWriteTextLayout.GetLineMetrics(array, out var actualLineCount);
		return (from l in array.Take((int)actualLineCount)
			select (int)l.Length).ToArray();
	}

	internal static IDWriteTextFormat Format(float fontSize, bool monospace = false)
	{
		lock (Formats)
		{
			float key = (monospace ? (0f - fontSize) : fontSize);
			if (Formats.TryGetValue(key, out IDWriteTextFormat value))
			{
				return value;
			}
			FontFamily fontFamily = Typeface.FontFamily;
			value = (monospace ? Factory.CreateTextFormat("Consolas", null, Vortice.DirectWrite.FontWeight.Normal, Vortice.DirectWrite.FontStyle.Normal, Vortice.DirectWrite.FontStretch.Normal, fontSize, "") : Factory.CreateTextFormat(fontFamily.Source, null, (Vortice.DirectWrite.FontWeight)Typeface.Weight.ToOpenTypeWeight(), (Typeface.Style == FontStyles.Italic) ? Vortice.DirectWrite.FontStyle.Italic : ((Typeface.Style == FontStyles.Oblique) ? Vortice.DirectWrite.FontStyle.Oblique : Vortice.DirectWrite.FontStyle.Normal), Vortice.DirectWrite.FontStretch.Normal, fontSize, ""));
			value.SetLineSpacing(LineSpacingMethod.Uniform, (float)(fontFamily.LineSpacing * (double)fontSize), (float)(fontFamily.Baseline * (double)fontSize));
			Formats[key] = value;
			return value;
		}
	}
}
