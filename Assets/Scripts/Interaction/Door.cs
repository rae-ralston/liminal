using UnityEngine;

// A single door: interaction trigger, teleport source, AND arrival marker, all
// on one GameObject. Replaces the old three-GameObject layout (SpawnPoint +
// DoorTeleporter + one-or-more DoorSprite children).
//
//  - Identity is a DoorId asset (unique per door in the whole game). The scene
//    this door lives in is on the DoorId.
//  - The connection it belongs to is a DoorConnection asset shared with the
//    door on the other side.
//  - The spot the player lands on when ARRIVING here is a local offset from the
//    door (drawn as a gizmo), not a separate SpawnPoint GameObject. This keeps
//    the player in front of the door instead of inside it. A Northern door uses
//    a negative Y offset (land below); a Southern door uses a positive Y offset
//    (land above/behind the leaf, "just entered").
//  - Width (single / double / wider doors) is one tiled SpriteRenderer sized in
//    world units, replacing the stack of DoorSprite children.
[RequireComponent(typeof(SpriteRenderer))]
public class Door : InteractableTrigger
{
  [Header("Identity")]
  [SerializeField] DoorId id;
  [SerializeField] DoorConnection connection;

  [Header("Arrival")]
  [Tooltip("Where the player is placed when arriving at THIS door, relative to the door. Northern door: negative Y (land below). Southern door: positive Y (land above/behind the leaf).")]
  [SerializeField] Vector2 spawnOffset = new Vector2(0f, -1.5f);
  [Tooltip("Z rotation (degrees) the player faces on arrival.")]
  [SerializeField] float spawnFacing;

/*
  [Header("Appearance")]
  [Tooltip("Door width in world units. The SpriteRenderer must be in Tiled draw mode; this drives its size so one object covers single/double/wider doors.")]
  [SerializeField] float width = 1.5f;
  [Tooltip("Door height in world units.")]
  [SerializeField] float height = 3f;
  */

  public DoorId Id => id;

  public DoorConnection Connection => connection;

  public Vector3 SpawnPosition => transform.position + (Vector3)spawnOffset;

  public Quaternion SpawnRotation => Quaternion.Euler(0f, 0f, spawnFacing);

  void OnValidate()
  {
    /*
    // keep the tiled sprite matching the authored width/height in-editor
    SpriteRenderer sr = GetComponent<SpriteRenderer>();
    if (sr != null && sr.drawMode != SpriteDrawMode.Simple)
    {
      sr.size = new Vector2(width, height);
    }
    */
  }

  public override void Interact()
  {
    if (connection == null)
    {
      Debug.LogError($"[Door] '{name}' has no DoorConnection assigned.", this);
      return;
    }

    // Audio lives on a PropAudio on this same door GameObject now.
    PropAudio audio = GetComponentInParent<PropAudio>();

    if (DoorStateRegistry.Instance != null && DoorStateRegistry.Instance.IsLocked(connection))
    {
      // locked: rattle, no traversal. Checked BEFORE the purchase gate, so
      // a (checker-flagged) priced+locked door reads as locked here, not
      // as purchasable.
      Debug.Log($"[Door] '{name}' is locked - needs its DoorUnlocker.");
      if (audio != null) audio.PlayLocked(connection.DoorType);
      return;
    }

    if (DoorStateRegistry.Instance != null && DoorStateRegistry.Instance.IsPurchaseRequired(connection))
    {
      // priced and not yet bought: behaves like a locked door until the
      // keypad purchase goes through. Once purchased it NEVER re-locks
      // (permanent unlocks, decided 2026-07-15) - a low balance only ever
      // changes the keypad light of UNPURCHASED doors.
      Debug.Log($"[Incremental] Door '{name}' needs purchasing (cost {connection.ClickCost}) - use its keypad.");
      if (audio != null) audio.PlayLocked(connection.DoorType);
      return;
    }

    if (!connection.TryGetTarget(id, out DoorId target))
    {
      // one-way from the wrong side behaves like a locked door
      Debug.Log($"[Door] '{name}' refuses - one-way connection entered from the wrong side.");
      if (audio != null) audio.PlayLocked(connection.DoorType);
      return;
    }

    if (audio != null) audio.PlayOpen(connection.DoorType);

    RoomTransitionManager.Instance.TeleportTo(target);
  }

  void OnDrawGizmosSelected()
  {
    // visual handle for the arrival spot so it can still be placed by eye
    Gizmos.color = Color.cyan;
    Vector3 p = transform.position + (Vector3)spawnOffset;
    Gizmos.DrawWireSphere(p, 0.2f);
    Gizmos.DrawLine(transform.position, p);
  }
}
