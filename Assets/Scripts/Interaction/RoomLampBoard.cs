using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal; // Light2D

// The SecurityRoom board (Ending brief E3): one BINARY lamp per room -
// dark/ghosted = unpowered, lit = activated. NO per-lamp fill bars (under
// Circuit decision #3 every gauge shows the same fraction, so 24 fill bars
// would be 24 identical bars carrying zero per-room information). The lamps
// say WHICH rooms; a separate CapacityColumn placement says HOW FULL.
//
// All lamps are present from the first minute - the dead board foreshadows the
// size of the job, and the first lamp lighting retroactively explains it. Zone
// grouping (TOP/MIDDLE/BOTTOM, design doc #5) is PURE sprite layout in the
// scene: three parent transforms, no code, no enum here.
//
// Dumb polling view (no pub/sub); never writes state.
public class RoomLampBoard : MonoBehaviour
{
    [System.Serializable]
    class RoomLamp
    {
        [Tooltip("The room this lamp represents (RoomId asset). Empty = lamp forced dark.")]
        public RoomId room;
        [Tooltip("Lamp sprite - lit colour when the room is activated, ghost colour otherwise.")]
        public SpriteRenderer lamp;
        [Tooltip("Optional Light2D glow - enabled while the room is activated.")]
        public Light2D glow;
    }

    [Tooltip("One entry per room. Should cover the Incremental all-rooms list EXACTLY (cross-checked at Start).")]
    [SerializeField] List<RoomLamp> lamps = new List<RoomLamp>();

    [Header("Colors")]
    [SerializeField] Color litColor = new Color(0.2f, 0.85f, 0.3f);
    // Unlit = a dim GREEN lamp, not grey - so a powered room reads as the same
    // lamp brightening AND its Light2D switching on, rather than a colour swap
    // alone. The board generator forces this onto the placed board too.
    [SerializeField] Color ghostColor = new Color(0.12f, 0.35f, 0.18f);

    [Tooltip("Approx seconds to ease each lamp's colour on activation - no snapping.")]
    [SerializeField] float lerpSeconds = 0.3f;

    void Start()
    {
        WarnIfLampListMismatched();
    }

    void Update()
    {
        Incremental incremental = Incremental.Instance;
        if (incremental == null)
        {
            return;
        }

        float t = lerpSeconds > 0f ? 1f - Mathf.Exp(-Time.deltaTime / lerpSeconds) : 1f;

        foreach (RoomLamp entry in lamps)
        {
            if (entry == null || entry.lamp == null)
            {
                continue;
            }

            bool on = entry.room != null && incremental.IsRoomActivated(entry.room);
            entry.lamp.color = Color.Lerp(entry.lamp.color, on ? litColor : ghostColor, t);

            if (entry.glow != null && entry.glow.enabled != on)
            {
                entry.glow.enabled = on;
            }
        }
    }

    // A silent miscount here would make the ending look ungatable - a lamp that
    // never lights, or a room with no lamp at all. Warn loudly at Start
    // (brief E3), cross-checking against Incremental's configured room list.
    void WarnIfLampListMismatched()
    {
        Incremental incremental = Incremental.Instance;
        if (incremental == null)
        {
            return;
        }

        HashSet<RoomId> lampRooms = new HashSet<RoomId>();
        foreach (RoomLamp entry in lamps)
        {
            if (entry == null || entry.room == null)
            {
                continue;
            }

            if (!lampRooms.Add(entry.room))
            {
                Debug.LogWarning($"[Ending] RoomLampBoard: duplicate lamp for room '{entry.room.name}'.", this);
            }
        }

        foreach (RoomId room in incremental.AllRooms)
        {
            if (room != null && !lampRooms.Contains(room))
            {
                Debug.LogWarning($"[Ending] RoomLampBoard: no lamp for room '{room.name}' - it can never light.", this);
            }
        }
    }
}
