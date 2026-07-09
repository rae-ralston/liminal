// The one capability interface in the new design. Implemented by the small
// effect components below (IncrementalStarter, FlatClickReward,
// MultiplierUpgrade, ClickSource, EndButtonSummoner). An interaction system
// can do GetComponents<IIncrementalEffect>() on a prop and apply everything
// found - a single prop can carry several effects (e.g. a lever that adds
// flat clicks AND bumps the multiplier once).
//
// Deliberately dropped from the old design: IPickupable, IMovable,
// IDestructible, IInitializable. Nothing in the clicker loop needs
// pickup/physics, and cutting them removes the whole audio-state-machine
// problem. If a hero prop ever needs pickup, hand-build it on that prop.
public interface IIncrementalEffect
{
    // Called by the interaction system when the player interacts with the
    // prop carrying this effect.
    void Apply();
}
