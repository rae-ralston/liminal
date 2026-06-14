using System.Collections.Generic;
using UnityEngine;
using FMODUnity;
using System.ComponentModel;

public class FMODEvents : MonoBehaviour
{       
    public static FMODEvents instance { get; private set; }

    // horizontal bar in inspector
    [field: Header("_____________________________________________________________________________________________________________________________")]   
    
     // Player Audio Entries
    [field: Header("Player Audio")]        
    [field: SerializeField] public EventReference footsteps { get; private set; }


    // horizontal bar in inspector
    [Header("_____________________________________________________________________________________________________________________________")]   

    // Prop Audio Entries
    [field: Header("Prop Audio Emitters")]    
    [SerializeField]
    private List<PropAudioEntry> propAudioEntries;    
    private Dictionary<PropAudioType, EventReference> propAudioLookup;

    
    // horizontal bar in inspector
    [field: Header("_____________________________________________________________________________________________________________________________")]   
    
    // Ambience Audio
    [field: Header("Ambience")]    
    [field: SerializeField] public EventReference ambience { get; private set; }


    // horizontal bar in inspector
    [field: Header("_____________________________________________________________________________________________________________________________")]   

    // Interaction Audio
    [field: Header("Interactables")]        
    [field: SerializeField] public EventReference passDoor { get; private set; }
    [field: SerializeField] public EventReference checkDrawer { get; private set; }
    [field: SerializeField] public EventReference pressButton { get; private set; }
    [field: SerializeField] public EventReference pullLever { get; private set; }
    [field: SerializeField] public EventReference lightSwitch { get; private set; }




    // add fields to the inspector to choose FMOD events from a dropdown menu
    
    
    

    public Dictionary<PropAudioType, EventReference> PropSounds = new Dictionary<PropAudioType, EventReference>();
    public Dictionary<PlayerAudioType, EventReference> PlayerSounds = new Dictionary<PlayerAudioType, EventReference>();

    private void Awake() 
    {
        if (instance != null) 
        {
            Debug.LogError("Found more htan one FMOD Ambience Event instance in the scene.");
        }
        instance = this;    

        // Prop Audio
        propAudioLookup = new Dictionary<PropAudioType, EventReference>();

        foreach (var entry in propAudioEntries)
        {
            propAudioLookup[entry.type] = entry.eventReference;
        }         
   }

    public EventReference GetPropEvent(PropAudioType type)
    {
        if (propAudioLookup.TryGetValue(type, out EventReference evt))
        {
            return evt;
        }

        Debug.LogWarning($"No FMOD event assigned for {type}. Go to FMODEvents object and add the event under Prop Audio Emitters");
        return default;
    }
}
