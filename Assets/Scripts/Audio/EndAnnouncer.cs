using FMOD.Studio;
using FMODUnity;
using UnityEngine;

/*
 * The two building-wide cues of the end chain. Lives in PersistentScene next
 * to the other managers, because that is the only scene that never unloads:
 * RoomTransitionManager keeps exactly one room loaded, so a cue fired from a
 * prop can only ever be heard in that prop's own room, and both of these have
 * to reach the player wherever they happen to be standing.
 *
 *   1. The end condition is reached  -> the alarm. The player learned at the
 *      start of the run that the clock rings and where it is; this tells them
 *      it is worth going back. IncrementalStartEndButtonGlow lights the clock
 *      itself, for when they arrive. A fire-and-forget one-shot: it is a
 *      notification, and the glow carries the state from there.
 *   2. The clock is pressed and the chain is authorised (Stage -> Called)
 *      -> the announcement that sends them to the AssemblyHall. This one is an
 *      OWNED instance, because it has to be able to stop: it runs until the
 *      player presses the first of the three stage buttons (Stage -> Small),
 *      which is the moment they are demonstrably there and no longer need
 *      directions. Author it as a loop and it repeats until they arrive;
 *      author it as a single line and it simply gets cut short if they beat
 *      it there. The code handles both.
 *
 * A polling view, in the same shape as EndStageObjects: it reads
 * Incremental.EndConditionLatched and GameManager.Stage and never writes
 * them. That keeps the trigger out of EndButtonSummoner (which stays
 * FMOD-agnostic like every other effect) and means the debug end-condition
 * path announces itself exactly like a real run.
 */
public class EndAnnouncer : MonoBehaviour
{
    bool alarmFired;
    bool announcementStarted;
    EventInstance announcement;

    void Update()
    {
        Incremental incremental = Incremental.Instance;
        GameManager gameManager = GameManager.Instance;
        if (incremental == null || gameManager == null || FMODEvents.Instance == null)
        {
            return;
        }

        // Arrival: the first stage button has been pressed, so the directions
        // have served their purpose. Checked before the start branch so a
        // player who somehow gets there in the same frame is never talked at.
        if (gameManager.Stage >= GameManager.EndStage.Small)
        {
            StopAnnouncement("first stage button pressed");
            return;
        }

        // Chain authorised. This also spends the alarm: if the player got to
        // the clock before this ever ran (a room reload, a forced end
        // condition), the alarm is moot and must not fire behind the
        // announcement.
        if (!announcementStarted && gameManager.Stage >= GameManager.EndStage.Called)
        {
            announcementStarted = true;
            alarmFired = true;
            StartAnnouncement();
            return;
        }

        if (!alarmFired && incremental.EndConditionLatched)
        {
            alarmFired = true;
            PlayOneShot(FMODEvents.Instance.EndConditionMet, "end-condition alarm");
        }
    }

    // Owned instance, created and released here (the PropAudio pattern) rather
    // than handed to AudioManager - that list releases on ITS destroy, which
    // would double-release what we stop ourselves. Not attached to a
    // GameObject: the event is 2D by requirement (see FMODEvents), so it has
    // no position to track.
    void StartAnnouncement()
    {
        EventReference reference = FMODEvents.Instance.EndAnnouncement;
        if (reference.IsNull)
        {
            Debug.LogWarning("[Ending] No FMOD event assigned for the end announcement - cue skipped.", this);
            return;
        }

        announcement = RuntimeManager.CreateInstance(reference);
        announcement.start();
        Debug.Log("[Ending] Cue: end announcement (chain called) - running until the first stage button.", this);
    }

    // Idempotent: Update calls this on every frame from Stage Small onward, and
    // OnDestroy calls it again if the run ends mid-announcement.
    void StopAnnouncement(string reason)
    {
        if (!announcement.isValid())
        {
            return;
        }

        announcement.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        announcement.release();
        announcement = default; // don't re-stop a released handle
        Debug.Log($"[Ending] End announcement stopped - {reason}.", this);
    }

    // transform.position is a formality - the cue events are 2D by requirement
    // (see FMODEvents), so the position is never used. A silent cue here means
    // the slot on the FMODEvents object is empty, hence the log.
    void PlayOneShot(EventReference reference, string what)
    {
        if (reference.IsNull)
        {
            Debug.LogWarning($"[Ending] No FMOD event assigned for the {what} - cue skipped.", this);
            return;
        }

        RuntimeManager.PlayOneShot(reference, transform.position);
        Debug.Log($"[Ending] Cue: {what}.", this);
    }

    void OnDestroy()
    {
        StopAnnouncement("torn down");
    }
}
