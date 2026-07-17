using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Circuit > Generate Room Terminals (The Circuit, 2026-07-16): the
// one-shot generator that shrinks the user's 24-scene wiring to "reposition
// each terminal + tune two numbers per room". It:
//
//  1. Creates a RoomId asset per room scene (sceneName read from the scene
//     file name) under RoomIdFolder - existing assets are reused, never
//     recreated, so tuned baseCapacity/activationCost values survive reruns.
//  2. Instantiates the Terminal prefab into every room scene that doesn't
//     already have a Terminal, wiring that instance's roomId (and fixing a
//     null roomId on an existing terminal). SecurityRoom's terminal gets
//     isBootstrap.
//  3. Wires the Incremental in PersistentScene: fills the allRooms list and
//     sets bootstrapRoom to SecurityRoom's RoomId.
//
// Re-runnable and skip-happy by design. Same scene-loop + GUID-only
// discipline as DoorConsistencyWindow (see CLAUDE.md Known Gotchas: assets
// loaded before an OpenScene loop can be native-destroyed mid-loop - only
// GUIDs cross scene loads here, resolved fresh per scene).
public class GenerateRoomTerminalsWindow : EditorWindow
{
  private const string RoomSceneFolder = "Assets/Scenes/Rooms";
  private const string RoomIdFolder = "Assets/Scenes/RoomIDs";
  private const string TerminalPrefabPath = "Assets/Prefabs/Terminal.prefab";
  private const string PersistentScenePath = "Assets/Scenes/PersistentScene.unity";
  private const string BootstrapSceneName = "SecurityRoom";

  private Vector2 scroll;
  private readonly List<string> report = new List<string>();

  [MenuItem("Tools/Circuit/Generate Room Terminals")]
  private static void Open()
  {
    GetWindow<GenerateRoomTerminalsWindow>("Room Terminals");
  }

  private void OnGUI()
  {
    EditorGUILayout.HelpBox(
      $"Creates a RoomId asset per room scene (in {RoomIdFolder}), places the Terminal prefab in every room scene " +
      "that lacks one (wiring its roomId; SecurityRoom's gets isBootstrap), and fills the Incremental's " +
      "allRooms + bootstrapRoom in PersistentScene. Re-runnable: existing RoomIds and terminals are kept, so " +
      "tuned numbers and repositioned terminals survive. Your remaining work: reposition each terminal and tune " +
      "baseCapacity/activationCost per RoomId asset.",
      MessageType.Info);

    bool hasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TerminalPrefabPath) != null;

    using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
    {
      if (!hasPrefab && GUILayout.Button("Create Placeholder Terminal Prefab", GUILayout.Height(26)))
        CreatePlaceholderPrefab();

      using (new EditorGUI.DisabledScope(!hasPrefab))
      {
        if (GUILayout.Button("Generate", GUILayout.Height(30)))
          Generate();
      }
    }

    if (!hasPrefab)
      EditorGUILayout.HelpBox($"No prefab at {TerminalPrefabPath}. Create the placeholder (borrows the Computer's sprite; " +
        "swap in real art later - edits to the prefab propagate to every placed terminal) or author one there yourself.",
        MessageType.Warning);

    if (EditorApplication.isPlaying)
      EditorGUILayout.HelpBox("Exit Play Mode first (this opens scenes).", MessageType.Warning);

    if (report.Count == 0)
      return;

