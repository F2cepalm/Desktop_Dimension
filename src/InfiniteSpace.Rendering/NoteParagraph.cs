using System.Collections.Generic;

namespace InfiniteSpace.Rendering;

internal sealed record NoteParagraph(ParagraphKind Kind, string Text, IReadOnlyList<StyledRun> Runs, int Level = 0, int Indent = 0, int Quote = 0, string? Marker = null, bool Tight = false);
