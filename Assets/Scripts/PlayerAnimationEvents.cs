using UnityEngine;
using FMODUnity;

public class PlayerAnimationEvents : MonoBehaviour
{
    [SerializeField] private EventReference footstepEvent;

    // Diese Methode wird vom Animation Event aufgerufen
    public void OnFootstep()
    {
        RuntimeManager.PlayOneShot(footstepEvent, transform.position);
    }
}