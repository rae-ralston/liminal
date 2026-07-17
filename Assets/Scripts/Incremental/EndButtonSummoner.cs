using UnityEngine;

// Summons the huge end-of-game button once the count threshold is reached.
// Lives on whatever prop/trigger performs the summoning; the end button
// itself is just a Prop (PropKind.EndButton) + an effect that calls into
// GameManager to end the game.
//
// No propId gate here - persistence comes free from
// GameManager.FinalButtonSummoned. The threshold reads the CURRENT BALANCE
// (decided 2026-07-15), so spending can delay the ending on purpose.
public class EndButtonSummoner : MonoBehaviour, IIncrementalEffect
{
    [SerializeField] long summonThreshold;
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

        if (!incremental.HasReached(summonThreshold))
        {
            Debug.Log($"[Incremental] End button locked: need {summonThreshold}, have {incremental.Count}.", this);
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
