using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public class SurfaceDetector : MonoBehaviour
{
    // scenes that intentionally have no floor tilemaps (e.g. the persistent
    // scene, which only holds managers) - don't warn about those
    private const string PersistentSceneName = "PersistentScene";

    [SerializeField] private Transform feetTransform;

    private readonly List<Tilemap> floorTilemaps = new();

    public SurfaceType CurrentSurface { get; private set; }

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        RefreshFloorTilemaps(SceneManager.GetActiveScene());
        UpdateCurrentSurface();
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    private void Update()
    {
        UpdateCurrentSurface();
    }

    private void OnActiveSceneChanged(Scene previousScene, Scene newScene)
    {
        RefreshFloorTilemaps(newScene);
    }

    private void RefreshFloorTilemaps(Scene scene)
    {
        floorTilemaps.Clear();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (FloorTilemapGroup group in root.GetComponentsInChildren<FloorTilemapGroup>(true))
            {
                floorTilemaps.AddRange(group.GetTilemaps());
            }
        }

        if (floorTilemaps.Count == 0 && scene.name != PersistentSceneName)
        {
            Debug.LogWarning($"No FloorTilemapGroup with Tilemaps found in scene '{scene.name}' - footsteps fall back to {SurfaceType.concrete}.");
        }
    }

    private void UpdateCurrentSurface()
    {
        foreach (Tilemap tilemap in floorTilemaps)
        {
            Vector3Int cell = tilemap.WorldToCell(feetTransform.position);
            SurfaceTile tile = tilemap.GetTile<SurfaceTile>(cell);

            if (tile != null)
            {
                CurrentSurface = tile.surfaceType;
                return;
            }
        }

        CurrentSurface = SurfaceType.concrete;
    }
}
