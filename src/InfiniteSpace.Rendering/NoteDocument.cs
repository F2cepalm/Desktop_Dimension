using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using InfiniteSpace.Spaces;
using Vortice.Direct2D1;
using Vortice.DirectWrite;

namespace InfiniteSpace.Rendering;

internal sealed class NoteDocument : IDisposable
{
	public const double DefaultWidth = 420.0;

	public const double DefaultHeight = 480.0;

	private static readonly double[] HeadingScale = new double[6] { 1.75, 1.45, 1.2, 1.05, 1.0, 1.0 };

	private readonly List<LaidParagraph> _paragraphs = new List<LaidParagraph>();

	private readonly string _text;

	private readonly double _fontSize;

	private readonly double? _width;

	private readonly bool _markdown;

	private readonly string _path;

	public double Width { get; }

	public double HeaderHeight { get; private set; }

	public double ContentHeight { get; private set; }

	public IDWriteTextLayout Header { get; private set; }

	public IReadOnlyList<LaidParagraph> Paragraphs => _paragraphs;

	public double RuleY { get; private set; }

	public double FirstHeight => Math.Min(HeaderHeight + ContentHeight, Math.Max(480.0, HeaderHeight + 3.0 * Line(_fontSize)));

	private NoteDocument(TextNote note)
	{
		_text = note.Text;
		_fontSize = note.FontSize;
		_width = note.Width;
		_markdown = note.File?.Markdown ?? false;
		_path = note.File?.Path ?? "";
		Width = note.Width ?? 420.0;
	}

	public static NoteDocument Build(TextNote note, ID2D1Brush? linkBrush = null)
	{
		NoteDocument noteDocument = new NoteDocument(note);
		noteDocument.Lay(linkBrush);
		return noteDocument;
	}

	public bool Matches(TextNote note)
	{
		if (note.Text == _text && note.FontSize == _fontSize && note.Width == _width && (note.File?.Markdown ?? false) == _markdown)
		{
			return (note.File?.Path ?? "") == _path;
		}
		return false;
	}

	public double MaxScroll(double height)
	{
		return Math.Max(0.0, ContentHeight - (height - HeaderHeight));
	}

	public string? LinkAt(Point at, double scroll)
	{
		double num = at.Y - HeaderHeight + scroll;
		foreach (LaidParagraph paragraph in _paragraphs)
		{
			if ((object)paragraph.Layout == null || num < paragraph.Top || num >= paragraph.Top + paragraph.Height)
			{
				continue;
			}
			paragraph.Layout.HitTestPoint((float)(at.X - paragraph.Left), (float)(num - paragraph.TextTop), out var _, out var isInside, out var hitTestMetrics);
			if (!isInside)
			{
				return null;
			}
			int textPosition = (int)hitTestMetrics.TextPosition;
			foreach (StyledRun run in paragraph.Source.Runs)
			{
				if (run.Url != null && textPosition >= run.Start && textPosition < run.Start + run.Length)
				{
					return run.Url;
				}
			}
			return null;
		}
		return null;
	}

	public bool IsOnHeader(Point at)
	{
		if (at.Y >= 0.0 && at.Y < HeaderHeight && at.X >= 0.0)
		{
			return at.X < Width;
		}
		return false;
	}

	public void Dispose()
	{
		Header?.Dispose();
		foreach (LaidParagraph paragraph in _paragraphs)
		{
			paragraph.Layout?.Dispose();
			paragraph.Marker?.Dispose();
		}
		_paragraphs.Clear();
	}

	private static double Line(double fontSize)
	{
		return fontSize * NoteText.LineSpacing;
	}

	private void Lay(ID2D1Brush? linkBrush)
	{
		double fontSize = _fontSize;
		float num = (float)(fontSize * 0.8);
		Header = NoteText.Factory.CreateTextLayout(Path.GetFileName(_path), NoteText.Format(num), (float)Width, 1000000f);
		Header.WordWrapping = WordWrapping.NoWrap;
		Header.SetTrimming(new Trimming
		{
			Granularity = TrimmingGranularity.Character
		}, null);
		RuleY = Line(num) + fontSize * 0.25;
		HeaderHeight = RuleY + fontSize * 0.5;
		List<NoteParagraph> list = (_markdown ? NoteParagraphs.Markdown(_text) : NoteParagraphs.Plain(_text));
		double num2 = 0.0;
		for (int i = 0; i < list.Count; i++)
		{
			NoteParagraph paragraph = list[i];
			if (i > 0)
			{
				num2 += SpaceBefore(paragraph, fontSize);
			}
			LaidParagraph laidParagraph = LayParagraph(paragraph, fontSize, num2, linkBrush);
			_paragraphs.Add(laidParagraph);
			num2 += laidParagraph.Height;
		}
		ContentHeight = num2 + fontSize * 0.4;
	}

