using UnityEngine;

// Rework of DoorTeleporter: resolves its teleport target from a shared
// DoorConnection asset instead of locally serialized strings. The door
// identifies itself by (owning scene, mySpawnId) and asks the connection for
// the other endpoint.
//
// Migration from the old string-based version: on each door, assign the
// DoorConnection asset + set mySpawnId to this door's SpawnPoint id. The old
// targetSceneName/targetSpawnId fields are gone; Unity drops them silently.
public class DoorTeleporter : InteractableTrigger
{
  [SerializeField] DoorConnection connection;
  [Tooltip("Spawn id of the SpawnPoint that belongs to THIS door. Identifies which endpoint of the connection this door is.")]
  [SerializeField] string mySpawnId;

  public override void Interact()
  {
    if (connection == null)
    {
      Debug.LogError($"[DoorTeleporter] '{name}' has no DoorConnection assigned.", this);
      return;
    }

    // Audio lives on a PropAudio in this door's hierarchy, same as before.
    PropAudio audio = GetComponentInParent<PropAudio>();

    if (DoorStateRegistry.Instance != null && DoorStateRegistry.Instance.IsLocked(connection))
    {
      // locked: rattle, no traversal
      if (audio != null) audio.PlayLocked(connection.DoorType);
      return;
    }

    string myScene = gameObject.scene.name;

    if (!connection.TryGetTarget(myScene, mySpawnId, out DoorConnection.Endpoint target))
    {
      // one-way from the wrong side behaves like a locked door
      if (audio != null) audio.PlayLocked(connection.DoorType);
      return;
    }

    if (audio != null) audio.PlayOpen(connection.DoorType);

    RoomLoader.Instance.TeleportTo(target.sceneName, target.spawnId);
  }
}
