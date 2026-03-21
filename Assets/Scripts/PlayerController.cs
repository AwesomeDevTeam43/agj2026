using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private Rigidbody2D rb;
    private InputHandler input;

    [SerializeField] private Shape_Data shapeData;
    [SerializeField] private float moveSpeed = 20f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        input = GetComponent<InputHandler>();
    }

    void Update()
    {
        Movement();
    }   

    void Movement()
    {
        Vector2 movement = new Vector2(input.MovementInput.x, input.MovementInput.y);
        movement.Normalize();
        rb.linearVelocity = movement * moveSpeed;
    }
}
