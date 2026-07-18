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

        // Same reasoning at the other end: a full bank credits zero, so the
        // press is refused instead of consuming the prop for nothing. A
        // PARTIALLY fitting reward still claims and clamps (waste-at-cap
        // ruling) - only the total-loss press is protected.
        if (incremental.AtCapacity)
        {
            Debug.Log("[Incremental] FlatClickReward refused - bank at capacity, spend some charge first.", this);
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
