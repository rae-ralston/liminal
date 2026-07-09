using UnityEngine;

// ---------------------------------------------------------------------------
// Effect components. Each is a small MonoBehaviour dropped onto a prop
// GameObject in the Inspector, next to Prop (identity) and an interaction
// trigger. All of them talk to Incremental.Instance - props never talk to
// each other.
// ---------------------------------------------------------------------------

// The special computer that starts The Incremental.
public class IncrementalStarter : MonoBehaviour, IIncrementalEffect
{
    [SerializeField] bool oneShot = true;

    public void Apply()
    {
    }
}

// A prop that grants a fixed amount of clicks, typically once
// (a lever, a hidden button behind a filing cabinet).
public class FlatClickReward : MonoBehaviour, IIncrementalEffect
{
    [SerializeField] long clickAmount;
    [SerializeField] bool oneShot = true;

    public void Apply()
    {
    }
}

// A prop that raises the tick multiplier, typically once per prop.
public class MultiplierUpgrade : MonoBehaviour, IIncrementalEffect
{
    [SerializeField] float multiplierAmount;
    [SerializeField] bool oneShot = true;

    public void Apply()
    {
    }
}

// The real clicker feature: a prop the player can spam-click.
// Not oneShot - every interact = one manual click. Optional cooldown to
// cap click rate (0 = uncapped spam).
public class ClickSource : MonoBehaviour, IIncrementalEffect
{
    [SerializeField] float clickCooldown;

    public void Apply()
    {
    }
}

// Summons the huge end-of-game button once the count threshold is reached.
// Lives on whatever prop/trigger performs the summoning; the end button
// itself is just a Prop (PropKind.EndButton) + an effect that calls into
// GameManager to end the game.
public class EndButtonSummoner : MonoBehaviour, IIncrementalEffect
{
    [SerializeField] long summonThreshold;
    [SerializeField] GameObject endButtonPrefab;

    public void Apply()
    {
    }
}
