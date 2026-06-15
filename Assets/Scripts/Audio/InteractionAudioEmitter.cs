using FMOD.Studio;
using FMODUnity;
using UnityEngine;

//[RequireComponent(typeof(StudioEventEmitter))]
public class InteractionAudioEmitter : MonoBehaviour
{
    [SerializeField]
    private InteractionAudioType interactionType;

    //private StudioEventEmitter emitter;
    private EventInstance instance;

    public void PlayOneShot()
    {
        AudioManager.instance.PlayOneShot(FMODEvents.instance.GetInteractionEvent(interactionType), transform.position);
    }
}