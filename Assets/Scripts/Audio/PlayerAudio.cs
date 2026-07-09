using FMODUnity;
using UnityEngine;
using FMOD.Studio;

/*
 * Player-emitted sounds. Lives on the player object - the same object as the
 * Animator, which matters: animation events can only call methods on
 * components of the GameObject holding the Animator. (If the Animator ever
 * moves to a child sprite object, add a small relay component there that
 * forwards to this one.)
 *
 * Callers stay oblivious of surfaces: all one-shots go through
 * PlayOnSurface, which stamps the current surface from the SurfaceDetector
 * onto the FMOD event as the "SurfaceType" labeled parameter.
 * Surface-dependent layering (footstep material, turn scuff) is authored
 * inside the FMOD events themselves.
 */
[RequireComponent(typeof(SurfaceDetector))]
public class PlayerAudio : MonoBehaviour
{
    private SurfaceDetector surfaceDetector;

    public void Awake()
    {
        surfaceDetector = GetComponent<SurfaceDetector>();
    }

    // called by animation event on the walk cycle
    public void PlayFootstep()
    {
        PlayOnSurface(FMODEvents.Instance.Footsteps);
    }

    // called by PlayerMovement when the facing direction changes sharply
    public void PlayTurn()
    {
        PlayOnSurface(FMODEvents.Instance.PlayerTurn);
    }

    private void PlayOnSurface(EventReference eventReference)
    {
        if (eventReference.IsNull)
        {
            return;
        }

        EventInstance instance = RuntimeManager.CreateInstance(eventReference);

        instance.setParameterByNameWithLabel(
            "SurfaceType",
            surfaceDetector.CurrentSurface.ToString());

        RuntimeManager.AttachInstanceToGameObject(
            instance,
            gameObject);

        instance.start();
        instance.release();
    }
}
