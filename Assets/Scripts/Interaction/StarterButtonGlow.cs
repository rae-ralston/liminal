using UnityEngine;
using UnityEngine.Rendering.Universal; // Light2D

// Player guidance + the "clock comes into existence" reveal (2026-07-24).
//
// The start button (currently dressed as a wall clock, fittingly - it ticks)
// stays HIDDEN until the player activates the bootstrap room at its terminal;
// then it appears, breathes a glow, and emits a looping tick to draw the player
// to the one interaction that starts the game. Glow and tick stop the instant
// it's pressed (Incremental.Running flips true); the button stays visible. The
// sprite is incidental - this code never assumes it's a clock.
//
// The trigger is Incremental.BootstrapActivated (reveal) and && !Running
// (glow/tick). NOT "MaxCapacity > 0": SeedBootstrapResidue() raises MaxCapacity
// at Start, before any interaction.
//
// Owns two things a plain glow view wouldn't, both gated on BootstrapActivated:
//   - REVEAL: the button body sprite is hidden until the room is activated.
//   - HIGHLIGHT GATE: InteractableHighlight is disabled until then, so walking
//     up to the hidden button doesn't reveal it early (it writes the body colour
//     unconditionally otherwise).
// The GLOW itself is a dedicated Light2D (and/or an optional glow sprite),
// never the button body - InteractableHighlight owns that colour and the two
// would fight. Dumb polling view; never writes game state.
public class StarterButtonGlow : MonoBehaviour
{
    [Header("Reveal - hidden until the bootstrap room is activated")]
    [Tooltip("The starter button's body sprite (whatever it is - currently a wall clock). Hidden (renderer disabled) until BootstrapActivated. Empty = the SpriteRenderer on this GameObject.")]
    [SerializeField] SpriteRenderer buttonBody;
    [Tooltip("The proximity highlight to suppress until BootstrapActivated, so approaching doesn't reveal the hidden clock. Empty = the InteractableHighlight on this GameObject.")]
    [SerializeField] InteractableHighlight highlight;

    [Header("Glow - breathes while waiting to be pressed")]
    [Tooltip("Light2D pulsed on while waiting (BootstrapActivated and not yet started). The clean way to glow without fighting InteractableHighlight over the body sprite.")]
    [SerializeField] Light2D glowLight;
    [Tooltip("Optional dedicated glow sprite to pulse - must NOT be the button body.")]
    [SerializeField] SpriteRenderer glowSprite;
    [SerializeField] Color glowColor = new Color(1f, 0.82f, 0.3f);

    [Header("Breathing")]
    [SerializeField] float pulseSpeed = 2f;
    [Tooltip("Alpha/intensity range of the breath (min..max).")]
    [SerializeField] float pulseMin = 0.35f;
    [SerializeField] float pulseMax = 1f;

    [Header("Sound")]
    [Tooltip("Tick loop source. Empty = the PropAudio on this GameObject. Put the tick in its definition's ambientLoop slot and turn OFF PropAudio.autoStartAmbientLoop so it doesn't play before the reveal.")]
    [SerializeField] PropAudio propAudio;

    float lightBaseIntensity = 1f;
    bool waiting;

    void Awake()
    {
        if (propAudio == null) propAudio = GetComponent<PropAudio>();
        if (highlight == null) highlight = GetComponent<InteractableHighlight>();
        if (buttonBody == null) buttonBody = GetComponent<SpriteRenderer>();

        // The body is reveal-controlled, not a glow target - guard against a
        // mis-wire that would make ApplyGlow and the reveal fight one sprite.
        if (glowSprite != null && glowSprite == buttonBody)
        {
            Debug.LogWarning($"[Starter] StarterButtonGlow '{name}': glowSprite is the button body - ignoring it. Use a dedicated glow sprite or a Light2D.", this);
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
        bool live = revealed && !incremental.Running;

        SetRevealed(revealed);

        if (live != waiting)
        {
            waiting = live;
            if (live)
            {
                if (propAudio != null) propAudio.StartAmbientLoop();
            }
            else
            {
                if (propAudio != null) propAudio.StopAmbientLoop();
                ApplyGlowOff();
            }
        }

        if (!live)
        {
            return;
        }

        float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed);
        float pulse = Mathf.Lerp(pulseMin, pulseMax, wave);
        ApplyGlow(pulse);
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

    void ApplyGlow(float pulse)
    {
        if (glowSprite != null)
        {
            Color c = glowColor;
            c.a = glowColor.a * pulse;
            glowSprite.color = c;
        }

        if (glowLight != null)
        {
            if (!glowLight.enabled)
            {
                glowLight.enabled = true;
            }

            glowLight.color = glowColor;
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
