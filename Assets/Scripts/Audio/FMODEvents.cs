using System.Collections.Generic;
using UnityEngine;
using FMODUnity;

public class FMODEvents : MonoBehaviour
{       
    public static FMODEvents instance { get; private set; }

    [Header("Prop Audio")]

    [SerializeField]
    private List<PropAudioEntry> propAudioEntries;
    private Dictionary<PropAudioType, EventReference> propAudioLookup;


    // add fields to the inspector to choose FMOD events from a dropdown menu
    [field: Header("Ambience")]
    [field: SerializeField] public EventReference ambience { get; private set; }


    [field: Header("Interactables")]
    [field: SerializeField] public EventReference passDoor { get; private set; }
    [field: SerializeField] public EventReference checkDrawer { get; private set; }
    [field: SerializeField] public EventReference pressButton { get; private set; }
    [field: SerializeField] public EventReference pullLever { get; private set; }
    [field: SerializeField] public EventReference lightSwitch { get; private set; }


    [field: Header("Sound Emitters")]
    [field: SerializeField] public EventReference computer { get; private set; }
    [field: SerializeField] public EventReference hvac { get; private set; }
    [field: SerializeField] public EventReference vendingMachine { get; private set; }
    
    

    public Dictionary<PropAudioType, EventReference> PropSounds = new Dictionary<PropAudioType, EventReference>();

    private void Awake() 
    {
        if (instance != null) 
        {
            Debug.LogError("Found more htan one FMOD Ambience Event instance in the scene.");
        }
        instance = this;    

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

        Debug.LogWarning($"No FMOD event assigned for {type}");
        return default;
    }
}
