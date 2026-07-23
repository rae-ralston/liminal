using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// The ending (Ending brief E6/E6b/E6c/E7/E8). Lives in PersistentScene next to
// the other manager singletons; like them, deliberately no DontDestroyOnLoad.
//
// Drives ONE coroutine from the End Button's terminal press to the end card.
// The budget is ~13s and it must not creep - every beat is serialized so it
// can be tuned without touching this file.
//
// Ordering is the whole point of the ending: THE ROOM DIES BEFORE THE PLAYER.
// The hall dissolves while the player's own light holds at an ember, so for
// the void hold there is a lit figure standing in nothing. Killing the ember
// early - or letting the hall outlive it - loses the image the ending exists
// for. If you change the timings, keep that ordering.
//
// Nothing here fades sprites. The dissolve kills LIGHT (E6c): with the room
// unlit and the camera clearing to black, that black IS the void.
public class EndSequenceController : MonoBehaviour
{
  public static EndSequenceController Instance { get; private set; }

  [Header("Beats (seconds) - total ~13s, do not let this creep")]
  [Tooltip("Silence after the End Button press, before the discharge starts.")]
  [SerializeField] float pressDelay = 2f;
  [Tooltip("The drain itself. The zoom and the dissolve run alongside it.")]
  [SerializeField] float drainDuration = 6f;
  [Tooltip("Player alone in the dark, still controllable, before the ember dies.")]
  [SerializeField] float voidHold = 3f;
  [Tooltip("Full black after the ember dies, before the end card.")]
  [SerializeField] float blackHold = 2f;
  [Tooltip("End card fade-in.")]
  [SerializeField] float cardFadeIn = 1f;

