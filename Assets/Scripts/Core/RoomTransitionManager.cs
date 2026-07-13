using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

// Orchestrates the whole room-to-room transition: fade out, additively load the
// target room, warp the player to the arrival Door, unload the previous room,
// fade in. Also bootstraps the initial room. (Formerly RoomLoader - "load" was
// only one step of what it does.)
public class RoomTransitionManager : MonoBehaviour
{
  public static RoomTransitionManager Instance { get; private set; }

  [SerializeField] private Transform player;
  [SerializeField] private FadeController fadeController;
  [Tooltip("The door the player starts at. Its DoorId provides both the room to load and where to place the player.")]
  [SerializeField] private DoorId initialDoor;

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
    yield return SceneManager.LoadSceneAsync(initialDoor.SceneName, LoadSceneMode.Additive);
    currentRoomScene = SceneManager.GetSceneByName(initialDoor.SceneName);
    SceneManager.SetActiveScene(currentRoomScene);
    MovePlayerToDoor(currentRoomScene, initialDoor);

    yield return fadeController.FadeIn();
  }

  public void TeleportTo(DoorId target)
  {
    if (target == null)
    {
      Debug.LogError("[RoomTransitionManager] TeleportTo called with a null DoorId.");
      return;
    }

    if (!isTransitioning)
      StartCoroutine(TeleportRoutine(target));
  }

  private IEnumerator TeleportRoutine(DoorId target)
  {
    isTransitioning = true;
    yield return fadeController.FadeOut();

    Scene previousRoomScene = currentRoomScene;
    yield return SceneManager.LoadSceneAsync(target.SceneName, LoadSceneMode.Additive);

    Scene newRoomScene = SceneManager.GetSceneByName(target.SceneName);
    SceneManager.SetActiveScene(newRoomScene);

    MovePlayerToDoor(newRoomScene, target);

    yield return SceneManager.UnloadSceneAsync(previousRoomScene);

    currentRoomScene = newRoomScene;
    yield return fadeController.FadeIn();
    isTransitioning = false;
  }

  private void MovePlayerToDoor(Scene scene, DoorId id)
  {
    Door door = FindDoor(scene, id);
    if (door == null)
      return;

    Vector3 spawnPosition = door.SpawnPosition;
    Vector3 delta = spawnPosition - player.position;
    player.position = spawnPosition;
    player.rotation = door.SpawnRotation;
    CinemachineCore.OnTargetObjectWarped(player, delta);
  }

  private static Door FindDoor(Scene scene, DoorId id)
  {
    foreach (GameObject root in scene.GetRootGameObjects())
    {
      foreach (Door door in root.GetComponentsInChildren<Door>(true))
      {
        if (door.Id == id)
          return door;
      }
    }

    Debug.LogError($"No Door with id '{(id != null ? id.name : "null")}' found in scene '{scene.name}'.");
    return null;
  }
}
