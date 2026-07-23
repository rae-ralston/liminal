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
  }

  /*
   * FMOD: Start pause-menu audio.
   * Created and started when the pause screen is activated so it does not
   * run nonstop in the background. Stopped and released again on unpause.
   */
  private void StartMenuAudio()
  {
    menuAudio = AudioManager.Instance.CreateEventInstance(FMODMenuEvent);
    menuAudio.start();
  }

  /*
   * FMOD: Stop and release the pause-menu audio instance.
   * ALLOWFADEOUT lets FMOD play out the release tail; release() frees the
   * instance once it has fully stopped so nothing keeps playing/leaking.
   */
  private void StopMenuAudio()
  {
    if (menuAudio.isValid())
    {
      menuAudio.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
      menuAudio.release();
    }
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
      StartMenuAudio();  // start pause music
      ActivateSnapshot(); // call FMOD Snapshot
    }
    else
    {
      playerMovement.enabled = true;
      menuPanel.SetActive(false);
      controlsPanel.SetActive(false);
      Time.timeScale = 1f;
      if (menuSnapshot.isValid())
      {
        menuSnapshot.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        menuSnapshot.release();
      }
      StopMenuAudio(); // stop and release pause music
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

  private void OnDestroy()
  {
    // Release any instance still alive if we're torn down while paused.
    StopMenuAudio();
    if (menuSnapshot.isValid())
    {
      menuSnapshot.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
      menuSnapshot.release();
    }
  }
}
