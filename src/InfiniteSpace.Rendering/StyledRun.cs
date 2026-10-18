namespace InfiniteSpace.Rendering;

internal readonly record struct StyledRun(int Start, int Length, TextStyle Style, string? Url = null);
