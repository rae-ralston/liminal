using UnityEngine;

// One asset per connection between two doors. Both door prefabs reference the
// SAME asset; each resolves its target as "the other endpoint" at runtime.
// This replaces the paired targetSceneName/targetSpawnId strings that had to
// be kept in sync manually on both sides.
//
// IMPORTANT: this asset is immutable config. Runtime state (is this
// connection currently locked?) lives in DoorStateRegistry, NOT here -
// mutating a ScriptableObject at runtime persists across play sessions in
// the editor and behaves differently in builds.
[CreateAssetMenu(fileName = "DoorConnection", menuName = "Liminal/Door Connection")]
public class DoorConnection : ScriptableObject
{
  [System.Serializable]
  public struct Endpoint
  {
    public string sceneName;
    public string spawnId;
  }

  [Header("Endpoints")]
  [SerializeField] Endpoint endpointA;
  [SerializeField] Endpoint endpointB;

  [Header("Lock config (state lives in DoorStateRegistry)")]
  [SerializeField] bool startsLocked;
  [Tooltip("Optional key/item/switch id required to unlock. Empty = unlockable by any Unlock() call.")]
  [SerializeField] string unlockId;

  [Header("Character")]
  [SerializeField] DoorType doorType = DoorType.WoodenInterior;
  [Tooltip("One-way: traversal only allowed from endpoint A to endpoint B.")]
  [SerializeField] bool isOneWay;
  [Tooltip("Optional fade duration override for this connection. <= 0 uses RoomLoader default. (Not yet wired - RoomLoader.TeleportTo takes no duration; reserved.)")]
  [SerializeField] float fadeDurationOverride = -1f;

  public Endpoint EndpointA => endpointA;

  public Endpoint EndpointB => endpointB;

  public bool StartsLocked => startsLocked;

  public string UnlockId => unlockId;

  public DoorType DoorType => doorType;

  public bool IsOneWay => isOneWay;

  public float FadeDurationOverride => fadeDurationOverride;

  // Which endpoint is the door living in 'sceneName' with 'spawnId'?
  // Returns the OTHER endpoint as the teleport target.
  public bool TryGetTarget(string sceneName, string spawnId, out Endpoint target)
  {
    if (Matches(endpointA, sceneName, spawnId))
    {
      target = endpointB;
      return true;
    }

    if (Matches(endpointB, sceneName, spawnId))
    {
      // one-way connections cannot be traversed from the B side
      if (isOneWay)
      {
        target = default;
        return false;
      }

      target = endpointA;
      return true;
    }

    Debug.LogError($"[DoorConnection] '{name}': no endpoint matches scene '{sceneName}' spawn '{spawnId}'. Check the asset wiring.", this);
    target = default;
    return false;
  }

  static bool Matches(Endpoint e, string sceneName, string spawnId)
  {
    return e.sceneName == sceneName && e.spawnId == spawnId;
  }
}
