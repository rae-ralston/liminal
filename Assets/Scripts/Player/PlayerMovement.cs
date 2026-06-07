using UnityEngine;
using UnityEngine.InputSystem;


public class PlayerMovement : MonoBehaviour
{
  private Rigidbody2D rb;
  public float moveSpeed = 5f;
  private InputAction moveAction;
  private Animator animator;
  private SpriteRenderer spriteRenderer;
  private InputAction interactAction;
  private IInteractable currentInteractable;

  // Start is called once before the first execution of Update after the MonoBehaviour is created
  void Start()
  {
    rb = GetComponent<Rigidbody2D>();
    moveAction = InputSystem.actions.FindAction("Move");
    animator = GetComponent<Animator>();
    spriteRenderer = GetComponent<SpriteRenderer>();
    interactAction = InputSystem.actions.FindAction("Interact");
  }

  // Update is called once per frame
  void Update()
  {
    Vector2 movement = moveAction.ReadValue<Vector2>();
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
