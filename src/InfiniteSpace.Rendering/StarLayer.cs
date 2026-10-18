using System.Windows.Media;

namespace InfiniteSpace.Rendering;

internal sealed record StarLayer(double CellSize, double Density, double Depth, int[] Frames, double TwinkleSpeed, Color[] Colors);
