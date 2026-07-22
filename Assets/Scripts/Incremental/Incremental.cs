using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

// Core state of The Incremental: the ONE source of truth for the count
// (decided 2026-07-13 - GameManager keeps game-flow flags only, never a
// second counter). Lives in PersistentScene next to the other manager
// singletons; like DoorStateRegistry, deliberately no DontDestroyOnLoad.
//
// Clicks are a currency, not a score: Count is the current spendable
// balance (all purchases route through TrySpend), TotalEarned is lifetime
// accumulation and never decreases. Door/end-button thresholds read the
// balance (decided 2026-07-15 - spending is meant to have skin in the game).
//
// The multiplier is source-based, not one additive float: charged
// multipliers drop when charge depletes, overdrive is a timed boost - a
// single float can't express that. Sources are keyed by propId (or a
// well-known id), which makes re-registration after a room reload naturally
// idempotent.
//
// The consumed registry for one-shot props also lives here (no second
// manager object): room scenes fully unload on transitions, so consumed
// state keyed by Prop.propId must sit in this persistent singleton or
// one-shot props would re-farm on every reload.
//
// The Circuit (2026-07-16): charge is capped by MaxCapacity, which ONLY
// rises - room activation (Terminal) and collected CapacityUpgrade props
// add capacity segments; nothing ever lowers it (the no-drain ruling at
// the capacity level). Charge stays ONE global number: gauges are derived
// views over Count / MaxCapacity, never per-room storage. The activation
// registry lives here too, same shape as the consumed registry.
public class Incremental : MonoBehaviour
{
    public static Incremental Instance { get; private set; }

    [SerializeField] float baseTicksPerSecond = 1f;

    [Header("The Circuit")]
    [Tooltip("The SecurityRoom - Start seeds the residue charge (exactly this room's activationCost) and the initial capacity floor so the residue fits. Unassigned = capacity layer disabled (uncapped legacy economy).")]
    [SerializeField] RoomId bootstrapRoom;

    [Tooltip("Every RoomId asset in the game. AllRoomsActivated checks against this explicit list - no asset-folder scanning at runtime. Tools > Circuit > Generate Room Terminals fills it.")]
    [SerializeField] List<RoomId> allRooms = new List<RoomId>();

    [Tooltip("Fraction of a new capacity segment's size granted as charge when the segment lands. 1 = the fill fraction holds (no visible sag on gauges); 0 = every bar in the building sags together. A balancing knob, not a system.")]
    [Range(0f, 1f)]
    [SerializeField] float chargeDumpFraction = 1f;

    [Tooltip("Fraction of MaxCapacity the bank must reach (with every room activated) for the end condition. Day-7 balancing knob. The SINGLE source - EndButtonSummoner/Sigil/checker all read EndConditionMet, never their own copy.")]
    [Range(0f, 1f)]
    [SerializeField] float endFraction = 1f;

    [Tooltip("Fires one impulse on StartIncremental() - the lever's camera-shake beat. Editor wiring: add this component here, add a CinemachineImpulseListener on the vCam. Unassigned = shake skipped, logged as a WOULD hook - doesn't block the phase.")]
    [SerializeField] CinemachineImpulseSource startImpulseSource;
    [Tooltip("Delay in seconds between StartIncremental() and the camera-shake impulse (Running still flips immediately - this only delays the shake beat).")]
    [SerializeField] float startImpulseDelay = 3f;

    public bool Running { get; private set; }

    // Current spendable balance. long, not float - a raw float counter
    // loses integer precision above ~16.7M.
    public long Count { get; private set; }

    // Lifetime accumulation; never decreases. Not read by any threshold
    // today (balance won that decision) but tracked for PA corruption
    // tiers / stats later - it is free to keep.
    public long TotalEarned { get; private set; }

    // Charge cap (The Circuit): capacity floor + every collected segment.
    // Only ever rises. 0 means the Circuit isn't wired yet (no bootstrap
    // room, no segments) - credit paths treat that as uncapped so the
    // pre-Circuit game keeps working until the editor wiring lands.
    public long MaxCapacity { get; private set; }

    // Gauge/debug ledger only - fill math never iterates this (proportional
    // fill everywhere is just Count / MaxCapacity).
    public IReadOnlyList<CapacitySegment> Segments => segments;

