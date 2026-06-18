using UnityEngine;
using UnityEngine.Tilemaps;

public class SurfaceDetector : MonoBehaviour
{
    [SerializeField] private Tilemap surfaceTilemap;
    [SerializeField] private Vector2 feetOffset = new(0f, -0.3f);
    [SerializeField] private Transform feetTransform;

    public SurfaceType CurrentSurface { get; private set; }

    public SurfaceType GetCurrentSurface()
    {
        Vector3Int cell = surfaceTilemap.WorldToCell(feetTransform.position);
        SurfaceTile tile = surfaceTilemap.GetTile<SurfaceTile>(cell);

        if (tile != null) {
            CurrentSurface = tile.surfaceType;
            return CurrentSurface;
        }
        else
        {
            return SurfaceType.concrete;
        }

    }
}