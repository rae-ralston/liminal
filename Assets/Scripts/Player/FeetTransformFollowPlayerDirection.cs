using UnityEngine;

public class SurfaceDetectorFollowPlayerDirection : MonoBehaviour
{
  [SerializeField] private PlayerMovement playerMovement;
  [SerializeField] private float walkUpOffset = 1f;
  [SerializeField] private float walkDownOffset = -2f;
  [SerializeField] private float walkLeftRightXOffset = 1f;
  [SerializeField] private float verticalOffsetForLeftAndRight = -1f;

  void Update()
  {
    PositionSurfaceDetector();
  }


  public void PositionSurfaceDetector()
  {
    Vector2 facing = playerMovement.FacingDirection;
    Vector3 position;

    if (Mathf.Abs(facing.x) > Mathf.Abs(facing.y))
      position = new Vector3(facing.x * walkLeftRightXOffset, verticalOffsetForLeftAndRight, 0f);
    else if (facing.y > 0f)
      position = new Vector3(0f, walkUpOffset, 0f);
    else
      position = new Vector3(0f, walkDownOffset, 0f);

    transform.localPosition = position;
  }
}
