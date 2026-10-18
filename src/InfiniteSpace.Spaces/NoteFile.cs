namespace InfiniteSpace.Spaces;

internal sealed class NoteFile
{
	public string Path { get; set; } = "";

	public bool Markdown { get; set; }

	public double Height { get; set; }

	public double Scroll { get; set; }

	public string? Stored { get; set; }

	public NoteFile ForNewText()
	{
		return new NoteFile
		{
			Path = Path,
			Markdown = Markdown,
			Height = Height,
			Scroll = Scroll
		};
	}

	public NoteFile Copy()
	{
		return new NoteFile
		{
			Path = Path,
			Markdown = Markdown,
			Height = Height,
			Scroll = Scroll,
			Stored = Stored
		};
	}
}
