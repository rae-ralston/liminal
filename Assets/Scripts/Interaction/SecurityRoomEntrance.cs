using UnityEngine;

public class SecurityRoomEntrance : InteractableTrigger
{
  [SerializeField] private SecurityRoomView securityRoomView;

  public override void Interact()
  {
    securityRoomView.Show();
  }
}
