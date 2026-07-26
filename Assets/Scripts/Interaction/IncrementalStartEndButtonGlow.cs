using UnityEngine;
using UnityEngine.Rendering.Universal; // Light2D

// Player guidance for the clock - the prop that is BOTH the start button and
// the end lever (merged 2026-07-26; it carries IncrementalStarter and
// EndButtonSummoner side by side, and PropInteraction applies both).
// Formerly StarterButtonGlow, which only knew the first of the two phases -
// hence the mouthful of a name: it is the glow for BOTH the incremental's
// start button and the end button, which are now one object.
//
// The clock stays HIDDEN until the player activates the bootstrap room at its
// terminal; then it appears, breathes a glow and emits a looping tick to draw
// the player to the one interaction that starts the game. That is phase START.
// It goes quiet the instant it's pressed (Incremental.Running flips true) and
// stays visible.
//
// Phase END lights it a second time once the end condition has been reached
// (Incremental.EndConditionLatched) and the chain is not yet authorised
// (GameManager.Stage still None). Same treatment, different colour and speed:
// the player already learned this object rings and knows where it is, so the
// glow only has to confirm what the building-wide one-shot (EndAnnouncer) told
// them. The LATCHED flag is deliberate - raw EndConditionMet drops back to
// false when a purchase lowers the fill ratio, which would strobe the glow.
//
// The sprite is incidental - this code never assumes it's a clock.
//
// Owns two things a plain glow view wouldn't, both gated on BootstrapActivated:
//   - REVEAL: the button body sprite is hidden until the room is activated.
//   - HIGHLIGHT GATE: InteractableHighlight is disabled until then, so walking
//     up to the hidden button doesn't reveal it early (it writes the body colour
//     unconditionally otherwise).
// The GLOW itself is a dedicated Light2D (and/or an optional glow sprite),
// never the button body - InteractableHighlight owns that colour and the two
// would fight. Dumb polling view; never writes game state.
public class IncrementalStartEndButtonGlow : MonoBehaviour
{
    enum Phase { None, Start, End }

    [Header("Reveal - hidden until the bootstrap room is activated")]
    [Tooltip("The starter button's body sprite (whatever it is - currently a wall clock). Hidden (renderer disabled) until BootstrapActivated. Empty = the SpriteRenderer on this GameObject.")]
    [SerializeField] SpriteRenderer buttonBody;
    [Tooltip("The proximity highlight to suppress until BootstrapActivated, so approaching doesn't reveal the hidden clock. Empty = the InteractableHighlight on this GameObject.")]
    [SerializeField] InteractableHighlight highlight;

    [Header("Glow - breathes while waiting to be pressed")]
    [Tooltip("Light2D pulsed on while waiting (either phase). The clean way to glow without fighting InteractableHighlight over the body sprite.")]
    [SerializeField] Light2D glowLight;
    [Tooltip("Optional dedicated glow sprite to pulse - must NOT be the button body.")]
    [SerializeField] SpriteRenderer glowSprite;
    [Tooltip("Phase START: waiting for the press that starts the incremental.")]
    [SerializeField] Color glowColor = new Color(1f, 0.82f, 0.3f);
    [Tooltip("Phase END: the end condition is reached and the chain is unauthorised - waiting for the press that calls it. A different colour so the second summons doesn't read as a repeat of the first.")]
    [SerializeField] Color endGlowColor = new Color(1f, 0.25f, 0.2f);

    [Header("Breathing")]
    [SerializeField] float pulseSpeed = 2f;
    [Tooltip("Breathing speed in phase END. Faster than the start pulse reads as urgency.")]
    [SerializeField] float endPulseSpeed = 3.5f;
    [Tooltip("Alpha/intensity range of the breath (min..max).")]
    [SerializeField] float pulseMin = 0.35f;
    [SerializeField] float pulseMax = 1f;

