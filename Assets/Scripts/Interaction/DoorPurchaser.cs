using UnityEngine;

// The diegetic door-purchase keypad (decided 2026-07-15): a small prop next
// to a priced door. Interacting attempts the purchase; the keypad's status
// light (DoorIndicatorLight on the same GameObject) IS the affordability
// display - shipping builds never show a raw number.
//
// An InteractableTrigger like Door itself, NOT a PropInteraction prop: the
// keypad needs conditional audio (confirm vs refusal), and doors/keypads are
// not props - no Prop identity, no propId. Purchase state is keyed by the
// DoorConnection asset in DoorStateRegistry; spending routes through
// Incremental.TrySpend inside DoorStateRegistry.TryPurchase. Purchases are
// PERMANENT - a bought door never re-locks, whatever the balance does.
public class DoorPurchaser : InteractableTrigger
{
  [Tooltip("The priced connection this keypad sells. Both sides of a door pair may have a keypad pointing at the SAME connection - either one completes the purchase.")]
  [SerializeField] DoorConnection connection;

  public DoorConnection Connection => connection;

  public override void Interact()
  {
    if (connection == null)
    {
      Debug.LogError($"[DoorPurchaser] '{name}' has no DoorConnection assigned.", this);
      return;
    }

    if (!connection.IsPriced)
    {
      Debug.LogWarning($"[DoorPurchaser] '{name}': connection '{connection.name}' has no clickCost - keypad does nothing.", this);
      return;
    }

    if (DoorStateRegistry.Instance == null)
    {
      Debug.LogError("[DoorPurchaser] No DoorStateRegistry in scene.", this);
      return;
    }

    PropAudio audio = GetComponent<PropAudio>();

    if (DoorStateRegistry.Instance.IsPurchased(connection))
    {
      // acknowledge, no second charge - purchases are permanent
      Debug.Log($"[Incremental] Keypad '{name}': '{connection.name}' already purchased.");
      if (audio != null) audio.PlayInteract();
      return;
    }

    if (DoorStateRegistry.Instance.TryPurchase(connection))
    {
      if (audio != null) audio.PlayInteract();
      Debug.Log("[Incremental] WOULD: PA reacts to a door purchase.");
      return;
    }

    // refused - unaffordable (or the Incremental isn't running yet)
    long balance = Incremental.Instance != null ? Incremental.Instance.Count : 0;
    Debug.Log($"[Incremental] Keypad '{name}': purchase refused - cost {connection.ClickCost}, balance {balance}.");
    if (audio != null) audio.PlayLocked();
    Debug.Log("[Incremental] WOULD: PA sneers at a failed keypad purchase.");
  }
}
