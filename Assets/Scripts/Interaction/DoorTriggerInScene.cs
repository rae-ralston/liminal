using UnityEngine;
using Unity.Cinemachine;

public class DoorTriggerInScene : InteractableTrigger
{
  [SerializeField] private Transform destination;
  public override void Interact()
  {
    Vector3 delta = destination.position - player.position;
    player.position = destination.position;
    CinemachineCore.OnTargetObjectWarped(player, delta);
    PropAudio audio = GetComponentInParent<PropAudio>();
    if (audio != null) audio.PlayInteract();
  }
}
