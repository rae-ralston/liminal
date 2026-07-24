using UnityEngine;

// Central used/unused body tint for spendable buttons (2026-07-24). Reads the
// two colours off Incremental (UnusedButtonColor / UsedButtonColor) so every
// button re-tints from one place, and shows:
//   unused  the button's one-shot has not fired yet
//   used    Incremental.IsConsumed(propId) - the one-shot has been consumed
//
// IsConsumed is the SINGLE truth: every spendable effect (FlatClickReward,
// CapacityUpgrade, MultiplierUpgrade, LightFedCharge) goes through the one-shot
// gate, so this needs no per-effect knowledge. The clicker's ClickSource never
// consumes, so a clicker just stays 'unused' forever - correct by construction.
//
// Owns the RESTING colour only, and runs EARLY (before InteractableHighlight /
// LightFedCharge) so it can hand them the resting colour as their base; their
// proximity glow / charge tint then layer ON TOP instead of fighting it:
//   - InteractableHighlight.SetBaseColor -> hover eases to a lighter tint of it
//   - LightFedCharge.SetBaseColor       -> charge eases from it toward charged
// A button carrying neither simply keeps the resting colour this writes.
//
// Props with no spend/Incremental role don't carry this component and keep
// their authored baseColor exactly as before.
//
// Dumb polling view; never writes game state.
[DefaultExecutionOrder(-50)]
public class ButtonStateTint : MonoBehaviour
{
    [Tooltip("Body sprite to tint. Empty = the SpriteRenderer on this GameObject.")]
    [SerializeField] SpriteRenderer body;

    Prop prop;
    InteractableHighlight highlight;
    LightFedCharge lightCharge;

    void Awake()
    {
        prop = GetComponent<Prop>();
        highlight = GetComponent<InteractableHighlight>();
        lightCharge = GetComponent<LightFedCharge>();
        if (body == null)
        {
            body = GetComponent<SpriteRenderer>();
        }
    }

    void Update()
    {
        Incremental incremental = Incremental.Instance;
        if (incremental == null)
        {
            return;
        }

        bool used = prop != null
            && !string.IsNullOrEmpty(prop.PropId)
            && incremental.IsConsumed(prop.PropId);

        Color color = used ? incremental.UsedButtonColor : incremental.UnusedButtonColor;

        if (body != null)
        {
            body.color = color;
        }

        // Hand the resting colour to the layered visuals so their eases start
        // from it. Null-safe: a button may have one, both, or neither.
        if (highlight != null) highlight.SetBaseColor(color);
        if (lightCharge != null) lightCharge.SetBaseColor(color);
    }
}
