// The derived economy state of a door connection, shown on its keypad's
// status light. Only Purchased is ever STORED (in DoorStateRegistry) -
// everything else is derived per query from (Purchased, current balance,
// peak balance), so the display self-corrects in both directions.
//
// Decided 2026-07-15: purchases are PERMANENT. "Suspended" never applies to
// a purchased door - it is the derived state of an UNPURCHASED door whose
// cost was affordable at some point and no longer is.
public enum DoorEconomyStatus
{
  // clickCost == 0: no keypad needed, the connection never blocks on money.
  NotPriced,

  // Red: not purchased, never had the balance to buy it.
  Locked,

  // Orange: not purchased, current balance covers the cost - can buy now.
  Unlockable,

  // Yellow: not purchased, balance covered the cost once but not anymore.
  Suspended,

  // Green: purchased. Permanent.
  Purchased,
}
