using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Opens every scene under RoomSceneFolder and inspects every prop that
// carries incremental effects. Companion to DoorConsistencyWindow - same
// motivation: propId wiring is the identical hand-copied-per-instance bug
// class that produced 7 real door-wiring bugs on 2026-07-13.
//
// Checks (scenes):
//  - one-shot effect whose GameObject has no Prop or an EMPTY propId
//    (consumed state silently won't survive room reloads)
//  - the same propId used by more than one prop instance anywhere
//    (consuming one would consume them all)
//  - the XOR law: PropInteraction + LightFedCharge on one GameObject
//    (interact would bypass the charge gate; checked by name so this rule
//    arms itself automatically once LightFedCharge exists)
//  - effect components with NO applier at all (neither PropInteraction nor
//    LightFedCharge - the effect can never fire)
//  - an InteractableTrigger with no trigger-enabled Collider2D on the same
//    GameObject (enter/exit callbacks can never fire)
// Checks (The Circuit, C7):
//  - exactly one Terminal per room scene (0 = dead room, 2+ = ambiguous)
//  - Terminal.roomId null, or its sceneName mismatching the scene it's in
//  - the same RoomId asset on terminals in different scenes
//  - exactly one isBootstrap terminal project-wide, and it's in SecurityRoom
//  - CapacityUpgrade with capacityAmount <= 0
//  - TerminalGauge slot referencing a prop with no CapacityUpgrade
// Checks (prefab assets under Assets/Prefabs):
//  - a NON-empty propId serialized in the prefab itself (rule: propId stays
//    empty in prefabs, set per placed instance - a shared id would consume
//    every instance at once), plus the XOR/applier/collider checks above.
// Checks (Scatter Placer output, §6 - GeneratedProp-marked objects only):
//  - a sourcePrefabGuid that resolves to no asset (source prefab deleted/moved)
//  - off-grid by > 0.01 (a hand-nudge that undid the alignment)
//  - root transform off its collider's south edge / X-centre (the §3.1 sort-
//    anchor law - the placed-instance form of the desk bug)
public class PropConsistencyWindow : EditorWindow
{
  private const string RoomSceneFolder = "Assets/Scenes/Rooms";
  private const string PrefabFolder = "Assets/Prefabs";
  private const string ChargeGateClassName = "LightFedCharge"; // Phase 5, may not exist yet
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

  // Everything collected during the scene loop is plain strings - same
  // discipline as DoorConsistencyWindow (see its comment on the implicit
  // Resources.UnloadUnusedAssets during repeated OpenScene calls).
  private class PropIdUse
  {
    public string scenePath;
    public string sceneName;
    public string hierarchyPath;
  }

  // Terminal facts extracted while each scene is open - strings/bools only,
  // never live RoomId references (GUID discipline, see Known Gotchas).
  private class TerminalInfo
  {
    public string scenePath;
    public string sceneName;
    public string hierarchyPath;
    public bool hasRoomId;
    public string roomIdName;
    public string roomIdGuid;
    public string roomSceneName;
    public bool isBootstrap;
  }

  private Vector2 scroll;
  private List<Issue> issues = new List<Issue>();
  private int propCount;
  private int sceneCount;
  private bool hasRun;

  [MenuItem("Tools/Props/Prop Consistency Checker")]
  private static void Open()
  {
    GetWindow<PropConsistencyWindow>("Prop Consistency");
  }

  private void OnGUI()
  {
    EditorGUILayout.HelpBox(
      $"Opens every scene in {RoomSceneFolder} and inspects every prop carrying incremental effects: empty/duplicate " +
      "propIds on one-shots, the interactable-XOR-chargeable law, effects with no applier, triggers without a trigger " +
      $"collider - then the same checks on prefabs under {PrefabFolder} (where propId must stay EMPTY). Opens scenes " +
      "one at a time (offering to save unsaved changes first) and restores what you had open when it's done.",
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
        $"All clear. {propCount} effect-carrying prop(s) across {sceneCount} scene(s) + prefabs, no problems found.",
        MessageType.Info);
      return;
    }

