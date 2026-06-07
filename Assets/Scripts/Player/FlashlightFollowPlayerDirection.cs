using UnityEngine;

public class FlashlightFollowPlayerDirection : MonoBehaviour
{
  [SerializeField] private PlayerMovement playerMovement;
  void Update()
  {
    Vector2 playerDirection = playerMovement.FacingDirection;
    float angle = Mathf.Atan2(playerDirection.y, playerDirection.x) * Mathf.Rad2Deg - 90f;
    transform.rotation = Quaternion.Euler(0, 0, angle);
  }
}
