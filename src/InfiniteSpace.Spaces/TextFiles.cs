using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace InfiniteSpace.Spaces;

internal static class TextFiles
{
	public const long MaxBytes = 1048576L;

	private static readonly string[] Markdown;

	private static readonly string[] Plain;

	static TextFiles()
	{
		Markdown = new string[4] { ".md", ".markdown", ".mdown", ".mkd" };
		Plain = new string[3] { ".txt", ".text", ".log" };
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
	}

	public static bool IsSupported(string path)
	{
		if (!IsMarkdown(path))
		{
			return Plain.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
		}
		return true;
	}

	public static bool IsMarkdown(string path)
	{
		return Markdown.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
	}

	public static string Read(string path)
	{
		byte[] array;
		try
		{
			FileInfo fileInfo = new FileInfo(path);
			if (!fileInfo.Exists)
			{
				throw new TextFileException(Path.GetFileName(path) + " is gone.");
			}
			if (fileInfo.Length > 1048576)
			{
				throw new TextFileException($"{fileInfo.Name} is {fileInfo.Length / 1024} KB; files up to {1024L} KB can be opened on a space.");
			}
			array = File.ReadAllBytes(path);
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) ? true : false)
		{
			throw new TextFileException(Path.GetFileName(path) + " could not be read: " + ex.Message, ex);
		}
		if ((long)array.Length > 1048576L)
		{
			throw new TextFileException($"{Path.GetFileName(path)} is over {1024L} KB.");
		}
		return Decode(array);
	}

	public static string Decode(ReadOnlySpan<byte> bytes)
	{
		string text;
		if (bytes.StartsWith("\ufeff"u8))
		{
			text = Encoding.UTF8.GetString(bytes.Slice(3, bytes.Length - 3));
		}
		else if (bytes.StartsWith(new byte[4] { 255, 254, 0, 0 }))
		{
			text = new UTF32Encoding(bigEndian: false, byteOrderMark: true).GetString(bytes.Slice(4, bytes.Length - 4));
		}
		else if (bytes.StartsWith(new byte[4] { 0, 0, 254, 255 }))
		{
			text = new UTF32Encoding(bigEndian: true, byteOrderMark: true).GetString(bytes.Slice(4, bytes.Length - 4));
		}
		else if (bytes.StartsWith(new byte[2] { 255, 254 }))
		{
			text = Encoding.Unicode.GetString(bytes.Slice(2, bytes.Length - 2));
		}
		else if (bytes.StartsWith(new byte[2] { 254, 255 }))
		{
			text = Encoding.BigEndianUnicode.GetString(bytes.Slice(2, bytes.Length - 2));
		}
		else
		{
			try
			{
				text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
			}
			catch (DecoderFallbackException)
			{
				text = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.ANSICodePage).GetString(bytes);
			}
		}
		return text.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\0", "");
	}
}
