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
        RunSection("Charge registry", ChargeRegistryChecks);
        RunSection("Circuit: capacity clamp", CircuitCapacityClamp);
        RunSection("Circuit: segments & charge dump", CircuitSegmentsAndDump);
        RunSection("Circuit: room activation & bootstrap", CircuitActivation);

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

    // Charge decay is driven by Advance (the same seam as the tick), so
    // feeding via the registry and advancing simulated time covers the whole
    // lit/unlit/unloaded contract without play mode.
    static void ChargeRegistryChecks(Incremental inc)
    {
        inc.StartIncremental();
        ChargeRegistry charges = inc.Charges;

        Check(charges.GetCharge(null) == 0f, "GetCharge(null) is 0");
        Check(charges.GetCharge("") == 0f, "GetCharge(empty) is 0");
        Check(charges.GetCharge("unknown") == 0f, "unknown propId reads 0");
        Check(charges.Feed(null, 0.5f, 0.1f) == 0f, "Feed(null propId) refused");

        CheckApprox(charges.Feed("p", 0.25f, 0.5f), 0.25f, "Feed accumulates gain");
        CheckApprox(charges.Feed("p", 0.25f, 0.5f), 0.5f, "second Feed stacks");
        CheckApprox(charges.Feed("p", 0f, 0.5f), 0.5f, "zero gain is a no-op read");

        inc.Advance(1.0);
        CheckApprox(charges.GetCharge("p"), 0.5f, "a fed entry skips exactly one decay pass (the lit-frame contract)");
        inc.Advance(0.5);
        CheckApprox(charges.GetCharge("p"), 0.25f, "unfed entry decays at its stored rate (0.5/s for 0.5s)");

        CheckApprox(charges.Feed("p", 5f, 0.5f), 1f, "charge clamps at 1");
        charges.Clear("p");
        Check(charges.GetCharge("p") == 0f, "Clear zeroes the entry");

        charges.Feed("q", 0.3f, 1f);
        inc.Advance(1.0); // fed -> skipped
        inc.Advance(1.0); // decays 1.0 -> fully drained, entry removed
        Check(charges.GetCharge("q") == 0f, "entry drains to zero and is removed");

        // Decay only runs on the economy's clock.
        charges.Feed("r", 0.5f, 1f);
        inc.Advance(0.0);
        CheckApprox(charges.GetCharge("r"), 0.5f, "zero/negative delta never decays");
    }

    // ------------------------------------------------------------------
    // The Circuit (2026-07-16). All pre-Circuit sections above run with
    // MaxCapacity == 0 (unwired) and must keep passing unchanged - that IS
    // the uncapped-legacy contract. Note the pre-start TrySpend refusal
    // still stands: only TryActivateRoom's bootstrap flag bypasses the
    // Running gate, TrySpend itself never does.
    // ------------------------------------------------------------------

    // Serialized private fields (chargeDumpFraction, bootstrapRoom, allRooms)
    // are set the same way the Inspector would - via SerializedObject.
    static SerializedObject Serialized(Object target)
    {
        return new SerializedObject(target);
    }

    static void SetDumpFraction(Incremental inc, float value)
    {
        SerializedObject so = Serialized(inc);
        so.FindProperty("chargeDumpFraction").floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static RoomId MakeRoom(string name, long activationCost, long baseCapacity)
    {
        RoomId room = ScriptableObject.CreateInstance<RoomId>();
        room.name = name;
        room.hideFlags = HideFlags.HideAndDontSave;
        SerializedObject so = Serialized(room);
        so.FindProperty("activationCost").longValue = activationCost;
        so.FindProperty("baseCapacity").longValue = baseCapacity;
        so.ApplyModifiedPropertiesWithoutUndo();
        return room;
    }

    static void CircuitCapacityClamp(Incremental inc)
    {
        inc.RaiseCapacityFloor(10);
        Check(inc.MaxCapacity == 10, "capacity floor raises MaxCapacity");
        inc.StartIncremental();

        inc.Advance(15.0);
        Check(inc.Count == 10, "Count pins at MaxCapacity");
        Check(inc.TotalEarned == 10, "TotalEarned banks the post-clamp delta only (wasted generation is not earned)");

        // Collision #3: whole ticks at cap are drained, never banked as a
        // backlog that dumps when capacity next rises.
        inc.Advance(3.7);
        Check(inc.Count == 10, "generation at cap is wasted");
        inc.RaiseCapacityFloor(20);
        Check(inc.MaxCapacity == 20 && inc.Count == 10, "raising the floor adds headroom without granting charge");
        inc.Advance(0.3);
        Check(inc.Count == 11, "only the fractional remainder survived the cap (0.7 + 0.3 = 1 tick, no 3-tick backlog dump)");
        Check(inc.TotalEarned == 11, "TotalEarned tracks banked ticks only");

        inc.RaiseCapacityFloor(5);
        Check(inc.MaxCapacity == 20, "capacity never lowers (floor raise below current is a no-op)");
        Check(inc.RecalculateCapacity() == inc.MaxCapacity, "re-derived capacity matches maintained MaxCapacity (floor only)");
        Check(inc.Count <= inc.MaxCapacity, "Count <= MaxCapacity invariant");
    }

    static void CircuitSegmentsAndDump(Incremental inc)
    {
        // Credit (and therefore the dump) deliberately works pre-start: the
        // bootstrap activation happens before the lever fires Running.
        inc.AddCapacitySegment(null, "seg_a", 10);
        Check(inc.MaxCapacity == 10, "segment raises MaxCapacity by its size");
        Check(inc.Count == 10, "default dump fraction 1.0 grants the full segment size as charge");
        Check(inc.TotalEarned == 10, "dumped charge banks into TotalEarned");

        SetDumpFraction(inc, 0.5f);
        inc.AddCapacitySegment(null, "seg_b", 10);
        Check(inc.MaxCapacity == 20 && inc.Count == 15, "dump fraction 0.5 grants half the segment size");

        SetDumpFraction(inc, 0f);
        inc.AddCapacitySegment(null, "seg_c", 10);
        Check(inc.MaxCapacity == 30 && inc.Count == 15, "dump fraction 0 grants capacity only (all bars sag together)");

        inc.AddCapacitySegment(null, "seg_bad", 0);
        inc.AddCapacitySegment(null, "seg_bad", -5);
        Check(inc.MaxCapacity == 30 && inc.Segments.Count == 3, "non-positive segment sizes are refused");

        Check(inc.RecalculateCapacity() == inc.MaxCapacity, "re-derived capacity matches after a segment sequence");
        Check(inc.Count <= inc.MaxCapacity, "Count <= MaxCapacity invariant");
    }

    static void CircuitActivation(Incremental inc)
    {
        RoomId roomA = MakeRoom("TestRoom_Bootstrap", 5, 10);
        RoomId roomB = MakeRoom("TestRoom_B", 100, 50);

        try
        {
            Check(!inc.AllRoomsActivated, "AllRoomsActivated false while the all-rooms list is empty (unwired build can't satisfy the end condition)");

            SerializedObject so = Serialized(inc);
            so.FindProperty("bootstrapRoom").objectReferenceValue = roomA;
            SerializedProperty rooms = so.FindProperty("allRooms");
            rooms.arraySize = 2;
            rooms.GetArrayElementAtIndex(0).objectReferenceValue = roomA;
            rooms.GetArrayElementAtIndex(1).objectReferenceValue = roomB;
            so.ApplyModifiedPropertiesWithoutUndo();

            inc.SeedBootstrapResidue();
            Check(inc.Count == 5, "residue seeds exactly the bootstrap room's activation cost");
            Check(inc.MaxCapacity == 5, "capacity floor makes the residue fit (Count <= MaxCapacity at seed)");
            Check(inc.TotalEarned == 0, "residue is found, not earned - TotalEarned stays 0");
            inc.SeedBootstrapResidue();
            Check(inc.Count == 5, "double seed is a no-op");

            Check(!inc.IsRoomActivated(null), "IsRoomActivated(null) is false");
            Check(!inc.TryActivateRoom(null, true), "TryActivateRoom(null) refused");
            Check(!inc.TryActivateRoom(roomA), "non-bootstrap activation refused pre-start");
            Check(!inc.TryActivateRoom(roomB, true), "bootstrap flag still refuses an unaffordable cost");

            Check(inc.TryActivateRoom(roomA, true), "bootstrap activation works pre-start (residue exactly affords it)");
            Check(inc.IsRoomActivated(roomA), "room registers as activated");
            Check(inc.MaxCapacity == 15, "activation adds the room's base capacity segment (5 floor + 10 base)");
            Check(inc.Count == 10, "residue spent, then the base segment's dump landed (0 + 10 at fraction 1.0)");
            Check(!inc.TryActivateRoom(roomA, true), "re-activation refused - no second charge");
            Check(inc.Count == 10, "refused re-activation leaves balance untouched");

            Check(!inc.AllRoomsActivated, "AllRoomsActivated false while a listed room is unactivated");

            inc.RaiseCapacityFloor(200);
            inc.StartIncremental();
            inc.AddClicks(140);
            Check(inc.Count == 150, "setup: affordable balance for the second room");
            Check(inc.TryActivateRoom(roomB), "post-start activation needs no bootstrap flag");
            Check(inc.Count == 100, "cost spent, then the base segment dumped (150 - 100 + 50)");
            Check(inc.MaxCapacity == 260, "second room's capacity landed (200 floor + 10 + 50)");

            Check(inc.AllRoomsActivated, "AllRoomsActivated true once every listed room is activated");
            Check(inc.RecalculateCapacity() == inc.MaxCapacity, "re-derived capacity matches after activations");
            Check(inc.Count <= inc.MaxCapacity, "Count <= MaxCapacity invariant");
        }
        finally
        {
            Object.DestroyImmediate(roomA);
            Object.DestroyImmediate(roomB);
        }
    }
}
