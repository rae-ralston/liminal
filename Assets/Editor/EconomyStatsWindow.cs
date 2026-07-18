using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Circuit > Economy Stats (2026-07-18): read-only dashboard over the
// numbers DoorConsistencyWindow's reachability lint already computes, plus
// totals/breakdowns useful for a balancing pass. This is NOT a lint - it never
// flags anything as wrong, it just reports. Run the Door Consistency Checker
// first (or alongside) if you want pass/fail validation; this tool answers
// "what does the map currently add up to."
//
// Reuses the same GUID-only scene-loop discipline as DoorConsistencyWindow
// (see CLAUDE.md Known Gotchas) because CapacityUpgrade totals only exist as
// scene props, not assets - RoomId/DoorId/DoorConnection numbers are all
// asset-level and could be read without opening scenes, but the scan opens
// every room scene once anyway for the upgrade tally and to stay in exact
// lockstep with the checker's own fixpoint (same inputs, same answer).
public class EconomyStatsWindow : EditorWindow
{
  private const string RoomSceneFolder = "Assets/Scenes/Rooms";
  private const string BootstrapSceneName = "SecurityRoom";

  private class RoomRow
  {
    public string sceneName;
    public long baseCapacity;
    public long activationCost;
    public long upgradeCapacity;
    public bool reachable;
    public bool activatable;
  }

  private class DoorRow
  {
    public string name;
    public string endpointA;
    public string endpointB;
    public long clickCost;
    public bool isOneWay;
    public bool startsLocked;
    public bool anyEndpointReachable;
  }

  private Vector2 scroll;
  private bool hasRun;
  private bool showRooms = true;
  private bool showDoors = true;

  private int roomCount;
  private int reachableCount;
  private int activatableCount;
  private long maxCapacityAttainable;
  private long totalBaseCapacity;
  private long totalUpgradeCapacity;
  private string bootstrapName;
  private long residueSeed;

  private long activationSpendTotal;
  private long doorSpendTotal;
  private int pricedDoorCount;
  private int freeDoorCount;
  private int oneWayDoorCount;
  private int startsLockedDoorCount;

  private List<RoomRow> roomRows = new List<RoomRow>();
  private List<DoorRow> doorRows = new List<DoorRow>();

  [MenuItem("Tools/Circuit/Economy Stats")]
  private static void Open()
  {
    GetWindow<EconomyStatsWindow>("Economy Stats");
  }

