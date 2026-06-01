using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FMODUnity;
using FMOD.Studio;

[CreateAssetMenu(menuName = "SO/Audio/Player", fileName = "New Player Sheet")]
public class PlayerAudioData : ScriptableObject
{
    [Header("Movement")]
    public EventReference footsteps;
    public EventReference jump;
    public EventReference dash;
    public EventReference land;

    [Header("Attacks")]
    public EventReference[] swordAttacks;
    public EventReference hammerAttack;
    public EventReference shield;
}