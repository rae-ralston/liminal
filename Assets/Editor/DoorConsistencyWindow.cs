using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Opens every scene under RoomSceneFolder, inspects every Door component, and
// cross-checks it against every DoorId / DoorConnection asset in the project.
// Mirrors the manual audit used to find and fix the door-wiring bugs on the
// Prop-implementation branch: missing ids/connections, DoorId.sceneName not
// matching the scene the door lives in, a door's id not being an endpoint of
// its own connection, DoorIds used more than once (or never), and
// DoorConnections that are one-sided, over-referenced, self-looping, missing
// an endpoint, or duplicated (two assets sharing the same endpoint pair).
//
// Economy lints (Phase 6, 2026-07-15): negative clickCost, priced connections
// that ALSO start locked (double gate - needs both a DoorUnlocker and a
// purchase), priced connections with no DoorPurchaser keypad anywhere (door
// can never be bought), keypads with no/unpriced connections or placed in a
// scene that is neither endpoint's, DoorIndicatorLights that can't resolve a
// connection, and rooms whose EVERY door is priced (progression smell).
public class DoorConsistencyWindow : EditorWindow
{
  private const string RoomSceneFolder = "Assets/Scenes/Rooms";
  private const string BootstrapSceneName = "SecurityRoom";

  private enum Severity { Warning, Error }

  private class Issue
  {
    public Severity severity;
    public string message;
    public string assetGuid;
    public string scenePath;
    public string hierarchyPath;
  }

  // Only GUIDs/strings survive here, never DoorId/DoorConnection object references:
  // opening the next scene (OpenSceneMode.Single) can trigger an implicit
  // Resources.UnloadUnusedAssets pass, and a plain C# reference in a collection
  // does not root a ScriptableObject asset against that sweep. Holding the
  // Object itself here previously caused a MissingReferenceException as soon as
  // a later scene load unloaded it out from under us.
  private class DoorInstance
  {
    public string scenePath;
    public string sceneName;
    public string hierarchyPath;
    public string idGuid;
    public string connectionGuid;
  }

  private Vector2 scroll;
  private List<Issue> issues = new List<Issue>();
  private int doorCount;
  private int sceneCount;
  private bool hasRun;

  [MenuItem("Tools/Doors/Door Consistency Checker")]
  private static void Open()
  {
    GetWindow<DoorConsistencyWindow>("Door Consistency");
  }

  private void OnGUI()
  {
    EditorGUILayout.HelpBox(
      $"Opens every scene in {RoomSceneFolder}, inspects every Door component, and cross-checks it against " +
      "every DoorId / DoorConnection asset in the project. This will open scenes one at a time (offering to " +
      "save any unsaved changes first) and restores whatever you had open when it's done.",
      MessageType.Info);

    using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
    {
      if (GUILayout.Button("Run Check", GUILayout.Height(30)))
        RunCheck();
    }

    if (EditorApplication.isPlaying)
      EditorGUILayout.HelpBox("Exit Play Mode to run the check (it needs to open scenes).", MessageType.Warning);

    if (!hasRun)
      return;

    EditorGUILayout.Space();
    int errorCount = issues.Count(i => i.severity == Severity.Error);
    int warningCount = issues.Count(i => i.severity == Severity.Warning);

    if (issues.Count == 0)
    {
      EditorGUILayout.HelpBox(
        $"All clear. {doorCount} door instance(s) across {sceneCount} scene(s), no problems found.",
        MessageType.Info);
      return;
    }

    EditorGUILayout.LabelField(
      $"{doorCount} door instance(s) across {sceneCount} scene(s)  —  {errorCount} error(s), {warningCount} warning(s)",
      EditorStyles.boldLabel);
    EditorGUILayout.Space();

    scroll = EditorGUILayout.BeginScrollView(scroll);
    foreach (Issue issue in issues.OrderByDescending(i => i.severity))
    {
      EditorGUILayout.BeginVertical("box");

      GUIStyle style = new GUIStyle(EditorStyles.wordWrappedLabel);
      style.normal.textColor = issue.severity == Severity.Error
        ? new Color(0.95f, 0.35f, 0.35f)
        : new Color(0.85f, 0.65f, 0.15f);
      EditorGUILayout.LabelField((issue.severity == Severity.Error ? "ERROR  " : "WARNING  ") + issue.message, style);

      if (!string.IsNullOrEmpty(issue.assetGuid) || !string.IsNullOrEmpty(issue.scenePath))
      {
        EditorGUILayout.BeginHorizontal();
        if (!string.IsNullOrEmpty(issue.assetGuid) && GUILayout.Button("Ping Asset", GUILayout.Width(90)))
          PingAsset(issue.assetGuid);
        if (!string.IsNullOrEmpty(issue.scenePath) && GUILayout.Button("Open & Select", GUILayout.Width(110)))
          OpenAndSelect(issue.scenePath, issue.hierarchyPath);
        EditorGUILayout.EndHorizontal();
      }

      EditorGUILayout.EndVertical();
    }
    EditorGUILayout.EndScrollView();
  }

