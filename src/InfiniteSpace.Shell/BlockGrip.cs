using System;

namespace InfiniteSpace.Shell;

[Flags]
internal enum BlockGrip
{
	None = 0,
	Left = 1,
	Top = 2,
	Right = 4,
	Bottom = 8,
	Move = Left | Top | Right | Bottom
}
