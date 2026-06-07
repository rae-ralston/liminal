using UnityEngine;

public class GameManager : MonoBehaviour
{
  public static GameManager Instance { get; private set; }
  [SerializeField] private float counterThreshold = 100f;
  [SerializeField] private float counterIncrement = 1f;
  public float Counter { get; private set; } = 0f;
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
    DontDestroyOnLoad(gameObject);
  }

  private void Update()
  {
    if (GameStarted && !FinalButtonSummoned)
    {
      Counter += counterIncrement * Time.deltaTime;
    }
  }

  public void StartGame()
  {
    if (!GameStarted)
      GameStarted = true;
    else if (Counter >= counterThreshold)
      SummonFinalButton();
  }

  public void IncrementCounter(float amount)
  {
    if (GameStarted)
      Counter += amount;
  }

  public void SummonFinalButton()
  {
    FinalButtonSummoned = true;
  }
}
