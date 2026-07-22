using UnityEngine;

// The vertical capacity column (Ending brief E2) - the single "how full is the
// bank" view, used TWICE with the SAME component: once on the SecurityRoom
// board and once on the AssemblyHall stage beside the End Button. Diegetic,
// NO numbers, ever.
//
// Two facts, both derived from Incremental every frame:
//   HEIGHT grows with MaxCapacity (early game = short stub, late game = tall
//     column), with a floor so an early tiny MaxCapacity is still visible.
//   FILL is Count / MaxCapacity - the same global fraction every Circuit gauge
//     shows (Q = C*V: adding capacitance at constant charge drops fill
//     everywhere at once).
//
// Vertical, BOTTOM-anchored: the fill sprite scales on Y from a BOTTOM-EDGE
// pivot (a centered pivot shrinks toward the middle). Colour ramp shared with
// TerminalGauge via GaugeColorRamp so the two can never drift.
//
// Dumb polling view (no pub/sub) like every other Circuit gauge; never writes.
public class CapacityColumn : MonoBehaviour
{
    [Tooltip("Scaled on Y to the column HEIGHT (grows with MaxCapacity). Usually the column body/frame root.")]
    [SerializeField] Transform columnRoot;
    [Tooltip("Static outline/backplate - stays lit. Optional.")]
    [SerializeField] SpriteRenderer frame;
    [Tooltip("Fill sprite, scaled on Y from its BOTTOM-edge pivot to the charge fraction.")]
    [SerializeField] SpriteRenderer fill;

    [Header("Height mapping (MaxCapacity -> column height)")]
    [Tooltip("Shortest the column ever gets, as a fraction of full height, so an early tiny MaxCapacity is still visible.")]
    [Range(0f, 1f)]
    [SerializeField] float minHeightFraction = 0.15f;
    [Tooltip("MaxCapacity that maps to FULL column height. DAY-7 BALANCING NUMBER, tied to the capacity ladder (tuning pass v1 ends ~450) - revisit this whenever capacities change.")]
    [SerializeField] float displayCapacityCeiling = 450f;

    [Header("Color ramp (fill fraction)")]
    [SerializeField] Color lowColor = new Color(0.85f, 0.2f, 0.15f);    // < 50%
    [SerializeField] Color midColor = new Color(1f, 0.55f, 0.1f);       // < 80%
    [SerializeField] Color highColor = new Color(0.95f, 0.85f, 0.2f);   // < 99%
    [SerializeField] Color fullColor = new Color(0.2f, 0.85f, 0.3f);    // 100%
    [SerializeField] Color frameColor = new Color(0.25f, 0.25f, 0.25f);

    [Tooltip("Approx seconds to ease height/fill toward target - no snapping (same discipline as TerminalGauge / DoorIndicatorLight).")]
    [SerializeField] float lerpSeconds = 0.3f;

    float heightCurrent;
    float fillCurrent;

    void Update()
    {
        Incremental incremental = Incremental.Instance;

        float heightTarget = 0f;
        float fillTarget = 0f;
        if (incremental != null && incremental.MaxCapacity > 0)
        {
            // sqrt curve + floor: the early stub is short but visible, growth
            // eases off toward the ceiling. Guard the divide (MaxCapacity is 0
            // until the bootstrap terminal seeds the floor).
            float capT = Mathf.Clamp01((float)incremental.MaxCapacity / Mathf.Max(1f, displayCapacityCeiling));
            heightTarget = Mathf.Lerp(minHeightFraction, 1f, Mathf.Sqrt(capT));
            fillTarget = Mathf.Clamp01((float)incremental.Count / incremental.MaxCapacity);
        }

        // Frame-rate-independent ease; guard lerpSeconds == 0. deltaTime is 0
        // at timeScale 0, so the column simply holds while paused.
        float t = lerpSeconds > 0f ? 1f - Mathf.Exp(-Time.deltaTime / lerpSeconds) : 1f;
        heightCurrent = Mathf.Lerp(heightCurrent, heightTarget, t);
        fillCurrent = Mathf.Lerp(fillCurrent, fillTarget, t);

        Draw(heightCurrent, fillCurrent);
    }

    void Draw(float height, float fill01)
    {
        if (columnRoot != null)
        {
            Vector3 s = columnRoot.localScale;
            s.y = height;
            columnRoot.localScale = s;
        }

        if (frame != null)
        {
            frame.enabled = true;
            frame.color = frameColor;
        }

        if (fill != null)
        {
            fill.enabled = fill01 > 0f;
            fill.color = GaugeColorRamp.Evaluate(fill01, lowColor, midColor, highColor, fullColor);
            Vector3 s = fill.transform.localScale;
            s.y = fill01;
            fill.transform.localScale = s;
        }
    }
}
