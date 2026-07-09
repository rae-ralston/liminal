using System.Collections.Generic;
using UnityEngine;
using FMODUnity;
using FMOD.Studio;

/*
 * The AudioManager handles game-wide audio events
 *
 * ### AMBIENCE BEDS
 * to be added
 *
 * ### EMITTERS on gameObjects
 * How to attach audio event to gameObjects:
 * Add new entry to enum in PropType.cs
 * Add new 'Prop Audio Entry' to FMODEvents gameObject via inspector. 
 * Choose the new Type from dropdown and if available connect to Event Reference from FMOD in the field below it
 * Add new prefab or gameObject to scene and add component PropAudioEmitter
 * On PropAudioEmitter choose corresponding type from list
 * Adding the actual audio event to a gameObject is done by adding the corresponding field to the FMODevents.cs
 * Calling the event is done by calling a method of the audioManager object and passing arguments
 *
 * ### PLAYER AUDIO EVENTS
 * Note: for methods to be available on Animations, they have to be part of a component attached to the animated object, 
 * in this case the player object
 */
public class AudioManager : MonoBehaviour    
{    
    private List<EventInstance> eventInstances;         
    private List<StudioEventEmitter> eventEmitters;     
    private EventInstance ambienceEventInstance;        
    private EventInstance musicEventInstance;           

    public static AudioManager instance { get; private set; }
   
    private void Awake() 
    {
        if (instance != null) 
        {
            Debug.LogError("Found more than one Audio Manager in the scene");
        }
        instance = this;

        eventInstances = new List<EventInstance>();
        eventEmitters = new List<StudioEventEmitter>();
    }

    void Start()
    {
        InitializeAmbience(FMODEvents.instance.ambience);
    }

    private void InitializeAmbience(EventReference ambienceEventReference)
    {
        ambienceEventInstance = CreateEventInstance(ambienceEventReference);
        ambienceEventInstance.start();
    }

    public void PlayMusic(EventReference musicEventReference)
    {
        musicEventInstance = CreateEventInstance(musicEventReference);
        musicEventInstance.start();
    }

    public void SetAmbienceParameter(string parameterName, float parameterValue)
    {
        ambienceEventInstance.setParameterByName(parameterName, parameterValue);
    }

    public void PlayOneShot(EventReference eventReference, Vector3 worldPos) 
    {
        RuntimeManager.PlayOneShot(eventReference, worldPos);
    }

    /*
     * Play events attached to gameObjects
     */
    public StudioEventEmitter InitializeEventEmitter(EventReference eventReference, GameObject emitterGameObject)
    {
        StudioEventEmitter emitter = emitterGameObject.GetComponent<StudioEventEmitter>();
        
        if (emitter == null)
        {
            emitter = emitterGameObject.AddComponent<StudioEventEmitter>();
        }

        emitter.EventReference = eventReference;
        emitter.Play();

        eventEmitters.Add(emitter);
        return emitter;
    }

    public void CreateEmitterInstance(EventInstance eventInstance, PropType PropType, GameObject gameObject)
    {
        EventReference eventRef = FMODEvents.instance.GetPropEvent(PropType);
        eventInstance = RuntimeManager.CreateInstance(eventRef);

        RuntimeManager.AttachInstanceToGameObject(
            eventInstance,
            gameObject
        );
        eventInstance.start();
    }

    /*
     * Play events freely, not attached to gameObjects
     */
    public EventInstance CreateEventInstance(EventReference eventReference)
    {
        EventInstance eventInstance = RuntimeManager.CreateInstance(eventReference);
        eventInstances.Add(eventInstance);
        return eventInstance;
    }
    
    private void CleanUp()
    {
        // remove all event instances from memory
        foreach (EventInstance eventInstance in eventInstances)
        {
            eventInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            eventInstance.release();
        }
        // remove all sounds that were emitted by object from memory
        foreach (StudioEventEmitter emitter in eventEmitters)
        {
            emitter.Stop();
        }
    }

    private void OnDestroy()
    { 
        CleanUp(); 
    }
}
