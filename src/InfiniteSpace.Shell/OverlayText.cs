using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows.Media;
using InfiniteSpace.Rendering;
using Vortice;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace InfiniteSpace.Shell;

internal static class OverlayText
{
	private static readonly Dictionary<float, IDWriteTextFormat> Formats = new Dictionary<float, IDWriteTextFormat>();

	public static OverlayPicture Paint(GpuCompositor gpu, string text, float fontSize, Color4? backing = null, Vector2 padding = default(Vector2), float radius = 0f)
	{
		IDWriteTextLayout layout = NoteText.Factory.CreateTextLayout(text, Format(fontSize), 1000000f, 1000000f);
		try
		{
			layout.WordWrapping = WordWrapping.NoWrap;
			TextMetrics metrics = layout.Metrics;
			double width = Math.Ceiling(metrics.WidthIncludingTrailingWhitespace + 2f * padding.X);
			double height = Math.Ceiling(metrics.Height + 2f * padding.Y);
			return OverlayPicture.Paint(gpu, width, height, (ID2D1DeviceContext context) =>
			{
				if (backing.HasValue)
				{
					Color4 valueOrDefault = backing.GetValueOrDefault();
					using ID2D1SolidColorBrush brush = context.CreateSolidColorBrush(valueOrDefault);
					context.FillRoundedRectangle(new RoundedRectangle(new RawRectF(0f, 0f, (float)width, (float)height), radius, radius), brush);
				}
				using ID2D1SolidColorBrush defaultFillBrush = context.CreateSolidColorBrush(new Color4(1f, 1f, 1f));
				context.DrawTextLayout(padding, layout, defaultFillBrush, DrawTextOptions.None);
			});
		}
		finally
		{
			if ((object)layout != null)
			{
				((IDisposable)layout).Dispose();
			}
		}
	}

	public static void Repaint(GpuCompositor gpu, ref OverlayPicture? picture, OverlaySprite sprite, string text, float fontSize, Color4? backing = null, Vector2 padding = default(Vector2), float radius = 0f)
	{
		IDWriteTextLayout layout = NoteText.Factory.CreateTextLayout(text, Format(fontSize), 1000000f, 1000000f);
		try
		{
			layout.WordWrapping = WordWrapping.NoWrap;
			TextMetrics metrics = layout.Metrics;
			double width = Math.Ceiling(metrics.WidthIncludingTrailingWhitespace + 2f * padding.X);
			double height = Math.Ceiling(metrics.Height + 2f * padding.Y);
			OverlayPicture? oldPicture = picture;
			OverlayPicture.Repaint(gpu, ref picture, width, height, (ID2D1DeviceContext context) =>
			{
				if (backing.HasValue)
				{
					Color4 valueOrDefault = backing.GetValueOrDefault();
					using ID2D1SolidColorBrush brush = context.CreateSolidColorBrush(valueOrDefault);
					context.FillRoundedRectangle(new RoundedRectangle(new RawRectF(0f, 0f, (float)width, (float)height), radius, radius), brush);
				}
				using ID2D1SolidColorBrush defaultFillBrush = context.CreateSolidColorBrush(new Color4(1f, 1f, 1f));
				context.DrawTextLayout(padding, layout, defaultFillBrush, DrawTextOptions.None);
			});
			if (picture != oldPicture)
			{
				sprite.Picture = picture;
				oldPicture?.Dispose();
			}
		}
		finally
		{
			if ((object)layout != null)
			{
				((IDisposable)layout).Dispose();
			}
		}
	}

	private static IDWriteTextFormat Format(float fontSize)
	{
		if (Formats.TryGetValue(fontSize, out IDWriteTextFormat value))
		{
			return value;
		}
		Typeface typeface = NoteText.Typeface;
		value = NoteText.Factory.CreateTextFormat(typeface.FontFamily.Source, null, (FontWeight)typeface.Weight.ToOpenTypeWeight(), FontStyle.Normal, FontStretch.Normal, fontSize, "");
		Formats[fontSize] = value;
		return value;
	}
}