    // True when the bank is full and any credit would bank zero. One-shot
    // reward effects check this BEFORE claiming their consume - same
    // reasoning as their Running check: never burn a one-shot on a no-op.
    // Deliberately NOT checked by streams (tick/ManualClick): waste-at-cap
    // for generation is the ruling; this only protects total-loss presses.
    public bool AtCapacity => MaxCapacity > 0 && Count >= MaxCapacity;

    public float Multiplier => 1f + multiplierBonusSum;

    // Charge store for light-fed props (Phase 5) - a plain class on the
    // economy's clock, exposed directly rather than through wrapper
    // methods. Advance drives its decay; see ChargeRegistry for why it
    // lives centrally.
    public ChargeRegistry Charges { get; } = new ChargeRegistry();

    readonly Dictionary<string, float> multiplierSources = new Dictionary<string, float>();
    float multiplierBonusSum;
    int permanentSourceCounter;

    // Fractional tick progress. Whole ticks move into Count, the remainder
    // stays here - pausing works for free via timeScale = 0.
    double tickAccumulator;

    readonly HashSet<string> consumedPropIds = new HashSet<string>();

    // MaxCapacity independent of segments - the bootstrap residue must fit
    // before any room is activated (segments alone would leave the cap at 0).
    long capacityFloor;
    bool residueSeeded;

    readonly List<CapacitySegment> segments = new List<CapacitySegment>();
    readonly HashSet<RoomId> activatedRooms = new HashSet<RoomId>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[Incremental] Duplicate instance destroyed.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        // Keeps the static clean when an editor tool creates and destroys a
        // temporary instance (see Assets/Editor/IncrementalSelfTest.cs).
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Start()
    {
        SeedBootstrapResidue();
    }

    // Runs once from Start; public only so the editor self-test can drive it
    // (Start does not run in edit mode). The residue charge is EXACTLY the
    // bootstrap room's activation cost, so pre-start it can only ever buy
    // that one activation (every other terminal requires Running - the
    // pre-start softlock is impossible by construction). Deliberately not
    // via Credit: residue is found in the bank, not generated, so
    // TotalEarned stays 0.
    public void SeedBootstrapResidue()
    {
        if (residueSeeded)
        {
            return;
        }

        if (bootstrapRoom == null)
        {
            Debug.LogWarning("[Circuit] No bootstrap RoomId assigned - capacity layer disabled, economy runs uncapped.", this);
            return;
        }

        residueSeeded = true;
        RaiseCapacityFloor(bootstrapRoom.ActivationCost);
        Count = System.Math.Min(bootstrapRoom.ActivationCost, MaxCapacity);
        Debug.Log($"[Circuit] Residue charge seeded: {Count} (bootstrap room '{bootstrapRoom.name}').");
    }

    void Update()
    {
        Advance(Time.deltaTime);
    }

    // The whole tick model, separated from Update so the editor self-test
    // can drive simulated time directly. Public for that reason only -
    // gameplay code should never call this.
    public void Advance(double deltaSeconds)
    {
        if (!Running || deltaSeconds <= 0)
        {
            return;
        }

        tickAccumulator += baseTicksPerSecond * Multiplier * deltaSeconds;

        long wholeTicks = (long)tickAccumulator;
        if (wholeTicks > 0)
        {
            // Drain whole ticks from the accumulator BEFORE the clamp:
            // generation at cap is simply wasted (the generator idles), never
            // silently banked - a banked backlog would dump the instant
            // capacity next rises, cheating the chargeDumpFraction knob.
            tickAccumulator -= wholeTicks;
            Credit(wholeTicks);
        }

        Charges.Decay((float)deltaSeconds);
    }

    // Called by IncrementalStarter (the special computer prop). Idempotent.
    public void StartIncremental()
    {
        if (Running)
        {
            Debug.Log("[Incremental] StartIncremental ignored - already running.");
            return;
        }

        Running = true;
        Debug.Log("[Incremental] Started.");

        if (startImpulseSource != null)
        {
            StartCoroutine(FireStartImpulseAfterDelay());
        }
        else
        {
            Debug.Log("[Incremental] WOULD: camera shake on start (no CinemachineImpulseSource wired).");
        }

        Debug.Log("[Incremental] WOULD: notify ViR system that The Incremental has begun.");
    }

    IEnumerator FireStartImpulseAfterDelay()
    {
        yield return new WaitForSeconds(startImpulseDelay);
        startImpulseSource.GenerateImpulse();
    }

