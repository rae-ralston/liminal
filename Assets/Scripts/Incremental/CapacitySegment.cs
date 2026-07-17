// One contribution to the building's total charge capacity (The Circuit,
// 2026-07-16). A segment is either a room's base capacity (granted when its
// Terminal activates the room) or a collected CapacityUpgrade prop.
//
// Segments exist ONLY for gauge views and debugging - the fill math never
// iterates them. Proportional fill everywhere is just Count / MaxCapacity
// (decision 3: adding capacitance at constant charge drops the voltage across
// the whole bank, so every bar in the building shows the same fraction). The
// maintained MaxCapacity is the sum of every segment's size plus the bootstrap
// capacity floor; RecalculateCapacity() re-derives it from this list to assert
// the two never drift.
public readonly struct CapacitySegment
{
    // Room this segment belongs to (which terminal's gauge shows it).
    public readonly RoomId Room;

    // Room asset name for a base segment, propId for a collected
    // CapacityUpgrade - lets a TerminalGauge slot resolve itself.
    public readonly string SourceId;

    public readonly long Size;

    public CapacitySegment(RoomId room, string sourceId, long size)
    {
        Room = room;
        SourceId = sourceId;
        Size = size;
    }
}
