using UnityEngine;

// The special computer that starts The Incremental. Dropped onto a prop
// GameObject next to Prop (identity) and an interaction trigger; talks only
// to Incremental.Instance - props never talk to each other.
public class IncrementalStarter : MonoBehaviour, IIncrementalEffect
{
    [SerializeField] bool oneShot = true;

    bool instanceConsumedFallback;

    void Start()
    {
        if (oneShot)
        {
            IncrementalOneShotGate.LogIfConsumedOnEntry(this);
        }
    }

    public void Apply()
    {
        if (Incremental.Instance == null)
        {
            Debug.LogWarning("[Incremental] IncrementalStarter ignored - no Incremental instance.", this);
            return;
        }

        if (oneShot && !IncrementalOneShotGate.TryClaim(this, ref instanceConsumedFallback))
        {
            return;
        }

        Incremental.Instance.StartIncremental();
    }
}
