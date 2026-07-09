using System.Collections.Generic;
using UnityEngine;
using FMODUnity;
using FMOD.Studio;

/*
 * The AudioManager handles game-wide audio: ambience, music and mixer buses.
 *
 * ### PROP SOUNDS on gameObjects
 * How to attach audio events to props (no code changes needed):
 * Create a sound set asset: right-click in Project window > Create > Audio > Prop Audio Definition
 * Fill in the FMOD event slots (ambient loop, interact, ...) - empty slots stay silent
 * Add the PropAudio component to the prop prefab and assign the definition asset
 * Gameplay scripts trigger one-shots via PropAudio methods, e.g. PlayInteract();
 * the ambient loop starts/stops automatically with the GameObject
 *
 * ### PLAYER AUDIO EVENTS
 * Note: for methods to be available on Animations, they have to be part of a component attached to the animated object,
 * in this case the player object
 */
public class AudioManager : MonoBehaviour
{
    // paths into the FMOD Studio mixer; adjust here if the buses get renamed
    [Header("Mixer bus paths")]
    [SerializeField] private string musicBusPath = "bus:/Music";
    [SerializeField] private string sfxBusPath = "bus:/SFX";

    private List<EventInstance> eventInstances;
    private EventInstance ambienceEventInstance;
    private EventInstance musicEventInstance;

    private Bus masterBus;
    private Bus musicBus;
    private Bus sfxBus;

    public static AudioManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogError("Found more than one Audio Manager in the scene");
        }
        Instance = this;

        eventInstances = new List<EventInstance>();
    }

    void Start()
    {
        InitializeBuses();
        InitializeAmbience(FMODEvents.Instance.Ambience);
    }

    /*
     * Ambience
     */
    private void InitializeAmbience(EventReference ambienceEventReference)
    {
        if (ambienceEventReference.IsNull)
        {
            Debug.LogWarning("No ambience event assigned on the FMODEvents object.");
            return;
        }

        ambienceEventInstance = CreateEventInstance(ambienceEventReference);
        ambienceEventInstance.start();
    }

    public void SetAmbienceParameter(string parameterName, float parameterValue)
    {
        if (!ambienceEventInstance.isValid())
        {
            return;
        }

        ambienceEventInstance.setParameterByName(parameterName, parameterValue);
    }

    /*
     * Music
     */
    public void PlayMusic(EventReference musicEventReference)
    {
        StopMusic();

        if (musicEventReference.IsNull)
        {
            return;
        }

        musicEventInstance = RuntimeManager.CreateInstance(musicEventReference);
        musicEventInstance.start();
    }

    public void StopMusic()
    {
        if (!musicEventInstance.isValid())
        {
            return;
        }

        musicEventInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        musicEventInstance.release();
    }

    /*
     * Volume control (0..1), e.g. for options menu sliders
     */
    public void SetMasterVolume(float volume)
    {
        masterBus.setVolume(Mathf.Clamp01(volume));
    }

    public void SetMusicVolume(float volume)
    {
        musicBus.setVolume(Mathf.Clamp01(volume));
    }

    public void SetSfxVolume(float volume)
    {
        sfxBus.setVolume(Mathf.Clamp01(volume));
    }

    private void InitializeBuses()
    {
        masterBus = RuntimeManager.GetBus("bus:/");
        musicBus = GetBusSafe(musicBusPath);
        sfxBus = GetBusSafe(sfxBusPath);
    }

    // volume calls on a missing bus are silently ignored by FMOD,
    // so a bad path degrades to a warning instead of breaking audio
    private Bus GetBusSafe(string busPath)
    {
        try
        {
            return RuntimeManager.GetBus(busPath);
        }
        catch (BusNotFoundException)
        {
            Debug.LogWarning($"FMOD bus '{busPath}' not found. Create it in the FMOD Studio mixer or fix the path on the AudioManager.");
            return default;
        }
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
        StopMusic();

        // remove all event instances from memory
        foreach (EventInstance eventInstance in eventInstances)
        {
            eventInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            eventInstance.release();
        }
    }

    private void OnDestroy()
    {
        CleanUp();
    }
}
