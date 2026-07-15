using UnityEngine;

// A prop that raises the tick multiplier, typically once per prop.
// Registers a permanent multiplier source via AddMultiplier (source-based -
// see Incremental.RegisterMultiplierSource for why not one additive float).
public class MultiplierUpgrade : MonoBehaviour, IIncrementalEffect
{
    [SerializeField] float multiplierAmount;
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
            Debug.LogWarning("[Incremental] MultiplierUpgrade ignored - no Incremental instance.", this);
            return;
        }

        // Checked BEFORE the gate so pressing the prop pre-start does not
        // burn its one-shot on an ignored upgrade.
        if (!incremental.Running)
        {
            Debug.Log("[Incremental] MultiplierUpgrade ignored - not running.", this);
            return;
        }

        if (oneShot && !IncrementalOneShotGate.TryClaim(this, ref instanceConsumedFallback))
        {
            return;
        }

        incremental.AddMultiplier(multiplierAmount);
    }
}
