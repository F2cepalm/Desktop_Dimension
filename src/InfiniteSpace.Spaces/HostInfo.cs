using System;
using System.Text.Json.Serialization;
using System.Windows;

namespace InfiniteSpace.Spaces;

internal sealed class HostInfo
{
	public string App { get; set; } = "";

	public string WindowClass { get; set; } = "";

	public string Title { get; set; } = "";

	public Guid Window { get; set; }

	public double Left { get; set; }

	public double Top { get; set; }

	public double Width { get; set; }

	public double Height { get; set; }

	[JsonIgnore]
	public Rect Frame
	{
		get
		{
			return new Rect(Left, Top, Math.Max(1.0, Width), Math.Max(1.0, Height));
		}
		set
		{
			double left = value.Left;
			double top = value.Top;
			double width = value.Width;
			double height = value.Height;
			Left = left;
			Top = top;
			Width = width;
			Height = height;
		}
	}
}
