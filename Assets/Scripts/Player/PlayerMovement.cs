using UnityEngine;
using UnityEngine.InputSystem;


public class PlayerMovement : MonoBehaviour
{
  private Rigidbody2D rb;
  public float moveSpeed = 5f;
  public Vector2 FacingDirection { get; private set; } = Vector2.down;
  private InputAction moveAction;
  private Animator animator;
  private SpriteRenderer spriteRenderer;
  private InputAction interactAction;
  private IInteractable currentInteractable;

  void Start()
  {
    rb = GetComponent<Rigidbody2D>();
    moveAction = InputSystem.actions.FindAction("Move");
    animator = GetComponent<Animator>();
    spriteRenderer = GetComponent<SpriteRenderer>();
    interactAction = InputSystem.actions.FindAction("Interact");
  }

  void Update()
  {
    Vector2 movement = moveAction.ReadValue<Vector2>();
    if (movement.sqrMagnitude > 0.01f)
      FacingDirection = movement.normalized;

    rb.linearVelocity = movement * moveSpeed;

    animator.SetFloat("Speed", movement.magnitude);
    animator.SetFloat("DirectionX", movement.x);
    animator.SetFloat("DirectionY", movement.y);

    if (movement.x < 0)
      spriteRenderer.flipX = true;
    else if (movement.x > 0)
      spriteRenderer.flipX = false;
    
    if (interactAction.WasPressedThisFrame() && currentInteractable != null)
      currentInteractable.Interact();
  }
    
  public void SetInteractable(IInteractable interactable)
  {
    currentInteractable = interactable;
  }

  public void ClearInteractable()
  {
    currentInteractable = null;
  }
}
