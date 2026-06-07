using UnityEngine;
using UnityEngine.InputSystem;

public class FlashlightAimToMouse : MonoBehaviour
{
    Camera cam;

    void Awake()
    {
        cam = Camera.main;
    }

    void Update()
    {
        Vector2 mousePos =
            Mouse.current.position.ReadValue();

        Vector3 worldPos =
            cam.ScreenToWorldPoint(mousePos);

        Vector2 direction =
            worldPos - transform.position;

        float angle =
            Mathf.Atan2(direction.y, direction.x)
            * Mathf.Rad2Deg -90f;

        transform.rotation =
            Quaternion.AngleAxis(angle, Vector3.forward);
    }
}