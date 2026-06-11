using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using FMODUnity;
using FMOD.Studio;
//using FMODEvents;
using System.Dynamic;

public enum SoundType {
    FOOTSTEP,
    DRAWER,
    DOOR
}

/*
 * The AudioManager handles game-wide audio events
 * How audio is added, triggered and processed
 *
 * Adding audio events to gameObjects is done by adding the corresponding field to the FMODevents.cs
 * Calling the event is done by calling a function of the audioManager object and passing arguments
 *
 * Example: play a footstep event
 * In the Animation window "Add event" adds a an empty call for an event
 * Select the added event
 * Then switch to in the inspector tab: choose the function to be fired by the event, here onFootstep()
 * Note: for functions to be available here, they have to be part of a compontent attached to the animated object, in this case the player object
 * onFootstep() is defined in FMODPlayerAnimationEvents.cs, which is a component of the player object
 * It calls AudioManager.playOneShot(eventReference, worldPos)
 * The AudioManager then handles the execution through FMOD's RuntimeManager and the event is played
 */
public class AudioManager : MonoBehaviour    
{    
    private List<EventInstance> eventInstances;         // create a list/array to store all playing audio events    
    private List<StudioEventEmitter> eventEmitters;     // create a list/array to store all emitter events audio events

    private EventInstance ambienceEventInstance;        // create a variable for storing a ambience event
    private EventInstance musicEventInstance;           // create a variable for storing a music event

    public static AudioManager instance { get; private set; } // create a publicly accessable instance of the AudioManager

    /*
     * singleton pattern
     * create a singleton instance of the AudioManager when an instance is created
     */
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

    /*
     * will start playing the ambience loop set in FMODEvents
     * the FMODEvents class is added as a component to a gameObject of the same name
     */
    private void InitializeAmbience(EventReference ambienceEventReference)
    {
        ambienceEventInstance = CreateEventInstance(ambienceEventReference);
        ambienceEventInstance.start();
    }

    /*
     * 
     */
    public void PlayMusic(EventReference musicEventReference)
    {
        musicEventInstance = CreateEventInstance(musicEventReference);
        musicEventInstance.start();
    }

    public void SetAmbienceParameter(string parameterName, float parameterValue)
    {
        ambienceEventInstance.setParameterByName(parameterName, parameterValue);
    }

    /*
     * Fire and forget an audio event at the given location.
     * This will immediately spawn an instance of the given event at a location.
     * The instance will play to completion. 
     * Parameters cannot be set.
     */
    public void PlayOneShot(EventReference eventReference, Vector3 worldPos) 
    {
        RuntimeManager.PlayOneShot(eventReference, worldPos);
    }

    /*
     * gameObjects can call this method and pass an audio event
     * usefull for props that emit sound
     */
    public StudioEventEmitter InitializeEventEmitter(EventReference eventReference, GameObject emitterGameObject)
    {
        StudioEventEmitter emitter = emitterGameObject.GetComponent<StudioEventEmitter>();  // get emitter from gameObject
        emitter.EventReference = eventReference;                                            // overwrite EventReference to the one passed to this method
        eventEmitters.Add(emitter);                                                         // add emitter to the list for when cleanUp is called
        return emitter;
    }

    /*
     * Create an instance of an audio event, that is played immediately
     * this can be used for playing loops and music at any time
     */
    public EventInstance CreateEventInstance(EventReference eventReference)
    {
        EventInstance eventInstance = RuntimeManager.CreateInstance(eventReference);        // create an instance of the audio event
        eventInstances.Add(eventInstance);                                                  // add event to the list for when cleanUp is called
        return eventInstance;
    }
    
    /*
     * removes every entry from the list EventInsances
     */
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

    /*
     * When the AudioManager is destroyed, i.e. by loading a new scene or reloading the current scene, this function will be called.
     */
    private void OnDestroy()
    { 
        CleanUp(); 
    }
}
