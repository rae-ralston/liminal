using UnityEngine;

// TESTING ONLY - an in-game button that forces the end condition so the ending
// can be reached in seconds instead of a full run.
//
// Lives in Assets/Scripts/Debug/ on purpose: this whole folder is meant to be
// obvious, and the placed object must come out of the scene before submission.
// See the loud warning log and the build guard below.
//
// An InteractableTrigger, NOT a PropInteraction prop. Props are gated behind
// room activation (the Circuit's C3 gate), so at game start - in an unpowered
// SecurityRoom, which is exactly when this button is useful - a prop-based
// button would refuse itself. Same reasoning as Terminal and DoorPurchaser.
//
// What it does NOT do: verify that the ending is reachable by honest play.
// It bypasses activation cost outright. Whether a real run can afford every
// room in some order is a separate question and wants its own editor test.
public class DebugEndConditionButton : InteractableTrigger
{
  [Tooltip("Safety catch. In the editor this button always works. In a player build it refuses unless this is ticked - so a forgotten button in the submitted game is inert rather than a one-press skip to the ending.")]
  [SerializeField] bool allowInPlayerBuilds = false;

  [Tooltip("Also jump the end chain straight to Large, so the End Button in AssemblyHall is live immediately and the SecurityRoom lever plus the two stage presses can be skipped. Leave OFF to exercise the real four-press chain.")]
  [SerializeField] bool advanceChainToLarge = false;

  public override void Interact()
  {
    if (!Application.isEditor && !allowInPlayerBuilds)
    {
      Debug.Log("[DEBUG] End-condition button refused - disabled in player builds.", this);
      return;
    }

    if (Incremental.Instance == null)
    {
      Debug.LogError("[DEBUG] End-condition button: no Incremental instance.", this);
      return;
    }

    Incremental.Instance.DebugSatisfyEndCondition();

    if (advanceChainToLarge)
    {
      GameManager gm = GameManager.Instance;
      if (gm == null)
      {
        Debug.LogError("[DEBUG] End-condition button: no GameManager instance, chain not advanced.", this);
        return;
      }

      // Walk the chain one guarded step at a time rather than setting Stage
      // directly - AdvanceEndStage stays the only way the chain moves, so
      // this cheat cannot produce a state the real chain never would.
      while (gm.Stage < GameManager.EndStage.Large)
      {
        GameManager.EndStage before = gm.Stage;
        gm.AdvanceEndStage(before);

        if (gm.Stage == before)
        {
          Debug.LogError($"[DEBUG] Chain refused to advance past {before}.", this);
          break;
        }
      }

      Debug.LogWarning($"[DEBUG] End chain forced to {gm.Stage} - the End Button in AssemblyHall is live.", this);
    }

    PropAudio audio = GetComponent<PropAudio>();
    if (audio != null) audio.PlayInteract();
  }
}
