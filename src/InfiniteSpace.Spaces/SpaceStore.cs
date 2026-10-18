using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;

namespace InfiniteSpace.Spaces;

internal sealed class SpaceStore(string dataDirectory, ILogger<SpaceStore> logger)
{
	public const string NotesFolder = "notes";

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		Converters = { (JsonConverter)new JsonStringEnumConverter() },
		TypeInfoResolver = new DefaultJsonTypeInfoResolver
		{
			Modifiers = { (Action<JsonTypeInfo>)LeaveOutStoredText }
		}
	};

	public string DataDirectory { get; } = dataDirectory;

	public string FilePath { get; } = Path.Combine(dataDirectory, "spaces.json");

	public List<SpaceRecord> Load()
	{
		if (!File.Exists(FilePath))
		{
			return new List<SpaceRecord>();
		}
		List<SpaceRecord> list;
		try
		{
			using FileStream utf8Json = File.OpenRead(FilePath);
			list = JsonSerializer.Deserialize<SpacesDocument>(utf8Json, JsonOptions)?.Spaces ?? new List<SpaceRecord>();
		}
		catch (Exception ex) when ((ex is JsonException || ex is IOException || ex is NotSupportedException) ? true : false)
		{
			logger.LogError(ex, "Could not read {File}; keeping a copy and starting empty", FilePath);
			try
			{
				File.Copy(FilePath, FilePath + ".corrupt", overwrite: true);
			}
			catch (IOException)
			{
			}
			return new List<SpaceRecord>();
		}
		foreach (TextNote item in FileNotes(list))
		{
			item.Text = ReadStored(item.File);
		}
		return list;
	}

	public void Save(IEnumerable<SpaceRecord> spaces)
	{
		Directory.CreateDirectory(DataDirectory);
		List<SpaceRecord> list = spaces.ToList();
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (TextNote item in FileNotes(list))
		{
			NoteFile file = item.File;
			if (file.Stored == null || !File.Exists(Path.Combine(DataDirectory, file.Stored)))
			{
				file.Stored = WriteStored(item.Text, file);
			}
			hashSet.Add(Path.GetFileName(file.Stored));
		}
		string text = FilePath + ".tmp";
		using (FileStream utf8Json = File.Create(text))
		{
			JsonSerializer.Serialize(utf8Json, new SpacesDocument
			{
				Spaces = list
			}, JsonOptions);
		}
		File.Move(text, FilePath, overwrite: true);
		RemoveUnused(hashSet);
	}

	private static IEnumerable<TextNote> FileNotes(IEnumerable<SpaceRecord> records)
	{
		foreach (SpaceRecord record in records)
		{
			foreach (TextNote note in record.Notes)
			{
				for (TextNote version = note; version != null; version = version.Previous)
				{
					if (version.File != null)
					{
						yield return version;
					}
				}
			}
		}
	}

	private string WriteStored(string text, NoteFile file)
	{
		string text2 = Path.Combine(DataDirectory, "notes");
		Directory.CreateDirectory(text2);
		string path = $"{Guid.NewGuid():N}{(file.Markdown ? ".md" : ".txt")}";
		string text3 = Path.Combine(text2, path);
		File.WriteAllText(text3 + ".tmp", text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		File.Move(text3 + ".tmp", text3, overwrite: true);
		return Path.Combine("notes", path);
	}

	private string ReadStored(NoteFile file)
	{
		try
		{
			string stored = file.Stored;
			if (stored != null && stored.Length > 0)
			{
				return File.ReadAllText(Path.Combine(DataDirectory, stored), Encoding.UTF8);
			}
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) ? true : false)
		{
			logger.LogError(ex, "Could not read the text of {File}", file.Path);
		}
		file.Stored = null;
		return "(" + Path.GetFileName(file.Path) + " could not be read back.)";
	}

	private void RemoveUnused(HashSet<string> kept)
	{
		string path = Path.Combine(DataDirectory, "notes");
		if (!Directory.Exists(path))
		{
			return;
		}
		foreach (string item in Directory.EnumerateFiles(path))
		{
			if (!kept.Contains(Path.GetFileName(item)))
			{
				try
				{
					File.Delete(item);
				}
				catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) ? true : false)
				{
					logger.LogWarning(ex, "Could not remove {File}", item);
				}
			}
		}
	}

	private static void LeaveOutStoredText(JsonTypeInfo type)
	{
		if (type.Type != typeof(TextNote))
		{
			return;
		}
		foreach (JsonPropertyInfo property in type.Properties)
		{
			if (property.Name == "Text")
			{
				property.ShouldSerialize = (object note, object? _) => ((TextNote)note).File == null;
			}
		}
	}
}
