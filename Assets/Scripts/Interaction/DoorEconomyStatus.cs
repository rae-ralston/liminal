// The derived economy state of a door connection, shown on its keypad's
// status light. Only Purchased is ever STORED (in DoorStateRegistry) -
// everything else is derived per query from the current IS-state
// (Purchased, current balance, MaxCapacity), so the display self-corrects
// in both directions and never depends on balance HISTORY.
//
// Decided 2026-07-15: purchases are PERMANENT, so no unlocked door regresses.
// Revised 2026-07-20: the red/yellow split is capacity-based, not history-
// based - it answers "what should the player DO?" (raise capacity vs keep
// earning) instead of "did they once hold this much?".
//
// NOTE: the member names Locked/Suspended are legacy - they now mean
// "beyond capacity" / "within capacity but unsaved". Kept as-is because
// DoorIndicatorLight's color fields (suspendedColor, lockedColor) are
// serialized on the keypad prefab; renaming would drop their stored values.
public enum DoorEconomyStatus
{
  // clickCost == 0: no keypad needed, the connection never blocks on money.
  NotPriced,

  // Red: not purchased, cost exceeds MaxCapacity - you can't even store
  // enough charge to buy it yet. Activate more rooms to raise capacity.
  Locked,

  // Orange: not purchased, current balance covers the cost - can buy now.
  Unlockable,

  // Yellow: not purchased, cost fits within MaxCapacity but current balance
  // doesn't cover it yet - attainable, just keep earning.
  Suspended,

  // Green: purchased. Permanent.
  Purchased,
}
