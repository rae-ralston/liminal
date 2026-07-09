using UnityEngine;

// Core state of The Incremental: current count, tick rate, multiplier.
//
// BOUNDARY DECISION NEEDED: GameManager already has counterThreshold /
// IncrementCounter / GameStarted - The Incremental may already partially
// live there. Either (a) this class absorbs that logic and GameManager
// delegates to it, or (b) this class is dropped and its members move into
// GameManager. Don't run both in parallel - one source of truth for the
// count. Structured here as its own singleton (matching the existing
// GameManager/AudioManager/RoomLoader singleton pattern) on the assumption
// of (a), since the clicker is a big enough system to deserve its own file.
public class Incremental : MonoBehaviour
{
    public static Incremental Instance { get; private set; }

    [SerializeField] float baseTicksPerSecond;

    public bool Running { get; private set; }

    public long Count { get; private set; }

    public float Multiplier { get; private set; }

    void Awake()
    {
    }

    void Update()
    {
        // auto-tick: Count += rate * multiplier over time, once Running
    }

    // Called by IncrementalStarter (the special computer prop).
    public void StartIncremental()
    {
    }

    // Flat one-time gain - FlatClickReward props.
    public void AddClicks(long amount)
    {
    }

    // Player spam-clicking a ClickSource prop. Kept separate from AddClicks
    // so click-feedback audio/UI can hook manual clicks specifically.
    public void ManualClick()
    {
    }

    // MultiplierUpgrade props.
    public void AddMultiplier(float amount)
    {
    }

    // For the endgame check (summon threshold for the end button).
    public bool HasReached(long threshold)
    {
        return false;
    }
}
