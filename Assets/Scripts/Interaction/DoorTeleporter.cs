using UnityEngine;

public class DoorTeleporter : InteractableTrigger
{
  [SerializeField] private string targetSceneName;
  [SerializeField] private string targetSpawnId;

  public override void Interact()
  {
    PropAudio audio = GetComponentInParent<PropAudio>();
    if (audio != null) audio.PlayInteract();
    RoomLoader.Instance.TeleportTo(targetSceneName, targetSpawnId);
  }
}