  private static void PingAsset(string guid)
  {
    string path = AssetDatabase.GUIDToAssetPath(guid);
    if (string.IsNullOrEmpty(path)) return;
    Object obj = AssetDatabase.LoadAssetAtPath<Object>(path);
    if (obj == null) return;
    Selection.activeObject = obj;
    EditorGUIUtility.PingObject(obj);
  }

  private static void OpenAndSelect(string scenePath, string hierarchyPath)
  {
    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
      return;

    Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
    GameObject go = FindByHierarchyPath(scene, hierarchyPath);
    if (go == null) return;
    Selection.activeGameObject = go;
    EditorGUIUtility.PingObject(go);
  }

  private void RunCheck()
  {
    if (EditorApplication.isPlaying)
    {
      Debug.LogError("[DoorConsistencyChecker] Cannot run while in Play Mode.");
      return;
    }

    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
      return;

    issues = new List<Issue>();
    doorCount = 0;

    // Just the GUIDs for now (see DoorInstance comment) - resolved to live
    // objects only after every scene load is done, further down.
    HashSet<string> allDoorIdGuids = new HashSet<string>(AssetDatabase.FindAssets("t:DoorId"));
    HashSet<string> allConnectionGuids = new HashSet<string>(AssetDatabase.FindAssets("t:DoorConnection"));

    Dictionary<string, List<DoorInstance>> doorIdUsage = new Dictionary<string, List<DoorInstance>>();
    Dictionary<string, List<DoorInstance>> connectionUsage = new Dictionary<string, List<DoorInstance>>();

    // DoorPurchaser keypads, keyed by their connection's GUID (same
    // GUID-only discipline as DoorInstance). Doors grouped per scene feed
    // the every-entrance-priced smell check.
    Dictionary<string, List<DoorInstance>> keypadUsage = new Dictionary<string, List<DoorInstance>>();
    List<DoorInstance> allDoors = new List<DoorInstance>();

    // Collectible CapacityUpgrade totals per scene, for the Circuit C7
    // reachability lint - plain longs, gathered while each scene is open.
    Dictionary<string, long> upgradeCapacityByScene = new Dictionary<string, long>();

    string[] scenePaths = AssetDatabase.FindAssets("t:Scene", new[] { RoomSceneFolder })
      .Select(AssetDatabase.GUIDToAssetPath)
      .OrderBy(p => p)
      .ToArray();
    sceneCount = scenePaths.Length;

    SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();

    foreach (string scenePath in scenePaths)
    {
      string sceneName = Path.GetFileNameWithoutExtension(scenePath);
      Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

      Door[] doors = Resources.FindObjectsOfTypeAll<Door>()
        .Where(d => d.gameObject.scene == scene)
        .ToArray();

      foreach (Door door in doors)
      {
        doorCount++;
        DoorId id = door.Id;
        DoorConnection connection = door.Connection;
        string hierarchyPath = GetHierarchyPath(door.transform);
        var instance = new DoorInstance
        {
          scenePath = scenePath,
          sceneName = sceneName,
          hierarchyPath = hierarchyPath,
          idGuid = id != null ? GetGuid(id) : null,
          connectionGuid = connection != null ? GetGuid(connection) : null,
        };
        allDoors.Add(instance);

        string tag = $"{sceneName} :: {hierarchyPath}";

        if (id == null)
        {
          AddIssue(Severity.Error, $"{tag} has no DoorId assigned.", scenePath: scenePath, hierarchyPath: hierarchyPath);
        }
        else
        {
          if (!doorIdUsage.TryGetValue(instance.idGuid, out List<DoorInstance> idUses))
            doorIdUsage[instance.idGuid] = idUses = new List<DoorInstance>();
          idUses.Add(instance);

          if (id.SceneName != sceneName)
          {
            AddIssue(Severity.Error,
              $"{tag}: DoorId '{id.name}' declares sceneName='{id.SceneName}' but is placed in scene '{sceneName}'.",
              assetGuid: instance.idGuid, scenePath: scenePath, hierarchyPath: hierarchyPath);
          }
        }

        if (connection == null)
        {
          AddIssue(Severity.Error, $"{tag} has no DoorConnection assigned.", scenePath: scenePath, hierarchyPath: hierarchyPath);
        }
        else
        {
          if (!connectionUsage.TryGetValue(instance.connectionGuid, out List<DoorInstance> connUses))
            connectionUsage[instance.connectionGuid] = connUses = new List<DoorInstance>();
          connUses.Add(instance);

          if (id != null && id != connection.EndpointA && id != connection.EndpointB)
          {
            AddIssue(Severity.Error,
              $"{tag}: DoorId '{id.name}' is not an endpoint of connection '{connection.name}' " +
              $"(endpoints: {NameOf(connection.EndpointA)} / {NameOf(connection.EndpointB)}).",
              assetGuid: instance.connectionGuid, scenePath: scenePath, hierarchyPath: hierarchyPath);
          }
        }
      }

      // --- economy scene objects: keypads + indicator lights ---

      DoorPurchaser[] keypads = Resources.FindObjectsOfTypeAll<DoorPurchaser>()
        .Where(k => k.gameObject.scene == scene)
        .ToArray();

      foreach (DoorPurchaser keypad in keypads)
      {
        string hierarchyPath = GetHierarchyPath(keypad.transform);
        DoorConnection connection = keypad.Connection;

        if (connection == null)
        {
          AddIssue(Severity.Error, $"{sceneName} :: {hierarchyPath}: DoorPurchaser keypad has no DoorConnection assigned.",
            scenePath: scenePath, hierarchyPath: hierarchyPath);
          continue;
        }

        var instance = new DoorInstance
        {
          scenePath = scenePath,
          sceneName = sceneName,
          hierarchyPath = hierarchyPath,
          connectionGuid = GetGuid(connection),
        };
        if (!keypadUsage.TryGetValue(instance.connectionGuid, out List<DoorInstance> uses))
          keypadUsage[instance.connectionGuid] = uses = new List<DoorInstance>();
        uses.Add(instance);
      }

      long upgradeSum = 0;
      foreach (CapacityUpgrade upgrade in Resources.FindObjectsOfTypeAll<CapacityUpgrade>()
                 .Where(c => c.gameObject.scene == scene))
      {
        SerializedProperty amount = new SerializedObject(upgrade).FindProperty("capacityAmount");
        if (amount != null && amount.longValue > 0)
          upgradeSum += amount.longValue;
      }
      upgradeCapacityByScene[sceneName] = upgradeSum;

      foreach (DoorIndicatorLight indicator in Resources.FindObjectsOfTypeAll<DoorIndicatorLight>()
                 .Where(l => l.gameObject.scene == scene))
      {
        // mirror the component's own Awake fallback: explicit connection,
        // else a DoorPurchaser or Door on itself or a parent
        var so = new SerializedObject(indicator);
        bool resolvable = so.FindProperty("connection").objectReferenceValue != null
          || indicator.GetComponentInParent<DoorPurchaser>(true) != null
          || indicator.GetComponentInParent<Door>(true) != null;

        if (!resolvable)
        {
          string hierarchyPath = GetHierarchyPath(indicator.transform);
          AddIssue(Severity.Warning,
            $"{sceneName} :: {hierarchyPath}: DoorIndicatorLight can't resolve a connection (none assigned, no DoorPurchaser/Door on it or a parent) - it will stay dark.",
            scenePath: scenePath, hierarchyPath: hierarchyPath);
        }
      }
    }

    bool restored = false;
    if (originalSetup != null && originalSetup.Length > 0 && originalSetup.All(s => !string.IsNullOrEmpty(s.path)))
    {
      EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
      restored = true;
    }
    if (!restored)
      Debug.LogWarning("[DoorConsistencyChecker] Could not restore your previous scene setup (it had an untitled/unsaved scene) — reopen it manually.");

    // Resolve fresh live objects now that scene loading is finished - safe to
    // hold onto them for the rest of this pass since no more scenes will load.
    Dictionary<string, DoorId> doorIdsByGuid = LoadAssets<DoorId>(allDoorIdGuids);
    Dictionary<string, DoorConnection> connectionsByGuid = LoadAssets<DoorConnection>(allConnectionGuids);

    foreach (KeyValuePair<string, DoorId> kv in doorIdsByGuid)
    {
      string guid = kv.Key;
      DoorId doorId = kv.Value;
      doorIdUsage.TryGetValue(guid, out List<DoorInstance> uses);
      int count = uses?.Count ?? 0;
      if (count == 0)
        AddIssue(Severity.Warning, $"DoorId '{doorId.name}' (sceneName={doorId.SceneName}) is not assigned to any door in any scene.", assetGuid: guid);
      else if (count > 1)
        AddIssue(Severity.Error, $"DoorId '{doorId.name}' is assigned to {count} doors ({string.Join(", ", uses.Select(u => u.sceneName))}).", assetGuid: guid);
    }

    Dictionary<(string, string), List<string>> pairMap = new Dictionary<(string, string), List<string>>();
    foreach (KeyValuePair<string, DoorConnection> kv in connectionsByGuid)
    {
      string guid = kv.Key;
      DoorConnection conn = kv.Value;
      string aName = NameOf(conn.EndpointA);
      string bName = NameOf(conn.EndpointB);

      if (conn.EndpointA == null || conn.EndpointB == null)
      {
        AddIssue(Severity.Error, $"DoorConnection '{conn.name}' is missing an endpoint (A={aName}, B={bName}).", assetGuid: guid);
      }
      else
      {
        if (conn.EndpointA == conn.EndpointB)
          AddIssue(Severity.Error, $"DoorConnection '{conn.name}' has endpointA == endpointB ({aName}).", assetGuid: guid);

        string keyA = GetGuid(conn.EndpointA);
        string keyB = GetGuid(conn.EndpointB);
        (string, string) pairKey = string.CompareOrdinal(keyA, keyB) <= 0 ? (keyA, keyB) : (keyB, keyA);
        if (!pairMap.TryGetValue(pairKey, out List<string> names))
          pairMap[pairKey] = names = new List<string>();
        names.Add(conn.name);
      }

      connectionUsage.TryGetValue(guid, out List<DoorInstance> connUses);
      int useCount = connUses?.Count ?? 0;
      if (useCount == 0)
      {
        AddIssue(Severity.Warning, $"DoorConnection '{conn.name}' ({aName} <-> {bName}) is not referenced by any door instance.", assetGuid: guid);
      }
      else if (useCount == 1)
      {
        AddIssue(Severity.Warning,
          $"DoorConnection '{conn.name}' ({aName} <-> {bName}) is only referenced by ONE door instance ({connUses[0].sceneName}).",
          assetGuid: guid, scenePath: connUses[0].scenePath, hierarchyPath: connUses[0].hierarchyPath);
      }
      else if (useCount > 2)
      {
        AddIssue(Severity.Error,
          $"DoorConnection '{conn.name}' is referenced by {useCount} door instances ({string.Join(", ", connUses.Select(u => u.sceneName))}).",
          assetGuid: guid);
      }
    }

    foreach (KeyValuePair<(string, string), List<string>> kv in pairMap)
    {
      if (kv.Value.Count > 1)
        AddIssue(Severity.Error, $"Duplicate DoorConnections share the same endpoint pair: {string.Join(", ", kv.Value)}.");
    }

    // --- economy checks (Phase 6: priced doors + keypads) ---

    foreach (KeyValuePair<string, DoorConnection> kv in connectionsByGuid)
    {
      string guid = kv.Key;
      DoorConnection conn = kv.Value;

      if (conn.ClickCost < 0)
        AddIssue(Severity.Error, $"DoorConnection '{conn.name}' has a negative clickCost ({conn.ClickCost}).", assetGuid: guid);

      keypadUsage.TryGetValue(guid, out List<DoorInstance> keypads);

      if (conn.IsPriced)
      {
        if (conn.StartsLocked)
          AddIssue(Severity.Warning,
            $"DoorConnection '{conn.name}' is priced (cost {conn.ClickCost}) AND startsLocked - double gate: it needs a DoorUnlocker AND a purchase. Intended?",
            assetGuid: guid);

        if (keypads == null || keypads.Count == 0)
          AddIssue(Severity.Error,
            $"DoorConnection '{conn.name}' is priced (cost {conn.ClickCost}) but NO DoorPurchaser keypad in any scene sells it - the door can never be purchased.",
            assetGuid: guid);
      }

      if (keypads == null)
        continue;

      foreach (DoorInstance keypad in keypads)
      {
        if (!conn.IsPriced)
        {
          AddIssue(Severity.Warning,
            $"{keypad.sceneName} :: {keypad.hierarchyPath}: keypad sells connection '{conn.name}', which has no clickCost - it does nothing.",
            assetGuid: guid, scenePath: keypad.scenePath, hierarchyPath: keypad.hierarchyPath);
        }

        // a keypad in a room neither endpoint lives in is almost certainly
        // a copy-paste artifact (the exact bug class this window exists for)
        if (conn.EndpointA != null && conn.EndpointB != null
            && keypad.sceneName != conn.EndpointA.SceneName && keypad.sceneName != conn.EndpointB.SceneName)
        {
          AddIssue(Severity.Warning,
            $"{keypad.sceneName} :: {keypad.hierarchyPath}: keypad sells connection '{conn.name}', but that connection joins '{conn.EndpointA.SceneName}' and '{conn.EndpointB.SceneName}' - keypad is in an unrelated room.",
            assetGuid: guid, scenePath: keypad.scenePath, hierarchyPath: keypad.hierarchyPath);
        }
      }
    }

    // every entrance priced = the player can only ever see this room's
    // inside after buying in blind - flag it as a progression smell
    foreach (IGrouping<string, DoorInstance> room in allDoors.GroupBy(d => d.sceneName))
    {
      List<DoorInstance> doors = room.ToList();
      if (doors.Count == 0)
        continue;

      bool allPriced = doors.All(d =>
        d.connectionGuid != null
        && connectionsByGuid.TryGetValue(d.connectionGuid, out DoorConnection c)
        && c.IsPriced);

      if (allPriced)
        AddIssue(Severity.Warning,
          $"Room '{room.Key}': EVERY door ({doors.Count}) is priced - no free way in or out. Progression smell; make sure this is deliberate.");
    }

    CheckReachability(connectionsByGuid, upgradeCapacityByScene);

    hasRun = true;
    Debug.Log($"[DoorConsistencyChecker] Checked {doorCount} door instances across {sceneCount} scenes: {issues.Count} problem(s) found.");
    Repaint();
  }

