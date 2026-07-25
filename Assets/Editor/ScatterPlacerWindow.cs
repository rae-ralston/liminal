using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Tools > Props > Scatter Placer (Prop_Placement_Tool_Brief.md P1).
//
// Places existing decorative prefabs onto the ACTIVE room scene on the grid,
// obeying floor / occupancy / wall rules, as a first-draft layout the user then
// hand-tunes. Editor-only, active-scene-only, one-undo-group per run.
//
// Deliberately NOT built here (per the brief): P2 variant generation (§7); the
// flood-fill passability report (§5.2b, deferred by the user); randomFlipX /
// rotation (excluded - flipped sprites read black under the flashlight until
// flipped art exists). The placer never creates or resizes colliders - it reads
// them; a prefab whose root anchor is off its footprint is an authoring bug it
// warns about, never silently compensates (§3.2).
public class ScatterPlacerWindow : EditorWindow
{
    enum Operation { Add, Regenerate, Remove }

    [System.Serializable]
    class PrefabEntry
    {
        public GameObject prefab;
        public int weight = 1;
    }

    // Measured once per distinct prefab by briefly instantiating it: footprint
    // (W×1, from the collider) and visual span (W×H, from the renderers), both
    // in cells, plus the §3.2 sort-anchor check.
    struct PrefabDims
    {
        public int w;            // footprint / span width in cells
        public int h;            // visual span height in cells
        public bool hasCollider;
        public bool anchorOk;
        public string warning;   // non-null if the prefab is unfit or off-anchor
    }

    const string GeneratedRootName = "GeneratedProps";
    const float AnchorTolerance = 0.05f;

    Operation operation = Operation.Add;
    readonly List<PrefabEntry> prefabs = new List<PrefabEntry> { new PrefabEntry() };
    int count = 20;
    int seed = 12345;
    float wallAffinity = 0f;
    float clustering = 0f;
    int minSpacingCells = 1;
    int doorClearanceCells = 3;
    int interactableClearanceCells = 2;
    Tilemap wallTilemapOverride;

    // Remove filters
    GameObject removePrefabFilter;
    int removeBatchIndex; // 0 = all

    Vector2 scroll;
    readonly List<string> report = new List<string>();

    [MenuItem("Tools/Props/Scatter Placer")]
    static void Open() => GetWindow<ScatterPlacerWindow>("Scatter Placer");

    // ------------------------------------------------------------------ GUI

    void OnGUI()
    {
        Scene active = SceneManager.GetActiveScene();
        int otherLoaded = SceneManager.sceneCount - 1;

        EditorGUILayout.Space(4);
        GUILayout.Label($"Active scene:  {active.name}", EditorStyles.boldLabel);
        if (otherLoaded > 0)
        {
            EditorGUILayout.LabelField($"({otherLoaded} other scene(s) loaded - only the active one is touched)", EditorStyles.miniLabel);
        }

        string block = HardBlockReason(active);
        if (block != null)
        {
            EditorGUILayout.HelpBox(block, MessageType.Error);
            DrawReport();
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorGUILayout.HelpBox("Exit Play Mode to run the placer.", MessageType.Warning);
            return;
        }

        WarnAboutExtraRoomScenes(active);

        EditorGUILayout.Space(6);
        operation = (Operation)EditorGUILayout.EnumPopup("Operation", operation);

        if (operation == Operation.Remove)
        {
            DrawRemoveControls(active);
        }
        else
        {
            DrawPlaceControls();
        }

        EditorGUILayout.Space(6);
        using (new EditorGUI.DisabledScope(operation != Operation.Remove && prefabs.All(p => p.prefab == null)))
        {
            if (GUILayout.Button(operation.ToString(), GUILayout.Height(30)))
            {
                Run(active);
            }
        }

        DrawReport();
    }

    void DrawPlaceControls()
    {
        EditorGUILayout.LabelField("Prefabs (weighted)", EditorStyles.boldLabel);
        int removeAt = -1;
        for (int i = 0; i < prefabs.Count; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                prefabs[i].prefab = (GameObject)EditorGUILayout.ObjectField(prefabs[i].prefab, typeof(GameObject), false);
                prefabs[i].weight = Mathf.Max(1, EditorGUILayout.IntField(prefabs[i].weight, GUILayout.Width(50)));
                if (GUILayout.Button("-", GUILayout.Width(24))) removeAt = i;
            }
        }
        if (removeAt >= 0 && prefabs.Count > 1) prefabs.RemoveAt(removeAt);
        if (GUILayout.Button("+ Add prefab row")) prefabs.Add(new PrefabEntry());

