using UnityEngine;

public class Surface : MonoBehaviour
{
    public SurfaceType surfaceType;
}

public enum SurfaceType
{
    concrete,
    tile,
    linoleum,
    carpet,
    wood
}