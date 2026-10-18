using System.Collections.Generic;

namespace InfiniteSpace.Spaces;

internal sealed class SpacesDocument
{
	public int Version { get; set; } = 1;

	public List<SpaceRecord> Spaces { get; set; } = new List<SpaceRecord>();
}
