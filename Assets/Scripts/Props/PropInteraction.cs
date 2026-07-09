using UnityEngine;

// Bridges the existing interaction system to the effect components.
// An InteractableTrigger subclass that, on interact, collects every
// IIncrementalEffect on this GameObject and applies them, then plays the
// interact sound if a PropAudio component is present (silent otherwise).
//
// The PropAudio stub that used to live in this file has been replaced by
// the real component: Assets/Scripts/Audio/PropAudio.cs (event slots come
// from a PropAudioDefinition asset assigned in the Inspector).
public class PropInteraction : InteractableTrigger
{
    public override void Interact()
    {
        foreach (IIncrementalEffect effect in GetComponents<IIncrementalEffect>())
        {
            effect.Apply();
        }

        PropAudio audio = GetComponent<PropAudio>();
        if (audio != null) audio.PlayInteract();
    }
}
