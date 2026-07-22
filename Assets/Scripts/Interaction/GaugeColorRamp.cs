using UnityEngine;

// Shared colour ramp for every Circuit fill view (TerminalGauge, CapacityColumn)
// so the thresholds can never drift (Ending brief E2). The thresholds are the
// shared thing - red < 50%, orange < 80%, yellow < 99%, green at 100%; the four
// colours stay serialized per component so art can tune them independently.
public static class GaugeColorRamp
{
    public static Color Evaluate(float fraction, Color low, Color mid, Color high, Color full)
    {
        if (fraction < 0.5f) return low;
        if (fraction < 0.8f) return mid;
        if (fraction < 0.99f) return high;
        return full;
    }
}
