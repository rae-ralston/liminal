using UnityEngine;

// The room-activation terminal (The Circuit, 2026-07-16): one per room scene.
// Interacting spends charge to activate the room, adding its base capacity
// segment to the bank. Activation is PERMANENT (registry in Incremental, same
// discipline as door purchases - never re-locks).
//
// An InteractableTrigger like DoorPurchaser, NOT a PropInteraction prop: no
// Prop identity, no propId - state is keyed by the RoomId asset. Also NOT a
// button prefab variant (would inherit Prop/PropInteraction and trip the
// prop checker's XOR/applier lints) - it lives on its own standalone prefab.
//
// The SecurityRoom terminal is the bootstrap: the only interaction in the
// game that works before Running, spending the seeded residue charge. Every
// other terminal is inert until the start lever fires.
public class Terminal : InteractableTrigger
{
  // "What room am I in" for the activation gate (PropInteraction /
  // LightFedCharge check the current room without per-interaction Find
  // calls). Last-enabled wins; one terminal per room scene is
  // checker-enforced. The clear is CONDITIONAL because an additive room
  // transition can run the NEW room's OnEnable before the OLD room's
  // OnDisable - an unconditional null would wipe the fresh registration.
  public static Terminal Current { get; private set; }

  [Tooltip("The room this terminal activates. EMPTY in the prefab - set per placed instance (same law as propIds/connections). Tools > Circuit > Generate Room Terminals wires it.")]
  [SerializeField] RoomId roomId;

  [Tooltip("SecurityRoom only: works before the Incremental is running, consuming the seeded residue charge. Exactly one bootstrap terminal exists in the game.")]
  [SerializeField] bool isBootstrap;

  public RoomId RoomId => roomId;
  public bool IsBootstrap => isBootstrap;

  // Single source of truth for "does this terminal respond to interaction" -
  // TerminalGlow reads this. TRUE for the bootstrap terminal even before
  // Running (it's the one interaction that works pre-start).
  public bool IsLive => Incremental.Instance != null && (Incremental.Instance.Running || isBootstrap);

  PropAudio audio;

  void Awake()
  {
    audio = GetComponent<PropAudio>();
  }

  void OnEnable()
  {
    Current = this;
  }

  void OnDisable()
  {
    if (Current == this) Current = null;
  }

  // Ambient state is just (activated, running) - not activated is Inactive
  // regardless of anything else; activated while Running is Active; activated
  // while not Running is Suspended. No isBootstrap check needed here: today
  // only the bootstrap terminal can reach "activated but not Running" (its
  // Interact() gate below is the only path that allows activating pre-Running)
  // but the state machine itself doesn't care how that combination came about.
  void Update()
  {
    if (audio == null || Incremental.Instance == null)
    {
      return;
    }

    if (!Incremental.Instance.IsRoomActivated(roomId))
    {
      audio.SetAmbientState(PropAudio.AmbientState.Inactive);
    }
    else
    {
      audio.SetAmbientState(Incremental.Instance.Running
        ? PropAudio.AmbientState.Active
        : PropAudio.AmbientState.Suspended);
    }
  }

  public override void Interact()
  {
    if (roomId == null)
    {
      Debug.LogError($"[Circuit] Terminal '{name}' has no RoomId assigned.", this);
      return;
    }

    if (Incremental.Instance == null)
    {
      Debug.LogError("[Circuit] No Incremental in scene.", this);
      return;
    }

    if (Incremental.Instance.IsRoomActivated(roomId))
    {
      // acknowledge, no second charge - activation is permanent
      Debug.Log($"[Circuit] Terminal '{name}': room '{roomId.name}' already activated.");
      if (audio != null) audio.PlayInteract();
      return;
    }

    if (!Incremental.Instance.Running && !isBootstrap)
    {
      // the dead-building wander: nothing responds until the bootstrap
      Debug.Log($"[Circuit] Terminal '{name}' inert: system not running.");
      if (audio != null) audio.PlayLocked();
      return;
    }

    if (Incremental.Instance.TryActivateRoom(roomId, isBootstrap))
    {
      if (audio != null) audio.PlayInteract();
      Debug.Log("[Circuit] WOULD: PA reacts to a room coming online.");
      return;
    }

    // refused - unaffordable
    Debug.Log($"[Circuit] Terminal '{name}': activation refused - cost {roomId.ActivationCost}, balance {Incremental.Instance.Count}.");
    if (audio != null) audio.PlayLocked();
  }
}
