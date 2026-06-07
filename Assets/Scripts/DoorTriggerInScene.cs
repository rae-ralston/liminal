using UnityEngine;
using Unity.Cinemachine;

public class DoorTriggerInScene : MonoBehaviour
{
  [SerializeField] private Transform destination;

  private void OnTriggerEnter2D(Collider2D other)
  {
    if (other.CompareTag("Player"))
    {
      Vector3 delta = destination.position - other.transform.position;
      other.transform.position = destination.position;
      CinemachineCore.OnTargetObjectWarped(other.transform, delta);
    }
  }
}
