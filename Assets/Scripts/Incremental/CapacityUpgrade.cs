using UnityEngine;

// A collectible prop that adds a capacity segment to the bank (The Circuit,
// 2026-07-16). Rate and capacity are separate upgrade types: MultiplierUpgrade
// still raises the tick rate, this raises MaxCapacity - a prop carries one or
// the other, not a hybrid.
//
// Apply routes through Incremental.AddCapacitySegment, which owns the whole
// consequence chain (segment ledger, MaxCapacity, chargeDumpFraction dump into
// the bank). The segment is tagged with the CURRENT room (Terminal.Current) so
// that room's TerminalGauge can show it as a live bar.
public class CapacityUpgrade : MonoBehaviour, IIncrementalEffect
{
    [SerializeField] long capacityAmount;
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
            Debug.LogWarning("[Incremental] CapacityUpgrade ignored - no Incremental instance.", this);
            return;
        }

        // Checked BEFORE the gate so pressing the prop pre-start does not
        // burn its one-shot on an ignored upgrade.
        if (!incremental.Running)
        {
            Debug.Log("[Incremental] CapacityUpgrade ignored - not running.", this);
            return;
        }

        if (oneShot && !IncrementalOneShotGate.TryClaim(this, ref instanceConsumedFallback))
        {
            return;
        }

        Prop prop = GetComponent<Prop>();
        string sourceId = prop != null && !string.IsNullOrEmpty(prop.PropId)
            ? prop.PropId
            : gameObject.name;

        RoomId currentRoom = Terminal.Current != null ? Terminal.Current.RoomId : null;
        incremental.AddCapacitySegment(currentRoom, sourceId, capacityAmount);
    }
}