        EditorGUILayout.Space(4);
        count = Mathf.Max(0, EditorGUILayout.IntField("Count", count));

        using (new EditorGUILayout.HorizontalScope())
        {
            seed = EditorGUILayout.IntField("Seed", seed);
            if (GUILayout.Button("Reroll", GUILayout.Width(70))) seed = Random.Range(int.MinValue, int.MaxValue);
        }

        wallAffinity = EditorGUILayout.Slider(new GUIContent("Wall Affinity", "+1 hugs walls, 0 indifferent, -1 avoids walls (centre)."), wallAffinity, -1f, 1f);
        clustering = EditorGUILayout.Slider(new GUIContent("Clustering", "0 = spread out, 1 = clump together."), clustering, 0f, 1f);
        minSpacingCells = Mathf.Max(0, EditorGUILayout.IntField(new GUIContent("Min Spacing (cells)", "Hard floor between prop bases."), minSpacingCells));

        EditorGUILayout.Space(2);
        doorClearanceCells = Mathf.Max(0, EditorGUILayout.IntField("Door Clearance (cells)", doorClearanceCells));
        interactableClearanceCells = Mathf.Max(0, EditorGUILayout.IntField("Interactable Clearance", interactableClearanceCells));
        wallTilemapOverride = (Tilemap)EditorGUILayout.ObjectField(new GUIContent("Wall Tilemap (override)", "Leave empty to resolve 'WallTilemap' by name."), wallTilemapOverride, typeof(Tilemap), true);
    }

    void DrawRemoveControls(Scene active)
    {
        EditorGUILayout.HelpBox("Removes only objects carrying the GeneratedProp marker in the active scene. Never touches hand-placed objects.", MessageType.Info);
        removePrefabFilter = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Prefab filter", "Empty = any prefab."), removePrefabFilter, typeof(GameObject), false);

        List<string> batches = BatchIdsInScene(active);
        string[] options = new[] { "(all batches)" }.Concat(batches).ToArray();
        removeBatchIndex = EditorGUILayout.Popup("Batch filter", Mathf.Clamp(removeBatchIndex, 0, options.Length - 1), options);
    }

    void DrawReport()
    {
        if (report.Count == 0) return;
        EditorGUILayout.Space(6);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (string line in report) EditorGUILayout.LabelField(line, EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndScrollView();
    }

    // ------------------------------------------------------------- targeting

    string HardBlockReason(Scene active)
    {
        if (!active.IsValid()) return "No active scene.";
        if (active.name == "PersistentScene") return "PersistentScene is active - switch to a room scene. The placer never writes to PersistentScene.";
        if (Object.FindObjectsByType<FloorTilemapGroup>(FindObjectsInactive.Exclude).All(g => g.gameObject.scene != active))
            return "The active scene has no FloorTilemapGroup - nothing defines 'floor' here.";
        return null;
    }

    void WarnAboutExtraRoomScenes(Scene active)
    {
        List<string> others = new List<string>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (s == active || s.name == "PersistentScene") continue;
            others.Add(s.name);
        }
        if (others.Count > 0)
            EditorGUILayout.HelpBox($"Other room scene(s) loaded and UNTOUCHED: {string.Join(", ", others)}. The placer only writes to '{active.name}'.", MessageType.Warning);
    }

    // ------------------------------------------------------------------- run

    void Run(Scene active)
    {
        report.Clear();

        if (operation == Operation.Remove) { RunRemove(active); return; }
        if (operation == Operation.Regenerate)
        {
            if (!EditorUtility.DisplayDialog("Regenerate", "Remove this scene's generated props (matching the current prefab filter) and re-place? This is destructive.", "Regenerate", "Cancel"))
                return;
            RemoveMatching(active, null, null, "Regenerate (clear)");
        }

        RunPlace(active);
    }

    void RunPlace(Scene active)
    {
        List<PrefabEntry> valid = prefabs.Where(p => p.prefab != null).ToList();
        if (valid.Count == 0) { report.Add("No prefabs assigned."); return; }

        // ---- measure each distinct prefab once ----
        Dictionary<GameObject, PrefabDims> dims = new Dictionary<GameObject, PrefabDims>();
        foreach (PrefabEntry e in valid.GroupBy(p => p.prefab).Select(g => g.First()))
        {
            PrefabDims d = Measure(e.prefab, active);
            dims[e.prefab] = d;
            if (d.warning != null) report.Add(d.warning);
        }

        // Drop unfit prefabs (no collider / off-anchor). An off-anchor prefab is
        // an authoring bug - placing it 40× just multiplies the desk sort bug.
        valid = valid.Where(p => dims[p.prefab].hasCollider && dims[p.prefab].anchorOk).ToList();
        if (valid.Count == 0) { report.Add("No usable prefabs after the anchor/collider audit - fix them and retry."); return; }

        Grid grid = FindGrid(active);
        if (grid == null) { report.Add("No Grid in the active scene."); return; }

        // ---- build the placement domain (once per run) ----
        HashSet<Vector3Int> floor = FloorCells(active);
        HashSet<Vector3Int> wall = WallCells(active);
        HashSet<Vector3Int> blocked = BlockedCells(active, grid, floor);
        Dictionary<Vector3Int, int> distToNonFloor = DistanceField(floor);

        // claimed footprint cells + placed bases + placed spans (for §5.2 rules)
        HashSet<Vector3Int> claimed = new HashSet<Vector3Int>(blocked);
        List<Vector3Int> placedBases = new List<Vector3Int>();
        List<RectInt> placedSpans = new List<RectInt>();

        System.Random rng = new System.Random(seed);
        string batchId = System.Guid.NewGuid().ToString();
        Transform root = FindOrCreateRoot(active);

        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName($"Scatter Placer ({operation})");

        int placed = 0, wanted = count;
        for (int i = 0; i < wanted; i++)
        {
            PrefabEntry entry = WeightedPickPrefab(valid, rng);
            PrefabDims d = dims[entry.prefab];

            // candidate base-left cells where this prefab fits right now
            List<Vector3Int> candidates = new List<Vector3Int>();
            List<double> weights = new List<double>();
            foreach (Vector3Int c in floor)
            {
                if (!Fits(c, d, floor, wall, claimed, placedBases, placedSpans)) continue;
                if (minSpacingCells > 0 && placedBases.Any(b => Cheb(b, c) < minSpacingCells)) continue;
                candidates.Add(c);
                weights.Add(CellWeight(c, distToNonFloor, placedBases));
            }

            if (candidates.Count == 0) break; // ran out of room

            int pick = WeightedIndex(weights, rng);
            Vector3Int baseCell = candidates[pick];

            PlaceOne(entry.prefab, baseCell, d, grid, root, batchId, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(entry.prefab)));

            // claim this prop's whole footprint row + record base/span
            for (int dx = 0; dx < d.w; dx++) claimed.Add(new Vector3Int(baseCell.x + dx, baseCell.y, 0));
            placedBases.Add(baseCell);
            placedSpans.Add(new RectInt(baseCell.x, baseCell.y, d.w, d.h));
            placed++;
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(active);

        report.Insert(0, placed == wanted
            ? $"Placed {placed} of {wanted}."
            : $"Placed {placed} of {wanted} requested - ran out of valid cells.");
        Repaint();
    }

    // ------------------------------------------------------- measurement

    PrefabDims Measure(GameObject prefab, Scene active)
    {
        GameObject temp = (GameObject)PrefabUtility.InstantiatePrefab(prefab, active);
        temp.transform.position = Vector3.zero;
        try
        {
            Collider2D col = temp.GetComponentInChildren<Collider2D>();
            Renderer[] rends = temp.GetComponentsInChildren<Renderer>();
            float cell = FindGrid(active) != null ? FindGrid(active).cellSize.x : 1f;

            if (col == null || rends.Length == 0)
                return new PrefabDims { hasCollider = col != null, anchorOk = false, warning = $"'{prefab.name}': no {(col == null ? "Collider2D" : "Renderer")} - excluded." };

            Bounds fp = col.bounds;
            Bounds vis = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) vis.Encapsulate(rends[i].bounds);

            int w = Mathf.Max(1, Mathf.RoundToInt(fp.size.x / cell));
            int h = Mathf.Max(1, Mathf.RoundToInt(vis.size.y / cell));

            // §3.2 sort-anchor: root transform must sit at the collider's south
            // edge (Y) and X-centre. It sits at world origin here.
            float dy = Mathf.Abs(fp.min.y - temp.transform.position.y);
            float dx = Mathf.Abs(fp.center.x - temp.transform.position.x);
            bool ok = dy <= AnchorTolerance && dx <= AnchorTolerance;
            string warn = ok ? null
                : $"'{prefab.name}': root anchor off footprint by (x {dx:+0.00;-0.00}, y {dy:+0.00;-0.00}) - {(dy > dx ? "pivot/transform Y" : "X centre")} is the likely culprit. Excluded until fixed (§3.1).";

            return new PrefabDims { w = w, h = h, hasCollider = true, anchorOk = ok, warning = warn };
        }
        finally
        {
            Object.DestroyImmediate(temp);
        }
    }

    // ------------------------------------------------------- domain build

    HashSet<Vector3Int> FloorCells(Scene active)
    {
        HashSet<Vector3Int> set = new HashSet<Vector3Int>();
        foreach (FloorTilemapGroup g in Object.FindObjectsByType<FloorTilemapGroup>(FindObjectsInactive.Exclude))
        {
            if (g.gameObject.scene != active) continue;
            foreach (Tilemap tm in g.GetTilemaps()) AddTiles(tm, set);
        }
        return set;
    }

    HashSet<Vector3Int> WallCells(Scene active)
    {
        HashSet<Vector3Int> set = new HashSet<Vector3Int>();
        Tilemap wall = wallTilemapOverride;
        if (wall == null || wall.gameObject.scene != active)
        {
            wall = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude)
                .FirstOrDefault(t => t.gameObject.scene == active && t.gameObject.name == "WallTilemap");
        }
        if (wall == null)
            report.Add("No 'WallTilemap' found (and no override) - wall-affinity and the floor-or-wall span rule treat every non-floor cell as void.");
        else
            AddTiles(wall, set);
        return set;
    }

    static void AddTiles(Tilemap tm, HashSet<Vector3Int> set)
    {
        foreach (Vector3Int c in tm.cellBounds.allPositionsWithin)
            if (tm.HasTile(c)) set.Add(new Vector3Int(c.x, c.y, 0));
    }

    HashSet<Vector3Int> BlockedCells(Scene active, Grid grid, HashSet<Vector3Int> floor)
    {
        HashSet<Vector3Int> blocked = new HashSet<Vector3Int>();

        // existing colliders (bounds, snapped to cells). Skip: our own markers;
        // tilemap/composite colliders (their AABB spans the whole wall outline =
        // the whole room, which would block every floor cell); and trigger zones
        // (interaction volumes - doors/interactables are handled by clearance).
        foreach (Collider2D col in Object.FindObjectsByType<Collider2D>(FindObjectsInactive.Exclude))
        {
            if (col.gameObject.scene != active) continue;
            if (col is TilemapCollider2D || col is CompositeCollider2D) continue;
            if (col.isTrigger) continue;
            if (col.GetComponentInParent<GeneratedProp>() != null) continue;
            foreach (Vector3Int c in floor)
                if (col.bounds.Contains(new Vector3(grid.GetCellCenterWorld(c).x, grid.GetCellCenterWorld(c).y, col.bounds.center.z)))
                    blocked.Add(c);
        }

        // door / interactable clearance
        foreach (Door door in Object.FindObjectsByType<Door>(FindObjectsInactive.Exclude))
            if (door.gameObject.scene == active) AddClearance(door.transform.position, doorClearanceCells, grid, floor, blocked);

        foreach (InteractableTrigger it in Object.FindObjectsByType<InteractableTrigger>(FindObjectsInactive.Exclude))
            if (it.gameObject.scene == active && !(it is Door)) AddClearance(it.transform.position, interactableClearanceCells, grid, floor, blocked);

        return blocked;
    }

    static void AddClearance(Vector3 world, int cells, Grid grid, HashSet<Vector3Int> floor, HashSet<Vector3Int> blocked)
    {
        Vector3Int centre = grid.WorldToCell(world);
        centre.z = 0;
        foreach (Vector3Int c in floor)
            if (Cheb(centre, c) <= cells) blocked.Add(c);
    }

    // Distance (in cells, 4-connectivity) from each floor cell to the nearest
    // non-floor cell. Small near walls/edges, large in open centres.
    static Dictionary<Vector3Int, int> DistanceField(HashSet<Vector3Int> floor)
    {
        Dictionary<Vector3Int, int> dist = new Dictionary<Vector3Int, int>();
        Queue<Vector3Int> q = new Queue<Vector3Int>();
        Vector3Int[] n4 = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };

        // seed: floor cells that touch a non-floor cell are distance 1
        foreach (Vector3Int c in floor)
            foreach (Vector3Int d in n4)
                if (!floor.Contains(c + d)) { if (!dist.ContainsKey(c)) { dist[c] = 1; q.Enqueue(c); } break; }

        while (q.Count > 0)
        {
            Vector3Int c = q.Dequeue();
            foreach (Vector3Int d in n4)
            {
                Vector3Int nc = c + d;
                if (floor.Contains(nc) && !dist.ContainsKey(nc)) { dist[nc] = dist[c] + 1; q.Enqueue(nc); }
            }
        }
        return dist;
    }

    // ------------------------------------------------------- fit + weight

    bool Fits(Vector3Int baseCell, PrefabDims d, HashSet<Vector3Int> floor, HashSet<Vector3Int> wall,
              HashSet<Vector3Int> claimed, List<Vector3Int> placedBases, List<RectInt> placedSpans)
    {
        // footprint: whole W×1 base row is free floor
        for (int dx = 0; dx < d.w; dx++)
        {
            Vector3Int c = new Vector3Int(baseCell.x + dx, baseCell.y, 0);
            if (!floor.Contains(c) || claimed.Contains(c)) return false;
        }

        // visual span: every W×H cell is floor or wall (never void) - §5.2 rule 1
        for (int dy = 0; dy < d.h; dy++)
            for (int dx = 0; dx < d.w; dx++)
            {
                Vector3Int c = new Vector3Int(baseCell.x + dx, baseCell.y + dy, 0);
                if (!floor.Contains(c) && !wall.Contains(c)) return false;
            }

        // §5.2 rule 3: no existing base inside my span, and my base not inside an existing span
        RectInt span = new RectInt(baseCell.x, baseCell.y, d.w, d.h);
        foreach (Vector3Int b in placedBases)
            if (span.Contains(new Vector2Int(b.x, b.y))) return false;
        foreach (RectInt s in placedSpans)
            if (s.Contains(new Vector2Int(baseCell.x, baseCell.y))) return false;

        return true;
    }

    double CellWeight(Vector3Int c, Dictionary<Vector3Int, int> distToNonFloor, List<Vector3Int> placedBases)
    {
        double w = 1.0;

        // wall affinity: bias by closeness to nearest non-floor cell
        if (Mathf.Abs(wallAffinity) > 0.001f)
        {
            int dist = distToNonFloor.TryGetValue(c, out int dv) ? dv : 8;
            double closeness = 1.0 / (1.0 + dist);              // ~1 at a wall, →0 in the open
            w *= System.Math.Pow(closeness, wallAffinity * 2.0); // +aff favours walls, -aff favours open
        }

        // clustering: bias by closeness to the nearest already-placed base
        if (placedBases.Count > 0)
        {
            int nearest = placedBases.Min(b => Cheb(b, c));
            double closeness = 1.0 / (1.0 + nearest);
            w *= System.Math.Pow(closeness, (clustering * 2.0 - 1.0) * 2.0); // 1→clump, 0→spread
        }

        return System.Math.Max(w, 1e-6);
    }

    // ------------------------------------------------------- placement

    void PlaceOne(GameObject prefab, Vector3Int baseCell, PrefabDims d, Grid grid, Transform root, string batchId, string prefabGuid)
    {
        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.gameObject.scene);
        inst.transform.SetParent(root, true);

        // anchor at the south edge (Y) X-centred over the W-cell base row
        Vector3 corner = grid.CellToWorld(baseCell);
        inst.transform.position = new Vector3(corner.x + d.w * grid.cellSize.x * 0.5f, corner.y, 0f);

        GeneratedProp mark = inst.AddComponent<GeneratedProp>();
        mark.sourcePrefabGuid = prefabGuid;
        mark.familyId = prefab.name;
        mark.batchId = batchId;
        mark.seed = seed;

        Undo.RegisterCreatedObjectUndo(inst, "Scatter place");
    }

    Transform FindOrCreateRoot(Scene active)
    {
        foreach (GameObject r in active.GetRootGameObjects())
            if (r.name == GeneratedRootName) return r.transform;

        GameObject go = new GameObject(GeneratedRootName);
        SceneManager.MoveGameObjectToScene(go, active);
        Undo.RegisterCreatedObjectUndo(go, "Create GeneratedProps root");
        return go.transform;
    }

    // ------------------------------------------------------------- remove

    void RunRemove(Scene active)
    {
        GameObject filterPrefab = removePrefabFilter;
        string filterGuid = filterPrefab != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(filterPrefab)) : null;

        List<string> batches = BatchIdsInScene(active);
        string batchFilter = (removeBatchIndex > 0 && removeBatchIndex - 1 < batches.Count) ? batches[removeBatchIndex - 1] : null;

        int matched = CountMatching(active, filterGuid, batchFilter);
        if (matched == 0) { report.Add("No generated props match the filter."); return; }
        if (matched > 50 && !EditorUtility.DisplayDialog("Remove", $"Delete {matched} generated props?", "Delete", "Cancel")) return;

        int removed = RemoveMatching(active, filterGuid, batchFilter, "Scatter remove");
        EditorSceneManager.MarkSceneDirty(active);
        report.Insert(0, $"Removed {removed} generated prop(s).");
        Repaint();
    }

    int RemoveMatching(Scene active, string guidFilter, string batchFilter, string undoName)
    {
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(undoName);
        int removed = 0;
        foreach (GeneratedProp gp in Object.FindObjectsByType<GeneratedProp>(FindObjectsInactive.Exclude))
        {
            if (gp.gameObject.scene != active) continue;
            if (guidFilter != null && gp.sourcePrefabGuid != guidFilter) continue;
            if (batchFilter != null && gp.batchId != batchFilter) continue;
            Undo.DestroyObjectImmediate(gp.gameObject);
            removed++;
        }
        Undo.CollapseUndoOperations(group);
        return removed;
    }

    int CountMatching(Scene active, string guidFilter, string batchFilter) =>
        Object.FindObjectsByType<GeneratedProp>(FindObjectsInactive.Exclude).Count(gp =>
            gp.gameObject.scene == active
            && (guidFilter == null || gp.sourcePrefabGuid == guidFilter)
            && (batchFilter == null || gp.batchId == batchFilter));

    List<string> BatchIdsInScene(Scene active) =>
        Object.FindObjectsByType<GeneratedProp>(FindObjectsInactive.Exclude)
            .Where(gp => gp.gameObject.scene == active && !string.IsNullOrEmpty(gp.batchId))
            .Select(gp => gp.batchId).Distinct().ToList();

    // ------------------------------------------------------------- helpers

    static Grid FindGrid(Scene active) =>
        Object.FindObjectsByType<Grid>(FindObjectsInactive.Exclude).FirstOrDefault(g => g.gameObject.scene == active);

    static int Cheb(Vector3Int a, Vector3Int b) => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

    PrefabEntry WeightedPickPrefab(List<PrefabEntry> list, System.Random rng)
    {
        int total = list.Sum(e => Mathf.Max(1, e.weight));
        int r = rng.Next(total);
        foreach (PrefabEntry e in list) { r -= Mathf.Max(1, e.weight); if (r < 0) return e; }
        return list[list.Count - 1];
    }

    static int WeightedIndex(List<double> weights, System.Random rng)
    {
        double total = weights.Sum();
        double r = rng.NextDouble() * total;
        for (int i = 0; i < weights.Count; i++) { r -= weights[i]; if (r <= 0) return i; }
        return weights.Count - 1;
    }
}
