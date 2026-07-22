using UnityEngine;
using UnityEngine.Rendering.Universal;

// Generic on/off toggle for Light2D sources. Drop next to PropInteraction on
// any prop that should flip a light when interacted with - a desk lamp
// toggling its own child light, or a wall switch pointed at a Light2D
// somewhere else in the room via the serialized field.
//
// Must live on the SAME GameObject as PropInteraction, same contract as
// DoorUnlocker: PropInteraction does GetComponents<IIncrementalEffect>() on
// itself and calls Apply() on everything it finds.
public class LightSwitch : MonoBehaviour, IIncrementalEffect
{
    [Tooltip("Light(s) this switch controls. Doesn't have to be a child - point it at any Light2D in the room for a remote switch.")]
    [SerializeField] Light2D[] targets;

    public void Apply()
    {
        if (targets == null || targets.Length == 0)
        {
            Debug.LogWarning($"[LightSwitch] '{name}' has no target lights assigned.", this);
            return;
        }

        foreach (Light2D light in targets)
        {
            if (light != null) light.enabled = !light.enabled;
        }
    }
}
