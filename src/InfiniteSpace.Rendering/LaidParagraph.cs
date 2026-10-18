using System.Collections.Generic;
using System.Windows;
using Vortice.DirectWrite;

namespace InfiniteSpace.Rendering;

internal sealed record LaidParagraph(NoteParagraph Source, double Top, double Left, double Width, double Height, double Padding, IDWriteTextLayout? Layout, IDWriteTextLayout? Marker)
{
	public double TextTop => Top + Padding;

	public List<Rect>? CodeBackings { get; set; }
}