  private void OnGUI()
  {
    EditorGUILayout.HelpBox(
      "Read-only stats over the current Circuit economy: MaxCapacity, spend totals, and a per-room/per-door " +
      "breakdown. Opens every scene in " + RoomSceneFolder + " once (to tally placed CapacityUpgrades), then " +
      "restores whatever you had open. This does not flag problems - use Tools > Doors > Door Consistency " +
      "Checker for that.",
      MessageType.Info);

    using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
    {
      if (GUILayout.Button("Scan", GUILayout.Height(30)))
        Scan();
    }

    if (EditorApplication.isPlaying)
      EditorGUILayout.HelpBox("Exit Play Mode to scan (it needs to open scenes).", MessageType.Warning);

    if (!hasRun)
      return;

    scroll = EditorGUILayout.BeginScrollView(scroll);

    EditorGUILayout.Space();
    EditorGUILayout.LabelField("Overview", EditorStyles.boldLabel);
    using (new EditorGUILayout.VerticalScope("box"))
    {
      Field("Rooms", $"{roomCount}");
      Field("Bootstrap room", $"{bootstrapName} (residue seed = {residueSeed})");
      Field("Reachable", $"{reachableCount} / {roomCount}");
      Field("Activatable", $"{activatableCount} / {roomCount}");
      Field("MaxCapacity (attainable)", $"{maxCapacityAttainable}");
      Field("Total baseCapacity (all rooms)", $"{totalBaseCapacity}");
      Field("Total CapacityUpgrade capacity (placed)", $"{totalUpgradeCapacity}");

      long theoreticalMax = totalBaseCapacity + totalUpgradeCapacity;
      if (theoreticalMax != maxCapacityAttainable)
        EditorGUILayout.HelpBox(
          $"{theoreticalMax - maxCapacityAttainable} capacity is stranded: reachable/activatable at a fixpoint " +
          $"({maxCapacityAttainable}) is less than the {theoreticalMax} the map contains in total. Some room or " +
          "upgrade is unreachable or unaffordable forever - check the Door Consistency Checker.",
          MessageType.Warning);
    }

    EditorGUILayout.Space();
    EditorGUILayout.LabelField("Spend by category", EditorStyles.boldLabel);
    using (new EditorGUILayout.VerticalScope("box"))
    {
      Field("Room activations", $"{roomCount} rooms, total {activationSpendTotal}");
      Field("Door purchases", $"{pricedDoorCount} priced ({freeDoorCount} free), total {doorSpendTotal}");
      Field("Grand total spend (activate + buy everything)", $"{activationSpendTotal + doorSpendTotal}");
      Field("One-way doors", $"{oneWayDoorCount}");
      Field("Doors starting locked (DoorUnlocker)", $"{startsLockedDoorCount}");
    }

    if (roomRows.Count > 0)
    {
      EditorGUILayout.Space();
      EditorGUILayout.LabelField("Extremes", EditorStyles.boldLabel);
      using (new EditorGUILayout.VerticalScope("box"))
      {
        RoomRow minCap = roomRows.OrderBy(r => r.baseCapacity).First();
        RoomRow maxCap = roomRows.OrderByDescending(r => r.baseCapacity).First();
        RoomRow minAct = roomRows.OrderBy(r => r.activationCost).First();
        RoomRow maxAct = roomRows.OrderByDescending(r => r.activationCost).First();
        Field("baseCapacity range", $"{minCap.baseCapacity} ({minCap.sceneName}) .. {maxCap.baseCapacity} ({maxCap.sceneName}), avg {roomRows.Average(r => r.baseCapacity):0.#}");
        Field("activationCost range", $"{minAct.activationCost} ({minAct.sceneName}) .. {maxAct.activationCost} ({maxAct.sceneName}), avg {roomRows.Average(r => r.activationCost):0.#}");

        List<DoorRow> priced = doorRows.Where(d => d.clickCost > 0).ToList();
        if (priced.Count > 0)
        {
          DoorRow minDoor = priced.OrderBy(d => d.clickCost).First();
          DoorRow maxDoor = priced.OrderByDescending(d => d.clickCost).First();
          Field("Priced door clickCost range", $"{minDoor.clickCost} ({minDoor.name}) .. {maxDoor.clickCost} ({maxDoor.name}), avg {priced.Average(d => d.clickCost):0.#}");
        }
      }
    }

    EditorGUILayout.Space();
    showRooms = EditorGUILayout.Foldout(showRooms, $"Per-room breakdown ({roomRows.Count})", true);
    if (showRooms)
    {
      using (new EditorGUILayout.VerticalScope("box"))
      {
        DrawRoomHeader();
        foreach (RoomRow row in roomRows.OrderByDescending(r => r.activationCost))
          DrawRoomRow(row);
      }
    }

    EditorGUILayout.Space();
    showDoors = EditorGUILayout.Foldout(showDoors, $"Priced doors ({pricedDoorCount})", true);
    if (showDoors)
    {
      using (new EditorGUILayout.VerticalScope("box"))
      {
        DrawDoorHeader();
        foreach (DoorRow row in doorRows.Where(d => d.clickCost > 0).OrderByDescending(d => d.clickCost))
          DrawDoorRow(row);
      }
    }

    EditorGUILayout.EndScrollView();
  }

  private static void Field(string label, string value)
  {
    EditorGUILayout.BeginHorizontal();
    EditorGUILayout.LabelField(label, GUILayout.Width(280));
    EditorGUILayout.LabelField(value, EditorStyles.boldLabel);
    EditorGUILayout.EndHorizontal();
  }

  private static void DrawRoomHeader()
  {
    EditorGUILayout.BeginHorizontal();
    EditorGUILayout.LabelField("Scene", EditorStyles.miniBoldLabel, GUILayout.Width(220));
    EditorGUILayout.LabelField("baseCap", EditorStyles.miniBoldLabel, GUILayout.Width(60));
    EditorGUILayout.LabelField("actCost", EditorStyles.miniBoldLabel, GUILayout.Width(60));
    EditorGUILayout.LabelField("upgrades", EditorStyles.miniBoldLabel, GUILayout.Width(60));
    EditorGUILayout.LabelField("reach", EditorStyles.miniBoldLabel, GUILayout.Width(50));
    EditorGUILayout.LabelField("active", EditorStyles.miniBoldLabel, GUILayout.Width(50));
    EditorGUILayout.EndHorizontal();
  }

