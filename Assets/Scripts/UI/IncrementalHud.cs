using TMPro;
using UnityEngine;

// Dev/debug counter display for Stage A/B testing. Polls Incremental in
// Update (no pub/sub in this project) and caches last-shown values to skip
// string rebuilds. Empty text while not running = clean pre-game screen.
//
// The SHIPPING presentation is meant to stay diegetic (door keypad lights,
// prop glow - no number HUD in the liminal setting, decided 2026-07-15);
// this component is disposable once that exists.
public class IncrementalHud : MonoBehaviour
{
    TMP_Text text;

    long lastCount = -1;
    long lastMaxCapacity = -1;
    float lastMultiplier = -1f;
    bool lastRunning;

    void Awake()
    {
        text = GetComponent<TMP_Text>();
        if (text == null)
        {
            Debug.LogWarning("[Incremental] IncrementalHud needs a TMP_Text on the same GameObject.", this);
            return;
        }

        text.text = string.Empty;
    }

    void Update()
    {
        if (text == null)
        {
            return;
        }

        Incremental incremental = Incremental.Instance;

        // Pre-start the HUD used to be blank, which made the seeded residue
        // and the bootstrap spend invisible (confused a real playtest,
        // 2026-07-18). Dev-only display: show as soon as the Circuit holds
        // any state; only a truly untouched game keeps the clean screen.
        bool visible = incremental != null && (incremental.Running || incremental.Count > 0 || incremental.MaxCapacity > 0);

        if (!visible)
        {
            if (lastRunning)
            {
                text.text = string.Empty;
                lastRunning = false;
                lastCount = -1;
                lastMaxCapacity = -1;
                lastMultiplier = -1f;
            }

            return;
        }

        lastRunning = true;

        if (incremental.Count != lastCount
            || incremental.MaxCapacity != lastMaxCapacity
            || !Mathf.Approximately(incremental.Multiplier, lastMultiplier))
        {
            lastCount = incremental.Count;
            lastMaxCapacity = incremental.MaxCapacity;
            lastMultiplier = incremental.Multiplier;
            string capacity = lastMaxCapacity > 0 ? $" / {lastMaxCapacity:N0}" : string.Empty;
            text.text = $"Count: {lastCount:N0}{capacity}   x{lastMultiplier:0.0}";
        }
    }
}
