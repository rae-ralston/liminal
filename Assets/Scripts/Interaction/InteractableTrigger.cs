using UnityEngine;

public abstract class InteractableTrigger : MonoBehaviour, IInteractable
{
  protected Transform player;
  public abstract void Interact();

  private void OnTriggerEnter2D(Collider2D other)
  {
    if (other.CompareTag("Player")) {
      player = other.transform;
      other.GetComponent<PlayerMovement>().SetInteractable(this);
    }
  }

  private void OnTriggerExit2D(Collider2D other)
  {
    if (other.CompareTag("Player")) {
      player = null;
      other.GetComponent<PlayerMovement>().ClearInteractable();
    }
  }
}
