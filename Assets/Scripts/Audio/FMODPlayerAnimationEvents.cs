using UnityEngine;

public class FMODPlayerAnimationEvents : MonoBehaviour
{    
    // called by animation event
    public void OnFootstep()
    {
        this.GetComponentInParent<PlayerAudioEmitter>().PlayOneShot();
    }
}