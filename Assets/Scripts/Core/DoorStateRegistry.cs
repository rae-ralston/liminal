using System.Collections.Generic;
using UnityEngine;

// Owns the RUNTIME state of door connections (locked/unlocked). Lives in
// PersistentScene next to the other managers (RoomLoader, GameManager).
// Like RoomLoader, deliberately no DontDestroyOnLoad - it relies on
// PersistentScene never unloading.
//
// DoorConnection assets hold immutable config; this registry holds what can
// change during play. This is also the single thing to serialize if save
// games are ever added.
public class DoorStateRegistry : MonoBehaviour
{
  public static DoorStateRegistry Instance { get; private set; }

  readonly Dictionary<DoorConnection, bool> lockedState = new Dictionary<DoorConnection, bool>();

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
}