    // Flat one-time gain - FlatClickReward props.
    public void AddClicks(long amount)
    {
        if (!Running)
        {
            Debug.Log("[Incremental] AddClicks ignored - not running.");
            return;
        }

        if (amount <= 0)
        {
            Debug.LogWarning($"[Incremental] AddClicks ignored - non-positive amount {amount}.");
            return;
        }

        Credit(amount);
    }

    // Player spam-clicking a ClickSource prop. +1 flat - the multiplier
    // affects the auto-tick only. Kept separate from AddClicks so
    // click-feedback audio/UI can hook manual clicks specifically.
    public void ManualClick()
    {
        if (!Running)
        {
            Debug.Log("[Incremental] ManualClick ignored - not running.");
            return;
        }

        Credit(1);
    }

    // The one credit path: clamps to MaxCapacity (uncapped while the Circuit
    // is unwired, MaxCapacity == 0) and banks the post-clamp delta into
    // TotalEarned - the meter counts what was actually stored, not what the
    // generator produced. Returns the banked amount.
    long Credit(long amount)
    {
        long banked = amount;
        if (MaxCapacity > 0)
        {
            banked = System.Math.Min(amount, MaxCapacity - Count);
        }

        if (banked <= 0)
        {
            return 0;
        }

        Count += banked;
        TotalEarned += banked;
        return banked;
    }

    // All purchases route through this - door keypads, upgrade props.
    // Refusal feedback (sound, PA sneer) is the caller's job. Refuses while
    // not running; the ONE exception to that gate is the bootstrap terminal,
    // which goes through TryActivateRoom instead.
    public bool TrySpend(long cost)
    {
        if (!Running)
        {
            Debug.Log("[Incremental] TrySpend refused - not running.");
            return false;
        }

        return SpendInternal(cost);
    }

    bool SpendInternal(long cost)
    {
        if (cost < 0)
        {
            Debug.LogWarning($"[Incremental] TrySpend refused - negative cost {cost}.");
            return false;
        }

        if (Count < cost)
        {
            Debug.Log($"[Incremental] TrySpend refused: cost {cost}, balance {Count}.");
            return false;
        }

        Count -= cost;
        Debug.Log($"[Incremental] Spent {cost}, balance now {Count}.");
        return true;
    }

    // Thresholds read the current balance, not TotalEarned (decided
    // 2026-07-15): spending can delay the end button - that is the point.
    public bool HasReached(long threshold)
    {
        return Running && Count >= threshold;
    }

    // ------------------------------------------------------------------
    // The Circuit: capacity & room activation
    // ------------------------------------------------------------------

    public bool IsRoomActivated(RoomId room)
    {
        return room != null && activatedRooms.Contains(room);
    }

    // Read-only view of the configured room list - the RoomLampBoard cross-
    // checks its lamps against this at Start, and the E9 checker will too.
    public IReadOnlyList<RoomId> AllRooms => allRooms;

    // THE single end-condition expression (brief E4): every room powered AND
    // the bank at endFraction of capacity. EndButtonSummoner, Sigil, and the
    // checker must all read THIS - never reimplement the two-part gate.
    public bool EndConditionMet
    {
        get
        {
            if (!AllRoomsActivated)
            {
                return false;
            }

            long threshold = (long)System.Math.Ceiling(endFraction * (double)MaxCapacity);
            return HasReached(threshold);
        }
    }

    // One half of the end condition (the other is the charge threshold).
    // False while the serialized list is empty, so an unwired build can
    // never accidentally satisfy the end condition.
    public bool AllRoomsActivated
    {
        get
        {
            if (allRooms.Count == 0)
            {
                return false;
            }

            foreach (RoomId room in allRooms)
            {
                if (room != null && !activatedRooms.Contains(room))
                {
                    return false;
                }
            }

            return true;
        }
    }

    // Refusal-log detail for EndButtonSummoner ("N rooms unpowered").
    // Counts against the serialized list, same source as AllRoomsActivated.
    public int UnpoweredRoomCount
    {
        get
        {
            int count = 0;
            foreach (RoomId room in allRooms)
            {
                if (room != null && !activatedRooms.Contains(room))
                {
                    count++;
                }
            }

            return count;
        }
    }

