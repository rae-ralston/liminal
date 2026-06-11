using UnityEngine;
using FMODUnity;

public class FMODPlayerAnimationEvents : MonoBehaviour
{
    [SerializeField] private EventReference footstepEvent;

    // Diese Methode wird vom Animation Event aufgerufen
    public void OnFootstep()
    {
        AudioManager.instance.PlayOneShot(footstepEvent, transform.position);
    }
}