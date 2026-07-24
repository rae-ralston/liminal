using TMPro;
using UnityEngine;

// The end card (Ending brief E8). A METER READING, not a stat screen: the
// building filing a closing report on itself. Flat administrative typography,
// no celebration, no score, no grade - the numbers are evidence of a shift
// worked, not points earned.
//
// Every string is serialized because the copy is the user's call. This class
// only supplies the numbers and the layout hooks.
//
// Populated once, by EndSequenceController, at the moment the card is shown -
// the values are frozen by then (the bank is empty, the session clock stopped
// at BeginEndSequence), so there is nothing to poll.
public class EndCard : MonoBehaviour
{
  [Header("Fields (leave any unassigned to omit that line)")]
  [SerializeField] TMP_Text formNumberText;
  [SerializeField] TMP_Text totalDrawText;
  [SerializeField] TMP_Text roomsServicedText;
  [SerializeField] TMP_Text durationText;
  [SerializeField] TMP_Text stampText;
  [SerializeField] TMP_Text closingLineText;

  [Header("Copy - {0} is substituted")]
  [Tooltip("A form number sells the administrative fiction harder than any sentence. Static text.")]
  [SerializeField] string formNumber = "FORM 7-C · CIRCUIT TERMINATION RECORD";
  [Tooltip("{0} = total charge drawn over the run (TotalEarned).")]
  [SerializeField] string totalDrawFormat = "TOTAL DRAW ............ {0}";
  [Tooltip("{0} = rooms activated, {1} = rooms in the building.")]
  [SerializeField] string roomsServicedFormat = "ROOMS SERVICED ........ {0} OF {1}";
  [Tooltip("{0} = session length, already formatted as H:MM:SS.")]
  [SerializeField] string durationFormat = "SHIFT DURATION ........ {0}";
  [Tooltip("Static. Reads like something stamped on the way out.")]
  [SerializeField] string stamp = "FILED";
  [Tooltip("The one closing line. The only place the card is allowed a voice.")]
  [SerializeField] string closingLine = "Thank you for your service.";

  // Called by EndSequenceController immediately before the fade-in.
  public void Populate()
  {
    Incremental incremental = Incremental.Instance;
    GameManager gameManager = GameManager.Instance;

    long totalDraw = incremental != null ? incremental.TotalEarned : 0;
    int serviced = incremental != null ? incremental.ActivatedRoomCount : 0;
    int total = incremental != null ? incremental.AllRooms.Count : 0;
    float seconds = gameManager != null ? gameManager.SessionDuration : 0f;

    Set(formNumberText, formNumber);
    Set(totalDrawText, string.Format(totalDrawFormat, totalDraw));
    Set(roomsServicedText, string.Format(roomsServicedFormat, serviced, total));
    Set(durationText, string.Format(durationFormat, FormatDuration(seconds)));
    Set(stampText, stamp);
    Set(closingLineText, closingLine);
  }

  // Wired to the Again button. Delegates rather than reloading here, because
  // the camera rig has to be restored before the reload and only the
  // controller knows what it changed.
  public void Again()
  {
    if (EndSequenceController.Instance == null)
    {
      Debug.LogError("[Ending] Again pressed but there is no EndSequenceController.", this);
      return;
    }

    EndSequenceController.Instance.Again();
  }

  static string FormatDuration(float seconds)
  {
    System.TimeSpan span = System.TimeSpan.FromSeconds(seconds);
    return $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}";
  }

  static void Set(TMP_Text field, string value)
  {
    if (field != null)
    {
      field.text = value;
    }
  }
}
