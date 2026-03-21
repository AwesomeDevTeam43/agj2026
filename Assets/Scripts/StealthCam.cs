using UnityEngine;

//we using cinemachine for this :DDDD 

public class StealthCam : MonoBehaviour
{
    [Header("References")]
    public Transform player;

    [Header("Camera Feel")]

    public float maxPeekDistance = 3f;
    public float moveSpeed = 8f;

    private Camera _camMain;

    private void Start()
    {
        _camMain = Camera.main;

        transform.SetParent(null);
    }

    private void LateUpdate()
    {
        if (player == null) return;

        Vector3 mouseWorldPos = _camMain.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0;

        Vector3 directionToMouse = mouseWorldPos - player.position;

        float clampedDistance = Mathf.Clamp(directionToMouse.magnitude, 0, maxPeekDistance);

        Vector3 idealPosition = player.position + directionToMouse.normalized * clampedDistance;
        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
        transform.position = Vector3.Lerp(transform.position, idealPosition, moveSpeed * Time.deltaTime);
        }
        else
        {
            transform.position = player.position;
        }
    }
}