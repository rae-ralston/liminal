using UnityEngine;

// A prop that grants a fixed amount of clicks, typically once
// (a lever, a hidden button behind a filing cabinet).
public class FlatClickReward : MonoBehaviour, IIncrementalEffect
{
    [SerializeField] long clickAmount;
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
        Incremental incremental = Incremental.Instance;
        if (incremental == null)
        {
            Debug.LogWarning("[Incremental] FlatClickReward ignored - no Incremental instance.", this);
            return;
        }

        // Checked BEFORE the gate so pressing the prop pre-start does not
        // burn its one-shot on an ignored AddClicks.
        if (!incremental.Running)
        {
            Debug.Log("[Incremental] FlatClickReward ignored - not running.", this);
            return;
        }

        if (oneShot && !IncrementalOneShotGate.TryClaim(this, ref instanceConsumedFallback))
        {
            return;
        }

        incremental.AddClicks(clickAmount);
        Debug.Log($"[Incremental] +{clickAmount} clicks, count now {incremental.Count}.", this);
    }
}
