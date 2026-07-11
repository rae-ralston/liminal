using FMOD.Studio;
using FMODUnity;
using UnityEngine;

/*
 * The single audio component for props. Drop it on a prop prefab and assign
 * a PropAudioDefinition asset - no code changes needed for new props.
 *
 * Gameplay components stay FMOD-agnostic and just call the intent-named
 * methods, null-checked so a prop without audio is simply silent:
 *
 *     var audio = GetComponent<PropAudio>();
 *     if (audio != null) audio.PlayInteract();
 *
 * Owns the lifecycle of its ambient loop instance: started on enable,
 * stopped and released on disable, so nothing leaks across room loads.
 */
public class PropAudio : MonoBehaviour
{
    [SerializeField] private PropAudioDefinition sounds;

    private EventInstance ambientInstance;
    private bool ambientPlaying;

    private void OnEnable()
    {
        StartAmbientLoop();
    }

    private void OnDisable()
    {
        StopAmbientLoop();
    }

    public void PlayInteract() => PlayOneShot(sounds != null ? sounds.interact : default);
    public void PlayLocked()   => PlayOneShot(sounds != null ? sounds.locked : default);
    public void PlayTurnOn()   => PlayOneShot(sounds != null ? sounds.turnOn : default);
    public void PlayTurnOff()  => PlayOneShot(sounds != null ? sounds.turnOff : default);

    // Doors: one "door" event chosen by a labeled "DoorType" parameter, reusing
    // the generic slots - assign the parameterized door-open event to the
    // definition's 'interact' slot and the rattle to 'locked'. The DoorType enum
    // name is passed as the parameter label, so FMOD's "DoorType" parameter
    // labels must match the enum members exactly.
    public void PlayOpen(DoorType doorType)   => PlayOneShotWithLabel(sounds != null ? sounds.interact : default, DoorTypeParameter, doorType.ToString());
    public void PlayLocked(DoorType doorType) => PlayOneShotWithLabel(sounds != null ? sounds.locked : default, DoorTypeParameter, doorType.ToString());

    private const string DoorTypeParameter = "DoorType";

    public void StartAmbientLoop()
    {
        if (ambientPlaying || sounds == null || sounds.ambientLoop.IsNull)
        {
            return;
        }

        ambientInstance = RuntimeManager.CreateInstance(sounds.ambientLoop);
        RuntimeManager.AttachInstanceToGameObject(ambientInstance, gameObject);
        ambientInstance.start();
        ambientPlaying = true;
    }

    public void StopAmbientLoop()
    {
        if (!ambientPlaying)
        {
            return;
        }

        ambientInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        ambientInstance.release();
        ambientPlaying = false;
    }

    private void PlayOneShot(EventReference eventReference)
    {
        if (eventReference.IsNull)
        {
            return;
        }

        RuntimeManager.PlayOneShot(eventReference, transform.position);
    }

    // Fire-and-forget one-shot that also hands a labeled parameter to FMOD.
    // RuntimeManager.PlayOneShot owns its instance internally and gives no
    // handle to set a parameter, so we create the instance ourselves, set the
    // label, start it, and release immediately - FMOD frees it when it ends.
    private void PlayOneShotWithLabel(EventReference eventReference, string parameterName, string label)
    {
        if (eventReference.IsNull)
        {
            return;
        }

        EventInstance instance = RuntimeManager.CreateInstance(eventReference);
        instance.set3DAttributes(RuntimeUtils.To3DAttributes(transform.position));
        instance.setParameterByNameWithLabel(parameterName, label);
        instance.start();
        instance.release();
    }
}
