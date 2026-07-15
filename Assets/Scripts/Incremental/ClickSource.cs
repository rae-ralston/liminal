using UnityEngine;

// The real clicker feature: a prop the player can spam-click.
// Not oneShot - every interact = one manual click. Optional cooldown to
// cap click rate (0 = uncapped spam).
public class ClickSource : MonoBehaviour, IIncrementalEffect
{
    [SerializeField] float clickCooldown;

    // Time.time-based, so it resets on room reload - acceptable for an
    // anti-spam cooldown.
    float nextClickTime;

    public void Apply()
    {
        if (Incremental.Instance == null)
        {
            Debug.LogWarning("[Incremental] ClickSource ignored - no Incremental instance.", this);
            return;
        }

        if (Time.time < nextClickTime)
        {
            return;
        }

        nextClickTime = Time.time + clickCooldown;
        Incremental.Instance.ManualClick();
    }
}