    EditorGUILayout.LabelField(
      $"{propCount} effect-carrying prop(s) across {sceneCount} scene(s)  —  {errorCount} error(s), {warningCount} warning(s)",
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
      Debug.LogError("[PropConsistencyChecker] Cannot run while in Play Mode.");
      return;
    }

    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
      return;

    issues = new List<Issue>();
    propCount = 0;

    Dictionary<string, List<PropIdUse>> propIdUsage = new Dictionary<string, List<PropIdUse>>();
    List<TerminalInfo> terminals = new List<TerminalInfo>();

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

      foreach (GameObject go in EffectCarryingObjects(scene))
      {
        propCount++;
        string hierarchyPath = GetHierarchyPath(go.transform);
        string tag = $"{sceneName} :: {hierarchyPath}";

        CheckObject(go, tag, scenePath, hierarchyPath);

        Prop prop = go.GetComponent<Prop>();
        string propId = prop != null ? prop.PropId : null;

        if (!string.IsNullOrEmpty(propId))
        {
          if (!propIdUsage.TryGetValue(propId, out List<PropIdUse> uses))
            propIdUsage[propId] = uses = new List<PropIdUse>();
          uses.Add(new PropIdUse { scenePath = scenePath, sceneName = sceneName, hierarchyPath = hierarchyPath });
        }

        if (HasOneShotEffect(go) && string.IsNullOrEmpty(propId))
        {
          AddIssue(Severity.Error,
            prop == null
              ? $"{tag} has a one-shot effect but NO Prop component - consumed state will not survive room reloads."
              : $"{tag} has a one-shot effect but an EMPTY propId - consumed state will not survive room reloads.",
            scenePath: scenePath, hierarchyPath: hierarchyPath);
        }
      }

