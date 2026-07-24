using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering; // SortingGroup
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

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
    // Lamps render one step ABOVE the board backing (order 0). Order in layer is
    // compared before the transparency-sort custom axis, so this holds regardless
    // of a lamp's Y - without it, a top-zone lamp (higher Y = farther back under
    // the (0,1,0) sort axis) draws behind the backing and disappears. Paired with
    // a SortingGroup on the board root so the whole assembly still sorts as one
    // unit against the player (order 1 alone would bleed the lamps over the
    // player when they stand in front of the board).
    const int LampSortingOrder = 1;

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
        // The board is a multi-sprite assembly (backing + 24 lamps + column +
        // lever); the group makes it sort as one unit against the player, and
        // lets the lamps sit in front of the backing via order-in-layer.
        root.AddComponent<SortingGroup>();

        GameObject lamps = NewGroup("Lamps", root.transform);
        NewGroup("ZoneTop", lamps.transform);
        NewGroup("ZoneMiddle", lamps.transform);
        NewGroup("ZoneBottom", lamps.transform);

        GameObject colGo = NewGroup("CapacityColumn", root.transform);
        WireColumn(colGo.AddComponent<CapacityColumn>(), colGo.transform);

        GameObject lever = NewSprite("EndLever", root.transform);
        lever.AddComponent<EndButtonSummoner>();
        MakeInteractable(lever);
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

    // The ending's driver (brief E6). Belongs in PersistentScene, alongside the
    // other manager singletons - run this with PersistentScene open. Auto-wires
    // what it can find in the open scenes; everything it can't find is left null
    // and logs a WOULD at runtime rather than breaking the sequence.
    [MenuItem("Tools/Circuit/Create End Sequence Controller")]
    public static void CreateEndSequenceController()
    {
        if (Object.FindAnyObjectByType<EndSequenceController>() != null)
        {
            Debug.LogWarning("[Ending] An EndSequenceController already exists in the open scenes - not creating a second.");
            return;
        }

        GameObject root = NewGroup("EndSequenceController", null);
        EndSequenceController controller = root.AddComponent<EndSequenceController>();

        SerializedObject so = new SerializedObject(controller);
        AssignIfFound(so, "vCam", Object.FindAnyObjectByType<CinemachineCamera>());
        AssignIfFound(so, "confiner", Object.FindAnyObjectByType<CinemachineConfiner2D>());
        AssignIfFound(so, "pixelPerfect", Object.FindAnyObjectByType<PixelPerfectCamera>());

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            AssignIfFound(so, "playerLight", player.GetComponentInChildren<Light2D>(true));
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log("[Ending] Still to wire by hand: globalLight (the ambient Light2D), flashlight, screenFade, endCard, endCardGroup. Each is optional - unwired parts log a WOULD and the sequence still completes.", root);
        Debug.Log("[Ending] CHECK 'Player Light': it was auto-wired from the FIRST Light2D on the Player, which on a player carrying both an ember and a flashlight may well be the wrong one. Player Light = the ember (outlives the hall); Flashlight = dies with the hall.", root);
        Finish(root, "Create End Sequence Controller");
    }

    // Board zones (design doc #5) mirror the building's geography, so a player
    // reading the board can map a dark lamp onto somewhere they have walked.
    // Order WITHIN each array is the order lamps are generated in - edit these
    // to re-arrange the board, then delete the lamps and re-generate.
    //
    // Names match RoomId assets with the "RoomId_" prefix dropped. A room that
    // appears in none of them still gets a lamp (in Middle) plus a warning -
    // a room silently missing from the board can never light, and the ending
    // would look ungatable.

    // Everything above and including Hallway_2_with_BreakoutSpace.
    static readonly string[] ZoneTopRooms =
    {
        "Cafeteria", "Kitchen", "Restroom_A", "Restroom_B",
        "ConferenceRoom_B", "ConferenceRoom_C", "BreakRoom",
        "SecurityRoom", "ServerRoom", "JanitorCloset",
        "SecretHallway", "BackAlley",
        "Hallway_2_with_BreakoutSpace",
    };

    // The hall and the offices flanking it.
    static readonly string[] ZoneMiddleRooms =
    {
        "Office_5", "Office_6", "AssemblyHall", "Office_7", "Office_8",
    };

    // Hallway_1 and everything below it.
    static readonly string[] ZoneBottomRooms =
    {
        "Hallway_1", "Office_1", "Office_2", "Office_3", "Office_4", "OpenSpace",
    };

    // Fills the board's lamp list from the game's room list (brief E3), creating
    // a sprite per room and wiring it to its RoomId. Run with the SecurityRoom
    // scene open (and PersistentScene too, so the authoritative room list can be
    // read off the live Incremental).
    //
    // ADDITIVE and non-destructive: rooms that already have a lamp entry are
    // left exactly as they are, so this can be re-run after new rooms are added
    // without disturbing a board you have already arranged. Deliberately no
    // Light2D per lamp - the lamp IS the sprite colour, and 24 point lights on
    // one board is a real cost on WebGL for no readability gain.
    [MenuItem("Tools/Circuit/Generate Board Lamps")]
    public static void GenerateBoardLamps()
    {
        RoomLampBoard board = Object.FindAnyObjectByType<RoomLampBoard>();
        if (board == null)
        {
            Debug.LogError("[Ending] No RoomLampBoard in the open scenes - run 'Create SecurityRoom End Board' first.");
            return;
        }

        List<RoomId> rooms = GatherRooms();
        if (rooms.Count == 0)
        {
            Debug.LogError("[Ending] No RoomId assets found - nothing to generate.");
            return;
        }

        // Idempotent: an older board (or one built before this pass existed) has
        // no SortingGroup and lamps at order 0 - re-running fixes both.
        if (board.GetComponent<SortingGroup>() == null)
        {
            Undo.AddComponent<SortingGroup>(board.gameObject);
            Debug.Log("[Ending] Added a SortingGroup to the board so it sorts as one unit against the player and the lamps sit in front of the backing.", board);
        }

        Transform lampsParent = FindOrCreateChild(board.transform, "Lamps");
        Transform[] zones =
        {
            FindOrCreateChild(lampsParent, "ZoneTop"),
            FindOrCreateChild(lampsParent, "ZoneMiddle"),
            FindOrCreateChild(lampsParent, "ZoneBottom"),
        };

        SerializedObject so = new SerializedObject(board);
        SerializedProperty lamps = so.FindProperty("lamps");

        // Prune dead rows FIRST. Deleting lamp GameObjects from the scene leaves
        // their entries behind with a live RoomId and a dangling SpriteRenderer,
        // and such a row would otherwise count as "already wired" - so a
        // re-generate would create nothing and the board would stay dark
        // forever. An entry is only real if BOTH ends survive.
        //
        // Rebuilt by copying the survivors rather than deleting in place:
        // DeleteArrayElementAtIndex has the null-then-remove quirk on rows
        // holding object references, and this sidesteps it entirely.
        List<RoomId> keepRooms = new List<RoomId>();
        List<Object> keepLamps = new List<Object>();
        List<Object> keepGlows = new List<Object>();

        for (int i = 0; i < lamps.arraySize; i++)
        {
            SerializedProperty row = lamps.GetArrayElementAtIndex(i);
            Object room = row.FindPropertyRelative("room").objectReferenceValue;
            Object lamp = row.FindPropertyRelative("lamp").objectReferenceValue;

            if (room == null || lamp == null)
            {
                continue;
            }

            keepRooms.Add(room as RoomId);
            keepLamps.Add(lamp);
            keepGlows.Add(row.FindPropertyRelative("glow").objectReferenceValue);
        }

        int pruned = lamps.arraySize - keepRooms.Count;
        if (pruned > 0)
        {
            lamps.arraySize = keepRooms.Count;
            for (int i = 0; i < keepRooms.Count; i++)
            {
                SerializedProperty row = lamps.GetArrayElementAtIndex(i);
                row.FindPropertyRelative("room").objectReferenceValue = keepRooms[i];
                row.FindPropertyRelative("lamp").objectReferenceValue = keepLamps[i];
                row.FindPropertyRelative("glow").objectReferenceValue = keepGlows[i];
            }

            Debug.Log($"[Ending] Pruned {pruned} dead lamp entries (deleted sprite, room still assigned) before generating.", board);
        }

        HashSet<RoomId> alreadyWired = new HashSet<RoomId>(keepRooms);

        // Generate in board order (zone, then position within the zone) rather
        // than in whatever order the room list happens to be in, so the
        // hierarchy reads the same way the board does.
        rooms.Sort((a, b) =>
        {
            int zoneA = ZoneIndexFor(a), zoneB = ZoneIndexFor(b);
            return zoneA != zoneB ? zoneA.CompareTo(zoneB)
                                  : OrderInZoneFor(a).CompareTo(OrderInZoneFor(b));
        });

        int created = 0;
        foreach (RoomId room in rooms)
        {
            if (room == null || alreadyWired.Contains(room))
            {
                continue;
            }

            Transform zone = zones[ZoneIndexFor(room)];
            int indexInZone = zone.childCount;

            GameObject lampGo = NewSprite($"Lamp_{room.name}", zone);
            lampGo.transform.localPosition = new Vector3(indexInZone * 0.6f, 0f, 0f);
            lampGo.transform.localScale = Vector3.one * 0.4f;
            lampGo.GetComponent<SpriteRenderer>().sortingOrder = LampSortingOrder;

            int index = lamps.arraySize;
            lamps.InsertArrayElementAtIndex(index);
            SerializedProperty entry = lamps.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("room").objectReferenceValue = room;
            entry.FindPropertyRelative("lamp").objectReferenceValue = lampGo.GetComponent<SpriteRenderer>();
            entry.FindPropertyRelative("glow").objectReferenceValue = null;

            Undo.RegisterCreatedObjectUndo(lampGo, "Generate Board Lamps");
            created++;
        }

        so.ApplyModifiedProperties();

        // Normalize EVERY lamp (created + already-wired) to the board order, so a
        // re-run also lifts old lamps left at order 0 in front of the backing.
        // Re-read the list off a fresh SerializedObject - the entries are private,
        // and 'so' above has just been applied.
        int reordered = 0;
        SerializedProperty finalLamps = new SerializedObject(board).FindProperty("lamps");
        for (int i = 0; i < finalLamps.arraySize; i++)
        {
            SpriteRenderer lamp = finalLamps.GetArrayElementAtIndex(i)
                .FindPropertyRelative("lamp").objectReferenceValue as SpriteRenderer;

            if (lamp != null && lamp.sortingOrder != LampSortingOrder)
            {
                Undo.RecordObject(lamp, "Normalize Lamp Sorting");
                lamp.sortingOrder = LampSortingOrder;
                EditorUtility.SetDirty(lamp);
                reordered++;
            }
        }

        if (reordered > 0)
        {
            Debug.Log($"[Ending] Set {reordered} lamp(s) to sortingOrder {LampSortingOrder} (in front of the board backing at 0).", board);
        }

        Debug.Log($"[Ending] Board lamps: {created} created, {alreadyWired.Count} already wired, {rooms.Count} rooms total. Arrange them onto the board art; the lamp list is wired by reference, so renaming and reparenting are both safe.", board);

        List<string> unzoned = new List<string>();
        foreach (RoomId room in rooms)
        {
            if (room != null && IsUnzoned(room))
            {
                unzoned.Add(ShortRoomName(room));
            }
        }

        if (unzoned.Count > 0)
        {
            Debug.LogWarning($"[Ending] Rooms in no zone list, placed in ZoneMiddle by default: {string.Join(", ", unzoned)}. Add them to ZoneTopRooms/ZoneMiddleRooms/ZoneBottomRooms in EndgameScaffold.cs.", board);
        }
    }

    // The live Incremental's serialized list is the authority (it is what
    // AllRoomsActivated checks). Falls back to every RoomId asset in the project
    // when PersistentScene isn't open - louder, but better than silently
    // generating a board that doesn't match the game.
    static List<RoomId> GatherRooms()
    {
        List<RoomId> rooms = new List<RoomId>();

        Incremental incremental = Object.FindAnyObjectByType<Incremental>();
        if (incremental != null)
        {
            SerializedProperty list = new SerializedObject(incremental).FindProperty("allRooms");
            for (int i = 0; i < list.arraySize; i++)
            {
                rooms.Add(list.GetArrayElementAtIndex(i).objectReferenceValue as RoomId);
            }

            return rooms;
        }

        Debug.LogWarning("[Ending] No Incremental in the open scenes - falling back to every RoomId asset in the project. Open PersistentScene and re-run to match the game's actual room list.");

        foreach (string guid in AssetDatabase.FindAssets("t:RoomId"))
        {
            rooms.Add(AssetDatabase.LoadAssetAtPath<RoomId>(AssetDatabase.GUIDToAssetPath(guid)));
        }

        return rooms;
    }

    // 0 = top, 1 = middle, 2 = bottom. Unlisted rooms land in the middle and
    // are called out by name in the summary log.
    static int ZoneIndexFor(RoomId room)
    {
        string name = ShortRoomName(room);
        if (System.Array.IndexOf(ZoneTopRooms, name) >= 0) return 0;
        if (System.Array.IndexOf(ZoneBottomRooms, name) >= 0) return 2;
        return 1;
    }

    static int OrderInZoneFor(RoomId room)
    {
        string name = ShortRoomName(room);
        int index = System.Array.IndexOf(ZoneTopRooms, name);
        if (index >= 0) return index;

        index = System.Array.IndexOf(ZoneMiddleRooms, name);
        if (index >= 0) return index;

        index = System.Array.IndexOf(ZoneBottomRooms, name);
        if (index >= 0) return index;

        // Unlisted: after everything named, in a stable order.
        return int.MaxValue - 1;
    }

    static bool IsUnzoned(RoomId room)
    {
        string name = ShortRoomName(room);
        return System.Array.IndexOf(ZoneTopRooms, name) < 0
            && System.Array.IndexOf(ZoneMiddleRooms, name) < 0
            && System.Array.IndexOf(ZoneBottomRooms, name) < 0;
    }

    // Asset names carry a "RoomId_" prefix that the zone lists don't repeat.
    static string ShortRoomName(RoomId room)
    {
        if (room == null)
        {
            return string.Empty;
        }

        const string prefix = "RoomId_";
        return room.name.StartsWith(prefix) ? room.name.Substring(prefix.Length) : room.name;
    }

    static Transform FindOrCreateChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        return existing != null ? existing : NewGroup(name, parent).transform;
    }

    // The end card (brief E8). Run with PersistentScene open. Builds the canvas
    // disabled, wires it into the EndSequenceController, and points the Again
    // button at EndCard.Again. Copy and typography are the user's to change -
    // every string is serialized on the EndCard component.
    [MenuItem("Tools/Circuit/Create End Card")]
    public static void CreateEndCard()
    {
        if (Object.FindAnyObjectByType<EndCard>() != null)
        {
            Debug.LogWarning("[Ending] An EndCard already exists in the open scenes - not creating a second.");
            return;
        }

        GameObject root = new GameObject("EndCard",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(EndCard));

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Above the ScreenFade blackout, which the sequence leaves fully opaque.
        canvas.sortingOrder = 100;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject background = NewImage("Background", root.transform, new Color(0f, 0f, 0f, 1f));
        Stretch(background.GetComponent<RectTransform>());

        // Lines are placed top-down; y is the running offset from the centre.
        float y = 260f;
        TMP_Text form = NewLine("FormNumber", root.transform, ref y, 28f, TextAlignmentOptions.Center);
        y -= 40f;
        TMP_Text draw = NewLine("TotalDraw", root.transform, ref y, 36f, TextAlignmentOptions.Left);
        TMP_Text rooms = NewLine("RoomsServiced", root.transform, ref y, 36f, TextAlignmentOptions.Left);
        TMP_Text duration = NewLine("ShiftDuration", root.transform, ref y, 36f, TextAlignmentOptions.Left);
        y -= 40f;
        TMP_Text stamp = NewLine("Stamp", root.transform, ref y, 48f, TextAlignmentOptions.Center);
        y -= 60f;
        TMP_Text closing = NewLine("ClosingLine", root.transform, ref y, 32f, TextAlignmentOptions.Center);

        GameObject againButton = NewAgainButton(root.transform, y - 120f);

        EndCard card = root.GetComponent<EndCard>();
        SerializedObject cardSo = new SerializedObject(card);
        cardSo.FindProperty("formNumberText").objectReferenceValue = form;
        cardSo.FindProperty("totalDrawText").objectReferenceValue = draw;
        cardSo.FindProperty("roomsServicedText").objectReferenceValue = rooms;
        cardSo.FindProperty("durationText").objectReferenceValue = duration;
        cardSo.FindProperty("stampText").objectReferenceValue = stamp;
        cardSo.FindProperty("closingLineText").objectReferenceValue = closing;
        cardSo.ApplyModifiedPropertiesWithoutUndo();

        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            againButton.GetComponent<Button>().onClick, card.Again);

        // Hand it to the controller before disabling, so the reference survives.
        EndSequenceController controller = Object.FindAnyObjectByType<EndSequenceController>();
        if (controller != null)
        {
            SerializedObject controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("endCard").objectReferenceValue = root;
            controllerSo.FindProperty("endCardGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
            controllerSo.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[Ending] End card wired into the EndSequenceController.", controller);
        }
        else
        {
            Debug.LogWarning("[Ending] No EndSequenceController found - wire the card's root and CanvasGroup onto it by hand.");
        }

        root.GetComponent<CanvasGroup>().alpha = 0f;
        root.SetActive(false);

        Finish(root, "Create End Card");
    }

    static TMP_Text NewLine(string name, Transform parent, ref float y, float size, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        TMP_Text text = go.GetComponent<TMP_Text>();
        text.fontSize = size;
        text.alignment = alignment;
        text.color = new Color(0.85f, 0.85f, 0.82f, 1f);
        text.text = name;

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(900f, size * 1.6f);
        rect.anchoredPosition = new Vector2(0f, y);

        y -= size * 1.6f;
        return text;
    }

    static GameObject NewAgainButton(Transform parent, float y)
    {
        GameObject go = NewImage("AgainButton", parent, new Color(0.12f, 0.12f, 0.12f, 1f));
        go.AddComponent<Button>();

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(260f, 72f);
        rect.anchoredPosition = new Vector2(0f, y);

        GameObject label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(go.transform, false);
        TMP_Text text = label.GetComponent<TMP_Text>();
        text.text = "AGAIN";
        text.fontSize = 32f;
        text.alignment = TextAlignmentOptions.Center;
        Stretch(label.GetComponent<RectTransform>());

        return go;
    }

    static GameObject NewImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static void AssignIfFound(SerializedObject so, string propertyName, Object value)
    {
        if (value == null)
        {
            Debug.Log($"[Ending] Scaffold could not find a '{propertyName}' in the open scenes - wire it by hand.");
            return;
        }

        so.FindProperty(propertyName).objectReferenceValue = value;
    }

    // A chain button: a placeholder sprite carrying a SummonStep at the given
    // expected stage, made actually pressable (see MakeInteractable).
    static GameObject NewButton(string name, Transform parent, GameManager.EndStage expected)
    {
        GameObject go = NewSprite(name, parent);
        SummonStep step = go.AddComponent<SummonStep>();
        SerializedObject so = new SerializedObject(step);
        so.FindProperty("expected").enumValueIndex = (int)expected;
        so.ApplyModifiedPropertiesWithoutUndo();
        MakeInteractable(go);
        return go;
    }

    // SummonStep/EndButtonSummoner are IIncrementalEffects - they only fire when
    // PropInteraction.Interact() collects them, which in turn only happens if the
    // player registered against a TRIGGER collider. Without both, the scaffold
    // builds objects that look right and do nothing when you walk up and press.
    //
    // Note the room gate still applies: PropInteraction refuses until the room
    // holding the object is activated (the Circuit's C3 gate).
    static void MakeInteractable(GameObject go)
    {
        go.AddComponent<PropInteraction>();

        BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(1.5f, 1.5f);
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
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();

        // A visible placeholder, not final art. An empty SpriteRenderer renders
        // nothing, which makes a freshly built scaffold impossible to find in
        // the scene view and impossible to walk up to on purpose. Replace with
        // Dylan's sprites when they land.
        renderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

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
