using UnityEngine;
public class PlayerController : MonoBehaviour
{
    private Rigidbody2D rb;
    private InputHandler input;

    [SerializeField] private ShapeData shapeData;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float acceleration = 8f;
    [SerializeField] private float deceleration = 15f;

    [Header("Rotation")]
    [SerializeField] private float rotationSmoothTime = 0.12f;
    [SerializeField] private float maxTurnSpeed = 720f;
    private float rotationVelocity;

    private Vector2 currentVelocity;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        input = GetComponent<InputHandler>();
    }

    void Update()
    {
        Movement();
        Rotation();
    }

    void FixedUpdate()
    {
        rb.linearVelocity = currentVelocity;
    }

    void Movement()
    {
        Vector2 movement = new Vector2(input.MovementInput.x, input.MovementInput.y).normalized;
        Vector2 targetVelocity = movement * moveSpeed;
        float lerpSpeed = (targetVelocity.magnitude > 0) ? acceleration : deceleration;
        currentVelocity = Vector2.Lerp(currentVelocity, targetVelocity, lerpSpeed * Time.deltaTime);
    }

    void Rotation()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 direction = mousePos - transform.position;
        if (direction.sqrMagnitude < 0.0001f) return;

        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        float currentAngle = transform.eulerAngles.z;

        float smoothedAngle = Mathf.SmoothDampAngle(
            currentAngle,
            targetAngle,
            ref rotationVelocity,
            rotationSmoothTime,
            maxTurnSpeed,
            Time.deltaTime
        );

        transform.rotation = Quaternion.Euler(0f, 0f, smoothedAngle);
    }
}

