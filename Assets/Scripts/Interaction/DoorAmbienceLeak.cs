using FMOD.Studio;
using FMODUnity;
using UnityEngine;

// Audio leakage: plays a filtered version of the DESTINATION room's ambience
// while the player is near this door. Navigation aid by default; wrongness
// tool when the leaked ambience is deliberately mismatched.
//
// FMOD-side contract (author in FMOD Studio):
// - one spatialized loop event (assigned to leakEvent) with a labeled "Room"
//   parameter selecting which room's bed plays, pre-filtered (low-pass) inside
//   the event, with a short max distance so it only reads right at the door.
// - the "Room" parameter's labels must match whatever string this passes:
//   by default the destination scene name (e.g. "Hallway_1"), or overrideRoomLabel.
//
// Lifecycle mirrors PropAudio's ambient loop: create+attach+start on enter,
// stop (ALLOWFADEOUT) + release on exit, so nothing leaks across room loads.
public class DoorAmbienceLeak : MonoBehaviour
{
  [SerializeField] EventReference leakEvent;
  [SerializeField] DoorConnection connection;
  [Tooltip("DoorId of THIS door - used to resolve the destination room from the connection.")]
  [SerializeField] DoorId myDoorId;
  [Tooltip("Leave empty to derive the Room label from the connection's other endpoint. Set to deliberately mismatch for a wrongness beat.")]
  [SerializeField] string overrideRoomLabel;

  const string RoomParameter = "Room";

  EventInstance leakInstance;
  bool leaking;

  void OnTriggerEnter2D(Collider2D other)
  {
    if (leaking || !other.CompareTag("Player"))
    {
      return;
    }

    StartLeak();
  }

  void OnTriggerExit2D(Collider2D other)
  {
    if (!other.CompareTag("Player"))
    {
      return;
    }

    StopLeak();
  }

  void OnDisable()
  {
    // room unloaded / object disabled while player was in range
    StopLeak();
  }

  void StartLeak()
  {
    if (leakEvent.IsNull)
    {
      return;
    }

    string room = ResolveRoomLabel();
    if (string.IsNullOrEmpty(room))
    {
      return;
    }

    leakInstance = RuntimeManager.CreateInstance(leakEvent);
    RuntimeManager.AttachInstanceToGameObject(leakInstance, gameObject);
    leakInstance.setParameterByNameWithLabel(RoomParameter, room);
    leakInstance.start();
    leaking = true;
  }

  void StopLeak()
  {
    if (!leaking)
    {
      return;
    }

    leakInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
    leakInstance.release();
    leaking = false;
  }

  string ResolveRoomLabel()
  {
    if (!string.IsNullOrEmpty(overrideRoomLabel))
    {
      return overrideRoomLabel;
    }

    if (connection != null
        && connection.TryGetTarget(myDoorId, out DoorId target))
    {
      return target.SceneName;
    }

    return null;
  }
}
