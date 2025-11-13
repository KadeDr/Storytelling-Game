using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CinemachinePanTilt _cineCamera;
    [SerializeField] private PlayerInputs _playerInputs; // Auto-generated input class

    [Header("Settings")]
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _mouseSensitivity = 1.5f;
    [SerializeField] private float _jumpHeight = 2f;
    [SerializeField] private float _gravity = -9.81f;
    [SerializeField] private string _itemTagName = "Item";
    [SerializeField] private float _pickupRange = 2f;

    [Header("Current State (Read-Only)")]
    [ReadOnly][SerializeField] private GameObject _heldItem;
    [ReadOnly][SerializeField] private float _heldItemDistance = 1f;

    private CharacterController _controller;
    private Vector2 _moveInput;
    private Vector2 _lookInput;

    private float _yaw;   // Player rotation
    private float _pitch; // Camera rotation (up/down)
    private float _verticalVelocity;

    private void OnEnable()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        _playerInputs = new PlayerInputs();
        _playerInputs.Player.Enable();

        // Subscribe to input events
        _playerInputs.Player.Move.performed += OnMove;
        _playerInputs.Player.Move.canceled += OnMove;
        _playerInputs.Player.Look.performed += OnLook;
        _playerInputs.Player.Look.canceled += OnLook;
        _playerInputs.Player.Jump.performed += OnJump;
        _playerInputs.Player.PickUp.performed += OnPickUp;
        _playerInputs.Player.ChangeHoldDistance.performed += OnChangeHoldDistance;
        _playerInputs.Player.ChangeHoldDistance.canceled += OnChangeHoldDistance;
    }

    private void OnDisable()
    {
        // Unsubscribe
        _playerInputs.Player.Move.performed -= OnMove;
        _playerInputs.Player.Move.canceled -= OnMove;
        _playerInputs.Player.Look.performed -= OnLook;
        _playerInputs.Player.Look.canceled -= OnLook;
        _playerInputs.Player.Jump.performed -= OnJump;
        _playerInputs.Player.PickUp.performed -= OnPickUp;
        _playerInputs.Player.ChangeHoldDistance.performed -= OnChangeHoldDistance;
        _playerInputs.Player.ChangeHoldDistance.canceled -= OnChangeHoldDistance;

        _playerInputs.Player.Disable();
    }

    private void Start()
    {
        _controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        HandleMovement();
        HandleLook();
    }

    private void OnMove(InputAction.CallbackContext ctx)
    {
        _moveInput = ctx.ReadValue<Vector2>();
    }

    private void OnLook(InputAction.CallbackContext ctx)
    {
        _lookInput = ctx.ReadValue<Vector2>();
    }

    private void OnJump(InputAction.CallbackContext ctx)
    {
        if (_controller.isGrounded)
        {
            _verticalVelocity = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
        }
    }

    private void OnPickUp(InputAction.CallbackContext ctx)
    {
        // Raycast in front of player to pick up items
        Ray ray = new Ray(_cineCamera.transform.position, _cineCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, _pickupRange))
        {
            print("Ray hit something!");
            if (hit.collider.CompareTag(_itemTagName))
            {
                // Implement item pickup logic here
                Debug.Log("Picked up: " + hit.collider.name);
                _heldItem = hit.collider.gameObject;
                _heldItem.transform.SetParent(_cineCamera.transform);
                _heldItem.GetComponent<Rigidbody>().isKinematic = true;
                _heldItem.GetComponent<BoxCollider>().enabled = false;
                _heldItem.transform.localPosition = new Vector3(0f, 0f, _heldItemDistance);
            }
        }
    }
    
    [SerializeField] private float _holdDistanceSpeed = 2f; // Adjustable sensitivity

    private void OnChangeHoldDistance(InputAction.CallbackContext ctx)
    {
        if (_heldItem == null) return;

        float scrollDelta = ctx.ReadValue<float>();

        // Adjust distance with sensitivity and clamp
        _heldItemDistance = Mathf.Clamp(
            _heldItemDistance + scrollDelta * _holdDistanceSpeed * Time.deltaTime,
            0.5f,
            3f
        );

        _heldItem.transform.localPosition = new Vector3(0f, 0f, _heldItemDistance);
    }


    private void HandleMovement()
    {
        // Ground check and gravity
        if (_controller.isGrounded && _verticalVelocity < 0)
        {
            _verticalVelocity = -2f; // keeps grounded
        }

        _verticalVelocity += _gravity * Time.deltaTime;

        // Convert input to world-space movement
        Vector3 move = new Vector3(_moveInput.x, 0f, _moveInput.y);
        move = transform.TransformDirection(move) * _moveSpeed;

        // Apply gravity & jump velocity
        move.y = _verticalVelocity;

        _controller.Move(move * Time.deltaTime);
    }

    private void HandleLook()
    {
        // Yaw (horizontal) turns player
        _yaw += _lookInput.x * _mouseSensitivity;
        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

        // Pitch (vertical) moves camera only
        _pitch -= _lookInput.y * _mouseSensitivity;
        _pitch = Mathf.Clamp(_pitch, -80f, 80f);

        if (_cineCamera != null)
        {
            _cineCamera.PanAxis.Value = _yaw;
            _cineCamera.TiltAxis.Value = _pitch;
        }
    }
}
