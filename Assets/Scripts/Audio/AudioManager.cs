using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using FMODUnity;

public enum SoundType {
    FOOTSTEP,
    DRAWER,
    DOOR
}

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance { get; private set; }

    /*
    * create a singleton instance of the AudioManager
    */
    private void Awake() 
    {
        if (instance != null) 
        {
            Debug.LogError("Found more than one Audio Manager in the scene");
        }
        instance = this;
    }

    /*
    * play a single audio event
    */
    public void PlayOneShot(EventReference sound, Vector3 worldPos) 
    {
        RuntimeManager.PlayOneShot(sound, worldPos);
    }
}