	private double SpaceBefore(NoteParagraph paragraph, double s)
	{
		int num;
		if ((object)paragraph != null)
		{
			if (paragraph.Kind == ParagraphKind.Heading)
			{
				if (paragraph.Level <= 2)
				{
					return s * 1.1;
				}
				return s * 0.8;
			}
			num = 0;
		}
		else
		{
			num = 1;
		}
		if (_markdown)
		{
			if (num == 0)
			{
				if (paragraph.Tight)
				{
					return s * 0.2;
				}
			}
			else if (num != 1)
			{
				goto IL_0054;
			}
			return s * 0.6;
		}
		goto IL_0054;
		IL_0054:
		return 0.0;
	}

	private LaidParagraph LayParagraph(NoteParagraph paragraph, double s, double top, ID2D1Brush? linkBrush)
	{
		double num = (double)paragraph.Indent * s * 1.6 + (double)paragraph.Quote * s * 1.1;
		if (paragraph.Kind == ParagraphKind.Rule)
		{
			return new LaidParagraph(paragraph, top, num, Width - num, s * 0.8, 0.0, null, null);
		}
		bool flag = paragraph.Kind == ParagraphKind.Code;
		double num2;
		if (paragraph.Kind == ParagraphKind.Heading)
		{
			num2 = s * HeadingScale[Math.Clamp(paragraph.Level, 1, 6) - 1];
		}
		else
		{
			num2 = (flag ? (s * 0.9) : s);
		}
		double num3 = (flag ? (s * 0.45) : 0.0);
		float maxWidth = (float)Math.Max(8.0, Width - num - 2.0 * num3);
		IDWriteTextLayout iDWriteTextLayout = NoteText.Factory.CreateTextLayout(paragraph.Text, NoteText.Format((float)num2, flag), maxWidth, 1000000f);
		iDWriteTextLayout.WordWrapping = (flag ? WordWrapping.Character : WordWrapping.EmergencyBreak);
		if (paragraph.Kind == ParagraphKind.Heading)
		{
			iDWriteTextLayout.SetFontWeight(Vortice.DirectWrite.FontWeight.Bold, new TextRange(0u, (uint)paragraph.Text.Length));
		}
		foreach (StyledRun run in paragraph.Runs)
		{
			Style(iDWriteTextLayout, run, num2, linkBrush);
		}
		double height = (double)iDWriteTextLayout.Metrics.Height + 2.0 * num3;
		IDWriteTextLayout iDWriteTextLayout2 = null;
		string marker = paragraph.Marker;
		if (marker != null)
		{
			iDWriteTextLayout2 = NoteText.Factory.CreateTextLayout(marker, NoteText.Format((float)num2), (float)(s * 1.6), 1000000f);
			iDWriteTextLayout2.WordWrapping = WordWrapping.NoWrap;
			iDWriteTextLayout2.TextAlignment = Vortice.DirectWrite.TextAlignment.Trailing;
		}
		return new LaidParagraph(paragraph, top, num, Width - num, height, num3, iDWriteTextLayout, iDWriteTextLayout2);
	}

	private static void Style(IDWriteTextLayout layout, StyledRun run, double size, ID2D1Brush? linkBrush)
	{
		TextRange textRange = new TextRange((uint)run.Start, (uint)run.Length);
		if (run.Style.HasFlag(TextStyle.Bold))
		{
			layout.SetFontWeight(Vortice.DirectWrite.FontWeight.Bold, textRange);
		}
		if (run.Style.HasFlag(TextStyle.Italic))
		{
			layout.SetFontStyle(Vortice.DirectWrite.FontStyle.Italic, textRange);
		}
		if (run.Style.HasFlag(TextStyle.Strike))
		{
			layout.SetStrikethrough(true, textRange);
		}
		if (run.Style.HasFlag(TextStyle.Code))
		{
			layout.SetFontFamilyName("Consolas", textRange);
			layout.SetFontSize((float)(size * 0.9), textRange);
		}
		if (run.Style.HasFlag(TextStyle.Link))
		{
			layout.SetUnderline(true, textRange);
			if ((object)linkBrush != null)
			{
				layout.SetDrawingEffect(linkBrush, textRange);
			}
		}
	}
}