      // Circuit C7: extract terminal facts as plain strings while the scene
      // is open; the aggregate lints run after the loop.
      foreach (Terminal terminal in Resources.FindObjectsOfTypeAll<Terminal>()
        .Where(t => t != null && t.gameObject.scene == scene))
      {
        RoomId roomId = terminal.RoomId;
        terminals.Add(new TerminalInfo
        {
          scenePath = scenePath,
          sceneName = sceneName,
          hierarchyPath = GetHierarchyPath(terminal.transform),
          hasRoomId = roomId != null,
          roomIdName = roomId != null ? roomId.name : null,
          roomIdGuid = roomId != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(roomId)) : null,
          roomSceneName = roomId != null ? roomId.SceneName : null,
          isBootstrap = terminal.IsBootstrap,
        });
      }

      // Circuit C7: gauge slots must point at CapacityUpgrade props. Scene
      // references can't cross scenes, so "different room" is impossible for
      // placed instances - a slot is either empty, valid, or mis-typed.
      foreach (TerminalGauge gauge in Resources.FindObjectsOfTypeAll<TerminalGauge>()
        .Where(g => g != null && g.gameObject.scene == scene))
      {
        string gaugePath = GetHierarchyPath(gauge.transform);
        SerializedProperty slots = new SerializedObject(gauge).FindProperty("slots");
        for (int i = 0; slots != null && i < slots.arraySize; i++)
        {
          SerializedProperty propRef = slots.GetArrayElementAtIndex(i).FindPropertyRelative("prop");
          if (propRef == null || propRef.objectReferenceValue == null) continue;

          Prop slotProp = propRef.objectReferenceValue as Prop;
          if (slotProp == null || slotProp.GetComponent<CapacityUpgrade>() == null)
          {
            AddIssue(Severity.Error,
              $"{sceneName} :: {gaugePath} gauge slot {i + 1} references '{propRef.objectReferenceValue.name}' which has no CapacityUpgrade - the bar can never go live.",
              scenePath: scenePath, hierarchyPath: gaugePath);
          }
        }
      }

      CheckGeneratedProps(scene, scenePath, sceneName);
    }

    bool restored = false;
    if (originalSetup != null && originalSetup.Length > 0 && originalSetup.All(s => !string.IsNullOrEmpty(s.path)))
    {
      EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
      restored = true;
    }
    if (!restored)
      Debug.LogWarning("[PropConsistencyChecker] Could not restore your previous scene setup (it had an untitled/unsaved scene) — reopen it manually.");

    foreach (KeyValuePair<string, List<PropIdUse>> kv in propIdUsage)
    {
      if (kv.Value.Count > 1)
      {
        AddIssue(Severity.Error,
          $"propId '{kv.Key}' is used by {kv.Value.Count} prop instances ({string.Join(", ", kv.Value.Select(u => $"{u.sceneName} :: {u.hierarchyPath}"))}) - consuming one consumes them all.",
          scenePath: kv.Value[0].scenePath, hierarchyPath: kv.Value[0].hierarchyPath);
      }
    }

    CheckTerminals(terminals, scenePaths);

    // Prefab pass - after all scene opens, so loaded assets can't be swept
    // out from under us by an implicit unload mid-loop.
    foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
    {
      string path = AssetDatabase.GUIDToAssetPath(guid);
      GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
      if (prefab == null) continue;

      foreach (Prop prop in prefab.GetComponentsInChildren<Prop>(true))
      {
        if (!string.IsNullOrEmpty(prop.PropId))
        {
          AddIssue(Severity.Error,
            $"Prefab '{path}' has propId '{prop.PropId}' serialized in the ASSET - propId must stay empty in prefabs and be set per placed instance.",
            assetGuid: guid);
        }
      }

      IEnumerable<GameObject> effectObjects = prefab.GetComponentsInChildren<MonoBehaviour>(true)
        .Where(mb => mb != null && mb is IIncrementalEffect)
        .Select(mb => mb.gameObject)
        .Distinct();
      foreach (GameObject go in effectObjects)
      {
        propCount++;
        string tag = go == prefab ? $"Prefab '{path}'" : $"Prefab '{path}' :: {go.name}";
        CheckObject(go, tag, null, null, guid);
      }
    }

    hasRun = true;
    Debug.Log($"[PropConsistencyChecker] Checked {propCount} effect-carrying props across {sceneCount} scenes + prefabs: {issues.Count} problem(s) found.");
    Repaint();
  }

  // Circuit C7 aggregate lints over the terminal facts collected during the
  // scene loop.
  private void CheckTerminals(List<TerminalInfo> terminals, string[] scenePaths)
  {
    foreach (string scenePath in scenePaths)
    {
      string sceneName = Path.GetFileNameWithoutExtension(scenePath);
      List<TerminalInfo> inScene = terminals.Where(t => t.scenePath == scenePath).ToList();

      if (inScene.Count == 0)
      {
        AddIssue(Severity.Error,
          $"{sceneName} has NO Terminal - the room can never be activated (dead room). Run Tools > Circuit > Generate Room Terminals.",
          scenePath: scenePath);
      }
      else if (inScene.Count > 1)
      {
        AddIssue(Severity.Error,
          $"{sceneName} has {inScene.Count} Terminals ({string.Join(", ", inScene.Select(t => t.hierarchyPath))}) - exactly one per room scene.",
          scenePath: scenePath, hierarchyPath: inScene[0].hierarchyPath);
      }
    }

    foreach (TerminalInfo terminal in terminals)
    {
      if (!terminal.hasRoomId)
      {
        AddIssue(Severity.Error,
          $"{terminal.sceneName} :: {terminal.hierarchyPath} has no RoomId assigned - the terminal can never activate anything.",
          scenePath: terminal.scenePath, hierarchyPath: terminal.hierarchyPath);
      }
      else if (terminal.roomSceneName != terminal.sceneName)
      {
        AddIssue(Severity.Error,
          $"{terminal.sceneName} :: {terminal.hierarchyPath} has RoomId '{terminal.roomIdName}' whose sceneName is '{terminal.roomSceneName}' - it must match the scene the terminal lives in.",
          scenePath: terminal.scenePath, hierarchyPath: terminal.hierarchyPath);
      }
    }

    foreach (IGrouping<string, TerminalInfo> group in terminals
      .Where(t => !string.IsNullOrEmpty(t.roomIdGuid))
      .GroupBy(t => t.roomIdGuid)
      .Where(g => g.Count() > 1))
    {
      TerminalInfo first = group.First();
      AddIssue(Severity.Error,
        $"RoomId '{first.roomIdName}' is assigned to {group.Count()} terminals ({string.Join(", ", group.Select(t => $"{t.sceneName} :: {t.hierarchyPath}"))}) - activating one would activate them all.",
        assetGuid: first.roomIdGuid, scenePath: first.scenePath, hierarchyPath: first.hierarchyPath);
    }

    List<TerminalInfo> bootstraps = terminals.Where(t => t.isBootstrap).ToList();
    if (terminals.Count > 0 && bootstraps.Count == 0)
    {
      AddIssue(Severity.Error,
        "No isBootstrap terminal anywhere - the residue charge can never be spent and the game can never start.");
    }
    else if (bootstraps.Count > 1)
    {
      AddIssue(Severity.Error,
        $"{bootstraps.Count} isBootstrap terminals ({string.Join(", ", bootstraps.Select(t => $"{t.sceneName} :: {t.hierarchyPath}"))}) - exactly one exists, in {BootstrapSceneName}.",
        scenePath: bootstraps[0].scenePath, hierarchyPath: bootstraps[0].hierarchyPath);
    }
    else if (bootstraps.Count == 1 && bootstraps[0].sceneName != BootstrapSceneName)
    {
      AddIssue(Severity.Error,
        $"The isBootstrap terminal is in {bootstraps[0].sceneName}, not {BootstrapSceneName} - the pre-start softlock guarantee only holds from the starting room.",
        scenePath: bootstraps[0].scenePath, hierarchyPath: bootstraps[0].hierarchyPath);
    }
  }

  // §6 (Scatter Placer) additions: audit the tool's own output in each scene.
  // Scoped to GeneratedProp so it never false-flags a deliberately-anchored
  // prop (a wall board, a terminal). All warnings - none block the game, they
  // flag a first-draft layout that drifted.
  private void CheckGeneratedProps(Scene scene, string scenePath, string sceneName)
  {
    Grid grid = Resources.FindObjectsOfTypeAll<Grid>().FirstOrDefault(g => g != null && g.gameObject.scene == scene);
    float half = (grid != null ? grid.cellSize.x : 1f) * 0.5f;
    Vector3 origin = grid != null ? grid.CellToWorld(Vector3Int.zero) : Vector3.zero;

    foreach (GeneratedProp gp in Resources.FindObjectsOfTypeAll<GeneratedProp>().Where(g => g != null && g.gameObject.scene == scene))
    {
      propCount++;
      string path = GetHierarchyPath(gp.transform);

      // 1. dead source prefab
      if (string.IsNullOrEmpty(gp.sourcePrefabGuid) || string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(gp.sourcePrefabGuid)))
        AddIssue(Severity.Warning,
          $"{sceneName} :: {path} GeneratedProp's sourcePrefabGuid resolves to no asset - the source prefab was deleted or moved.",
          scenePath: scenePath, hierarchyPath: path);

      // 2. off-grid (a hand-nudge that undid the alignment). Anchors land on
      //    half-cell multiples from the grid origin (cell edges + W-centred X).
      if (grid != null && half > 0f)
      {
        Vector3 rel = gp.transform.position - origin;
        float ox = Mathf.Abs(rel.x / half - Mathf.Round(rel.x / half)) * half;
        float oy = Mathf.Abs(rel.y / half - Mathf.Round(rel.y / half)) * half;
        if (ox > 0.01f || oy > 0.01f)
          AddIssue(Severity.Warning,
            $"{sceneName} :: {path} is off-grid by ({ox:0.00}, {oy:0.00}) - a hand-nudge undid the alignment pass.",
            scenePath: scenePath, hierarchyPath: path);
      }

      // 3. sort anchor (§3.1): root must sit at the collider's south edge / X
      //    centre. This is the placed-instance form of the 2026-07-22 desk bug.
      Collider2D col = gp.GetComponentInChildren<Collider2D>();
      if (col != null)
      {
        float dy = Mathf.Abs(gp.transform.position.y - col.bounds.min.y);
        float dx = Mathf.Abs(gp.transform.position.x - col.bounds.center.x);
        if (dy > 0.05f || dx > 0.05f)
          AddIssue(Severity.Warning,
            $"{sceneName} :: {path} root is off its collider's south edge (x {dx:0.00}, y {dy:0.00}) - the §3.1 sort-anchor law (desk bug); it will occlude wrong from one side.",
            scenePath: scenePath, hierarchyPath: path);
      }
    }
  }

  // Shared per-GameObject rules (used for both scene instances and prefabs):
  // XOR law, effect-with-no-applier, trigger-without-trigger-collider.
  private void CheckObject(GameObject go, string tag, string scenePath, string hierarchyPath, string assetGuid = null)
  {
    bool hasInteraction = go.GetComponent<PropInteraction>() != null;
    bool hasChargeGate = go.GetComponent(ChargeGateClassName) != null;

    if (hasInteraction && hasChargeGate)
    {
      AddIssue(Severity.Error,
        $"{tag} has BOTH PropInteraction and {ChargeGateClassName} - interact bypasses the charge gate (a prop is interactable XOR chargeable).",
        assetGuid: assetGuid, scenePath: scenePath, hierarchyPath: hierarchyPath);
    }

    if (!hasInteraction && !hasChargeGate && go.GetComponents<MonoBehaviour>().Any(mb => mb is IIncrementalEffect))
    {
      AddIssue(Severity.Warning,
        $"{tag} has incremental effect(s) but neither PropInteraction nor {ChargeGateClassName} - nothing can ever apply them.",
        assetGuid: assetGuid, scenePath: scenePath, hierarchyPath: hierarchyPath);
    }

    if (go.GetComponent<InteractableTrigger>() != null
        && !go.GetComponents<Collider2D>().Any(c => c.isTrigger))
    {
      AddIssue(Severity.Error,
        $"{tag} has an InteractableTrigger but no trigger-enabled Collider2D - enter/exit (and interact) can never fire.",
        assetGuid: assetGuid, scenePath: scenePath, hierarchyPath: hierarchyPath);
    }

    // Circuit C7: a non-positive capacityAmount is refused at runtime, so the
    // prop would consume its one-shot for nothing. Warning on prefabs (the
    // template may leave it unset), error on placed instances.
    CapacityUpgrade capacityUpgrade = go.GetComponent<CapacityUpgrade>();
    if (capacityUpgrade != null)
    {
      SerializedProperty amount = new SerializedObject(capacityUpgrade).FindProperty("capacityAmount");
      if (amount != null && amount.longValue <= 0)
      {
        AddIssue(scenePath != null ? Severity.Error : Severity.Warning,
          $"{tag} has a CapacityUpgrade with capacityAmount {amount.longValue} - it must be positive (AddCapacitySegment refuses it).",
          assetGuid: assetGuid, scenePath: scenePath, hierarchyPath: hierarchyPath);
      }
    }
  }

  // Every scene GameObject that carries at least one IIncrementalEffect OR a
  // Prop component (a Prop with no effects is fine, but we still want its
  // propId in the duplicate check).
  private static IEnumerable<GameObject> EffectCarryingObjects(Scene scene)
  {
    return Resources.FindObjectsOfTypeAll<MonoBehaviour>()
      .Where(mb => mb != null && mb.gameObject.scene == scene)
      .Where(mb => mb is IIncrementalEffect || mb is Prop)
      .Select(mb => mb.gameObject)
      .Distinct();
  }

  // "One-shot" is a serialized bool named 'oneShot' on the effect - read via
  // SerializedObject so this keeps working for future effects without a
  // hardcoded type list.
  private static bool HasOneShotEffect(GameObject go)
  {
    foreach (MonoBehaviour mb in go.GetComponents<MonoBehaviour>())
    {
      if (!(mb is IIncrementalEffect)) continue;
      SerializedProperty oneShot = new SerializedObject(mb).FindProperty("oneShot");
      if (oneShot != null && oneShot.boolValue)
        return true;
    }
    return false;
  }

  private void AddIssue(Severity severity, string message, string assetGuid = null, string scenePath = null, string hierarchyPath = null)
  {
    issues.Add(new Issue { severity = severity, message = message, assetGuid = assetGuid, scenePath = scenePath, hierarchyPath = hierarchyPath });
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
