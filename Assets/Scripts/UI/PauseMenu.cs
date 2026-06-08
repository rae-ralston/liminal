using UnityEngine;
using UnityEngine.InputSystem;

public class PauseMenu : MonoBehaviour
{
  [SerializeField] private GameObject menuPanel;
  [SerializeField] private GameObject controlsPanel;
  [SerializeField] private PlayerMovement playerMovement;
  private bool isPaused = false;

  private void Start()
  {
    menuPanel.SetActive(false);
    controlsPanel.SetActive(false);
  }

  private void Update()
  {
    if (Keyboard.current.escapeKey.wasPressedThisFrame)
      TogglePause();
  }

  private void TogglePause()
  {
    isPaused = !isPaused;

    if (isPaused)
    {
      playerMovement.enabled = false;
      menuPanel.SetActive(true);
      controlsPanel.SetActive(false);
      Time.timeScale = 0f;
    }
    else
    {
      playerMovement.enabled = true;
      menuPanel.SetActive(false);
      controlsPanel.SetActive(false);
      Time.timeScale = 1f;
    }
  }

  public void OnResumePressed()
  {
    TogglePause();
  }

  public void OnControlsPressed()
  {
    menuPanel.SetActive(false);
    controlsPanel.SetActive(true);
  }

  public void OnBackPressed()
  {
    controlsPanel.SetActive(false);
    menuPanel.SetActive(true);
  }

  public void OnQuitPressed()
  {
    Application.Quit();
  }
}
