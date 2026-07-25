using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

// Boot-time click gate. Holds a black "Click" screen until the player gives the
// first input gesture, then reveals the game. Two reasons it exists:
//   1. WebGL browsers suspend the audio context until a user gesture - starting
//      FMOD ambience before the first click plays it silently. The click is what
//      lets audio begin, so AudioManager.StartAmbience() is called from HERE
//      (inside the gesture), not from AudioManager.Start().
//   2. It gives the game a deliberate cold open instead of dropping the player
//      straight in.
// Lives on its own top-most Canvas in PersistentScene, above every other UI.
public class SplashScreen : MonoBehaviour
{
  [SerializeField] private CanvasGroup canvasGroup;
  [SerializeField] private float fadeDuration = 0.6f;

  [Header("Prompt pulse (optional)")]
  [SerializeField] private Graphic prompt;      // the "Click" label
  [SerializeField] private float pulseSpeed = 2f;
  [SerializeField] private float pulseMinAlpha = 0.35f;

  private bool dismissed;

  // Freeze movement in Start, not Awake: by Start() every Awake has run, so
  // GameManager.Instance is guaranteed set regardless of script execution order.
  // There is no run in progress at boot, so borrowing the ending's freeze flag
  // is safe - it is cleared the instant we dismiss.
  private void Start()
  {
    if (canvasGroup != null)
    {
      canvasGroup.alpha = 1f;
      canvasGroup.blocksRaycasts = true;
    }

    if (GameManager.Instance != null)
      GameManager.Instance.MovementFrozen = true;
  }

  private void Update()
  {
    if (dismissed) return;

    if (prompt != null)
    {
      float t = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed);
      Color c = prompt.color;
      c.a = Mathf.Lerp(pulseMinAlpha, 1f, t);
      prompt.color = c;
    }

    if (FirstGesture())
      Dismiss();
  }

  // Any mouse / touch / key press. Low-level current-device reads so no
  // InputAction asset entry is needed; matches the project's new-Input-System use.
  private static bool FirstGesture()
  {
    Mouse mouse = Mouse.current;
    if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;

    Touchscreen touch = Touchscreen.current;
    if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) return true;

    Keyboard kb = Keyboard.current;
    if (kb != null && kb.anyKey.wasPressedThisFrame) return true;

    return false;
  }

  private void Dismiss()
  {
    dismissed = true;
    if (canvasGroup != null)
      canvasGroup.blocksRaycasts = false;

    // FIRST FMOD playback must happen inside this user gesture (WebGL autoplay).
    if (AudioManager.Instance != null)
      AudioManager.Instance.StartAmbience();

    if (GameManager.Instance != null)
      GameManager.Instance.MovementFrozen = false;

    StartCoroutine(FadeAndDisable());
  }

  // Unscaled time so the fade still runs even if something has zeroed timeScale.
  private IEnumerator FadeAndDisable()
  {
    float from = canvasGroup != null ? canvasGroup.alpha : 1f;
    float elapsed = 0f;
    while (elapsed < fadeDuration)
    {
      elapsed += Time.unscaledDeltaTime;
      if (canvasGroup != null)
        canvasGroup.alpha = Mathf.Lerp(from, 0f, elapsed / fadeDuration);
      yield return null;
    }
    if (canvasGroup != null)
      canvasGroup.alpha = 0f;
    gameObject.SetActive(false);
  }
}
