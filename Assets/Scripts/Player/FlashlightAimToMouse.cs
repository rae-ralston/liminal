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
        // The pause menu freezes time; the splash and the end card freeze
        // movement. Neither stops Update(), and this aim is pure input ->
        // rotation with no time term, so it has to opt out explicitly or the
        // beam keeps tracking the cursor the player is using on the menu.
        if (Time.timeScale == 0f)
        {
            return;
        }

        if (GameManager.Instance != null && GameManager.Instance.MovementFrozen)
        {
            return;
        }

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