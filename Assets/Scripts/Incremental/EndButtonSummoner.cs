using UnityEngine;

// Summons the huge end-of-game button once the end condition is reached.
// Lives on whatever prop/trigger performs the summoning; the end button
// itself is just a Prop (PropKind.EndButton) + an effect that calls into
// GameManager to end the game.
//
// No propId gate here - persistence comes free from
// GameManager.FinalButtonSummoned.
//
// The Circuit C6: the condition is AllRoomsActivated AND the charge at
// endFraction of MaxCapacity. Still reads the CURRENT BALANCE (decided
// 2026-07-15), so spending can delay the ending on purpose. An unwired
// build can never fire it: AllRoomsActivated is false while the room list
// is empty. endFraction is a Day-7 balancing knob (if "sit and wait at
// cap" feels flat, drop it - don't add mechanics).
public class EndButtonSummoner : MonoBehaviour, IIncrementalEffect
{
    [Tooltip("Fraction of MaxCapacity the charge must reach (with every room activated) to summon the end button. Day-7 balancing knob.")]
    [Range(0f, 1f)]
    [SerializeField] float endFraction = 1f;
    [SerializeField] GameObject endButtonPrefab;

    public void Apply()
    {
        Incremental incremental = Incremental.Instance;
        if (incremental == null)
        {
            Debug.LogWarning("[Incremental] EndButtonSummoner ignored - no Incremental instance.", this);
            return;
        }

        GameManager gameManager = GameManager.Instance;
        if (gameManager == null)
        {
            Debug.LogWarning("[Incremental] EndButtonSummoner ignored - no GameManager instance.", this);
            return;
        }

        if (gameManager.FinalButtonSummoned)
        {
            Debug.Log("[Incremental] End button already summoned - skipping.", this);
            return;
        }

        long chargeThreshold = (long)System.Math.Ceiling(endFraction * (double)incremental.MaxCapacity);
        if (!incremental.AllRoomsActivated || !incremental.HasReached(chargeThreshold))
        {
            // distinguish the two failures in the log - rooms first, since
            // charge percent is meaningless while capacity is still missing
            if (!incremental.AllRoomsActivated)
            {
                Debug.Log($"[Incremental] End locked: {incremental.UnpoweredRoomCount} rooms unpowered.", this);
            }
            else
            {
                long percent = incremental.MaxCapacity > 0 ? incremental.Count * 100 / incremental.MaxCapacity : 0;
                Debug.Log($"[Incremental] End locked: charge at {percent}% (need {chargeThreshold}, have {incremental.Count}).", this);
            }

            PropAudio audio = GetComponent<PropAudio>();
            if (audio != null) audio.PlayLocked();
            return;
        }

        gameManager.SummonFinalButton();
        Debug.Log("[Incremental] End button summoned.", this);
        Debug.Log($"[Incremental] WOULD: instantiate End Button prefab '{(endButtonPrefab != null ? endButtonPrefab.name : "None")}'.", this);
        Debug.Log("[Incremental] WOULD: notify ViR system of endgame phase.", this);
    }
}
