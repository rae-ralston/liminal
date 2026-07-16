using UnityEngine;
using UnityEngine.Rendering.Universal;

// Dumb view component for a door connection's economy status: polls
// DoorStateRegistry.GetEconomyStatus every frame (no pub/sub in this
// project) and drives a small status SpriteRenderer and/or Light2D:
//
//   red    Locked      not purchased, never affordable so far
//   orange Unlockable  not purchased, can buy right now
//   yellow Suspended   not purchased, was affordable once, not anymore
//   green  Purchased   bought - permanent
//
// Lives on the keypad prefab (pointed at a dedicated child status sprite -
// NOT the keypad body, whose color InteractableHighlight already owns) but
// works anywhere: assign a connection explicitly, or leave it empty to
// borrow one from a DoorPurchaser or Door on this GameObject or a parent.
public class DoorIndicatorLight : MonoBehaviour
{
  [Tooltip("Connection whose status to show. Leave empty to use the DoorPurchaser's (or Door's) connection on this GameObject or a parent.")]
  [SerializeField] DoorConnection connection;
  [Tooltip("Status sprite to tint. Leave empty to use the SpriteRenderer on this GameObject (don't share it with InteractableHighlight - both would write its color).")]
  [SerializeField] SpriteRenderer statusSprite;
  [Tooltip("Optional Light2D tinted along with the sprite.")]
  [SerializeField] Light2D statusLight;

  [SerializeField] Color lockedColor = new Color(0.9f, 0.15f, 0.15f);
  [SerializeField] Color unlockableColor = new Color(1f, 0.55f, 0.1f);
  [SerializeField] Color suspendedColor = new Color(0.95f, 0.85f, 0.2f);
  [SerializeField] Color purchasedColor = new Color(0.2f, 0.85f, 0.3f);

  bool hasStatus;
  DoorEconomyStatus lastStatus;

  void Awake()
  {
    if (connection == null)
    {
      DoorPurchaser purchaser = GetComponentInParent<DoorPurchaser>();
      if (purchaser != null) connection = purchaser.Connection;
    }

    if (connection == null)
    {
      Door door = GetComponentInParent<Door>();
      if (door != null) connection = door.Connection;
    }

    if (connection == null)
    {
      Debug.LogWarning($"[DoorIndicatorLight] '{name}' has no connection (assigned or inherited) - light stays dark.", this);
    }

    if (statusSprite == null)
    {
      statusSprite = GetComponent<SpriteRenderer>();
    }
  }

  void Update()
  {
    if (connection == null || DoorStateRegistry.Instance == null)
    {
      return;
    }

    DoorEconomyStatus status = DoorStateRegistry.Instance.GetEconomyStatus(connection);
    if (hasStatus && status == lastStatus)
    {
      return;
    }

    hasStatus = true;
    lastStatus = status;
    Apply(StatusColor(status));
  }

  Color StatusColor(DoorEconomyStatus status)
  {
    switch (status)
    {
      case DoorEconomyStatus.Unlockable: return unlockableColor;
      case DoorEconomyStatus.Suspended: return suspendedColor;
      case DoorEconomyStatus.Purchased: return purchasedColor;
      // a keypad light on an unpriced connection reads as an open door
      case DoorEconomyStatus.NotPriced: return purchasedColor;
      default: return lockedColor;
    }
  }

  void Apply(Color color)
  {
    if (statusSprite != null)
    {
      statusSprite.color = color;
    }

    if (statusLight != null)
    {
      statusLight.color = color;
    }
  }
}
