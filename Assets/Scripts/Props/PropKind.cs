// Gameplay-oriented prop taxonomy. This is deliberately NOT the sound-list
// category taxonomy (Room Ambience, Wrongness etc. are sounds, not props) -
// it describes what role a prop plays in the game.
public enum PropKind
{
    Decoration,        // sprite + maybe ambient audio, no gameplay effect
    IncrementalSource, // starts or feeds The Incremental (computers, buttons, levers)
    EndButton          // the summoned end-of-game button
}
