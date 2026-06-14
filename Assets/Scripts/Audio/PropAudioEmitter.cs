using FMODUnity;
using UnityEngine;

[RequireComponent(typeof(StudioEventEmitter))]
public class PropAudioEmitter : MonoBehaviour
{
    [SerializeField]
    private PropAudioType propType;

    private StudioEventEmitter emitter;

    private void Start()
    {
        AudioManager.instance.InitializeEventEmitter(this.GetEvent(), this.gameObject);
    }

    private EventReference GetEvent()
    {
        switch (propType)
        {
            case PropAudioType.Computer:
                return FMODEvents.instance.computer;

            case PropAudioType.Hvac:
                return FMODEvents.instance.hvac;

            case PropAudioType.vendingMachine:
                return FMODEvents.instance.vendingMachine; 

            default:
                return default;
        }
    }
}