using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Doors > Seal Rooms Outside Ship Scope (deadline shortcut, 2026-07-25).
//
// Confines the player to a hand-picked KEEP-SET of rooms by sealing every door
// that leaves it. A door is a DoorConnection asset with two DoorId endpoints,
// each carrying its scene name; this seals (startsLocked = true) any connection
// whose two endpoints are NOT both inside the keep-set.
//
// SCOPE / SAFETY:
//  - Asset-only. Touches DoorConnection assets, never scenes. Re-runnable.
//  - Only ADDS seals to boundary/outside connections. It deliberately does NOT
//    touch connections that are wholly inside the keep-set, so an intentional
//    in-scope locked door / keypad puzzle is left alone. (Consequence: if you
//    later GROW the keep-set, re-running won't re-open a door it sealed under a
//    smaller set - unseal those by hand; they're listed in the report.)
//  - Capacity only accrues when a room's terminal activates, and sealed rooms
//    never activate, so MaxCapacity and the ending threshold scale to the
//    keep-set for free. Pruning the board / Incremental.allRooms is a SEPARATE
//    step (those dark lamps otherwise linger) - run this first, read the
//    reachability report, then prune.
public static class ShipScopeTool
{
    // The rooms that ship. Edit this list to change scope, then re-run.
    // Names must match the scene file names exactly (same string DoorId stores).
    static readonly HashSet<string> KeepSet = new HashSet<string>
    {
        "SecurityRoom",
        "AssemblyHall",
        "Hallway_1",
        "Hallway_2_with_BreakoutSpace",
        "Office_1",
        "Office_8",
        "OpenSpace",
    };

    const string StartScene = "SecurityRoom"; // reachability BFS origin (bootstrap)

    [MenuItem("Tools/Doors/Seal Rooms Outside Ship Scope")]
    static void SealOutsideScope()
    {
        List<DoorConnection> all = LoadAllConnections();
        if (all.Count == 0)
        {
            Debug.LogWarning("[ShipScope] No DoorConnection assets found.");
            return;
        }

        var sealedNow = new List<string>();
        var alreadySealed = new List<string>();
        var keptOpen = new List<string>();      // intra-keep connections left alone
        var inScopeGates = new List<string>();   // intra-keep but locked/priced - verify intended

        foreach (DoorConnection conn in all)
        {
            string a = conn.EndpointA != null ? conn.EndpointA.SceneName : null;
            string b = conn.EndpointB != null ? conn.EndpointB.SceneName : null;
            bool bothKept = a != null && b != null && KeepSet.Contains(a) && KeepSet.Contains(b);

            if (bothKept)
            {
                keptOpen.Add($"{conn.name}  ({a} <-> {b})");
                if (conn.StartsLocked || conn.IsPriced)
                {
                    string why = conn.StartsLocked ? "startsLocked" : $"priced {conn.ClickCost}";
                    inScopeGates.Add($"{conn.name}  ({a} <-> {b})  [{why}]");
                }
                continue; // leave in-scope connections untouched
            }

            if (conn.StartsLocked)
            {
                alreadySealed.Add($"{conn.name}  ({a ?? "?"} <-> {b ?? "?"})");
                continue;
            }

            var so = new SerializedObject(conn);
            SerializedProperty locked = so.FindProperty("startsLocked");
            if (locked == null)
            {
                Debug.LogError($"[ShipScope] '{conn.name}' has no 'startsLocked' field - schema changed?", conn);
                continue;
            }
            locked.boolValue = true;
            so.ApplyModifiedProperties(); // records undo
            EditorUtility.SetDirty(conn);
            sealedNow.Add($"{conn.name}  ({a ?? "?"} <-> {b ?? "?"})");
        }

        AssetDatabase.SaveAssets();

        var sb = new StringBuilder();
        sb.AppendLine($"[ShipScope] Keep-set ({KeepSet.Count} rooms): {string.Join(", ", KeepSet.OrderBy(s => s))}");
        sb.AppendLine($"Sealed now: {sealedNow.Count}   already sealed: {alreadySealed.Count}   kept open (in-scope): {keptOpen.Count}");
        Append(sb, "SEALED THIS RUN", sealedNow);
        Append(sb, "ALREADY SEALED (left as-is)", alreadySealed);
        if (inScopeGates.Count > 0)
            Append(sb, "IN-SCOPE GATES (locked/priced inside the keep-set - verify each is reachable/intended)", inScopeGates);

        ReportReachability(all, sb);
        Debug.Log(sb.ToString());
    }

    // Graph connectivity over connections that stay OPEN inside the keep-set
    // (both endpoints kept AND not sealed). Answers "can the player still reach
    // every kept room from the start room?" Priced/in-scope-locked edges ARE
    // included as traversable here (they open with clicks / an unlocker) but are
    // flagged separately above, so a keypad on the path won't false-alarm.
    static void ReportReachability(List<DoorConnection> all, StringBuilder sb)
    {
        var adj = new Dictionary<string, HashSet<string>>();
        foreach (string room in KeepSet) adj[room] = new HashSet<string>();

        foreach (DoorConnection conn in all)
        {
            string a = conn.EndpointA != null ? conn.EndpointA.SceneName : null;
            string b = conn.EndpointB != null ? conn.EndpointB.SceneName : null;
            if (a == null || b == null || !KeepSet.Contains(a) || !KeepSet.Contains(b)) continue;
            if (conn.StartsLocked) continue; // sealed => not an edge

            adj[a].Add(b);
            if (!conn.IsOneWay) adj[b].Add(a);
        }

        var seen = new HashSet<string> { StartScene };
        var queue = new Queue<string>();
        queue.Enqueue(StartScene);
        while (queue.Count > 0)
        {
            string cur = queue.Dequeue();
            if (!adj.TryGetValue(cur, out HashSet<string> next)) continue;
            foreach (string n in next)
                if (seen.Add(n)) queue.Enqueue(n);
        }

        var unreached = KeepSet.Where(r => !seen.Contains(r)).OrderBy(s => s).ToList();
        sb.AppendLine();
        sb.AppendLine($"REACHABILITY from '{StartScene}' over open in-scope doors: {seen.Intersect(KeepSet).Count()}/{KeepSet.Count} rooms reachable.");
        if (unreached.Count > 0)
            sb.AppendLine($"  !! STRANDED (no open path from {StartScene}): {string.Join(", ", unreached)}");
        else
            sb.AppendLine("  OK - every kept room is reachable.");
    }

