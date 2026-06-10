using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public enum SoundType {
    FOOTSTEP,
    DRAWER,
    DOOR
}

public class AudioManager : MonoBehaviour    
{
    private List<EventInstance> eventInstances;
    public static AudioManager instance { get; private set; }

    /*
     * singleton pattern
     * create a singleton instance of the AudioManager
     */
    private void Awake() 
    {
        if (instance != null) 
        {
            Debug.LogError("Found more than one Audio Manager in the scene");
        }
        instance = this;

        eventInstances = new List<EventInstance>();
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
     * Create an instance of an audio event 
     */
    /*
    public EventInstance CreateEventInstance(EventReference eventReference)
    {
        EventInstance eventInstance = RuntimeManager.CreateInstance(eventReference);
        //if (eventInstance != null)
        //{
            eventInstance.Add(eventInstance);
            return eventInstance;
        //}        
    }
    */

    /*
     * removes every entry from the list EventInsances
     */
    private void CleanUp()
    {
        foreach (EventInstance eventInstance in eventInstances)
        {
            eventInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            eventInstance.release();
        }
    }

    /*
     * When the AudioManager is destroyed, i.e. loading a new scene or reloading the current scene, this function will be called.
     */
    private void OnDestroy()
    { 
        CleanUp(); 
    }
}
