using UnityEngine;
using UnityEngine.Rendering.Universal;

// The charge gate for light-fed props (Phase 5): sustained flashlight
// exposure fills a normalized charge; at full charge every
// IIncrementalEffect on this GameObject fires, mirroring
// PropInteraction.Interact(). A gate IN FRONT of the effect pipeline -
// IIncrementalEffect stays untouched, payout logic is reused not duplicated.
//
// XOR law: a light-charged prop must NOT also carry PropInteraction (that
// would apply the effects directly, bypassing this gate) - the Prop
// Consistency Checker enforces it.
//
// Charge lives in Incremental.Charges keyed by propId, so decay keeps
// running centrally while this room is unloaded. A prop with no propId
// falls back to an instance-local value (warned once): its charge resets on
// room reload, same contract as the consumed-state fallback.
//
// NOTE (Circuit C3): once room activation lands, this gains the same
// "room not powered -> refuse light" check as PropInteraction.
public class LightFedCharge : MonoBehaviour
{
    [Tooltip("Seconds of sustained flashlight exposure to reach full charge.")]
    [SerializeField] float chargeTime = 4f;
    [Tooltip("Normalized charge lost per second while unlit (0.25 = full-to-empty in 4 seconds).")]
    [SerializeField] float decayRate = 0.25f;
    [Tooltip("Sprite tint at full charge, lerped from the base color as charge rises.")]
    [SerializeField] Color chargedColor = Color.white;
    [Tooltip("Fraction of the sprite that must sit inside the beam cone to count as lit (sampled at the bounds center + corners). 0.75 = 4 of 5 sample points - grazing the prop's edge is not enough.")]
    [Range(0.2f, 1f)]
    [SerializeField] float requiredCoverage = 0.75f;

    Prop prop;
    PropAudio propAudio;
    SpriteRenderer spriteRenderer;
    Color baseColor;
    Light2D flashlight;
    float localCharge; // fallback when there is no propId
    bool chargeLoopRunning;

    string PropId => prop != null ? prop.PropId : null;

    void Start()
    {
        prop = GetComponent<Prop>();
        propAudio = GetComponent<PropAudio>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            baseColor = spriteRenderer.color;
        }

        // The flashlight lives under the player in PersistentScene, so it
        // survives room loads - resolve once. No line-of-sight raycast in
        // this first pass: charging through a wall is accepted jank until
        // playtests say otherwise.
        FlashlightAimToMouse aim = FindAnyObjectByType<FlashlightAimToMouse>();
        if (aim != null)
        {
            flashlight = aim.GetComponent<Light2D>();
        }

        if (flashlight == null)
        {
            Debug.LogWarning("[Incremental] LightFedCharge found no flashlight Light2D - this prop can never charge.", this);
        }

        if (string.IsNullOrEmpty(PropId))
        {
            Debug.LogWarning($"[Incremental] LightFedCharge on '{gameObject.name}' has no propId - charge will not persist across room reloads.", this);
        }

        Debug.Log("[Incremental] WOULD: ViR ambient glow trickle-charges this prop.", this);
    }

    void Update()
    {
        Incremental incremental = Incremental.Instance;
        if (incremental == null || !incremental.Running)
        {
            return;
        }

        // A consumed one-shot refuses light instead of charging up to a
        // payout that would no-op.
        if (incremental.IsConsumed(PropId))
        {
            return;
        }

        bool lit = IsLit();
        float charge = AdvanceCharge(incremental, lit);

        if (lit && !chargeLoopRunning && propAudio != null)
        {
            propAudio.StartCharge();
            chargeLoopRunning = true;
        }

        if (chargeLoopRunning)
        {
            if (propAudio != null)
            {
                propAudio.SetChargeProgress(charge);
            }

            // The riser follows the decay down and dies with it.
            if (charge <= 0f)
            {
                if (propAudio != null)
                {
                    propAudio.StopCharge(false);
                }
                chargeLoopRunning = false;
            }
        }

        ApplyVisual(charge);

        if (charge >= 1f)
        {
            CompleteCharge(incremental);
        }
    }

    // Lit: feed gain into the central registry (which also exempts the
    // entry from this pass's decay). Unlit: just read - central decay
    // handles the drain. Props without a propId run the same math on an
    // instance-local float instead.
    float AdvanceCharge(Incremental incremental, bool lit)
    {
        float gain = Time.deltaTime / Mathf.Max(chargeTime, 0.01f);

        if (!string.IsNullOrEmpty(PropId))
        {
            return lit
                ? incremental.Charges.Feed(PropId, gain, decayRate)
                : incremental.Charges.GetCharge(PropId);
        }

        localCharge = lit
            ? Mathf.Min(1f, localCharge + gain)
            : Mathf.Max(0f, localCharge - decayRate * Time.deltaTime);
        return localCharge;
    }

    // "Lit" means MOSTLY in the beam: the sprite's bounds center + 4 corners
    // are each tested against the cone, and requiredCoverage of them must be
    // inside - a beam edge grazing one corner (or the base pivot, which is
    // what a single-point test would check) does not count.
    bool IsLit()
    {
        if (flashlight == null || !flashlight.isActiveAndEnabled)
        {
            return false;
        }

        Bounds bounds = spriteRenderer != null
            ? spriteRenderer.bounds
            : new Bounds(transform.position, Vector3.zero);

        Vector2 min = bounds.min;
        Vector2 max = bounds.max;
        int inside = 0;
        if (InCone(bounds.center)) inside++;
        if (InCone(min)) inside++;
        if (InCone(max)) inside++;
        if (InCone(new Vector2(min.x, max.y))) inside++;
        if (InCone(new Vector2(max.x, min.y))) inside++;

        return inside >= Mathf.CeilToInt(requiredCoverage * 5f);
    }

    bool InCone(Vector2 point)
    {
        Vector2 toPoint = point - (Vector2)flashlight.transform.position;
        if (toPoint.magnitude > flashlight.pointLightOuterRadius)
        {
            return false;
        }

        // FlashlightAimToMouse keeps the beam along the light's transform.up.
        return Vector2.Angle(flashlight.transform.up, toPoint) <= flashlight.pointLightOuterAngle * 0.5f;
    }

    void CompleteCharge(Incremental incremental)
    {
        foreach (IIncrementalEffect effect in GetComponents<IIncrementalEffect>())
        {
            effect.Apply();
        }

        if (propAudio != null)
        {
            propAudio.StopCharge(true);
        }
        chargeLoopRunning = false;

        incremental.Charges.Clear(PropId);
        localCharge = 0f;
        ApplyVisual(0f);

        Debug.Log($"[Incremental] Charge complete on '{gameObject.name}' - effects applied.", this);
        Debug.Log("[Incremental] WOULD: PA reacts to charge completion.");
    }

    void ApplyVisual(float charge)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.Lerp(baseColor, chargedColor, charge);
        }
    }

    void OnDisable()
    {
        // Leave the sprite exactly as we found it (room unloads must never
        // inherit a half-charged tint). PropAudio stops its own loop
        // instance in its OnDisable.
        chargeLoopRunning = false;
        if (spriteRenderer != null)
        {
            spriteRenderer.color = baseColor;
        }
    }
}
