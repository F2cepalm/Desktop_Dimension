using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;

namespace InfiniteSpace.Spaces;

internal sealed class SpaceRecord
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

	public FillRule FillRule { get; set; } = FillRule.Nonzero;

	public List<double[]> Figures { get; set; } = new List<double[]>();

	public List<InkStroke> Strokes { get; set; } = new List<InkStroke>();

	public List<TextNote> Notes { get; set; } = new List<TextNote>();

	public HostInfo? Host { get; set; }

	[JsonIgnore]
	public Point? Anchor => Host?.Frame.TopLeft;

	public void Transform(Matrix m, Func<TextNote, Point>? placeNote = null, Func<InkStroke, Matrix>? placeStroke = null)
	{
		if (m.IsIdentity && placeNote == null && placeStroke == null)
		{
			return;
		}
		foreach (double[] figure in Figures)
		{
			TransformPoints(figure, m);
		}
		foreach (InkStroke stroke in Strokes)
		{
			TransformInk(stroke, m, null, placeStroke, Anchor);
		}
		foreach (TextNote note in Notes)
		{
			TransformInk(note, m, placeNote);
		}
	}

	public static void TransformInk(IInk ink, Matrix m, Func<TextNote, Point>? placeNote = null, Func<InkStroke, Matrix>? placeStroke = null, Point? anchor = null)
	{
		if (!(ink is InkStroke { Points: var points } inkStroke))
		{
			if (ink is TextNote textNote)
			{
				for (TextNote textNote2 = textNote; textNote2 != null; textNote2 = textNote2.Previous)
				{
					Point point = placeNote?.Invoke(textNote2) ?? m.Transform(new Point(textNote2.X, textNote2.Y));
					textNote2.X = Math.Round(point.X, 2);
					textNote2.Y = Math.Round(point.Y, 2);
				}
			}
		}
		else
		{
			TransformPoints(points, placeStroke?.Invoke(inkStroke) ?? inkStroke.Placed(m, anchor));
			inkStroke.Revision++;
		}
	}

	private static void TransformPoints(double[] xy, Matrix m)
	{
		for (int i = 0; i + 1 < xy.Length; i += 2)
		{
			Point point = m.Transform(new Point(xy[i], xy[i + 1]));
			xy[i] = Math.Round(point.X, 2);
			xy[i + 1] = Math.Round(point.Y, 2);
		}
	}

	public IInk? TakeNewestInk()
	{
		int num = IndexOfNewest(Strokes);
		int num2 = IndexOfNewest(Notes);
		if (num < 0 && num2 < 0)
		{
			return null;
		}
		if (num2 >= 0 && (num < 0 || Notes[num2].Order > Strokes[num].Order))
		{
			TextNote textNote = Notes[num2];
			Notes.RemoveAt(num2);
			TextNote previous = textNote.Previous;
			if (previous != null)
			{
				Notes.Add(previous);
			}
			return textNote;
		}
		InkStroke result = Strokes[num];
		Strokes.RemoveAt(num);
		return result;
	}

	public void PutBack(IInk ink)
	{
		if (!(ink is InkStroke item))
		{
			if (ink is TextNote textNote)
			{
				TextNote previous = textNote.Previous;
				if (previous != null)
				{
					Notes.Remove(previous);
				}
				Notes.Add(textNote);
			}
		}
		else
		{
			Strokes.Add(item);
		}
	}

	public void Replace(TextNote note, TextNote edited)
	{
		Notes.Remove(note);
		edited.Previous = note;
		TextNote? cur = edited;
		for (int i = 0; i < 20 && cur != null; i++)
		{
			cur = cur.Previous;
		}
		if (cur != null)
		{
			cur.Previous = null;
		}
		Notes.Add(edited);
	}

	private static int IndexOfNewest<T>(List<T> items) where T : IInk
	{
		int num = -1;
		for (int i = 0; i < items.Count; i++)
		{
			if (num < 0 || items[i].Order >= items[num].Order)
			{
				num = i;
			}
		}
		return num;
	}
}