  private static void DrawRoomRow(RoomRow row)
  {
    EditorGUILayout.BeginHorizontal();
    EditorGUILayout.LabelField(row.sceneName, GUILayout.Width(220));
    EditorGUILayout.LabelField(row.baseCapacity.ToString(), GUILayout.Width(60));
    EditorGUILayout.LabelField(row.activationCost.ToString(), GUILayout.Width(60));
    EditorGUILayout.LabelField(row.upgradeCapacity.ToString(), GUILayout.Width(60));
    EditorGUILayout.LabelField(row.reachable ? "yes" : "NO", GUILayout.Width(50));
    EditorGUILayout.LabelField(row.activatable ? "yes" : "NO", GUILayout.Width(50));
    EditorGUILayout.EndHorizontal();
  }

  private static void DrawDoorHeader()
  {
    EditorGUILayout.BeginHorizontal();
    EditorGUILayout.LabelField("Connection", EditorStyles.miniBoldLabel, GUILayout.Width(260));
    EditorGUILayout.LabelField("cost", EditorStyles.miniBoldLabel, GUILayout.Width(50));
    EditorGUILayout.LabelField("A <-> B", EditorStyles.miniBoldLabel, GUILayout.Width(300));
    EditorGUILayout.EndHorizontal();
  }

  private static void DrawDoorRow(DoorRow row)
  {
    EditorGUILayout.BeginHorizontal();
    EditorGUILayout.LabelField(row.name, GUILayout.Width(260));
    EditorGUILayout.LabelField(row.clickCost.ToString(), GUILayout.Width(50));
    string arrow = row.isOneWay ? " -> " : " <-> ";
    EditorGUILayout.LabelField($"{row.endpointA}{arrow}{row.endpointB}", GUILayout.Width(300));
    EditorGUILayout.EndHorizontal();
  }