    // Tools > Circuit > Prune Circuit To Ship Scope.
    //
    // Removes rooms outside the keep-set from Incremental.allRooms and the
    // SecurityRoom board's lamp list. REQUIRED, not cosmetic: EndConditionMet
    // gates on AllRoomsActivated, which needs EVERY listed room activated - a
    // sealed room can never activate, so leaving it in allRooms makes the End
    // Button un-summonable and the game unwinnable (Incremental.cs:402).
    //
    // Scene-touching: needs the scenes holding Incremental and the RoomLampBoard
    // loaded (normal setup: PersistentScene + SecurityRoom). Deactivates the
    // orphaned lamp GameObjects so the board doesn't show dead dark lamps.
    // Revert cleanly with git if the keep-set changes.
    [MenuItem("Tools/Circuit/Prune Circuit To Ship Scope")]
    static void PruneCircuit()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[ShipScope] Prune to keep-set ({KeepSet.Count}): {string.Join(", ", KeepSet.OrderBy(s => s))}");

        Incremental inc = Object.FindAnyObjectByType<Incremental>(FindObjectsInactive.Include);
        if (inc == null)
        {
            Debug.LogError("[ShipScope] No Incremental in any loaded scene. Open PersistentScene and retry.");
            return;
        }

        var incSo = new SerializedObject(inc);
        SerializedProperty rooms = incSo.FindProperty("allRooms");
        var removedRooms = new List<string>();
        for (int i = rooms.arraySize - 1; i >= 0; i--)
        {
            SerializedProperty el = rooms.GetArrayElementAtIndex(i);
            RoomId rid = el.objectReferenceValue as RoomId;
            if (rid != null && KeepSet.Contains(rid.SceneName)) continue;

            removedRooms.Add(rid != null ? $"{rid.name} ({rid.SceneName})" : "<null slot>");
            el.objectReferenceValue = null;           // avoid the two-step delete quirk on object-ref arrays
            rooms.DeleteArrayElementAtIndex(i);
        }
        incSo.ApplyModifiedProperties();
        EditorUtility.SetDirty(inc);
        EditorSceneManager.MarkSceneDirty(inc.gameObject.scene);

        sb.AppendLine($"Incremental.allRooms: removed {removedRooms.Count}, kept {rooms.arraySize}.");
        Append(sb, "REMOVED FROM allRooms", removedRooms);

        RoomLampBoard board = Object.FindAnyObjectByType<RoomLampBoard>(FindObjectsInactive.Include);
        if (board == null)
        {
            sb.AppendLine("No RoomLampBoard in a loaded scene - board NOT pruned (open SecurityRoom to prune it).");
        }
        else
        {
            var boardSo = new SerializedObject(board);
            SerializedProperty lamps = boardSo.FindProperty("lamps");
            var removedLamps = new List<string>();
            int deactivated = 0;
            for (int i = lamps.arraySize - 1; i >= 0; i--)
            {
                SerializedProperty el = lamps.GetArrayElementAtIndex(i);
                RoomId rid = el.FindPropertyRelative("room").objectReferenceValue as RoomId;
                if (rid != null && KeepSet.Contains(rid.SceneName)) continue;

                SpriteRenderer sr = el.FindPropertyRelative("lamp").objectReferenceValue as SpriteRenderer;
                if (sr != null)
                {
                    Undo.RecordObject(sr.gameObject, "Prune lamp");
                    sr.gameObject.SetActive(false);
                    deactivated++;
                }
                removedLamps.Add(rid != null ? $"{rid.name} ({rid.SceneName})" : "<null slot>");
                lamps.DeleteArrayElementAtIndex(i);
            }
            boardSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(board);
            EditorSceneManager.MarkSceneDirty(board.gameObject.scene);

            sb.AppendLine($"RoomLampBoard.lamps: removed {removedLamps.Count} entries, deactivated {deactivated} lamp objects, kept {lamps.arraySize}.");
            Append(sb, "REMOVED FROM board", removedLamps);
        }

        sb.AppendLine();
        sb.AppendLine("Done. SAVE the modified scene(s) (Ctrl+S) to persist.");
        Debug.Log(sb.ToString());
    }

    static List<DoorConnection> LoadAllConnections() =>
        AssetDatabase.FindAssets("t:DoorConnection")
            .Select(g => AssetDatabase.LoadAssetAtPath<DoorConnection>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(c => c != null)
            .ToList();

    static void Append(StringBuilder sb, string header, List<string> items)
    {
        if (items.Count == 0) return;
        sb.AppendLine($"-- {header} ({items.Count}) --");
        foreach (string s in items.OrderBy(x => x)) sb.AppendLine("   " + s);
    }
}
