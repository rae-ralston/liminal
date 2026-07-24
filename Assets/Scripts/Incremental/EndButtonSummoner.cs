using UnityEngine;

// The SecurityRoom end lever (Ending brief E5). The FIRST of the four-press end
// chain: it authorises the chain (Stage None -> Called) once the end condition
// is met. It does NOT summon or spawn anything - the AssemblyHall stage objects
// are pre-placed and enable themselves from GameManager.Stage (see
// EndStageObjects). Persistence is free from GameManager.Stage; no propId gate.
//
// Lives on the lever prop as an IIncrementalEffect. An unwired build can never
// fire it: Incremental.EndConditionMet is false while the room list is empty
// (AllRoomsActivated == false).
//
// (Name kept for continuity; it is the lever, not a spawner. The endFraction
// knob and endButtonPrefab it used to carry are gone - the condition is the
// single Incremental.EndConditionMet, and the stage objects are pre-placed.)
public class EndButtonSummoner : MonoBehaviour, IIncrementalEffect
{
    public void Apply()
    {
        Incremental incremental = Incremental.Instance;
        if (incremental == null)
        {
            Debug.LogWarning("[Ending] End lever ignored - no Incremental instance.", this);
            return;
        }

        GameManager gameManager = GameManager.Instance;
        if (gameManager == null)
        {
            Debug.LogWarning("[Ending] End lever ignored - no GameManager instance.", this);
            return;
        }

        if (gameManager.Stage >= GameManager.EndStage.Called)
        {
            Debug.Log("[Ending] End lever already thrown - chain already authorised.", this);
            return;
        }

        if (!incremental.EndConditionMet)
        {
            // Distinguish the two failures - rooms first, since charge percent
            // is meaningless while capacity is still missing.
            if (!incremental.AllRoomsActivated)
            {
                Debug.Log($"[Ending] Locked: {incremental.UnpoweredRoomCount} rooms unpowered.", this);
            }
            else
            {
                long percent = incremental.MaxCapacity > 0 ? incremental.Count * 100 / incremental.MaxCapacity : 0;
                Debug.Log($"[Ending] Locked: charge at {percent}%.", this);
            }

            PropAudio audio = GetComponent<PropAudio>();
            if (audio != null) audio.PlayLocked();
            return;
        }

        gameManager.AdvanceEndStage(GameManager.EndStage.None);
        Debug.Log("[Ending] End lever thrown - chain authorised (Stage -> Called).", this);
    }
}
