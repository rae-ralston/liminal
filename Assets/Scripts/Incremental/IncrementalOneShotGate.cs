using UnityEngine;

// Shared one-shot gate for the effect components - a static helper, NOT a
// base class (effects stay flat components per the composition constraint).
//
// Consumed state is keyed by Prop.propId in the persistent Incremental
// singleton so it survives room reloads. A prop without a propId falls back
// to an instance-local bool (with a warning) - it will re-farm after a
// reload, which is exactly what the warning says.
static class IncrementalOneShotGate
{
    // Claims the one-shot use of the prop carrying `effect`. True = apply
    // the effect now; false = already consumed, skip. Duplicate propIds
    // surface naturally via the "already consumed" log firing on a prop
    // that was never used.
    public static bool TryClaim(MonoBehaviour effect, ref bool instanceConsumedFallback)
    {
        string propId = ResolvePropId(effect);

        if (string.IsNullOrEmpty(propId))
        {
            if (instanceConsumedFallback)
            {
                Debug.Log($"[Incremental] {effect.GetType().Name} on '{effect.gameObject.name}' already consumed (instance-local) - skipping (would show consumed visual state).", effect);
                return false;
            }

            Debug.LogWarning($"[Incremental] {effect.GetType().Name} on '{effect.gameObject.name}' has no propId - consumed state will NOT survive room reloads.", effect);
            instanceConsumedFallback = true;
            return true;
        }

        if (!Incremental.Instance.TryConsume(propId))
        {
            Debug.Log($"[Incremental] '{propId}' already consumed - skipping (would show consumed visual state).", effect);
            return false;
        }

        return true;
    }

    // One-shot effects call this from Start() so a prop re-loaded in
    // consumed state announces itself on room entry.
    public static void LogIfConsumedOnEntry(MonoBehaviour effect)
    {
        if (Incremental.Instance == null)
        {
            return;
        }

        string propId = ResolvePropId(effect);
        if (Incremental.Instance.IsConsumed(propId))
        {
            Debug.Log($"[Incremental] '{propId}' re-loaded in consumed state (would show consumed visuals).", effect);
        }
    }

    static string ResolvePropId(MonoBehaviour effect)
    {
        Prop prop = effect.GetComponent<Prop>();
        return prop != null ? prop.PropId : null;
    }
}
