using UnityEngine;

// One asset per connection between two doors. Both doors reference the SAME
// asset; each door also carries its own DoorId, and the connection returns
// "the other endpoint" as the teleport target.
//
// Endpoints are DoorId assets (not scene objects / strings): identity is the
// asset reference itself, so there is nothing to keep in sync and a rename
// can't silently break a link. The destination scene name lives on the DoorId.
//
// IMPORTANT: this asset is immutable config. Runtime state (is this connection
// currently locked?) lives in DoorStateRegistry, NOT here - mutating a
// ScriptableObject at runtime persists across play sessions in the editor and
// behaves differently in builds.
[CreateAssetMenu(fileName = "DoorConnection", menuName = "Liminal/Door Connection")]
public class DoorConnection : ScriptableObject
{
  [Header("Endpoints (each door in the scene carries the matching DoorId)")]
  [SerializeField] DoorId endpointA;
  [SerializeField] DoorId endpointB;

  [Header("Lock config (state lives in DoorStateRegistry)")]
  [SerializeField] bool startsLocked;
  [Tooltip("Optional key/item/switch id required to unlock. Empty = unlockable by any Unlock() call.")]
  [SerializeField] string unlockId;

  [Header("Economy (purchase state lives in DoorStateRegistry)")]
  [Tooltip("Click cost to purchase this connection at its keypad. 0 = free door, no purchase needed. Purchases are PERMANENT (decided 2026-07-15).")]
  [SerializeField] long clickCost;

  [Header("Character")]
  [SerializeField] DoorType doorType = DoorType.WoodenInterior;
  [Tooltip("One-way: traversal only allowed from endpoint A to endpoint B.")]
  [SerializeField] bool isOneWay;
  [Tooltip("Optional fade duration override for this connection. <= 0 uses RoomTransitionManager default. (Not yet wired - RoomTransitionManager.TeleportTo takes no duration; reserved.)")]
  [SerializeField] float fadeDurationOverride = -1f;

  public DoorId EndpointA => endpointA;

  public DoorId EndpointB => endpointB;

  public bool StartsLocked => startsLocked;

  public string UnlockId => unlockId;

  public long ClickCost => clickCost;

  // A priced connection blocks traversal until purchased (at a keypad prop).
  public bool IsPriced => clickCost > 0;

  public DoorType DoorType => doorType;

  public bool IsOneWay => isOneWay;

  public float FadeDurationOverride => fadeDurationOverride;

  // Given the DoorId of the door being used, return the DoorId to teleport to
  // (the OTHER endpoint).
  public bool TryGetTarget(DoorId from, out DoorId target)
  {
    if (from == endpointA)
    {
      target = endpointB;
      return true;
    }

    if (from == endpointB)
    {
      // one-way connections cannot be traversed from the B side
      if (isOneWay)
      {
        target = null;
        return false;
      }

      target = endpointA;
      return true;
    }

    Debug.LogError($"[DoorConnection] '{name}': DoorId '{(from != null ? from.name : "null")}' is not an endpoint of this connection. Check the door's assigned DoorId.", this);
    target = null;
    return false;
  }
}
