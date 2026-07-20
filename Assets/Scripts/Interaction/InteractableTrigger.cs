using UnityEngine;

public abstract class InteractableTrigger : MonoBehaviour, IInteractable
{
  protected Transform player;
  private PlayerMovement playerMovement;
  public abstract void Interact();

  private void OnTriggerEnter2D(Collider2D other)
  {
    if (other.CompareTag("Player")) {
      player = other.transform;
      playerMovement = other.GetComponent<PlayerMovement>();
      playerMovement.AddInteractable(this);
    }
  }

  private void OnTriggerExit2D(Collider2D other)
  {
    if (other.CompareTag("Player")) {
      ClearFromPlayer();
    }
  }

  private void OnDisable()
  {
    ClearFromPlayer();
  }

  private void ClearFromPlayer()
  {
    if (playerMovement != null)
      playerMovement.RemoveInteractable(this);
    player = null;
    playerMovement = null;
  }
}
