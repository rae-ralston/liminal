using UnityEditor;
using UnityEngine;

// Builds the endgame STARTING POINTS as transform groups in the open scene, to
// the Ending brief's shape: the SecurityRoom board (RoomLampBoard + a
// CapacityColumn + the end lever) and the AssemblyHall stage (EndStageObjects +
// the three chain buttons + the stage CapacityColumn). Empty transforms plus
// placeholder SpriteRenderers, named and nested, with the component references
// already wired - next session is dropping in Dylan's sprites, adding the 24
// lamps under the zone groups, and tuning.
//
// These are scene scaffolds, not prefabs: build one in the right scene, arrange
// it, then drag it into a prefab once the art lands. Menu lives under
// Tools/Circuit alongside the other Circuit editor tools. Sigils (E4) and the
// discharge sequence (E6+) are NOT built here - see Ending_Build_Brief.md.
public static class EndgameScaffold
{
    [MenuItem("Tools/Circuit/Create SecurityRoom End Board")]
    public static void CreateEndBoard()
    {
        //  EndBoard [RoomLampBoard]
        //    Lamps
        //      ZoneTop / ZoneMiddle / ZoneBottom   (design doc #5 - layout only)
        //    CapacityColumn [CapacityColumn] -> ColumnRoot(Frame, Fill)
        //    EndLever [EndButtonSummoner]           (add PropInteraction next session)
        //      SigilAnchor                          (E4 placeholder - hidden sigil above the lever)
        GameObject root = NewGroup("EndBoard", null);
        root.AddComponent<RoomLampBoard>();

        GameObject lamps = NewGroup("Lamps", root.transform);
        NewGroup("ZoneTop", lamps.transform);
        NewGroup("ZoneMiddle", lamps.transform);
        NewGroup("ZoneBottom", lamps.transform);

        GameObject colGo = NewGroup("CapacityColumn", root.transform);
        WireColumn(colGo.AddComponent<CapacityColumn>(), colGo.transform);

        GameObject lever = NewGroup("EndLever", root.transform);
        lever.AddComponent<EndButtonSummoner>();
        NewGroup("SigilAnchor", lever.transform);

        Finish(root, "Create SecurityRoom End Board");
    }

    [MenuItem("Tools/Circuit/Create AssemblyHall End Stage")]
    public static void CreateEndStage()
    {
        //  EndStage [EndStageObjects]
        //    SmallButton  [SummonStep expected=Called]
        //    LargerButton [SummonStep expected=Small]
        //    EndButton    [SummonStep expected=Large]   (the terminal press)
        //    CapacityColumn [CapacityColumn] -> ColumnRoot(Frame, Fill)   (the master gauge)
        //    SigilAnchor                                 (E4 placeholder - inert sigil behind the stage)
        GameObject root = NewGroup("EndStage", null);
        EndStageObjects stage = root.AddComponent<EndStageObjects>();

        GameObject small = NewButton("SmallButton", root.transform, GameManager.EndStage.Called);
        GameObject larger = NewButton("LargerButton", root.transform, GameManager.EndStage.Small);
        GameObject end = NewButton("EndButton", root.transform, GameManager.EndStage.Large);

        GameObject colGo = NewGroup("CapacityColumn", root.transform);
        WireColumn(colGo.AddComponent<CapacityColumn>(), colGo.transform);

        NewGroup("SigilAnchor", root.transform);

        SerializedObject so = new SerializedObject(stage);
        so.FindProperty("smallButton").objectReferenceValue = small;
        so.FindProperty("largerButton").objectReferenceValue = larger;
        so.FindProperty("endButton").objectReferenceValue = end;
        so.FindProperty("stageColumn").objectReferenceValue = colGo;
        so.ApplyModifiedPropertiesWithoutUndo();

        Finish(root, "Create AssemblyHall End Stage");
    }

    // A chain button: a placeholder sprite carrying a SummonStep at the given
    // expected stage. Wire PropInteraction / a collider next session.
    static GameObject NewButton(string name, Transform parent, GameManager.EndStage expected)
    {
        GameObject go = NewSprite(name, parent);
        SummonStep step = go.AddComponent<SummonStep>();
        SerializedObject so = new SerializedObject(step);
        so.FindProperty("expected").enumValueIndex = (int)expected;
        so.ApplyModifiedPropertiesWithoutUndo();
        return go;
    }

    // ColumnRoot (scaled on Y for height) with a Frame + bottom-pivot Fill,
    // wired into the CapacityColumn. (The Fill sprite still needs a BOTTOM-edge
    // pivot set on its import.)
    static void WireColumn(CapacityColumn col, Transform parent)
    {
        GameObject columnRoot = NewGroup("ColumnRoot", parent);
        GameObject frame = NewSprite("Frame", columnRoot.transform);
        GameObject fill = NewSprite("Fill", columnRoot.transform);

        SerializedObject so = new SerializedObject(col);
        so.FindProperty("columnRoot").objectReferenceValue = columnRoot.transform;
        so.FindProperty("frame").objectReferenceValue = frame.GetComponent<SpriteRenderer>();
        so.FindProperty("fill").objectReferenceValue = fill.GetComponent<SpriteRenderer>();
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static GameObject NewGroup(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        if (parent != null)
        {
            go.transform.SetParent(parent, false);
        }

        return go;
    }

    static GameObject NewSprite(string name, Transform parent)
    {
        GameObject go = NewGroup(name, parent);
        go.AddComponent<SpriteRenderer>();  // sprite assigned next session
        return go;
    }

    static void Finish(GameObject root, string undoName)
    {
        Undo.RegisterCreatedObjectUndo(root, undoName);
        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
        Debug.Log($"[Ending] {undoName}: scaffold created. Add sprites, wire lamps to RoomIds, drag to a prefab next session.", root);
    }
}