  [Header("E6b - camera zoom-out")]
  [Tooltip("The vCam in PersistentScene. Unassigned = zoom skipped (logged); the ending still runs.")]
  [SerializeField] CinemachineCamera vCam;
  [Tooltip("Ortho size to reach by the end of the drain.")]
  [SerializeField] float endZoomOrthoSize = 14f;
  [Tooltip("Zoom length. Roughly drainDuration so the zoom lands as the last light dies.")]
  [SerializeField] float zoomDuration = 6f;
  [Tooltip("Ease-out. DELIBERATELY not shared with the discharge curve: the drain is linear, the zoom is not.")]
  [SerializeField] AnimationCurve zoomCurve = new AnimationCurve(
    new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 0f, 0f));
  [Tooltip("Confiner on the vCam, if any. It clamps ortho size to its bounds - i.e. it caps the zoom-out - so it is disabled for the sequence and restored on reload.")]
  [SerializeField] CinemachineConfiner2D confiner;
  [Tooltip("PixelPerfectCamera on the Brain camera, if any. It snaps to integer upscales and will judder or override a continuous ortho lerp, so it is disabled for the sequence.")]
  [SerializeField] PixelPerfectCamera pixelPerfect;

  [Header("E6c - the dissolve")]
  [Tooltip("The global/ambient Light2D, if one exists. Serialized rather than hunted for - a scene search would also catch it as a room light and double-lerp it.")]
  [SerializeField] Light2D globalLight;
  [Tooltip("Fraction of the drain over which room lights die. Below 1 so the last of the bank empties into an already-dark hall.")]
  [Range(0.1f, 1f)]
  [SerializeField] float dissolveLeadFraction = 0.85f;

  [Tooltip("The player's FLASHLIGHT (not the ember). It lives on the Player in PersistentScene, so the scene-scoped dissolve cannot see it - it has to be handed over explicitly or it burns through the ending. Dies on the dissolve curve, WITH the hall: the flashlight is equipment, part of the building's kit. The ember below is the player's own presence and outlives everything.")]
  [SerializeField] Light2D flashlight;

  [Header("E7 - the ember (drives the Player Light2D directly)")]
  [Tooltip("The Player's Light2D. Unassigned = resolved from the Player tag at Begin().")]
  [SerializeField] Light2D playerLight;
  [Tooltip("Radius the ViR falls to and HOLDS at through the dissolve and the void hold. Tuned against the ending's zoomed-out camera, NOT the same as the pre-start ember.")]
  [SerializeField] float emberRadius = 2.5f;
  [Tooltip("Intensity the ViR falls to and holds at.")]
  [SerializeField] float emberIntensity = 0.6f;
  [Tooltip("How long the fall from full to ember takes (runs alongside the drain).")]
  [SerializeField] float emberFallDuration = 4f;
  [Tooltip("The final extinguish, after the void hold. This beat is PUSHED by this controller, never polled - its timing belongs to the sequence.")]
  [SerializeField] float extinguishDuration = 1f;

  [Header("E8 - blackout & end card")]
  [Tooltip("Full-screen black Image's CanvasGroup in PersistentScene. Safety net for the final blackout only - the dissolve does the real work.")]
  [SerializeField] CanvasGroup screenFade;
  [Tooltip("End card root, disabled at start.")]
  [SerializeField] GameObject endCard;
  [Tooltip("End card CanvasGroup, faded in over cardFadeIn.")]
  [SerializeField] CanvasGroup endCardGroup;
  [Tooltip("Boot scene name, loaded single-mode by the Again button. Application.Quit is a no-op in WebGL, so Again is the only exit.")]
  [SerializeField] string bootSceneName = "PersistentScene";

  // Camera rig state captured at Begin() so the Again path can put it back.
  // A second run starting zoomed out (or unconfined) is a silent, ugly bug.
  float restoreOrthoSize;
  bool restoreConfinerEnabled;
  bool restorePixelPerfectEnabled;
  bool rigCaptured;

  void Awake()
  {
    if (Instance != null && Instance != this)
    {
      Debug.LogWarning("[Ending] Duplicate EndSequenceController destroyed.", this);
      Destroy(gameObject);
      return;
    }

    Instance = this;
  }

  void OnDestroy()
  {
    if (Instance == this)
    {
      Instance = null;
    }
  }

  // Called by SummonStep on the End Button's terminal press. Idempotent -
  // refuses if the sequence is already running.
  public void Begin()
  {
    GameManager gm = GameManager.Instance;
    if (gm == null)
    {
      Debug.LogError("[Ending] Begin refused - no GameManager instance.", this);
      return;
    }

    if (gm.EndSequenceRunning)
    {
      Debug.Log("[Ending] Begin ignored - end sequence already running.", this);
      return;
    }

    gm.BeginEndSequence();
    StartCoroutine(RunSequence());
  }

  IEnumerator RunSequence()
  {
    Debug.Log("[Ending] End sequence begun.");
    Debug.Log("[Ending] WOULD: End Button press cue, then duck the ambience for the silence.");

    ResolvePlayerLight();
    CaptureAndFreeCameraRig();

    // 1. Silence.
    yield return new WaitForSeconds(pressDelay);

    // 2. The discharge, the zoom, the dissolve and the ember fall all start
    //    together. Everything derived from Count follows for free.
    if (Incremental.Instance != null)
    {
      Incremental.Instance.BeginFinalDischarge(drainDuration);
    }
    else
    {
      Debug.LogError("[Ending] No Incremental instance - the drain cannot run.", this);
    }

    Debug.Log("[Ending] WOULD: relay-unlatch cascade, room hums dying, tick slowing to nothing.");

    StartCoroutine(ZoomOut());
    StartCoroutine(DissolveRoomLights());
    StartCoroutine(FallToEmber());

    yield return new WaitForSeconds(drainDuration);

    // 3. The void. Player is lit, alone, and still controllable - movement is
    //    never disabled, only interaction.
    Debug.Log("[Ending] Void hold - the hall is gone, the player is not.");
    yield return new WaitForSeconds(voidHold);

    // 4. Only now does the ember die (E7 beat two, pushed not polled).
    yield return BeginFinalExtinguish();

    // 5. Black settles, holds, then the card.
    yield return FadeScreen(1f, 0.5f);
    yield return new WaitForSeconds(blackHold);
    yield return ShowEndCard();

    Debug.Log("[Ending] End sequence complete.");
  }

  // ------------------------------------------------------------------
  // E6b - zoom
  // ------------------------------------------------------------------

  void CaptureAndFreeCameraRig()
  {
    rigCaptured = true;

    if (confiner != null)
    {
      restoreConfinerEnabled = confiner.enabled;
      confiner.enabled = false;
    }

    if (pixelPerfect != null)
    {
      // Accepts non-integer scaling for the ~13s of the ending. The
      // alternative - stepping the zoom at integer factors - is chunky, and
      // silently lerping against a live PixelPerfectCamera is not an option:
      // it judders in visible steps or gets overridden outright.
      restorePixelPerfectEnabled = pixelPerfect.enabled;
      pixelPerfect.enabled = false;
    }

    if (vCam != null)
    {
      restoreOrthoSize = vCam.Lens.OrthographicSize;
    }
  }

  IEnumerator ZoomOut()
  {
    if (vCam == null)
    {
      Debug.Log("[Ending] WOULD: zoom the camera out (no CinemachineCamera wired) - the sequence continues unzoomed.");
      yield break;
    }

    float startSize = vCam.Lens.OrthographicSize;
    float elapsed = 0f;

    while (elapsed < zoomDuration)
    {
      elapsed += Time.deltaTime;
      float t = zoomCurve.Evaluate(Mathf.Clamp01(elapsed / zoomDuration));
      // Follow stays on the player throughout - the camera keeps tracking,
      // control is never taken away.
      vCam.Lens.OrthographicSize = Mathf.Lerp(startSize, endZoomOrthoSize, t);
      yield return null;
    }

    vCam.Lens.OrthographicSize = endZoomOrthoSize;
  }

  // ------------------------------------------------------------------
  // E6c - the dissolve (light, not alpha)
  // ------------------------------------------------------------------

  IEnumerator DissolveRoomLights()
  {
    List<Light2D> lights = CollectRoomLights();
    if (lights.Count == 0)
    {
      Debug.LogWarning("[Ending] Dissolve found no room lights - the hall will not go dark.", this);
      yield break;
    }

    float[] startIntensities = new float[lights.Count];
    for (int i = 0; i < lights.Count; i++)
    {
      startIntensities[i] = lights[i].intensity;
    }

    float duration = Mathf.Max(0.01f, drainDuration * dissolveLeadFraction);
    float elapsed = 0f;

    while (elapsed < duration)
    {
      elapsed += Time.deltaTime;
      float t = Mathf.Clamp01(elapsed / duration);
      for (int i = 0; i < lights.Count; i++)
      {
        if (lights[i] != null)
        {
          lights[i].intensity = Mathf.Lerp(startIntensities[i], 0f, t);
        }
      }

      yield return null;
    }

    for (int i = 0; i < lights.Count; i++)
    {
      if (lights[i] != null)
      {
        lights[i].intensity = 0f;
      }
    }

    Debug.Log($"[Ending] Dissolve complete - {lights.Count} lights out.");
  }

  // Scene-scoped: the room scene is the ACTIVE scene (RoomTransitionManager
  // sets it on load), and the Player - with its Light2D - lives in
  // PersistentScene, so this excludes the player's light structurally rather
  // than by filtering. The explicit player check below is a cheap defensive
  // guard, NOT the mechanism.
  List<Light2D> CollectRoomLights()
  {
    List<Light2D> lights = new List<Light2D>();
    Scene roomScene = SceneManager.GetActiveScene();

    foreach (GameObject root in roomScene.GetRootGameObjects())
    {
      foreach (Light2D light in root.GetComponentsInChildren<Light2D>(true))
      {
        if (light == playerLight)
        {
          continue;
        }

        lights.Add(light);
      }
    }

    if (globalLight != null && !lights.Contains(globalLight))
    {
      lights.Add(globalLight);
    }

    // The flashlight joins the ROOM lights deliberately - it is the one thing
    // on the player that dies with the hall rather than outliving it.
    if (flashlight != null && flashlight != playerLight && !lights.Contains(flashlight))
    {
      lights.Add(flashlight);
    }

    return lights;
  }

  // ------------------------------------------------------------------
  // E7 - the ember, in two beats
  // ------------------------------------------------------------------

  void ResolvePlayerLight()
  {
    if (playerLight != null)
    {
      return;
    }

    GameObject player = GameObject.FindGameObjectWithTag("Player");
    if (player != null)
    {
      playerLight = player.GetComponentInChildren<Light2D>(true);
    }

    if (playerLight == null)
    {
      Debug.LogWarning("[Ending] WOULD: hold the player at an ember (no Light2D found on the Player) - the void beat will read as an empty black screen.", this);
    }
  }

  // Beat one: fall to the ember and HOLD. Must not reach zero while the hall
  // is dissolving or the player vanishes with the room.
  IEnumerator FallToEmber()
  {
    if (playerLight == null)
    {
      yield break;
    }

    float startRadius = playerLight.pointLightOuterRadius;
    float startIntensity = playerLight.intensity;
    float elapsed = 0f;

    while (elapsed < emberFallDuration)
    {
      elapsed += Time.deltaTime;
      float t = Mathf.Clamp01(elapsed / emberFallDuration);
      playerLight.pointLightOuterRadius = Mathf.Lerp(startRadius, emberRadius, t);
      playerLight.intensity = Mathf.Lerp(startIntensity, emberIntensity, t);
      yield return null;
    }

    playerLight.pointLightOuterRadius = emberRadius;
    playerLight.intensity = emberIntensity;
  }

  // Beat two: the explicit kill, pushed by the sequence after the void hold.
  // Public so a future ViRController can take this over unchanged.
  public IEnumerator BeginFinalExtinguish()
  {
    if (playerLight == null)
    {
      yield break;
    }

    Debug.Log("[Ending] Ember out.");

    float startRadius = playerLight.pointLightOuterRadius;
    float startIntensity = playerLight.intensity;
    float elapsed = 0f;

    while (elapsed < extinguishDuration)
    {
      elapsed += Time.deltaTime;
      float t = Mathf.Clamp01(elapsed / extinguishDuration);
      playerLight.pointLightOuterRadius = Mathf.Lerp(startRadius, 0f, t);
      playerLight.intensity = Mathf.Lerp(startIntensity, 0f, t);
      yield return null;
    }

    playerLight.intensity = 0f;
  }

  // ------------------------------------------------------------------
  // E8 - blackout & card
  // ------------------------------------------------------------------

  IEnumerator FadeScreen(float target, float duration)
  {
    if (screenFade == null)
    {
      yield break;
    }

    float start = screenFade.alpha;
    float elapsed = 0f;

    while (elapsed < duration)
    {
      elapsed += Time.deltaTime;
      screenFade.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
      yield return null;
    }

    screenFade.alpha = target;
  }

  IEnumerator ShowEndCard()
  {
    if (endCard == null || endCardGroup == null)
    {
      Debug.Log("[Ending] WOULD: show the end card (not wired yet).", this);
      yield break;
    }

    endCard.SetActive(true);
    endCardGroup.alpha = 0f;
    endCardGroup.blocksRaycasts = true;

    // Populate AFTER enabling (the fields have to exist) and BEFORE the fade,
    // so no frame shows placeholder text.
    EndCard card = endCard.GetComponent<EndCard>();
    if (card != null)
    {
      card.Populate();
    }
    else
    {
      Debug.LogWarning("[Ending] End card has no EndCard component - showing whatever text was authored.", this);
    }

    float elapsed = 0f;
    while (elapsed < cardFadeIn)
    {
      elapsed += Time.deltaTime;
      endCardGroup.alpha = Mathf.Clamp01(elapsed / cardFadeIn);
      yield return null;
    }

    endCardGroup.alpha = 1f;
  }

  // Wired to the end card's "Again" button. Restores the camera rig BEFORE
  // reloading: PersistentScene's objects survive a single-mode load of the
  // boot scene only if the boot scene IS PersistentScene, and a second run
  // starting zoomed out with the confiner off is exactly the silent bug the
  // brief warns about. Restoring first is correct either way.
  public void Again()
  {
    RestoreCameraRig();
    SceneManager.LoadScene(bootSceneName);
  }

  public void RestoreCameraRig()
  {
    if (!rigCaptured)
    {
      return;
    }

    if (vCam != null)
    {
      vCam.Lens.OrthographicSize = restoreOrthoSize;
    }

    if (confiner != null)
    {
      confiner.enabled = restoreConfinerEnabled;
    }

    if (pixelPerfect != null)
    {
      pixelPerfect.enabled = restorePixelPerfectEnabled;
    }
  }
}
