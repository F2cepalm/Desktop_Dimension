using System;

namespace InfiniteSpace.Spaces;

internal sealed class TextFileException : Exception
{
	public TextFileException(string message, Exception? inner = null)
		: base(message, inner)
	{
	}
}
