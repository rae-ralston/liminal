using UnityEngine;

// The AssemblyHall stage objects (Ending brief E5). The three chain buttons and
// the stage CapacityColumn are PRE-PLACED in the AssemblyHall scene and start
// disabled - nothing is instantiated at runtime, which sidesteps spawn-point
// wiring and the additive-load ordering trap entirely. This one component polls
// GameManager.Stage and enables each object when the chain reaches it (correct
// even when polling starts late on a room reload).
//
// View-only: reads GameManager.Stage, never writes it. The presses themselves
// are SummonStep effects on the buttons.
public class EndStageObjects : MonoBehaviour
{
    [Tooltip("Small button - appears when the SecurityRoom lever authorises the chain (Stage Called).")]
    [SerializeField] GameObject smallButton;
    [Tooltip("Larger button - appears after the small button is pressed (Stage Small).")]
    [SerializeField] GameObject largerButton;
    [Tooltip("The End Button - live once the larger button is pressed (Stage Large).")]
    [SerializeField] GameObject endButton;
    [Tooltip("The stage master CapacityColumn - appears full alongside the End Button (Stage Large).")]
    [SerializeField] GameObject stageColumn;

    void Awake()
    {
        // Guarantee the disabled start the brief calls for, regardless of how
        // the scene was authored.
        Show(smallButton, false);
        Show(largerButton, false);
        Show(endButton, false);
        Show(stageColumn, false);
    }

    void Update()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            return;
        }

        Show(smallButton, gm.Stage >= GameManager.EndStage.Called);
        Show(largerButton, gm.Stage >= GameManager.EndStage.Small);

        bool live = gm.Stage >= GameManager.EndStage.Large;
        Show(endButton, live);
        Show(stageColumn, live);
    }

    static void Show(GameObject go, bool on)
    {
        if (go != null && go.activeSelf != on)
        {
            go.SetActive(on);
        }
    }
}
