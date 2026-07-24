using System.Collections.Generic;
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
  private readonly List<InteractableTrigger> nearbyInteractables = new List<InteractableTrigger>();

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
    // Frozen once the end card is up (set by EndSequenceController). Zero the
    // velocity ourselves rather than disabling the component, so there's no
    // residual slide and the animator settles to idle.
    if (GameManager.Instance != null && GameManager.Instance.MovementFrozen)
    {
      rb.linearVelocity = Vector2.zero;
      animator.SetFloat("Speed", 0f);
      return;
    }

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
    
    if (interactAction.WasPressedThisFrame())
    {
      // Ending brief E6 step 1: the whole interaction pipeline is gated OFF
      // once the discharge starts - one check in front of the single place
      // interaction is dispatched, rather than a flag threaded through every
      // InteractableTrigger subclass. Movement deliberately stays enabled:
      // the player walks the dark hall during the void hold.
      if (GameManager.Instance != null && GameManager.Instance.EndSequenceRunning)
        return;

      InteractableTrigger target = GetNearestInteractable();
      if (target != null)
        target.Interact();
    }
  }

  private InteractableTrigger GetNearestInteractable()
  {
    InteractableTrigger nearest = null;
    float nearestSqrDist = float.MaxValue;
    Vector2 pos = transform.position;

    for (int i = 0; i < nearbyInteractables.Count; i++)
    {
      InteractableTrigger candidate = nearbyInteractables[i];
      if (candidate == null) continue;

      float sqrDist = ((Vector2)candidate.transform.position - pos).sqrMagnitude;
      if (sqrDist < nearestSqrDist)
      {
        nearestSqrDist = sqrDist;
        nearest = candidate;
      }
    }

    return nearest;
  }

  public void AddInteractable(InteractableTrigger interactable)
  {
    if (!nearbyInteractables.Contains(interactable))
      nearbyInteractables.Add(interactable);
  }

  public void RemoveInteractable(InteractableTrigger interactable)
  {
    nearbyInteractables.Remove(interactable);
  }
}
