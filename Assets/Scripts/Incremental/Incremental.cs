using System.Collections.Generic;
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
public class Incremental : MonoBehaviour
{
    public static Incremental Instance { get; private set; }

    [SerializeField] float baseTicksPerSecond = 1f;

    public bool Running { get; private set; }

    // Current spendable balance. long, not float - a raw float counter
    // loses integer precision above ~16.7M.
    public long Count { get; private set; }

    // Lifetime accumulation; never decreases. Not read by any threshold
    // today (balance won that decision) but tracked for PA corruption
    // tiers / stats later - it is free to keep.
    public long TotalEarned { get; private set; }

    // Highest balance ever held; never decreases. Exists for the door
    // keypads' "Suspended" display state (yellow = "you could afford this
    // once, not now"): could-afford-once is a fact about balance HISTORY,
    // so deriving it from the peak works retroactively even for doors whose
    // room was unloaded when the peak happened - no per-connection latch
    // to store or poll.
    public long PeakCount { get; private set; }

    public float Multiplier => 1f + multiplierBonusSum;

    readonly Dictionary<string, float> multiplierSources = new Dictionary<string, float>();
    float multiplierBonusSum;
    int permanentSourceCounter;

    // Fractional tick progress. Whole ticks move into Count, the remainder
    // stays here - pausing works for free via timeScale = 0.
    double tickAccumulator;

    readonly HashSet<string> consumedPropIds = new HashSet<string>();

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
            tickAccumulator -= wholeTicks;
            Count += wholeTicks;
            TotalEarned += wholeTicks;
            if (Count > PeakCount) PeakCount = Count;
        }
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
        Debug.Log("[Incremental] WOULD: notify ViR system that The Incremental has begun.");
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

        Count += amount;
        TotalEarned += amount;
        if (Count > PeakCount) PeakCount = Count;
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

        Count += 1;
        TotalEarned += 1;
        if (Count > PeakCount) PeakCount = Count;
    }

    // All purchases route through this - door keypads, upgrade props.
    // Refusal feedback (sound, PA sneer) is the caller's job.
    public bool TrySpend(long cost)
    {
        if (!Running)
        {
            Debug.Log("[Incremental] TrySpend refused - not running.");
            return false;
        }

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