    [Header("Sound")]
    [Tooltip("Tick loop source. Empty = the PropAudio on this GameObject. Put the tick in its definition's ambientLoop slot and turn OFF PropAudio.autoStartAmbientLoop so it doesn't play before the reveal.")]
    [SerializeField] PropAudio propAudio;
    [Tooltip("Also run the tick loop during phase END, so the clock is audible again in the room once the ending is available. The building-wide cue is EndAnnouncer's one-shot; this is only the local layer.")]
    [SerializeField] bool tickInEndPhase = true;

    float lightBaseIntensity = 1f;
    Phase phase = Phase.None;

    void Awake()
    {
        if (propAudio == null) propAudio = GetComponent<PropAudio>();
        if (highlight == null) highlight = GetComponent<InteractableHighlight>();
        if (buttonBody == null) buttonBody = GetComponent<SpriteRenderer>();

        // The body is reveal-controlled, not a glow target - guard against a
        // mis-wire that would make ApplyGlow and the reveal fight one sprite.
        if (glowSprite != null && glowSprite == buttonBody)
        {
            Debug.LogWarning($"[Starter] IncrementalStartEndButtonGlow '{name}': glowSprite is the button body - ignoring it. Use a dedicated glow sprite or a Light2D.", this);
            glowSprite = null;
        }

        if (glowLight != null)
        {
            lightBaseIntensity = glowLight.intensity;
        }
    }

    void Start()
    {
        SetRevealed(false);
        ApplyGlowOff();
    }

    void Update()
    {
        Incremental incremental = Incremental.Instance;
        bool revealed = incremental != null && incremental.BootstrapActivated;

        SetRevealed(revealed);

        Phase current = ResolvePhase(incremental, revealed);
        if (current != phase)
        {
            EnterPhase(current);
        }

        if (phase == Phase.None)
        {
            return;
        }

        float speed = phase == Phase.End ? endPulseSpeed : pulseSpeed;
        float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * speed);
        ApplyGlow(Mathf.Lerp(pulseMin, pulseMax, wave), phase == Phase.End ? endGlowColor : glowColor);
    }

    // The two waiting states, in chain order. Everything else - not revealed
    // yet, running with the end condition unmet, chain already authorised -
    // is None.
    Phase ResolvePhase(Incremental incremental, bool revealed)
    {
        if (!revealed)
        {
            return Phase.None;
        }

        if (!incremental.Running)
        {
            return Phase.Start;
        }

        // A missing GameManager means the chain can't be read at all - stay
        // dark rather than glow at a press that would go nowhere.
        GameManager gameManager = GameManager.Instance;
        bool unauthorised = gameManager != null && gameManager.Stage < GameManager.EndStage.Called;

        return incremental.EndConditionLatched && unauthorised ? Phase.End : Phase.None;
    }

    void EnterPhase(Phase next)
    {
        phase = next;

        if (propAudio != null)
        {
            bool tick = next == Phase.Start || (next == Phase.End && tickInEndPhase);
            if (tick) propAudio.StartAmbientLoop();
            else propAudio.StopAmbientLoop();
        }

        if (next == Phase.None)
        {
            ApplyGlowOff();
        }
    }

    void SetRevealed(bool revealed)
    {
        if (buttonBody != null && buttonBody.enabled != revealed)
        {
            buttonBody.enabled = revealed;
        }

        if (highlight != null && highlight.enabled != revealed)
        {
            highlight.enabled = revealed;
        }
    }

    void ApplyGlow(float pulse, Color color)
    {
        if (glowSprite != null)
        {
            Color c = color;
            c.a = color.a * pulse;
            glowSprite.color = c;
        }

        if (glowLight != null)
        {
            if (!glowLight.enabled)
            {
                glowLight.enabled = true;
            }

            glowLight.color = color;
            glowLight.intensity = lightBaseIntensity * pulse;
        }
    }

    void ApplyGlowOff()
    {
        if (glowSprite != null)
        {
            Color c = glowColor;
            c.a = 0f;
            glowSprite.color = c;
        }

        if (glowLight != null)
        {
            glowLight.enabled = false;
        }
    }
}
