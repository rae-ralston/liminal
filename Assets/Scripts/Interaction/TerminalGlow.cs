using UnityEngine;
using UnityEngine.Rendering.Universal;

// Terminal analog of DoorIndicatorLight (Terminal_feedback.md, 2026-07-20):
// dumb polling view over Incremental, breathing 3-state glow answering
// "can I act" the way TerminalGauge answers "how full is the bank."
// Simpler 3-state than the keypad's 4-state (LEAN call in the design doc) -
// activation costs are small and always reachable by the door checker's
// construction, so the capacity-vs-charge split earns less here.
//
//   dark red   Locked       not activated, balance < ActivationCost
//   dark pink  Activatable  not activated, balance >= ActivationCost
//   ghost      Activated    room already activated (permanent)
//
// Breathing reuses InteractableHighlight's sin-wave pulse, applied to
// intensity/alpha only - color itself is a flat per-state value, no
// cross-fade (the state IS the message, no pub/sub in this project).
//
// Lives on the terminal prefab (pointed at a dedicated child glow sprite -
// NOT the terminal body, whose color InteractableHighlight already owns).
public class TerminalGlow : MonoBehaviour
{
  [Tooltip("Terminal this glow tracks. Leave empty to use the Terminal on this GameObject or a parent.")]
  [SerializeField] Terminal terminal;
  [Tooltip("Glow sprite to pulse. Leave empty to use the SpriteRenderer on this GameObject (don't share it with InteractableHighlight).")]
  [SerializeField] SpriteRenderer glowSprite;
  [Tooltip("Optional Light2D pulsed along with the sprite.")]
  [SerializeField] Light2D glowLight;

  [SerializeField] Color lockedColor = new Color(0.5f, 0.05f, 0.05f);
  [SerializeField] Color activatableColor = new Color(0.9f, 0.25f, 0.55f);
  [SerializeField] Color activatedColor = new Color(0.35f, 0.35f, 0.4f);
  [Tooltip("Pre-start color (The Circuit C4): a dead building has no lit terminals - flat, no breathing.")]
  [SerializeField] Color offColor = new Color(0.1f, 0.1f, 0.1f);

  [Header("Breathing")]
  [SerializeField] float pulseSpeed = 1.5f;
  [Tooltip("Alpha/intensity range of the breath (min..max).")]
  [SerializeField] float pulseMin = 0.4f;
  [SerializeField] float pulseMax = 1f;

  float lightBaseIntensity;
  bool isDark;

  void Awake()
  {
    if (terminal == null)
    {
      terminal = GetComponentInParent<Terminal>();
    }

    if (terminal == null)
    {
      Debug.LogWarning($"[Circuit] TerminalGlow '{name}' has no Terminal (assigned or in parents) - glow stays dark.", this);
    }

    if (glowSprite == null)
    {
      glowSprite = GetComponent<SpriteRenderer>();
    }

    if (glowLight != null)
    {
      // Left disabled on the prefab/instance on purpose - an active Light2D
      // skews the editor's 2D lighting preview for everything else being
      // edited nearby. Turned on here so it's only ever lit at runtime.
      glowLight.gameObject.SetActive(true);
      lightBaseIntensity = glowLight.intensity;
    }
  }

  void Update()
  {
    if (terminal == null || Incremental.Instance == null)
    {
      return;
    }

    Incremental incremental = Incremental.Instance;
    if (!terminal.IsLive)
    {
      if (!isDark)
      {
        isDark = true;
        Apply(offColor, 1f);
      }
      return;
    }

    isDark = false;

    Color color = StateColor(incremental);
    float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed);
    float pulse = Mathf.Lerp(pulseMin, pulseMax, wave);
    Apply(color, pulse);
  }

  Color StateColor(Incremental incremental)
  {
    if (incremental.IsRoomActivated(terminal.RoomId))
    {
      return activatedColor;
    }

    long cost = terminal.RoomId != null ? terminal.RoomId.ActivationCost : long.MaxValue;
    return incremental.Count >= cost ? activatableColor : lockedColor;
  }

  void Apply(Color color, float pulse)
  {
    if (glowSprite != null)
    {
      Color c = color;
      c.a = color.a * pulse;
      glowSprite.color = c;
    }

    if (glowLight != null)
    {
      glowLight.color = color;
      glowLight.intensity = lightBaseIntensity * pulse;
    }
  }
}
