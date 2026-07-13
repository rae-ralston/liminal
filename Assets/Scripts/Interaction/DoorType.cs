// Door character - drives which door sound plays via a single FMOD event
// selected by a labeled parameter (see PropAudio.PlayOpen/PlayLocked).
//
// FMOD CONTRACT: the "DoorType" labeled parameter on the door event must have
// a label for each name below, spelled EXACTLY the same (the enum's ToString()
// is passed to setParameterByNameWithLabel). Add a value here => add the
// matching label in FMOD Studio.
public enum DoorType
{
  WoodenInterior,
  GlassOffice,
  HeavyFire,
  RestroomStall,
  Exterior
}
