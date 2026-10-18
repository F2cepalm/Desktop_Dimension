using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using InfiniteSpace.Spaces;

namespace InfiniteSpace.Shell;

internal sealed class AppSettings
{
	private const string FileName = "settings.json";

	public bool? StartWithWindows { get; set; }

	public string Theme { get; set; } = "Dreamy";

	public string? InkColor { get; set; }

	public double PaintWidth { get; set; } = 3.0;

	public double TextSize { get; set; } = 16.0;

	public bool TextBold { get; set; }

	public bool TextItalic { get; set; }

	public bool ShowFrameStats { get; set; }

	[JsonIgnore]
	public InkPen Pen
	{
		get
		{
			return InkPen.Create(InkColor, PaintWidth, TextSize, TextBold, TextItalic);
		}
		set
		{
			string color = value.Color;
			double thickness = value.Thickness;
			double fontSize = value.FontSize;
			bool bold = value.Bold;
			bool italic = value.Italic;
			InkColor = color;
			PaintWidth = thickness;
			TextSize = fontSize;
			TextBold = bold;
			TextItalic = italic;
		}
	}

	public static AppSettings Load(string directory)
	{
		try
		{
			string path = Path.Combine(directory, "settings.json");
			return File.Exists(path) ? (JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new AppSettings()) : new AppSettings();
		}
		catch (Exception ex) when ((ex is IOException || ex is JsonException || ex is UnauthorizedAccessException) ? true : false)
		{
			return new AppSettings();
		}
	}

	public void Save(string directory)
	{
		Directory.CreateDirectory(directory);
		File.WriteAllText(Path.Combine(directory, "settings.json"), JsonSerializer.Serialize(this));
	}
}