  private void Scan()
  {
    if (EditorApplication.isPlaying)
    {
      Debug.LogError("[EconomyStats] Cannot scan while in Play Mode.");
      return;
    }

    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
      return;

    // GUID-only count check - safe before any scene opens (no live refs held).
    if (AssetDatabase.FindAssets("t:RoomId").Length == 0)
    {
      Debug.LogWarning("[EconomyStats] No RoomId assets found - Circuit not wired yet.");
      hasRun = false;
      return;
    }

    // --- scene loop: tally placed CapacityUpgrade capacity per scene (the
    // one number that isn't asset-level - everything else RoomId/DoorId/
    // DoorConnection already carry without opening a single scene). Only
    // GUID-free plain data (scene name -> long) survives this loop - per
    // CLAUDE.md Known Gotchas, a live RoomId/DoorConnection reference held
    // across OpenScene(Single) can go native-destroyed mid-loop (implicit
    // Resources.UnloadUnusedAssets), so those assets are loaded fresh only
    // AFTER every scene load below is done. ---

    string[] scenePaths = AssetDatabase.FindAssets("t:Scene", new[] { RoomSceneFolder })
      .Select(AssetDatabase.GUIDToAssetPath)
      .OrderBy(p => p)
      .ToArray();

    Dictionary<string, long> upgradeCapacityByScene = new Dictionary<string, long>();
    SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();

    foreach (string scenePath in scenePaths)
    {
      string sceneName = Path.GetFileNameWithoutExtension(scenePath);
      Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

      long upgradeSum = 0;
      foreach (CapacityUpgrade upgrade in Resources.FindObjectsOfTypeAll<CapacityUpgrade>()
                 .Where(c => c.gameObject.scene == scene))
      {
        SerializedProperty amount = new SerializedObject(upgrade).FindProperty("capacityAmount");
        if (amount != null && amount.longValue > 0)
          upgradeSum += amount.longValue;
      }
      upgradeCapacityByScene[sceneName] = upgradeSum;
    }

    bool restored = false;
    if (originalSetup != null && originalSetup.Length > 0 && originalSetup.All(s => !string.IsNullOrEmpty(s.path)))
    {
      EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
      restored = true;
    }
    if (!restored)
      Debug.LogWarning("[EconomyStats] Could not restore your previous scene setup (it had an untitled/unsaved scene) - reopen it manually.");

    // --- resolve fresh live objects now that scene loading is finished -
    // safe to hold onto them for the rest of this pass since no more scenes
    // will load (same discipline as DoorConsistencyWindow.RunCheck). ---

    Dictionary<string, RoomId> roomsByGuid = LoadAssets<RoomId>(AssetDatabase.FindAssets("t:RoomId"));
    Dictionary<string, DoorConnection> connectionsByGuid = LoadAssets<DoorConnection>(AssetDatabase.FindAssets("t:DoorConnection"));

    Dictionary<string, RoomId> roomsByScene = new Dictionary<string, RoomId>();
    foreach (RoomId room in roomsByGuid.Values)
      if (!string.IsNullOrEmpty(room.SceneName) && !roomsByScene.ContainsKey(room.SceneName))
        roomsByScene[room.SceneName] = room;

    // --- exact fixpoint (mirrors DoorConsistencyWindow.CheckReachability) ---

    long UpgradesIn(string sceneName) =>
      upgradeCapacityByScene.TryGetValue(sceneName, out long u) ? u : 0;

    if (!roomsByScene.TryGetValue(BootstrapSceneName, out RoomId bootstrap))
    {
      Debug.LogWarning($"[EconomyStats] No RoomId for '{BootstrapSceneName}' - can't compute the fixpoint.");
      hasRun = false;
      return;
    }

    HashSet<string> entered = new HashSet<string> { BootstrapSceneName };
    HashSet<string> activated = new HashSet<string> { BootstrapSceneName };
    long capacity = bootstrap.ActivationCost + bootstrap.BaseCapacity + UpgradesIn(BootstrapSceneName);

    List<DoorConnection> connections = connectionsByGuid.Values
      .Where(c => c.EndpointA != null && c.EndpointB != null)
      .ToList();

    bool changed = true;
    while (changed)
    {
      changed = false;

      foreach (DoorConnection conn in connections)
      {
        if (conn.IsPriced && conn.ClickCost > capacity)
          continue;

        string sceneA = conn.EndpointA.SceneName;
        string sceneB = conn.EndpointB.SceneName;
        if (entered.Contains(sceneA) && entered.Add(sceneB))
          changed = true;
        if (!conn.IsOneWay && entered.Contains(sceneB) && entered.Add(sceneA))
          changed = true;
      }

      foreach (string sceneName in entered.ToList())
      {
        if (activated.Contains(sceneName) || !roomsByScene.TryGetValue(sceneName, out RoomId room))
          continue;
        if (room.ActivationCost > capacity)
          continue;

        activated.Add(sceneName);
        capacity += room.BaseCapacity + UpgradesIn(sceneName);
        changed = true;
      }
    }

    // --- assemble stats ---

    roomRows = roomsByScene.Values.Select(r => new RoomRow
    {
      sceneName = r.SceneName,
      baseCapacity = r.BaseCapacity,
      activationCost = r.ActivationCost,
      upgradeCapacity = UpgradesIn(r.SceneName),
      reachable = entered.Contains(r.SceneName),
      activatable = activated.Contains(r.SceneName),
    }).ToList();

    doorRows = connections.Select(c => new DoorRow
    {
      name = c.name,
      endpointA = c.EndpointA.SceneName,
      endpointB = c.EndpointB.SceneName,
      clickCost = c.ClickCost,
      isOneWay = c.IsOneWay,
      startsLocked = c.StartsLocked,
      anyEndpointReachable = entered.Contains(c.EndpointA.SceneName) || entered.Contains(c.EndpointB.SceneName),
    }).ToList();

    roomCount = roomRows.Count;
    reachableCount = roomRows.Count(r => r.reachable);
    activatableCount = roomRows.Count(r => r.activatable);
    maxCapacityAttainable = capacity;
    totalBaseCapacity = roomRows.Sum(r => r.baseCapacity);
    totalUpgradeCapacity = roomRows.Sum(r => r.upgradeCapacity);
    bootstrapName = BootstrapSceneName;
    residueSeed = bootstrap.ActivationCost;

    activationSpendTotal = roomRows.Sum(r => r.activationCost);
    doorSpendTotal = doorRows.Where(d => d.clickCost > 0).Sum(d => d.clickCost);
    pricedDoorCount = doorRows.Count(d => d.clickCost > 0);
    freeDoorCount = doorRows.Count(d => d.clickCost == 0);
    oneWayDoorCount = doorRows.Count(d => d.isOneWay);
    startsLockedDoorCount = doorRows.Count(d => d.startsLocked);

    hasRun = true;
    Debug.Log($"[EconomyStats] Scanned {roomCount} rooms, {doorRows.Count} connections. MaxCapacity attainable: {maxCapacityAttainable}.");
    Repaint();
  }

  private static Dictionary<string, T> LoadAssets<T>(IEnumerable<string> guids) where T : Object
  {
    var result = new Dictionary<string, T>();
    foreach (string guid in guids)
    {
      string path = AssetDatabase.GUIDToAssetPath(guid);
      T asset = AssetDatabase.LoadAssetAtPath<T>(path);
      if (asset != null) result[guid] = asset;
    }
    return result;
  }
}