    EditorGUILayout.Space();
    scroll = EditorGUILayout.BeginScrollView(scroll);
    foreach (string line in report)
      EditorGUILayout.LabelField(line, EditorStyles.wordWrappedLabel);
    EditorGUILayout.EndScrollView();
  }

  // Standalone prefab, NOT a button variant (a variant would inherit
  // Prop/PropInteraction and trip the prop checker's XOR/applier lints).
  // Borrows the Computer prop's sprite + material for the body (decision 9:
  // every terminal is the same computer-style prop for the jam), tinted so
  // terminals read distinctly until the art pass.
  private void CreatePlaceholderPrefab()
  {
    GameObject go = new GameObject("Terminal");
    try
    {
      SpriteRenderer body = go.AddComponent<SpriteRenderer>();
      SpriteRenderer donor = LoadDonorRenderer();
      if (donor != null)
      {
        body.sprite = donor.sprite;
        body.sharedMaterial = donor.sharedMaterial;
      }
      body.color = new Color(0.55f, 0.85f, 0.8f, 1f);
      body.spriteSortPoint = SpriteSortPoint.Pivot; // Y-sorting convention

      // Generous trigger on purpose - see the open "interaction triggers
      // feel unreliable" issue; don't make the terminal another offender.
      BoxCollider2D trigger = go.AddComponent<BoxCollider2D>();
      trigger.isTrigger = true;
      trigger.size = new Vector2(1.6f, 1.6f);

      go.AddComponent<Terminal>();
      go.AddComponent<PropAudio>();
      go.AddComponent<InteractableHighlight>();

      PrefabUtility.SaveAsPrefabAsset(go, TerminalPrefabPath);
      report.Insert(0, $"Created placeholder prefab at {TerminalPrefabPath}" +
        (donor == null ? " (no donor sprite found - assign one on the prefab)." : $" (sprite borrowed from '{donor.gameObject.name}')."));
      Debug.Log($"[Circuit] {report[0]}");
    }
    finally
    {
      DestroyImmediate(go);
    }
  }

  private static SpriteRenderer LoadDonorRenderer()
  {
    foreach (string path in new[] { "Assets/Prefabs/Computer.prefab", "Assets/Prefabs/Doors/Keypad.prefab" })
    {
      GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
      SpriteRenderer renderer = prefab != null ? prefab.GetComponentInChildren<SpriteRenderer>() : null;
      if (renderer != null && renderer.sprite != null)
        return renderer;
    }
    return null;
  }

  private void Generate()
  {
    if (EditorApplication.isPlaying)
      return;
    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
      return;

    report.Clear();
    int idsCreated = 0, idsReused = 0, placed = 0, skipped = 0, fixedIds = 0;

    // ---- 1. RoomId assets (no scenes open yet, so live refs are safe here;
    //         only GUIDs survive past this block) ----

    string[] scenePaths = AssetDatabase.FindAssets("t:Scene", new[] { RoomSceneFolder })
      .Select(AssetDatabase.GUIDToAssetPath)
      .OrderBy(p => p)
      .ToArray();

    if (scenePaths.Length == 0)
    {
      report.Add($"No scenes found under {RoomSceneFolder} - nothing to do.");
      return;
    }

    Dictionary<string, string> roomIdGuidByScene = new Dictionary<string, string>();
    foreach (string guid in AssetDatabase.FindAssets("t:RoomId"))
    {
      RoomId existing = AssetDatabase.LoadAssetAtPath<RoomId>(AssetDatabase.GUIDToAssetPath(guid));
      if (existing == null || string.IsNullOrEmpty(existing.SceneName))
        continue;
      if (roomIdGuidByScene.ContainsKey(existing.SceneName))
        report.Add($"WARNING: duplicate RoomId for scene '{existing.SceneName}' ('{existing.name}' ignored - fix by hand).");
      else
        roomIdGuidByScene[existing.SceneName] = guid;
    }

    if (!AssetDatabase.IsValidFolder(RoomIdFolder))
      AssetDatabase.CreateFolder(Path.GetDirectoryName(RoomIdFolder).Replace('\\', '/'), Path.GetFileName(RoomIdFolder));

    foreach (string scenePath in scenePaths)
    {
      string sceneName = Path.GetFileNameWithoutExtension(scenePath);
      if (roomIdGuidByScene.ContainsKey(sceneName))
      {
        idsReused++;
        continue;
      }

      RoomId roomId = ScriptableObject.CreateInstance<RoomId>();
      roomId.EditorInit(sceneName);
      string assetPath = $"{RoomIdFolder}/RoomId_{sceneName}.asset";
      AssetDatabase.CreateAsset(roomId, assetPath);
      roomIdGuidByScene[sceneName] = AssetDatabase.AssetPathToGUID(assetPath);
      idsCreated++;
    }
    AssetDatabase.SaveAssets();

    if (!roomIdGuidByScene.ContainsKey(BootstrapSceneName))
      report.Add($"WARNING: no scene named '{BootstrapSceneName}' - no bootstrap terminal was marked and bootstrapRoom stays unassigned.");

    string prefabGuid = AssetDatabase.AssetPathToGUID(TerminalPrefabPath);

    // ---- 2. Scene loop (GUID-only across loads) ----

    SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();

    foreach (string scenePath in scenePaths)
    {
      string sceneName = Path.GetFileNameWithoutExtension(scenePath);
      Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

      // resolve fresh, post-open
      RoomId roomId = AssetDatabase.LoadAssetAtPath<RoomId>(AssetDatabase.GUIDToAssetPath(roomIdGuidByScene[sceneName]));

      Terminal existing = Resources.FindObjectsOfTypeAll<Terminal>()
        .FirstOrDefault(t => t.gameObject.scene == scene && !EditorUtility.IsPersistent(t));

      if (existing != null)
      {
        if (existing.RoomId == null)
        {
          WireTerminal(existing, roomId, sceneName == BootstrapSceneName);
          EditorSceneManager.SaveScene(scene);
          fixedIds++;
          report.Add($"{sceneName}: existing terminal had no RoomId - wired to '{roomId.name}'.");
        }
        else
        {
          skipped++;
        }
        continue;
      }

      GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(prefabGuid));
      if (prefab == null)
      {
        report.Add($"ERROR: terminal prefab vanished mid-run ({TerminalPrefabPath}) - aborting scene loop.");
        break;
      }

      GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
      instance.transform.position = Vector3.zero;
      WireTerminal(instance.GetComponent<Terminal>(), roomId, sceneName == BootstrapSceneName);
      EditorSceneManager.SaveScene(scene);
      placed++;
    }

    // ---- 3. Incremental wiring in PersistentScene ----

    Scene persistent = EditorSceneManager.OpenScene(PersistentScenePath, OpenSceneMode.Single);
    Incremental incremental = Resources.FindObjectsOfTypeAll<Incremental>()
      .FirstOrDefault(i => i.gameObject.scene == persistent && !EditorUtility.IsPersistent(i));

    if (incremental == null)
    {
      report.Add("ERROR: no Incremental found in PersistentScene - allRooms/bootstrapRoom NOT wired.");
    }
    else
    {
      // all scene loads are done - live RoomId refs are safe from here on
      List<RoomId> allRooms = roomIdGuidByScene
        .OrderBy(kv => kv.Key)
        .Select(kv => AssetDatabase.LoadAssetAtPath<RoomId>(AssetDatabase.GUIDToAssetPath(kv.Value)))
        .Where(r => r != null)
        .ToList();

      SerializedObject so = new SerializedObject(incremental);
      SerializedProperty roomsProp = so.FindProperty("allRooms");
      roomsProp.arraySize = allRooms.Count;
      for (int i = 0; i < allRooms.Count; i++)
        roomsProp.GetArrayElementAtIndex(i).objectReferenceValue = allRooms[i];

      roomIdGuidByScene.TryGetValue(BootstrapSceneName, out string bootstrapGuid);
      so.FindProperty("bootstrapRoom").objectReferenceValue = bootstrapGuid != null
        ? AssetDatabase.LoadAssetAtPath<RoomId>(AssetDatabase.GUIDToAssetPath(bootstrapGuid))
        : null;
      so.ApplyModifiedPropertiesWithoutUndo();
      EditorSceneManager.SaveScene(persistent);
      report.Add($"Incremental wired: {allRooms.Count} rooms in allRooms, bootstrapRoom = {(bootstrapGuid != null ? BootstrapSceneName : "<none>")}.");
    }

    if (originalSetup != null && originalSetup.Length > 0 && originalSetup.All(s => !string.IsNullOrEmpty(s.path)))
      EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
    else
      report.Add("NOTE: could not restore your previous scene setup - reopen it manually.");

    report.Insert(0, $"Done. RoomIds: {idsCreated} created, {idsReused} reused. Terminals: {placed} placed, {skipped} already present, {fixedIds} re-wired.");
    Debug.Log($"[Circuit] {report[0]}");
    Repaint();
  }

  private static void WireTerminal(Terminal terminal, RoomId roomId, bool isBootstrap)
  {
    SerializedObject so = new SerializedObject(terminal);
    so.FindProperty("roomId").objectReferenceValue = roomId;
    if (isBootstrap)
      so.FindProperty("isBootstrap").boolValue = true;
    so.ApplyModifiedPropertiesWithoutUndo();
  }
}
