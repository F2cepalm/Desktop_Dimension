using System;
using System.Collections.Generic;

namespace InfiniteSpace.Shell;

internal sealed record TrayActions(Action Draw, Action Block, Action Move, Action Remove, Action Ink, Action EraseWithBrush, Action EraseAll, IReadOnlyList<string> Themes, Func<string> CurrentTheme, Action<string> SetTheme, Func<bool> IsAutoStartEnabled, Action<bool> SetAutoStart, Action OpenDataFolder, Action Exit);
