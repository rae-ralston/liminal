using System.Collections.Generic;
using UnityEngine;

// Owns the RUNTIME state of door connections (locked/unlocked). Lives in
// PersistentScene next to the other managers (RoomTransitionManager, GameManager).
// Like RoomTransitionManager, deliberately no DontDestroyOnLoad - it relies on
// PersistentScene never unloading.
//
// DoorConnection assets hold immutable config; this registry holds what can
// change during play. This is also the single thing to serialize if save
// games are ever added.
public class DoorStateRegistry : MonoBehaviour
{
  public static DoorStateRegistry Instance { get; private set; }

  readonly Dictionary<DoorConnection, bool> lockedState = new Dictionary<DoorConnection, bool>();

  // Economy state (2026-07-15): the ONLY stored fact per priced connection
  // is "has it been purchased" - purchases are permanent by design. The
  // red/orange/yellow/green keypad status is derived per query from this
  // plus Incremental's current/peak balance (see GetEconomyStatus).
  readonly HashSet<DoorConnection> purchased = new HashSet<DoorConnection>();

  void Awake()
  {
    if (Instance != null && Instance != this)
    {
      Debug.LogWarning("[DoorStateRegistry] Duplicate instance destroyed.", this);
      Destroy(gameObject);
      return;
    }

    Instance = this;
  }

  public bool IsLocked(DoorConnection connection)
  {
    if (connection == null)
    {
      return false;
    }

    // lazily seed from the asset's config the first time a connection is asked about
    if (!lockedState.TryGetValue(connection, out bool locked))
    {
      locked = connection.StartsLocked;
      lockedState[connection] = locked;
    }

    return locked;
  }

  // Unlock with an id check. Pass null/empty id to attempt an unconditional
  // unlock (only succeeds if the connection has no unlockId requirement).
  public bool TryUnlock(DoorConnection connection, string withId = null)
  {
    if (connection == null)
    {
      return false;
    }

    if (!IsLocked(connection))
    {
      return true;
    }

    bool idRequired = !string.IsNullOrEmpty(connection.UnlockId);
    bool idMatches = idRequired && connection.UnlockId == withId;

    if (!idRequired || idMatches)
    {
      lockedState[connection] = false;
      return true;
    }

    return false;
  }

  // For remote unlockers (a button prop elsewhere flipping a door open) -
  // bypasses the id check on purpose. This is what DoorUnlocker calls.
  public void ForceSetLocked(DoorConnection connection, bool locked)
  {
    if (connection == null)
    {
      return;
    }

    lockedState[connection] = locked;
  }

  // ------------------------------------------------------------------
  // Economy (door purchases - permanent, keypad-driven)
  // ------------------------------------------------------------------

  public bool IsPurchased(DoorConnection connection)
  {
    return connection != null && purchased.Contains(connection);
  }

  // True when money still stands between the player and this connection.
  // This is the traversal-blocking check Door uses, alongside IsLocked.
  public bool IsPurchaseRequired(DoorConnection connection)
  {
    return connection != null && connection.IsPriced && !purchased.Contains(connection);
  }

  // The one purchase path: spends via Incremental.TrySpend, marks the
  // connection purchased on success. Idempotent - buying twice is refused
  // as already-owned (true, no second charge).
  public bool TryPurchase(DoorConnection connection)
  {
    if (connection == null || !connection.IsPriced)
    {
      return false;
    }

    if (purchased.Contains(connection))
    {
      return true;
    }

    if (Incremental.Instance == null)
    {
      Debug.LogError("[DoorStateRegistry] TryPurchase failed - no Incremental in scene.", this);
      return false;
    }

    if (!Incremental.Instance.TrySpend(connection.ClickCost))
    {
      return false;
    }

    purchased.Add(connection);
    Debug.Log($"[Incremental] Door connection '{connection.name}' purchased for {connection.ClickCost}. Permanent.");
    return true;
  }

  // Derived per query, never stored (aside from the purchased set): the
  // keypad light state, all read from the current IS-state. Yellow
  // ("within reach - your capacity can hold the cost, you just haven't
  // saved it") falls out of Incremental.MaxCapacity; red means the cost
  // exceeds MaxCapacity, i.e. activate more rooms before this is buyable.
  public DoorEconomyStatus GetEconomyStatus(DoorConnection connection)
  {
    if (connection == null || !connection.IsPriced)
    {
      return DoorEconomyStatus.NotPriced;
    }

    if (purchased.Contains(connection))
    {
      return DoorEconomyStatus.Purchased;
    }

    Incremental incremental = Incremental.Instance;
    if (incremental == null || !incremental.Running)
    {
      return DoorEconomyStatus.Locked;
    }

    if (incremental.Count >= connection.ClickCost)
    {
      return DoorEconomyStatus.Unlockable;
    }

    return incremental.MaxCapacity >= connection.ClickCost
      ? DoorEconomyStatus.Suspended
      : DoorEconomyStatus.Locked;
  }
}
