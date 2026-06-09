using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FMODUnity;

public class FMODEvents : MonoBehaviour
{
    [field: Header("Footsteps")]
    [field: SerializeField] public EventReference footstepSound { get; private set; }
    public static FMODEvents instance { get; private set; }

    private void Awake() 
    {
        if (instance != null) 
        {
            Debug.LogError("Found more htan one FMOD Event instance in the scene.");
        }
        instance = this;
    }
}
