using UnityEngine;
using FMODUnity;

/*
 * Holds the truly global FMOD events only (player, ambience, music).
 * Prop and interaction sounds live on the props themselves:
 * a PropAudio component plus a PropAudioDefinition asset - see PropAudio.cs.
 */
public class FMODEvents : MonoBehaviour
{
    public static FMODEvents Instance { get; private set; }

    // Player Audio Entries
    [field: Header("Player Audio")]
    [field: SerializeField] public EventReference Footsteps { get; private set; }
    [field: SerializeField] public EventReference PlayerTurn { get; private set; }

    // Ambience Audio
    [field: Header("Ambience")]
    [field: SerializeField] public EventReference Ambience { get; private set; }

    // The Ending. Global because they must reach the player in ANY room -
    // only one room scene is loaded at a time, so a prop can only be heard in
    // its own room. Played by EndAnnouncer (PersistentScene).
    //
    // BOTH events must be 2D (no spatializer in FMOD Studio): they are fired
    // from the persistent rig, which is nowhere near the player.
    [field: Header("The Ending")]
    [field: SerializeField]
    [field: Tooltip("One-shot when the end condition is first reached - the alarm that tells the player, wherever they are, that the clock is worth returning to.")]
    public EventReference EndConditionMet { get; private set; }
    [field: SerializeField]
    [field: Tooltip("Starts when the clock is pressed and the chain is authorised (Stage -> Called) - the announcement that sends the player to the AssemblyHall. EndAnnouncer owns the instance and stops it when the first stage button is pressed, so this can be a loop (repeats until they arrive) or a single line (cut short if they beat it there).")]
    public EventReference EndAnnouncement { get; private set; }

    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogError("Found more than one FMODEvent instance in the scene.");
        }
        Instance = this;
   }
}
