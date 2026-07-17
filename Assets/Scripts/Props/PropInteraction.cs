using UnityEngine;

// Bridges the existing interaction system to the effect components.
// An InteractableTrigger subclass that, on interact, collects every
// IIncrementalEffect on this GameObject and applies them, then plays the
// interact sound if a PropAudio component is present (silent otherwise).
//
// The PropAudio stub that used to live in this file has been replaced by
// the real component: Assets/Scripts/Audio/PropAudio.cs (event slots come
// from a PropAudioDefinition asset assigned in the Inspector).
//
// The Circuit C3: gated in front of the effect pipeline by room activation.
// Terminal.Current is "what room am I in" (last-enabled wins, one terminal
// per room scene). A room with no terminal at all reads as unpowered too -
// every prop refuses until terminals are placed, same as a dead room.
public class PropInteraction : InteractableTrigger
{
    public override void Interact()
    {
        if (Incremental.Instance == null)
        {
            Debug.LogError("[Circuit] No Incremental in scene.", this);
            return;
        }

        RoomId currentRoom = Terminal.Current != null ? Terminal.Current.RoomId : null;
        if (!Incremental.Instance.IsRoomActivated(currentRoom))
        {
            Debug.Log("[Circuit] Prop inert: room not powered.");
            PropAudio lockedAudio = GetComponent<PropAudio>();
            if (lockedAudio != null) lockedAudio.PlayLocked();
            return;
        }

        foreach (IIncrementalEffect effect in GetComponents<IIncrementalEffect>())
        {
            effect.Apply();
        }

        PropAudio audio = GetComponent<PropAudio>();
        if (audio != null) audio.PlayInteract();
    }
}
