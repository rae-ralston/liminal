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
        bool running = incremental != null && incremental.Running;

        if (!running)
        {
            if (lastRunning)
            {
                text.text = string.Empty;
                lastRunning = false;
                lastCount = -1;
                lastMultiplier = -1f;
            }

            return;
        }

        lastRunning = true;

        if (incremental.Count != lastCount || !Mathf.Approximately(incremental.Multiplier, lastMultiplier))
        {
            lastCount = incremental.Count;
            lastMultiplier = incremental.Multiplier;
            text.text = $"Count: {lastCount:N0}   x{lastMultiplier:0.0}";
        }
    }
}