  // --- Circuit C7: the reachability lint (exact feasibility) ---
  //
  // A purchase is EVER affordable iff its cost fits under an attainable
  // MaxCapacity (Count <= MaxCapacity, and charge regenerates up to the cap -
  // capacity, not income, is the binding resource). Because the economy is
  // fully MONOTONE (capacity only rises, unlocks are permanent, charge
  // refills for free), greedy simulation is exact: simulate progression from
  // SecurityRoom to a fixpoint - cross every door whose cost fits, activate
  // every entered room whose cost fits, repeat until nothing changes.
  // Anything still locked out at the fixpoint is unbuyable FOREVER -> ERROR.
  //
  // The earlier OPTIMISTIC bound (sum all reachable capacity, assume other
  // priced doors are crossable) missed the mutual-lockout case a real
  // playtest hit on 2026-07-18: every exit of SecurityRoom priced above the
  // starting cap - each door's bound counted rooms behind the OTHER
  // unaffordable doors. The fixpoint catches it.
  //
  // Model notes: pre-start wandering (C4: doors free before Running) grants
  // no ECONOMIC access - all purchases/activations happen post-lever with
  // locks enforced, so simulating from SecurityRoom with locks on is right.
  // startsLocked (DoorUnlocker) doors are treated as crossable - lock-puzzle
  // solvability is not this lint's job.
  private void CheckReachability(Dictionary<string, DoorConnection> connectionsByGuid, Dictionary<string, long> upgradeCapacityByScene)
  {
    // RoomId config lives on the assets, so this lint works before any
    // terminal is placed. No RoomId assets = Circuit not wired yet; skip
    // silently rather than flooding a pre-Circuit project with errors.
    Dictionary<string, RoomId> roomsByGuid = LoadAssets<RoomId>(AssetDatabase.FindAssets("t:RoomId"));
    if (roomsByGuid.Count == 0)
      return;

    Dictionary<string, RoomId> roomsByScene = new Dictionary<string, RoomId>();
    Dictionary<string, string> roomGuidByScene = new Dictionary<string, string>();
    foreach (KeyValuePair<string, RoomId> kv in roomsByGuid)
    {
      if (!string.IsNullOrEmpty(kv.Value.SceneName) && !roomsByScene.ContainsKey(kv.Value.SceneName))
      {
        roomsByScene[kv.Value.SceneName] = kv.Value;
        roomGuidByScene[kv.Value.SceneName] = kv.Key;
      }
    }

    if (!roomsByScene.TryGetValue(BootstrapSceneName, out RoomId bootstrap))
    {
      AddIssue(Severity.Warning,
        $"No RoomId for '{BootstrapSceneName}' - the reachability lint can't run without the bootstrap room.");
      return;
    }

    long UpgradesIn(string sceneName) =>
      upgradeCapacityByScene.TryGetValue(sceneName, out long u) ? u : 0;

    // Start state: bootstrap room activated via the residue seed - capacity
    // floor (== its activationCost) + its base segment + its upgrades.
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

    // Fixpoint reached: 'capacity' is the maximum attainable MaxCapacity.

    foreach (KeyValuePair<string, DoorConnection> kv in connectionsByGuid)
    {
      DoorConnection conn = kv.Value;
      if (!conn.IsPriced || conn.EndpointA == null || conn.EndpointB == null)
        continue;

      bool anyEndpointEntered = entered.Contains(conn.EndpointA.SceneName) || entered.Contains(conn.EndpointB.SceneName);
      if (anyEndpointEntered && conn.ClickCost > capacity)
        AddIssue(Severity.Error,
          $"DoorConnection '{conn.name}' clickCost {conn.ClickCost} exceeds the {capacity} max attainable capacity - unbuyable FOREVER.",
          assetGuid: kv.Key);
      // both endpoints unreachable -> the room errors below already cover it
    }

    foreach (KeyValuePair<string, RoomId> kv in roomsByScene)
    {
      if (activated.Contains(kv.Key))
        continue;

      string guid = roomGuidByScene[kv.Key];
      if (!entered.Contains(kv.Key))
        AddIssue(Severity.Error,
          $"RoomId '{kv.Value.name}' (scene '{kv.Key}') can never be REACHED from {BootstrapSceneName} - every route is blocked by doors that can never be afforded.",
          assetGuid: guid);
      else
        AddIssue(Severity.Error,
          $"RoomId '{kv.Value.name}' activationCost {kv.Value.ActivationCost} exceeds the {capacity} max attainable capacity - the room can NEVER be powered.",
          assetGuid: guid);
    }
  }

  private void AddIssue(Severity severity, string message, string assetGuid = null, string scenePath = null, string hierarchyPath = null)
  {
    issues.Add(new Issue { severity = severity, message = message, assetGuid = assetGuid, scenePath = scenePath, hierarchyPath = hierarchyPath });
  }

  private static string NameOf(DoorId id) => id != null ? id.name : "<null>";

  private static string GetGuid(Object asset)
  {
    return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long _) ? guid : null;
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

  private static string GetHierarchyPath(Transform t)
  {
    StringBuilder sb = new StringBuilder(t.name);
    while (t.parent != null)
    {
      t = t.parent;
      sb.Insert(0, t.name + "/");
    }
    return sb.ToString();
  }

  private static GameObject FindByHierarchyPath(Scene scene, string hierarchyPath)
  {
    if (string.IsNullOrEmpty(hierarchyPath)) return null;
    string[] parts = hierarchyPath.Split('/');
    Transform current = scene.GetRootGameObjects().FirstOrDefault(r => r.name == parts[0])?.transform;
    for (int i = 1; current != null && i < parts.Length; i++)
      current = current.Find(parts[i]);
    return current != null ? current.gameObject : null;
  }
}
