using UnityEngine;
using UnityEngine.Tilemaps;

/*
 * Marker component that tells the SurfaceDetector where a room's walkable
 * floor lives. Place it on the parent GameObject of a room's floor Tilemaps
 * (e.g. the "FloorTilemaps" object) - one per room scene.
 *
 * On every active-scene change the SurfaceDetector scans the scene for
 * FloorTilemapGroup components and collects the floor tilemaps via
 * GetTilemaps(). Only tiles on these tilemaps (authored as SurfaceTile
 * assets) can be detected underfoot; everything else falls back to
 * SurfaceType.concrete. A room scene without this component therefore
 * plays concrete footsteps everywhere.
 */
public class FloorTilemapGroup : MonoBehaviour
{
    // what counts as a floor tilemap is decided here, not in the detector:
    // every Tilemap under this object, including inactive ones
    public Tilemap[] GetTilemaps()
    {
        return GetComponentsInChildren<Tilemap>(true);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (GetComponentsInChildren<Tilemap>(true).Length == 0)
        {
            Debug.LogWarning(
                $"FloorTilemapGroup on '{name}' has no Tilemaps underneath it - the SurfaceDetector will not find any floor here.",
                this);
        }
    }
#endif
}
