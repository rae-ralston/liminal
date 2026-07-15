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
// Checks (prefab assets under Assets/Prefabs):
//  - a NON-empty propId serialized in the prefab itself (rule: propId stays
//    empty in prefabs, set per placed instance - a shared id would consume
//    every instance at once), plus the XOR/applier/collider checks above.
public class PropConsistencyWindow : EditorWindow
{
  private const string RoomSceneFolder = "Assets/Scenes/Rooms";
  private const string PrefabFolder = "Assets/Prefabs";
  private const string ChargeGateClassName = "LightFedCharge"; // Phase 5, may not exist yet

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
