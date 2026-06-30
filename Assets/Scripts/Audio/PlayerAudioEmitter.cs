using FMODUnity;
using UnityEngine;
using FMOD.Studio;

[RequireComponent(typeof(SurfaceDetector))]
public class PlayerAudioEmitter : MonoBehaviour
{
    [SerializeField]
    private PlayerAudioType playerAudioType;
    
    private SurfaceDetector surfaceDetector;

    public void Awake()
    {
        surfaceDetector = GetComponent<SurfaceDetector>();
    }

    public void PlayFootstepOnSurface()
    {
        SurfaceType surfaceType = surfaceDetector.CurrentSurface;
        
        EventInstance instance = RuntimeManager.CreateInstance(GetEvent());

        instance.setParameterByNameWithLabel(
            "SurfaceType",
            surfaceType.ToString());

        RuntimeManager.AttachInstanceToGameObject(
            instance,
            gameObject);

        instance.start();
        instance.release();
    }

    private EventReference GetEvent()
    {
        switch (playerAudioType)
        {
            case PlayerAudioType.footsteps:
                return FMODEvents.instance.footsteps;

            default:
                return default;
        }
    }
    
}