    // The one activation path (called by Terminal). bootstrap bypasses the
    // Running gate for exactly one caller: the SecurityRoom terminal must
    // spend the residue charge BEFORE the start lever fires Running.
    // TrySpend itself keeps refusing pre-start for everyone else.
    public bool TryActivateRoom(RoomId room, bool bootstrap = false)
    {
        if (room == null)
        {
            Debug.LogWarning("[Circuit] TryActivateRoom refused - null RoomId.");
            return false;
        }

        if (activatedRooms.Contains(room))
        {
            Debug.Log($"[Circuit] Room '{room.name}' already activated - no second charge.");
            return false;
        }

        if (!Running && !bootstrap)
        {
            Debug.Log("[Circuit] TryActivateRoom refused - not running (bootstrap terminal only).");
            return false;
        }

        if (!SpendInternal(room.ActivationCost))
        {
            return false;
        }

        activatedRooms.Add(room);
        AddCapacitySegment(room, room.name, room.BaseCapacity);
        Debug.Log($"[Circuit] Room activated: {room.name} (+{room.BaseCapacity} capacity).");
        return true;
    }

    // Every capacity gain routes through here: room base segments (via
    // TryActivateRoom) and collected CapacityUpgrade props. Adds the segment
    // to the ledger, raises MaxCapacity, and dumps chargeDumpFraction of the
    // segment's size into the bank (clamped, banked into TotalEarned).
    public void AddCapacitySegment(RoomId room, string sourceId, long size)
    {
        if (size <= 0)
        {
            Debug.LogWarning($"[Circuit] AddCapacitySegment ignored - non-positive size {size} ('{sourceId}').");
            return;
        }

        segments.Add(new CapacitySegment(room, sourceId, size));
        MaxCapacity += size;

        long dumped = Credit((long)System.Math.Round(chargeDumpFraction * (double)size));
        Debug.Log($"[Circuit] Capacity +{size} ('{sourceId}') - MaxCapacity {MaxCapacity}, charge dumped {dumped}, balance {Count}.");
    }

    // Raises MaxCapacity independent of segments. Used by the bootstrap seed
    // (the residue must fit before any room is activated) and by the editor
    // self-test to opt into clamping. Never lowers - capacity only rises.
    public void RaiseCapacityFloor(long amount)
    {
        if (amount <= capacityFloor)
        {
            return;
        }

        MaxCapacity += amount - capacityFloor;
        capacityFloor = amount;
    }

    // Debug/self-test hook: re-derives what MaxCapacity should be from the
    // floor + segment ledger, so drift between the maintained value and the
    // ledger is an assertable bug rather than a silent one.
    public long RecalculateCapacity()
    {
        long sum = capacityFloor;
        foreach (CapacitySegment segment in segments)
        {
            sum += segment.Size;
        }

        return sum;
    }

    // ------------------------------------------------------------------
    // Multiplier sources
    // ------------------------------------------------------------------

    // Registering an existing key overwrites its amount, so a prop
    // re-registering after a room reload is naturally idempotent.
    public void RegisterMultiplierSource(string key, float amount)
    {
        if (string.IsNullOrEmpty(key))
        {
            Debug.LogWarning("[Incremental] RegisterMultiplierSource ignored - null/empty key.");
            return;
        }

        multiplierSources[key] = amount;
        RecalculateMultiplier();
        Debug.Log($"[Incremental] Multiplier source '{key}' = {amount}, multiplier now x{Multiplier:0.0#}.");
    }

    public void UnregisterMultiplierSource(string key)
    {
        if (string.IsNullOrEmpty(key) || !multiplierSources.Remove(key))
        {
            return;
        }

        RecalculateMultiplier();
        Debug.Log($"[Incremental] Multiplier source '{key}' removed, multiplier now x{Multiplier:0.0#}.");
    }

    // MultiplierUpgrade props - a permanent source that never unregisters.
    public void AddMultiplier(float amount)
    {
        permanentSourceCounter++;
        RegisterMultiplierSource($"permanent#{permanentSourceCounter}", amount);
    }

    void RecalculateMultiplier()
    {
        float sum = 0f;
        foreach (float amount in multiplierSources.Values)
        {
            sum += amount;
        }

        multiplierBonusSum = sum;
    }

    // ------------------------------------------------------------------
    // Consumed registry (one-shot props)
    // ------------------------------------------------------------------

    public bool IsConsumed(string propId)
    {
        return !string.IsNullOrEmpty(propId) && consumedPropIds.Contains(propId);
    }

    // False on null/empty/already-consumed - callers treat false as
    // "do not apply the effect".
    public bool TryConsume(string propId)
    {
        if (string.IsNullOrEmpty(propId))
        {
            return false;
        }

        return consumedPropIds.Add(propId);
    }
}
