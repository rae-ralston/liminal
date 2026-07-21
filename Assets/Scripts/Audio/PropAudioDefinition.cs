using FMODUnity;
using UnityEngine;

/*
 * One asset per prop sound set (e.g. "VendingMachine", "OfficeDoor").
 * Create via right-click in Project window: Create > Audio > Prop Audio Definition,
 * fill in the FMOD event slots and drag the asset onto the PropAudio component
 * of a prop prefab. Empty slots are fine - PropAudio skips them silently.
 *
 * Several prefabs may share one definition; editing the asset retunes all of them.
 */
[CreateAssetMenu(fileName = "NewPropAudioDefinition", menuName = "Audio/Prop Audio Definition")]
public class PropAudioDefinition : ScriptableObject
{
    [Header("Loops")]
    public EventReference ambientLoop;           // hum/buzz playing while the prop is active
    public EventReference ambientLoopInactive;   // plays instead while inactive; empty = silent
    public EventReference ambientLoopSuspended;  // plays instead mid-transition (e.g. activated but the wider system hasn't started); empty = silent

    [Header("One-shots")]
    public EventReference interact;      // default sound on interaction
    public EventReference locked;        // interaction refused
    public EventReference turnOn;
    public EventReference turnOff;

    [Header("Charge (light-fed props)")]
    public EventReference chargeLoop;     // looping riser while charging, driven by a continuous "Charge" parameter (0-1)
    public EventReference chargeComplete; // one-shot confirm at full charge
}
