using UnityEngine;
using UnityEngine.InputSystem;
using FMODUnity;
using FMOD.Studio;

public class PauseMenu : MonoBehaviour
{
  [SerializeField] private GameObject menuPanel;
  [SerializeField] private GameObject controlsPanel;
  [SerializeField] private PlayerMovement playerMovement;
  [SerializeField] private EventReference FMODMenuEvent;
  [SerializeField] private EventReference FMODMenuSnapshot;  
  private EventInstance menuAudio;
  private EventInstance menuSnapshot;
  private bool isPaused = false;

  private void Start()
  {
    menuPanel.SetActive(false);
    controlsPanel.SetActive(false);
    StartMenuAudio();
  }

  /*
   * FMOD: Start Audio playback muted/in background
   * This way the audio does not restart everytime the player presses pause. 
   * If it turns out to be too processing heavy, we can move this call into TogglePause(), 
   * just before calling the snapshot
   */
  private void StartMenuAudio()
  {
    menuAudio = AudioManager.Instance.CreateEventInstance(FMODMenuEvent);
    menuAudio.start();
  }

  /*
   * FMOD: activate audio mixer snapshot
   * laods a snaptshot of the FMOD audio mixer in which the current game audio (atmo, player sounds) is muted 
   * and pause menu sounds/music are unmuted
   */
  private void ActivateSnapshot()
  {
    menuSnapshot = AudioManager.Instance.CreateEventInstance(FMODMenuSnapshot);
    menuSnapshot.start();
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
      ActivateSnapshot(); // call FMOD Snapshot      
    }
    else
    {
      playerMovement.enabled = true;
      menuPanel.SetActive(false);
      controlsPanel.SetActive(false);
      Time.timeScale = 1f;
      if (menuSnapshot.isValid()) menuSnapshot.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
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
