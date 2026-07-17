using UnityEngine;

// Stable, project-asset identity for ONE room scene (The Circuit, 2026-07-16).
// Same pattern and rationale as DoorId: identity is the asset reference itself,
// so terminals and the activation registry key on something type-safe and
// rename-proof instead of a scene-name string.
//
// Also carries the room's two Circuit numbers, because they are per-room
// CONFIG, not runtime state (same discipline as DoorConnection.clickCost):
//  - baseCapacity: the capacity segment this room contributes when activated.
//  - activationCost: the charge its Terminal spends to activate it.
// Runtime activation state lives ONLY in Incremental's activation registry,
// never on this asset (SO runtime mutation persists in-editor).
[CreateAssetMenu(fileName = "RoomId", menuName = "Liminal/Room Id")]
public class RoomId : ScriptableObject
{
  [Tooltip("Room scene this asset identifies. Must match the scene file name exactly - the checkers lint a mismatch.")]
  [SerializeField] string sceneName;

  [Tooltip("Capacity segment this room adds to MaxCapacity when its terminal activates it.")]
  [SerializeField] long baseCapacity = 10;

  [Tooltip("Charge the room's terminal spends to activate the room. Must stay reachable - the door checker's reachability lint flags a cost above the capacity attainable without it.")]
  [SerializeField] long activationCost = 5;

  public string SceneName => sceneName;
  public long BaseCapacity => baseCapacity;
  public long ActivationCost => activationCost;

#if UNITY_EDITOR
  // For Tools > Circuit > Generate Room Terminals only - runtime code never
  // writes to this asset.
  public void EditorInit(string scene)
  {
    sceneName = scene;
  }
#endif
}
