using System;

namespace InfiniteSpace.Rendering;

[Flags]
internal enum TextStyle
{
	None = 0,
	Bold = 1,
	Italic = 2,
	Strike = 4,
	Code = 8,
	Link = 0x10
}
