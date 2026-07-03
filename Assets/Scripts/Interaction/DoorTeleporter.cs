using UnityEngine;

public class DoorTeleporter : InteractableTrigger
{
  [SerializeField] private string targetSceneName;
  [SerializeField] private string targetSpawnId;

  public override void Interact()
  {
    GetComponentInParent<InteractionAudioEmitter>().PlayOneShot();
    RoomLoader.Instance.TeleportTo(targetSceneName, targetSpawnId);
  }
}
