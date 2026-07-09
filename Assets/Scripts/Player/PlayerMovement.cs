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

  [Header("Turn sound")]
  [SerializeField] private float turnAngleThreshold = 90f;
  [SerializeField] private float turnCooldown = 0.25f;
  private PlayerAudio playerAudio;
  private float lastTurnTime;

  void Start()
  {
    rb = GetComponent<Rigidbody2D>();
    moveAction = InputSystem.actions.FindAction("Move");
    animator = GetComponent<Animator>();
    spriteRenderer = GetComponent<SpriteRenderer>();
    interactAction = InputSystem.actions.FindAction("Interact");
    playerAudio = GetComponent<PlayerAudio>();
  }

  void Update()
  {
    Vector2 movement = moveAction.ReadValue<Vector2>();
    if (movement.sqrMagnitude > 0.01f)
    {
      Vector2 newDirection = movement.normalized;
      if (playerAudio != null
          && Vector2.Angle(FacingDirection, newDirection) >= turnAngleThreshold
          && Time.time - lastTurnTime >= turnCooldown)
      {
        playerAudio.PlayTurn();
        lastTurnTime = Time.time;
      }
      FacingDirection = newDirection;
    }

    rb.linearVelocity = movement * moveSpeed;

    animator.SetFloat("Speed", movement.magnitude);
    animator.SetFloat("DirectionX", movement.x);
    animator.SetFloat("DirectionY", movement.y);
    animator.SetFloat("LastDirectionX", FacingDirection.x);
    animator.SetFloat("LastDirectionY", FacingDirection.y);

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
