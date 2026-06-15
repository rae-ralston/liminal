using System.Collections.Generic;
using UnityEngine;
using FMODUnity;
using System.ComponentModel;

public class FMODEvents : MonoBehaviour
{       
    public static FMODEvents instance { get; private set; }

    // add fields to the inspector to choose FMOD events from a dropdown menu

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
    [SerializeField]
    private List<InteractionAudioEntry> interactionAudioEntries;    
    private Dictionary<InteractionAudioType, EventReference> interactionAudioLookup;


    private void Awake() 
    {
        if (instance != null) 
        {
            Debug.LogError("Found more htan one FMOD Ambience Event instance in the scene.");
        }
        instance = this;    

        CreatePropAudioDictionary();            
        CreateInteractionAudioDictionary();            
   }

   private void CreatePropAudioDictionary()
    {
        propAudioLookup = new Dictionary<PropAudioType, EventReference>();

        foreach (var entry in propAudioEntries)
        {
            propAudioLookup[entry.type] = entry.eventReference;
        } 
    }
   private void CreateInteractionAudioDictionary()
    {
        interactionAudioLookup = new Dictionary<InteractionAudioType, EventReference>();

        foreach (var entry in interactionAudioEntries)
        {
            interactionAudioLookup[entry.type] = entry.eventReference;
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

    public EventReference GetInteractionEvent(InteractionAudioType type)
    {
        if (interactionAudioLookup.TryGetValue(type, out EventReference evt))
        {
            return evt;
        }

        Debug.LogWarning($"No FMOD event assigned for {type}. Go to FMODEvents object and add the event under Interaction Audio Emitters");
        return default;
    }
}
