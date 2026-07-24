using UnityEngine;

// Game-flow flags ONLY. The count/economy is Incremental
// (Assets/Scripts/Incremental/Incremental.cs) - the one source of truth,
// decided 2026-07-13. Never grow a second counter here.
//
// Lives in PersistentScene; like the other manager singletons there,
// deliberately no DontDestroyOnLoad (the scene never unloads, and the call
// warns on non-root objects).
public class GameManager : MonoBehaviour
{
  // The four-press end chain (Ending brief E1). The cross-scene state the
  // SecurityRoom lever and the AssemblyHall stage objects (EndStageObjects)
  // and both Sigils all poll - the scene objects are views, this singleton
  // owns the state.
  //   None   - chain not started.
  //   Called - the SecurityRoom end lever has authorised the chain.
  //   Small  - the stage's small button has been pressed.
  //   Large  - the End Button is live (its press begins the discharge).
  public enum EndStage { None = 0, Called = 1, Small = 2, Large = 3 }

  public static GameManager Instance { get; private set; }
  public bool GameStarted { get; private set; } = false;

  // NOTE: the brief writes this property as `EndStage EndStage`; a property
  // cannot share a name with the nested enum type (CS0102), so it is `Stage`.
  public EndStage Stage { get; private set; } = EndStage.None;

  // Derived so there is ONE source of truth for "the End Button is live"
  // (brief E1). This was a standalone flag set by the old SummonFinalButton();
  // that single-press path is replaced by the four-press chain. Existing
  // readers (EndButtonSummoner) keep working against this name.
  public bool FinalButtonSummoned => Stage >= EndStage.Large;

  // Set once the discharge begins (brief E6, EndSequenceController); one-way,
  // never cleared. Systems gate on it (e.g. interaction lockout during the
  // ending). The sequence itself is not built yet.
  public bool EndSequenceRunning { get; private set; } = false;

  // Set true while the end card is on screen so the player stops walking. The
  // void hold BEFORE the card deliberately leaves movement enabled (the lit
  // figure walks the dark hall); only once the card is up is the run over.
  // Cleared naturally by the Again reload (a fresh GameManager), and
  // explicitly in EndSequenceController.Again() for safety.
  public bool MovementFrozen { get; set; } = false;

  // Session length for the end card's meter reading. Starts at boot so a run
  // driven by the debug button (which never touches StartGame) still reports
  // something honest, and is overwritten when the run properly begins. Frozen
  // at BeginEndSequence so the number doesn't keep climbing while the player
  // reads the card.
  private float sessionStartTime;
  private float sessionEndTime = -1f;

  public float SessionDuration =>
    (sessionEndTime < 0f ? Time.time : sessionEndTime) - sessionStartTime;

  private void Awake()
  {
    if (Instance != null && Instance != this)
    {
      Destroy(gameObject);
      return;
    }

    Instance = this;
    sessionStartTime = Time.time;
  }

  // Called by SecurityStation. The old threshold branch that summoned the
  // final button from here is now the end chain's job (EndButtonSummoner).
  public void StartGame()
  {
    GameStarted = true;
    sessionStartTime = Time.time;
  }

  // Advance the end chain by exactly one step. `expected` is the stage the
  // caller believes is current - the advance refuses unless it matches, so a
  // duplicated trigger firing twice cannot skip or double-advance the chain
  // (brief E1). Idempotent by construction.
  public void AdvanceEndStage(EndStage expected)
  {
    if (Stage != expected)
    {
      Debug.Log($"[Ending] AdvanceEndStage refused: stage is {Stage}, caller expected {expected}.");
      return;
    }

    if (Stage >= EndStage.Large)
    {
      Debug.Log("[Ending] AdvanceEndStage refused: chain already at Large.");
      return;
    }

    Stage = (EndStage)((int)Stage + 1);
    Debug.Log($"[Ending] Stage → {Stage}.");
  }

  // Called by EndSequenceController.Begin() (E6) when the End Button is
  // pressed. One-way; never cleared.
  public void BeginEndSequence()
  {
    EndSequenceRunning = true;
    sessionEndTime = Time.time;
  }
}
