using UnityEngine;
using UnityEngine.Rendering.Universal;

// View-only proximity highlight for interactable props: while the player is
// inside the prop's trigger, the sprite pulses toward a highlight color (and
// an optional Light2D fades in). Purely cosmetic - interaction itself stays
// in InteractableTrigger/PropInteraction, which this never touches.
//
// Self-contained on purpose: Unity delivers OnTriggerEnter2D/Exit2D to every
// component on the GameObject, so this listens to the SAME trigger collider
// PropInteraction uses without any hook in the interaction code. Drop it on
// any prop that has a trigger collider and a SpriteRenderer; both extras
// (highlight light) are optional.
public class InteractableHighlight : MonoBehaviour
{
  [SerializeField] private Color highlightColor = Color.white;
  [Tooltip("How far the sprite tints toward the highlight color at the pulse peak (0-1).")]
  [SerializeField] private float pulseStrength = 0.6f;
  [SerializeField] private float pulseSpeed = 4f;
  [Tooltip("Seconds to fade the highlight fully in/out on enter/exit.")]
  [SerializeField] private float fadeTime = 0.15f;
  [Tooltip("Optional Light2D faded in while highlighted (e.g. a small point light on the prop). Leave empty for sprite tint only.")]
  [SerializeField] private Light2D highlightLight;

  private SpriteRenderer spriteRenderer;
  private Color baseColor;
  private float lightBaseIntensity;
  private float glow;          // 0 = off, 1 = fully highlighted (eased)
  private bool playerInRange;

  private void Awake()
  {
    spriteRenderer = GetComponent<SpriteRenderer>();
    if (spriteRenderer != null)
      baseColor = spriteRenderer.color;

    if (highlightLight != null)
    {
      lightBaseIntensity = highlightLight.intensity;
      highlightLight.enabled = false;
    }
  }

  private void OnTriggerEnter2D(Collider2D other)
  {
    if (other.CompareTag("Player"))
      playerInRange = true;
  }

  private void OnTriggerExit2D(Collider2D other)
  {
    if (other.CompareTag("Player"))
      playerInRange = false;
  }

  private void Update()
  {
    float target = playerInRange ? 1f : 0f;
    if (glow == target && glow == 0f)
      return; // idle - nothing to animate, nothing to restore

    glow = fadeTime > 0f
      ? Mathf.MoveTowards(glow, target, Time.deltaTime / fadeTime)
      : target;

    // Gentle 0..1 wave; freezes with timeScale, which pause wants anyway.
    float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed);
    float amount = glow * pulseStrength * Mathf.Lerp(0.6f, 1f, wave);

    if (spriteRenderer != null)
      spriteRenderer.color = Color.Lerp(baseColor, highlightColor, amount);

    if (highlightLight != null)
    {
      highlightLight.enabled = glow > 0f;
      highlightLight.intensity = lightBaseIntensity * glow * Mathf.Lerp(0.6f, 1f, wave);
    }
  }

  private void OnDisable()
  {
    // Leave the prop exactly as we found it (room unloads, consumed-state
    // visuals later, etc. must never inherit a half-faded tint).
    glow = 0f;
    playerInRange = false;
    if (spriteRenderer != null)
      spriteRenderer.color = baseColor;
    if (highlightLight != null)
    {
      highlightLight.intensity = lightBaseIntensity;
      highlightLight.enabled = false;
    }
  }
}
