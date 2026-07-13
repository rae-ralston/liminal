using UnityEngine;

// The GDD's "button unlocks a door" prop: drop this next to PropInteraction
// on any interactable prop. Reuses the IIncrementalEffect application path
// (PropInteraction applies every effect on the GameObject) even though it
// doesn't touch the counter - one interaction pipeline for all prop effects.
//
// Must live on the SAME GameObject as PropInteraction, which does
// GetComponents<IIncrementalEffect>() on itself and calls Apply().
public class DoorUnlocker : MonoBehaviour, IIncrementalEffect
{
  [SerializeField] DoorConnection connection;
  [Tooltip("If set, this prop LOCKS the connection instead of unlocking it.")]
  [SerializeField] bool lockInstead;

  public void Apply()
  {
    if (connection == null)
    {
      Debug.LogError($"[DoorUnlocker] '{name}' has no DoorConnection assigned.", this);
      return;
    }

    if (DoorStateRegistry.Instance == null)
    {
      Debug.LogError("[DoorUnlocker] No DoorStateRegistry in scene.", this);
      return;
    }

    DoorStateRegistry.Instance.ForceSetLocked(connection, lockInstead);
  }
}
