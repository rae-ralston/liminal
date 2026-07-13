using UnityEngine;

// Stable, project-asset identity for ONE door endpoint in the game. This
// replaces the free-typed spawnId string that used to live on both SpawnPoint
// and DoorConnection.Endpoint and had to be kept in sync by hand.
//
// Why an asset instead of a string:
//  - Identity is the asset reference itself, so there is nothing to type and
//    nothing to keep in sync - a rename can't silently break a link.
//  - Unity allows asset->asset drag/drop in the Project window (it forbids
//    asset->scene-object). So a DoorConnection asset can reference two DoorId
//    assets, and each door in a scene references the DoorId it represents.
//  - The scene name lives here, once, instead of being duplicated on the
//    SpawnPoint's scene and the connection endpoint.
[CreateAssetMenu(fileName = "DoorId", menuName = "Liminal/Door Id")]
public class DoorId : ScriptableObject
{
  [Tooltip("Room scene this door lives in. RoomTransitionManager needs a scene name to " +
           "additively load the target room before placing the player.")]
  [SerializeField] string sceneName;

  // The scene this door belongs to. Single source of truth per door.
  public string SceneName => sceneName;
}
