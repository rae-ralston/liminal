using UnityEngine;

// Slim identity component - the only thing every prop has. No behavior,
// no capability assumptions. Capabilities (interaction, incremental effects,
// audio) are separate components dropped onto the same GameObject in the
// Inspector.
public class Prop : MonoBehaviour
{
    [SerializeField] string propId;
    [SerializeField] PropKind kind;

    public string PropId => propId;

    public PropKind Kind => kind;
}
