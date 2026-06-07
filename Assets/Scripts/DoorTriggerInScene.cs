using UnityEngine;
using Unity.Cinemachine;

public class DoorTriggerInScene : MonoBehaviour, IInteractable
{
  [SerializeField] private Transform destination;
  private Transform player;

  public void Interact()
  {

    Vector3 delta = destination.position - player.position;
    player.position = destination.position;
    CinemachineCore.OnTargetObjectWarped(player, delta);
  }

  private void OnTriggerEnter2D(Collider2D other)
  {
    if (other.CompareTag("Player"))
    {
      player = other.transform;
      other.GetComponent<PlayerMovement>().SetInteractable(this);
    }
  }

  private void OnTriggerExit2D(Collider2D other)
  {
    if (other.CompareTag("Player"))
    {
      player = null;
      other.GetComponent<PlayerMovement>().ClearInteractable();
    }
  }
}
