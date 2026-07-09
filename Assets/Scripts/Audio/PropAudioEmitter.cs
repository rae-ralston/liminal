using FMOD.Studio;
using FMODUnity;
using UnityEngine;

public class PropAudioEmitter : MonoBehaviour
{
    [SerializeField]
    private PropType propType;

    private StudioEventEmitter emitter;
    private EventInstance instance;

    private void Start()
    {
        AudioManager.instance.CreateEmitterInstance(instance, propType, gameObject);
    }    
}