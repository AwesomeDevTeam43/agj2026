
using UnityEngine;
using UnityEngine.InputSystem;

public class InputHandler : MonoBehaviour
{
    [Header("Input Action Asset")]
    [SerializeField] private InputActionAsset playerControls;

    [Header("Action Map Name Reference")]
    [SerializeField] private string actionMapName = "Player";

    private InputAction movementAction;
    /*private InputAction lookAction;
    private InputAction aimAction;
    private InputAction jumpAction;
    private InputAction sprintAction;*/

    public Vector2 MovementInput { get; private set; }
    /*public Vector2 LookInput { get; private set; }
    public bool AimTriggered { get; private set; }
    public bool JumpTriggered { get; private set; }
    public bool SprintTriggered { get; private set; }*/

    private void Awake()
    {
        InputActionMap mapReference = playerControls.FindActionMap(actionMapName);

        movementAction = mapReference.FindAction("Move");
        /*jumpAction = mapReference.FindAction("Jump");
        lookAction = mapReference.FindAction("Look");
        aimAction = mapReference.FindAction("Aim");
        sprintAction = mapReference.FindAction("Sprint");*/

        InputEvents();
    }

    private void InputEvents()
    {
        movementAction.performed += inputInfo => MovementInput = inputInfo.ReadValue<Vector2>();
        movementAction.canceled += inputInfo => MovementInput = Vector2.zero;
        /*
        jumpAction.performed += inputInfo => JumpTriggered = true;
        jumpAction.canceled += inputInfo => JumpTriggered = false;
        
        lookAction.performed += inputInfo => LookInput = inputInfo.ReadValue<Vector2>();
        lookAction.canceled += inputInfo => LookInput = Vector2.zero;

        aimAction.performed += inputInfo => AimTriggered = true;
        aimAction.canceled += inputInfo => AimTriggered = false;

        sprintAction.performed += inputInfo => SprintTriggered = true;
        sprintAction.canceled += inputInfo => SprintTriggered = false;*/
    }

    private void OnEnable()
    {
        playerControls.FindActionMap(actionMapName).Enable();
    }

    private void OnDisable()
    {
        playerControls.FindActionMap(actionMapName).Disable();
    }
}