using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FMODUnity;
//using FMOD.Studio;

public class FMODEvents : MonoBehaviour
{    
    // add fields to the inspector to choose FMOD events from a dropdown menu
    [field: Header("Ambience")]
    [field: SerializeField] public EventReference ambience { get; private set; }

    [field: Header("OpenDoor")]
    [field: SerializeField] public EventReference openDoor { get; private set; }

    [field: Header("checkDrawer")]
    [field: SerializeField] public EventReference checkDrawer { get; private set; }

    [field: Header("pressButton")]
    [field: SerializeField] public EventReference pressButton { get; private set; }

    [field: Header("pullLever")]
    [field: SerializeField] public EventReference pullLever { get; private set; }
    
    // create a publicly avaible instance of FMODAmbienceEvents. Methods can be called by FMODAmbienceEvents.methodName()
    public static FMODEvents instance { get; private set; }

    // on Awake() make sure there is only one instance of FMODAmbienceEvents (singleton pattern)
    // also setup the ambienceEventInstances with a fresh empty list for the audio events to store
    private void Awake() 
    {
        if (instance != null) 
        {
            Debug.LogError("Found more htan one FMOD Ambience Event instance in the scene.");
        }
        instance = this;
    }    
}
