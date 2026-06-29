using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RoomLoader : MonoBehaviour
{
  public static RoomLoader Instance { get; private set; }

  [SerializeField] private Transform player;
  [SerializeField] private FadeController fadeController;
  [SerializeField] private string initialRoomSceneName;
  [SerializeField] private string initialSpawnId;

  private Scene currentRoomScene;
  private bool isTransitioning;

  private void Awake()
  {
    if (Instance != null && Instance != this)
    {
      Destroy(gameObject);
      return;
    }

    Instance = this;
  }

  private void Start()
  {
    StartCoroutine(LoadInitialRoom());
  }

  private IEnumerator LoadInitialRoom()
  {
    yield return SceneManager.LoadSceneAsync(initialRoomSceneName, LoadSceneMode.Additive);
    currentRoomScene = SceneManager.GetSceneByName(initialRoomSceneName);
    SceneManager.SetActiveScene(currentRoomScene);
    MovePlayerToSpawn(currentRoomScene, initialSpawnId);
  }

  public void TeleportTo(string targetSceneName, string targetSpawnId)
  {
    if (!isTransitioning)
      StartCoroutine(TeleportRoutine(targetSceneName, targetSpawnId));
  }

  private IEnumerator TeleportRoutine(string targetSceneName, string targetSpawnId)
  {
    isTransitioning = true;
    yield return fadeController.FadeOut();

    Scene previousRoomScene = currentRoomScene;
    yield return SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Additive);

    Scene newRoomScene = SceneManager.GetSceneByName(targetSceneName);
    SceneManager.SetActiveScene(newRoomScene);

    MovePlayerToSpawn(newRoomScene, targetSpawnId);

    yield return SceneManager.UnloadSceneAsync(previousRoomScene);

    currentRoomScene = newRoomScene;
    yield return fadeController.FadeIn();
    isTransitioning = false;
  }

  private void MovePlayerToSpawn(Scene scene, string spawnId)
  {
    Transform spawn = FindSpawnPoint(scene, spawnId);
    if (spawn == null)
      return;

    Vector3 delta = spawn.position - player.position;
    player.position = spawn.position;
    player.rotation = spawn.rotation;
    CinemachineCore.OnTargetObjectWarped(player, delta);
  }

  private static Transform FindSpawnPoint(Scene scene, string id)
  {
    foreach (GameObject root in scene.GetRootGameObjects())
    {
      foreach (SpawnPoint spawnPoint in root.GetComponentsInChildren<SpawnPoint>(true))
      {
        if (spawnPoint.Id == id)
          return spawnPoint.transform;
      }
    }

    Debug.LogError($"No SpawnPoint with id '{id}' found in scene '{scene.name}'.");
    return null;
  }
}
