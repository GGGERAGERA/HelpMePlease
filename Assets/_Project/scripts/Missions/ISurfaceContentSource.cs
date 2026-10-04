using System;
using System.Collections.Generic;

/// <summary>Meta content contributions; gameplay continues consuming only resolved RunConfig.</summary>
public interface ISurfaceContentSource
{
    event Action Changed;
    IEnumerable<(string objectiveId,SurfaceSectorContent content)> GetActiveContent(string sectorId);
    bool TryGetMarker(string sectorId,out SurfaceMarkerPresentation presentation,out int priority);
}
