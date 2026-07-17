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
  public static GameManager Instance { get; private set; }
  public bool GameStarted { get; private set; } = false;
  public bool FinalButtonSummoned { get; private set; } = false;

  private void Awake()
  {
    if (Instance != null && Instance != this)
    {
      Destroy(gameObject);
      return;
    }

    Instance = this;
  }

  // Called by SecurityStation. The old threshold branch that summoned the
  // final button from here is now EndButtonSummoner's job.
  public void StartGame()
  {
    GameStarted = true;
  }

  public void SummonFinalButton()
  {
    FinalButtonSummoned = true;
  }
}
