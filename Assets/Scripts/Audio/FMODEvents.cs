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

    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogError("Found more than one FMODEvent instance in the scene.");
        }
        Instance = this;
   }
}
