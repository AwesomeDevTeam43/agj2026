using UnityEngine;
public class PlayerController : MonoBehaviour
{
    private Rigidbody2D rb;
    private InputHandler input;

    [SerializeField] private ShapeData shapeData;
    [SerializeField] private float moveSpeed = 20f;
    private ShapeVisualizer shapeVisualizer;

    void Awake()
    {
        shapeVisualizer = GetComponent<ShapeVisualizer>();
    }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        input = GetComponent<InputHandler>();
        if (shapeData != null)
        {
            shapeVisualizer.ApplyShape(shapeData);
        }
    }

    void Update()
    {
        Movement();
        Rotation();
    }

    void Movement()
    {
        Vector2 movement = new Vector2(input.MovementInput.x, input.MovementInput.y);
        movement.Normalize();
        rb.linearVelocity = movement * moveSpeed;
    }

    void Rotation()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 10f));
        Vector2 direction = new Vector2(mousePos.x - transform.position.x, mousePos.y - transform.position.y);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
    }
}
