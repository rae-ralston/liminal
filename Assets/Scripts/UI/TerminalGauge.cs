using UnityEngine;

// Diegetic capacity gauge on a room's terminal (The Circuit, 2026-07-16).
// Dumb polling view like DoorIndicatorLight/IncrementalHud - no pub/sub.
// NO numbers anywhere: the no-HUD law stands, this is sprites on a prop.
//
// Layout (authored in the prefab, this component only drives fill + color):
// the GLOBAL bar at the bottom, the room's segment bars stacked above. Bar 1
// is always the room's base segment (fills once the room is activated); the
// remaining slots are inspector-wired to CapacityUpgrade props. Slots are a
// fixed array of 6 (2-3 typical, AssemblyHall ~5 - no dynamic list needed).
//
// Slot states:
//   hidden  - slot unassigned: both sprites off.
//   ghost   - assigned, prop not collected: frame only, empty outline.
//   filling - collected: fill fraction = Count / MaxCapacity. PROPORTIONAL -
//             every live bar in the building shows the same fraction (adding
//             capacitance at constant charge drops the voltage across the
//             whole bank).
//
// Fill sprites are scaled on X, so their pivot must sit at the LEFT edge
// (a centered pivot would shrink toward the middle).
public class TerminalGauge : MonoBehaviour
{
    [System.Serializable]
    class Bar
    {
        [Tooltip("Static outline/backplate - stays visible in ghost state.")]
        public SpriteRenderer frame;
        [Tooltip("Fill sprite, scaled on X from its LEFT-edge pivot.")]
        public SpriteRenderer fill;
    }

    [System.Serializable]
    class Slot
    {
        [Tooltip("The CapacityUpgrade prop this bar tracks (its propId keys the collected check). Empty = slot hidden.")]
        public Prop prop;
        public Bar bar;
    }

    [Tooltip("Terminal whose room this gauge shows. Empty = resolved from this GameObject or a parent (the gauge lives on the terminal prefab).")]
    [SerializeField] Terminal terminal;

    [Tooltip("Building-wide bar at the bottom of the stack.")]
    [SerializeField] Bar globalBar;

    [Tooltip("Bar 1: the room's base capacity segment. Ghost until the room is activated.")]
    [SerializeField] Bar baseBar;

    [SerializeField] Slot[] slots = new Slot[6];

    [Header("Color ramp (fill fraction, all bars)")]
    [SerializeField] Color lowColor = new Color(0.85f, 0.2f, 0.15f);     // < 50%
    [SerializeField] Color midColor = new Color(1f, 0.55f, 0.1f);        // < 80%
    [SerializeField] Color highColor = new Color(0.95f, 0.85f, 0.2f);    // < 99%
    [SerializeField] Color fullColor = new Color(0.2f, 0.85f, 0.3f);     // 100%
    [SerializeField] Color ghostColor = new Color(0.25f, 0.25f, 0.25f);

    void Awake()
    {
        if (terminal == null)
        {
            terminal = GetComponentInParent<Terminal>();
        }

        if (terminal == null)
        {
            Debug.LogWarning($"[Circuit] TerminalGauge '{name}' has no Terminal (assigned or in parents) - gauge stays dark.", this);
        }
    }

    void Update()
    {
        Incremental incremental = Incremental.Instance;
        if (incremental == null || terminal == null)
        {
            return;
        }

        float fraction = incremental.MaxCapacity > 0
            ? Mathf.Clamp01((float)incremental.Count / incremental.MaxCapacity)
            : 0f;

        SetLive(globalBar, fraction);

        bool activated = incremental.IsRoomActivated(terminal.RoomId);
        if (activated)
        {
            SetLive(baseBar, fraction);
        }
        else
        {
            SetGhost(baseBar);
        }

        foreach (Slot slot in slots)
        {
            if (slot == null || slot.bar == null)
            {
                continue;
            }

            if (slot.prop == null)
            {
                SetHidden(slot.bar);
            }
            else if (incremental.IsConsumed(slot.prop.PropId))
            {
                SetLive(slot.bar, fraction);
            }
            else
            {
                SetGhost(slot.bar);
            }
        }
    }

    // red < 50%, orange < 80%, yellow < 99%, green at 100%
    Color RampColor(float fraction)
    {
        if (fraction < 0.5f) return lowColor;
        if (fraction < 0.8f) return midColor;
        if (fraction < 0.99f) return highColor;
        return fullColor;
    }

    void SetLive(Bar bar, float fraction)
    {
        if (bar == null)
        {
            return;
        }

        if (bar.frame != null)
        {
            bar.frame.enabled = true;
            bar.frame.color = ghostColor;
        }

        if (bar.fill != null)
        {
            bar.fill.enabled = fraction > 0f;
            bar.fill.color = RampColor(fraction);
            Vector3 scale = bar.fill.transform.localScale;
            scale.x = fraction;
            bar.fill.transform.localScale = scale;
        }
    }

    void SetGhost(Bar bar)
    {
        if (bar == null)
        {
            return;
        }

        if (bar.frame != null)
        {
            bar.frame.enabled = true;
            bar.frame.color = ghostColor;
        }

        if (bar.fill != null)
        {
            bar.fill.enabled = false;
        }
    }

    void SetHidden(Bar bar)
    {
        if (bar == null)
        {
            return;
        }

        if (bar.frame != null) bar.frame.enabled = false;
        if (bar.fill != null) bar.fill.enabled = false;
    }
}
