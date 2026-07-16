using System.Collections.Generic;
using UnityEngine;

// Central charge store for light-fed props (Phase 5) - a plain class owned
// by Incremental, deliberately NOT a MonoBehaviour: charge is economy state
// on the economy's clock (Incremental.Advance drives Decay, so it pauses,
// self-tests and resets exactly when the economy does), and it needs no
// scene presence or Inspector surface.
//
// Charge lives here, keyed by propId, for the same reason as the consumed
// registry: room scenes fully unload on transitions, and charge frozen in
// an unloaded room would be an exploit (charge a prop, leave, come back to
// a still-warm machine). Decay ticks centrally; an entry fed since the
// last pass skips that one pass, so an unlit prop and an unloaded prop
// drain through the same code path - unloaded props are simply dark by
// definition.
public class ChargeRegistry
{
    class Entry
    {
        public float charge;         // normalized 0..1
        public float decayPerSecond; // normalized charge lost per second while unfed
        public bool fedSinceLastDecay;
    }

    readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();

    // Reusable key buffer for the decay pass - fully drained entries get removed.
    readonly List<string> drained = new List<string>();

    public float GetCharge(string propId)
    {
        return !string.IsNullOrEmpty(propId) && entries.TryGetValue(propId, out Entry entry)
            ? entry.charge
            : 0f;
    }

    // Called every frame by a lit charging prop. Adds `gain` (normalized,
    // e.g. deltaTime / chargeTime) and exempts the entry from the next
    // decay pass. decayPerSecond is stored per entry so decay keeps running
    // centrally after the prop's room unloads. Returns the new charge.
    public float Feed(string propId, float gain, float decayPerSecond)
    {
        if (string.IsNullOrEmpty(propId))
        {
            Debug.LogWarning("[Incremental] ChargeRegistry.Feed ignored - null/empty propId.");
            return 0f;
        }

        if (gain <= 0f)
        {
            return GetCharge(propId);
        }

        if (!entries.TryGetValue(propId, out Entry entry))
        {
            entries[propId] = entry = new Entry();
        }

        entry.charge = Mathf.Min(1f, entry.charge + gain);
        entry.decayPerSecond = decayPerSecond;
        entry.fedSinceLastDecay = true;
        return entry.charge;
    }

    // Payout (or consumed cleanup): the prop starts cold next time.
    public void Clear(string propId)
    {
        if (!string.IsNullOrEmpty(propId))
        {
            entries.Remove(propId);
        }
    }

    // Driven by Incremental.Advance - never call from gameplay code.
    public void Decay(float deltaSeconds)
    {
        if (entries.Count == 0 || deltaSeconds <= 0f)
        {
            return;
        }

        drained.Clear();
        foreach (KeyValuePair<string, Entry> kv in entries)
        {
            Entry entry = kv.Value;
            if (entry.fedSinceLastDecay)
            {
                entry.fedSinceLastDecay = false;
                continue;
            }

            entry.charge -= entry.decayPerSecond * deltaSeconds;
            if (entry.charge <= 0f)
            {
                drained.Add(kv.Key);
            }
        }

        foreach (string propId in drained)
        {
            entries.Remove(propId);
        }
    }
}
