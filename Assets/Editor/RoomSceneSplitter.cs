using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RoomSceneSplitter
{
  private const string OutputFolder = "Assets/Scenes/Rooms";

  [MenuItem("Tools/Room Splitter/Split Selected Room(s) Into Scenes")]
  private static void SplitSelectedRooms()
  {
    GameObject[] roomRoots = Selection.gameObjects;
    if (roomRoots.Length == 0)
    {
      Debug.LogWarning("Select one or more root GameObjects in the open scene to split.");
      return;
    }

    Directory.CreateDirectory(OutputFolder);

    foreach (GameObject roomRoot in roomRoots)
      SplitRoom(roomRoot);

    AssetDatabase.Refresh();
  }

  [MenuItem("Tools/Room Splitter/Split Selected Room(s) Into Scenes", true)]
  private static bool ValidateSplitSelectedRooms()
  {
    return Selection.gameObjects.Length > 0;
  }

  private static void SplitRoom(GameObject roomRoot)
  {
    string roomName = roomRoot.name;
    string scenePath = $"{OutputFolder}/{roomName}.unity";

    if (File.Exists(scenePath))
    {
      Debug.LogError($"Scene already exists at '{scenePath}', skipping '{roomName}'.");
      return;
    }

    Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

    GameObject roomCopy = Object.Instantiate(roomRoot);
    roomCopy.name = roomName;
    EditorSceneManager.MoveGameObjectToScene(roomCopy, newScene);
    EditorSceneManager.SaveScene(newScene, scenePath);
    EditorSceneManager.CloseScene(newScene, true);

    AddSceneToBuildSettings(scenePath);

    Debug.Log($"Copied '{roomName}' into '{scenePath}'. Main_Layout is untouched. Cross-scene references on doors that used to point at this room (e.g. DoorTriggerInScene.destination) are now broken and need to be rewired to DoorTeleporter + SpawnPoint.");
  }

  private static void AddSceneToBuildSettings(string scenePath)
  {
    List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
    if (scenes.Exists(s => s.path == scenePath))
      return;

    scenes.Add(new EditorBuildSettingsScene(scenePath, true));
    EditorBuildSettings.scenes = scenes.ToArray();
  }
}
