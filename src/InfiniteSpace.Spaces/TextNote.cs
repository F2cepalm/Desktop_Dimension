using System.Text.Json.Serialization;

namespace InfiniteSpace.Spaces;

internal sealed class TextNote : IInk
{
	public const double DefaultFontSize = 16.0;

	public long Order { get; set; }

	public double X { get; set; }

	public double Y { get; set; }

	public string Text { get; set; } = "";

	public double FontSize { get; set; } = 16.0;

	public string? Color { get; set; }

	public double? Width { get; set; }

	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
	public bool Bold { get; set; }

	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
	public bool Italic { get; set; }

	[JsonIgnore]
	public TextNote? Previous { get; set; }

	public NoteFile? File { get; set; }
}
