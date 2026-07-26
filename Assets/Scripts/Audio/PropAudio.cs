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
    public enum AmbientState { Active, Inactive, Suspended }

    [SerializeField] private PropAudioDefinition sounds;
    [Tooltip("Start the ambient loop automatically on enable. Turn OFF for props whose loop is gated by a controller - e.g. the starter button, whose tick IncrementalStartEndButtonGlow starts only once the bootstrap room is activated.")]
    [SerializeField] private bool autoStartAmbientLoop = true;

    private EventInstance ambientInstance;
    private bool ambientPlaying;
    private AmbientState ambientState = AmbientState.Active; // props that never call SetAmbientState keep the old unconditional loop

    private EventInstance chargeInstance;
    private bool chargePlaying;

    private const string DoorTypeParameter = "DoorType";
    private const string ChargeParameter = "Charge";

    private void OnEnable()
    {
        if (autoStartAmbientLoop)
        {
            StartAmbientLoop();
        }
    }

    private void OnDisable()
    {
        StopAmbientLoop();
        // Room unload mid-charge: kill the riser, no completion chime.
        StopCharge(false);
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

    public void StartAmbientLoop()
    {
        EventReference loop = CurrentAmbientLoop();
        if (ambientPlaying || loop.IsNull)
        {
            return;
        }

        ambientInstance = RuntimeManager.CreateInstance(loop);
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

    // Swaps which ambient loop plays (e.g. a terminal dark / live / suspended
    // mid-transition). No-op if the state didn't change; restarts the loop
    // instance if already playing so props that never call this keep today's
    // unconditional-loop behavior.
    public void SetAmbientState(AmbientState state)
    {
        if (state == ambientState)
        {
            return;
        }

        ambientState = state;

        if (!ambientPlaying)
        {
            return;
        }

        StopAmbientLoop();
        StartAmbientLoop();
    }

    private EventReference CurrentAmbientLoop()
    {
        if (sounds == null)
        {
            return default;
        }

        switch (ambientState)
        {
            case AmbientState.Inactive:  return sounds.ambientLoopInactive;
            case AmbientState.Suspended: return sounds.ambientLoopSuspended;
            default:                     return sounds.ambientLoop;
        }
    }

    // ------------------------------------------------------------------
    // Charge (light-fed props) - the anticipation -> release arc. A looping
    // riser driven by a continuous "Charge" parameter (0-1; the FMOD event's
    // parameter name must match ChargeParameter exactly), resolved by the
    // chargeComplete one-shot. Same owned-instance lifecycle as the ambient
    // loop: created here, released here, nothing leaks across room loads.
    // ------------------------------------------------------------------

    public void StartCharge()
    {
        if (chargePlaying || sounds == null || sounds.chargeLoop.IsNull)
        {
            return;
        }

        chargeInstance = RuntimeManager.CreateInstance(sounds.chargeLoop);
        RuntimeManager.AttachInstanceToGameObject(chargeInstance, gameObject);
        chargeInstance.setParameterByName(ChargeParameter, 0f);
        chargeInstance.start();
        chargePlaying = true;
    }

    public void SetChargeProgress(float normalized)
    {
        if (!chargePlaying)
        {
            return;
        }

        chargeInstance.setParameterByName(ChargeParameter, Mathf.Clamp01(normalized));
    }

    // completed = true plays the chargeComplete confirm (even if no loop was
    // authored); false is the decay/abort case and just kills the loop.
    public void StopCharge(bool completed)
    {
        if (chargePlaying)
        {
            chargeInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            chargeInstance.release();
            chargePlaying = false;
        }

        if (completed)
        {
            PlayOneShot(sounds != null ? sounds.chargeComplete : default);
        }
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
