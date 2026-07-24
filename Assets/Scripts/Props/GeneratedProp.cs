using UnityEngine;

// Marker for a prop placed by Tools > Props > Scatter Placer (Prop_Placement_
// Tool_Brief.md §4). Pure serialized data - NO Update, no logic, nothing the
// game reads. It exists so the placer can find/remove/rebatch its own output
// after the user has reparented and regrouped by hand: a parent GameObject
// loses that grouping the moment anything is dragged out, but the component
// rides along through reparenting, duplication, and prefab-link breaks.
//
// The placer also drops instances under a plain "GeneratedProps" empty for
// hierarchy tidiness, but THIS component is the sole authority for find/remove -
// never the parent.
//
// Deliberately carries NO economy identity (no Prop/propId/effect) - generated
// props are decorative, which keeps the Prop Consistency Checker silent by
// construction (brief §2). Promoting one to interactable is a manual act, and
// the propId law applies from that moment.
[DisallowMultipleComponent]
public class GeneratedProp : MonoBehaviour
{
    [Tooltip("AssetDatabase GUID of the prefab that was placed - lets Remove filter by prefab and the checker flag a prefab that was later deleted.")]
    public string sourcePrefabGuid;

    [Tooltip("Free-text family label set by the run, e.g. \"Cabinet\" - grouping only, no behaviour.")]
    public string familyId;

    [Tooltip("GUID per tool run, so a later pass can remove exactly the batch it created.")]
    public string batchId;

    [Tooltip("The run's seed, recorded so a layout can be reproduced.")]
    public int seed;
}
