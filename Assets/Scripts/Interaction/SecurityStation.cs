using UnityEngine;

public class SecurityStation : InteractableTrigger
{
  public override void Interact()
  {
    GameManager.Instance.StartGame();
  }
}
