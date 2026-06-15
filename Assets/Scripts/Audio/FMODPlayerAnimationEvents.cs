using UnityEngine;

public class FMODPlayerAnimationEvents : MonoBehaviour
{    
    // called by animation event
    public void OnFootstep()
    {
        GetComponentInParent<PlayerAudioEmitter>().PlayOneShot();
    }
}