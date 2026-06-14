using FMODUnity;
using UnityEngine;

public class PlayerAudioEmitter : MonoBehaviour
{
    [SerializeField]
    private PlayerAudioType playerAudioType;

    public void PlayOneShot()
    {
        AudioManager.instance.PlayOneShot(this.GetEvent(), this.transform.position);
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