using UnityEngine;

// One press in the four-press end chain (Ending brief E5). Carried by each stage
// button as an IIncrementalEffect (PropInteraction applies it on interact).
// `expected` is the stage that must be current for this press to count:
//   small button  -> expected = Called (advances Called -> Small)
//   larger button -> expected = Small  (advances Small -> Large)
//   End Button    -> expected = Large  (TERMINAL press: begins the discharge,
//                                       does not advance the chain)
// AdvanceEndStage's expected-guard makes a double-fire from a duplicated
// trigger a no-op.
public class SummonStep : MonoBehaviour, IIncrementalEffect
{
    [Tooltip("The stage that must be current for this press. Large = the terminal End Button press (begins the discharge; does not advance).")]
    [SerializeField] GameManager.EndStage expected = GameManager.EndStage.Called;

    public void Apply()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogWarning("[Ending] SummonStep ignored - no GameManager instance.", this);
            return;
        }

        if (expected == GameManager.EndStage.Large)
        {
            // Terminal press - only valid once the End Button is actually live.
            if (gm.Stage != GameManager.EndStage.Large)
            {
                Debug.Log($"[Ending] End Button press ignored - stage is {gm.Stage}, not Large.", this);
                return;
            }

            if (gm.EndSequenceRunning)
            {
                Debug.Log("[Ending] End Button already pressed - discharge already running.", this);
                return;
            }

            Debug.Log("[Ending] End Button pressed - beginning discharge.", this);

            if (EndSequenceController.Instance == null)
            {
                Debug.LogError("[Ending] No EndSequenceController in PersistentScene - the End Button does nothing.", this);
                return;
            }

            EndSequenceController.Instance.Begin();
            return;
        }

        gm.AdvanceEndStage(expected);
        Debug.Log("[Ending] WOULD: play the chain confirm cue.", this);
    }
}
