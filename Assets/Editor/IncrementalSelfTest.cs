using UnityEditor;
using UnityEngine;

// Economy core self-test (Tools > Incremental > Run Self Tests) - same
// pattern as DoorConsistencyWindow: an editor menu item that runs assertions
// against live gameplay code and logs pass/fail. Drives
// Incremental.Advance(double) directly, so no play mode is needed: this is
// pure tick math + invariants that everything else sits on.
//
// Each section gets a fresh throwaway Incremental component (Awake does not
// run in edit mode, so the static Instance is never touched).
public static class IncrementalSelfTest
{
    static int passed;
    static int failed;

    [MenuItem("Tools/Incremental/Run Self Tests")]
    public static void Run()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("[Incremental] Self tests must run in edit mode (they create throwaway instances).");
            return;
        }

        passed = 0;
        failed = 0;

        RunSection("Tick accumulation", TickAccumulation);
        RunSection("Spend & Count/TotalEarned invariants", SpendInvariants);
        RunSection("Pre-start guards", PreStartGuards);
        RunSection("Multiplier sources", MultiplierSources);
        RunSection("Multiplier drives the tick", MultiplierTick);
        RunSection("Consumed registry", ConsumedRegistry);

        if (failed == 0)
        {
            Debug.Log($"[Incremental] Self tests PASSED: {passed} checks.");
        }
        else
        {
            Debug.LogError($"[Incremental] Self tests FAILED: {failed} of {passed + failed} checks failed (see errors above).");
        }
    }

    static void RunSection(string name, System.Action<Incremental> body)
    {
        GameObject host = new GameObject("IncrementalSelfTest_" + name)
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        try
        {
            body(host.AddComponent<Incremental>());
        }
        catch (System.Exception e)
        {
            failed++;
            Debug.LogError($"[Incremental] Section '{name}' threw: {e}");
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    static void Check(bool condition, string description)
    {
        if (condition)
        {
            passed++;
        }
        else
        {
            failed++;
            Debug.LogError($"[Incremental] CHECK FAILED: {description}");
        }
    }

    static void CheckApprox(float actual, float expected, string description)
    {
        Check(Mathf.Approximately(actual, expected), $"{description} (expected {expected}, got {actual})");
    }

    // ------------------------------------------------------------------

    static void TickAccumulation(Incremental inc)
    {
        inc.StartIncremental();
        inc.StartIncremental(); // idempotent - must not reset anything

        inc.Advance(0.4);
        Check(inc.Count == 0, "0.4s at 1 tick/s stays below one whole tick");
        inc.Advance(0.4);
        Check(inc.Count == 0, "0.8s accumulated, still no whole tick");
        inc.Advance(0.4);
        Check(inc.Count == 1, "1.2s accumulated yields exactly one tick");
        inc.Advance(10.0);
        Check(inc.Count == 11, "10 more seconds yield 10 more ticks (remainder preserved)");
        Check(inc.TotalEarned == inc.Count, "TotalEarned matches Count before any spending");

        inc.Advance(-1.0);
        Check(inc.Count == 11, "negative delta is ignored");
        inc.Advance(0.0);
        Check(inc.Count == 11, "zero delta is ignored");
    }

    static void SpendInvariants(Incremental inc)
    {
        inc.StartIncremental();
        inc.Advance(10.0);
        Check(inc.Count == 10, "setup: 10 ticks earned");

        Check(!inc.TrySpend(11), "TrySpend refuses one over balance");
        Check(inc.Count == 10, "refused spend leaves balance untouched");
        Check(inc.TrySpend(10), "TrySpend succeeds at exact balance (boundary)");
        Check(inc.Count == 0, "successful spend empties balance");
        Check(inc.TotalEarned == 10, "TotalEarned never decreases on spend");
        Check(inc.TrySpend(0), "zero cost always succeeds");
        Check(!inc.TrySpend(-5), "negative cost refused");

        Check(inc.HasReached(0), "HasReached(0) true while running");
        Check(!inc.HasReached(1), "HasReached(1) false at zero balance");

        inc.AddClicks(50);
        Check(inc.Count == 50 && inc.TotalEarned == 60, "AddClicks raises both Count and TotalEarned");
        inc.AddClicks(-3);
        Check(inc.Count == 50, "non-positive AddClicks ignored");
        inc.ManualClick();
        Check(inc.Count == 51 && inc.TotalEarned == 61, "ManualClick is +1 flat on both");
    }

    static void PreStartGuards(Incremental inc)
    {
        inc.Advance(5.0);
        Check(inc.Count == 0, "no ticks before StartIncremental");
        inc.AddClicks(10);
        Check(inc.Count == 0, "AddClicks ignored before start");
        inc.ManualClick();
        Check(inc.Count == 0, "ManualClick ignored before start");
        Check(!inc.TrySpend(0), "TrySpend refused before start (even zero cost)");
        Check(!inc.HasReached(0), "HasReached false before start");
    }

    static void MultiplierSources(Incremental inc)
    {
        inc.StartIncremental();

        CheckApprox(inc.Multiplier, 1f, "baseline multiplier is x1");
        inc.RegisterMultiplierSource("a", 1f);
        CheckApprox(inc.Multiplier, 2f, "one source of +1 gives x2");
        inc.RegisterMultiplierSource("a", 1f);
        CheckApprox(inc.Multiplier, 2f, "re-registering the same key/amount is idempotent (the room-reload case)");
        inc.RegisterMultiplierSource("a", 2f);
        CheckApprox(inc.Multiplier, 3f, "re-registering overwrites the amount, never stacks");
        inc.RegisterMultiplierSource("b", 0.5f);
        CheckApprox(inc.Multiplier, 3.5f, "second source adds on top");
        inc.UnregisterMultiplierSource("a");
        CheckApprox(inc.Multiplier, 1.5f, "unregister removes exactly that source");
        inc.UnregisterMultiplierSource("a");
        CheckApprox(inc.Multiplier, 1.5f, "double unregister is harmless");
        inc.UnregisterMultiplierSource(null);
        CheckApprox(inc.Multiplier, 1.5f, "null unregister is harmless");
        inc.RegisterMultiplierSource(null, 5f);
        CheckApprox(inc.Multiplier, 1.5f, "null-key register is refused");
        inc.RegisterMultiplierSource("", 5f);
        CheckApprox(inc.Multiplier, 1.5f, "empty-key register is refused");

        inc.AddMultiplier(1f);
        CheckApprox(inc.Multiplier, 2.5f, "AddMultiplier registers a permanent source");
        inc.AddMultiplier(1f);
        CheckApprox(inc.Multiplier, 3.5f, "second AddMultiplier stacks (distinct permanent keys)");
    }

    static void MultiplierTick(Incremental inc)
    {
        inc.StartIncremental();
        inc.RegisterMultiplierSource("m", 1f); // x2
        inc.Advance(1.0);
        Check(inc.Count == 2, "x2 multiplier yields 2 ticks per second");

        inc.UnregisterMultiplierSource("m");
        inc.RegisterMultiplierSource("m", 0.5f); // x1.5
        inc.Advance(1.0);
        Check(inc.Count == 3, "x1.5 for 1s yields 1 whole tick, 0.5 carried");
        inc.Advance(1.0);
        Check(inc.Count == 5, "carried remainder pays out on the next second (0.5+1.5=2)");
    }

    static void ConsumedRegistry(Incremental inc)
    {
        Check(!inc.TryConsume(null), "TryConsume(null) refused");
        Check(!inc.TryConsume(""), "TryConsume(empty) refused");
        Check(!inc.IsConsumed(null), "IsConsumed(null) is false");
        Check(!inc.IsConsumed(""), "IsConsumed(empty) is false");

        Check(inc.TryConsume("office1_flatreward_a"), "first TryConsume succeeds");
        Check(inc.IsConsumed("office1_flatreward_a"), "IsConsumed true after consume");
        Check(!inc.TryConsume("office1_flatreward_a"), "duplicate TryConsume refused (the duplicate-propId case)");
        Check(!inc.IsConsumed("office1_flatreward_b"), "unrelated id stays unconsumed");
    }
}
