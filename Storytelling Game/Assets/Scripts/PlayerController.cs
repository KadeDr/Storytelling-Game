using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using System.Collections;
using TMPro;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CinemachinePanTilt _cineCamera;
    [SerializeField] private PlayerInputs _playerInputs; // Auto-generated input class

    [Header("Settings")]
    [SerializeField] private float _sprintMultiplier = 1.5f; // Adjustable
    [SerializeField] private float _initialMoveSpeed = 5f; // Base move speed
    [SerializeField] private float _mouseSensitivity = 1.5f;
    [SerializeField] private float _jumpHeight = 2f;
    [SerializeField] private float _gravity = -9.81f;
    [SerializeField] private string _itemTagName = "Item";
    [SerializeField] private float _pickupRange = 2f;
    [SerializeField] private float _rotationSpeed = 100f; // Adjustable
    [SerializeField] private Transform _itemDefaultLocation;
    [SerializeField] private TextMeshProUGUI _holdDistanceSensitivityText;
    [SerializeField] private CanvasGroup _holdDistanceSensitivityGroup;
    [SerializeField] private Vector3 _fadeInOffset = new Vector3(0f, -20f, 0f);
    [SerializeField] private Vector3 _fadeOutOffset = new Vector3(0f, 10f, 0f); // slide a bit up when fading out
    [SerializeField] private AnimationCurve _fadeOutCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("Current State (Read-Only)")]
    [ReadOnly][SerializeField] private GameObject _heldItem;
    [ReadOnly][SerializeField] private float _heldItemDistance = 1f;
    [ReadOnly][SerializeField] private float _holdDistanceSpeed = 0.2f; // Adjustable sensitivity
    [ReadOnly][SerializeField] private float _moveSpeed = 5f;

    private CharacterController _controller;
    private Vector2 _moveInput;
    private Vector2 _lookInput;
    private Vector2 _itemRotateInput;
    private Vector3 _initialTextPosition;

    private float _yaw;   // Player rotation
    private float _pitch; // Camera rotation (up/down)
    private float _verticalVelocity;
    private float _itemRollInput;

    private bool _isShiftHeld;
    private bool _isRotatingItem = false;

    Vector3 hiddenOffset = new Vector3(0f, -20f, 0f); // start 20 units below
    Vector3 visiblePosition = new Vector3(0f, 0f, 0f);           // final position


    private enum SensitivityTextState
    {
        Hidden,
        FadingIn,
        Visible,
        FadingOut
    }

    private SensitivityTextState _textState = SensitivityTextState.Hidden;
    private Coroutine _fadeCoroutine;

    private void Awake()
    {
        _playerInputs = new PlayerInputs();
    }

    private void OnEnable()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        _playerInputs.Player.Enable();

        // Subscribe to input events
        _playerInputs.Player.Move.performed += OnMove;
        _playerInputs.Player.Move.canceled += OnMove;
        _playerInputs.Player.Look.performed += OnLook;
        _playerInputs.Player.Look.canceled += OnLook;
        _playerInputs.Player.Jump.performed += OnJump;
        _playerInputs.Player.PickUp.performed += OnPickUp;
        _playerInputs.Player.ChangeHoldDistance.performed += OnChangeHoldDistance;
        // _playerInputs.Player.ChangeHoldDistance.canceled += OnChangeHoldDistance;
        _playerInputs.Player.Modifier.performed += OnModifierPerformed;
        _playerInputs.Player.Modifier.canceled += OnModifierCanceled;
        _playerInputs.Player.RotateModifier.performed += OnRotateModifierPressed;
        _playerInputs.Player.RotateModifier.canceled += OnRotateModifierReleased;
        _playerInputs.Player.Rotate.performed += OnItemRotate;
        _playerInputs.Player.Rotate.canceled += OnItemRotate;
        _playerInputs.Player.RotateRoll.performed += OnItemRoll;
        _playerInputs.Player.RotateRoll.canceled += OnItemRoll;
        _playerInputs.Player.Drop.performed += OnDrop;
        _playerInputs.Player.Sprint.performed += OnSprintPerformed;
        _playerInputs.Player.Sprint.canceled += OnSprintCanceled;
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
        // _playerInputs.Player.ChangeHoldDistance.canceled -= OnChangeHoldDistance;
        _playerInputs.Player.Modifier.performed -= OnModifierPerformed;
        _playerInputs.Player.Modifier.canceled -= OnModifierCanceled;
        _playerInputs.Player.RotateModifier.performed -= OnRotateModifierPressed;
        _playerInputs.Player.RotateModifier.canceled -= OnRotateModifierReleased;
        _playerInputs.Player.Rotate.performed -= OnItemRotate;
        _playerInputs.Player.Rotate.canceled -= OnItemRotate;
        _playerInputs.Player.RotateRoll.performed -= OnItemRoll;
        _playerInputs.Player.RotateRoll.canceled -= OnItemRoll;
        _playerInputs.Player.Drop.performed -= OnDrop;
        _playerInputs.Player.Sprint.performed -= OnSprintPerformed;
        _playerInputs.Player.Sprint.canceled -= OnSprintCanceled;

        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }

        _playerInputs.Player.Disable();
    }

    private void Start()
    {
        _initialTextPosition = _holdDistanceSensitivityText.rectTransform.localPosition;
        _controller = GetComponent<CharacterController>();

        if (_holdDistanceSensitivityGroup == null)
        {
            Debug.LogError("HoldDistanceSensitivityGroup not assigned!");
        }
        else
        {
            // Ensure text GameObject is active so CanvasGroup alpha works.
            _holdDistanceSensitivityText.gameObject.SetActive(true);
            _holdDistanceSensitivityGroup.alpha = 0f;
            _holdDistanceSensitivityGroup.interactable = false;
            _holdDistanceSensitivityGroup.blocksRaycasts = false;
        }
    }

    private void Update()
    {
        HandleMovement();
        HandleLook();

        // Smooth item distance update (your existing code)...

        if (_heldItem != null && _isRotatingItem)
        {
            // Rotate pitch (X) and yaw (Y) based on mouse movement
            _heldItem.transform.Rotate(
                Vector3.up, _itemRotateInput.x * _rotationSpeed * Time.deltaTime, Space.World);

            _heldItem.transform.Rotate(
                Vector3.right, -_itemRotateInput.y * _rotationSpeed * Time.deltaTime, Space.World);
        }
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
                foreach (var col in _heldItem.GetComponentsInChildren<Collider>())
                {
                    col.enabled = false;
                }
                _heldItem.transform.localPosition = new Vector3(0f, 0f, _heldItemDistance);
            }
        }
    }

    private float _visibleTimer = 0f;

private void OnChangeHoldDistance(InputAction.CallbackContext ctx)
{
    float scrollDelta = ctx.ReadValue<float>();

    if (_heldItem == null) return;

    if (_isShiftHeld)
    {
        // Adjust hold distance sensitivity
        _holdDistanceSpeed = Mathf.Clamp(_holdDistanceSpeed + scrollDelta * 0.05f, 0.1f, 3f);

        _holdDistanceSensitivityText.text = $"Hold Distance Sensitivity: {_holdDistanceSpeed:F2} Kade's";

        // Reset visible timer
        _visibleTimer = 2f;

        // Start the coroutine if not running
        if (_fadeCoroutine == null)
        {
            _fadeCoroutine = StartCoroutine(FadeSensitivityTextSlideCurve());
        }

        return;
    }

    if (_isRotatingItem)
    {
        _heldItem.transform.Rotate(_cineCamera.transform.forward, scrollDelta * _rotationSpeed, Space.World);
        return;
    }

    // Normal scroll → move item
    _heldItemDistance = Mathf.Clamp(_heldItemDistance + scrollDelta * _holdDistanceSpeed, 0.5f, 3f);
    _heldItem.transform.localPosition = new Vector3(0f, 0f, _heldItemDistance);
}

private IEnumerator FadeSensitivityTextSlideCurve(float fadeTime = 0.25f)
{
    _textState = SensitivityTextState.FadingIn;
    RectTransform rt = _holdDistanceSensitivityText.rectTransform;

    _holdDistanceSensitivityText.gameObject.SetActive(true);

    Vector3 startPos = _initialTextPosition + _fadeInOffset;
    Vector3 endPos = _initialTextPosition;

    float t = 0f;
    while (t < fadeTime)
    {
        t += Time.deltaTime;
        float alpha = Mathf.Lerp(_holdDistanceSensitivityGroup.alpha, 1f, t / fadeTime);
        _holdDistanceSensitivityGroup.alpha = alpha;
        rt.localPosition = Vector3.Lerp(startPos, endPos, t / fadeTime);
        yield return null;
    }

    _holdDistanceSensitivityGroup.alpha = 1f;
    rt.localPosition = endPos;
    _textState = SensitivityTextState.Visible;

    // Keep visible while _visibleTimer > 0
    while (_visibleTimer > 0f)
    {
        _visibleTimer -= Time.deltaTime;
        yield return null;
    }

    _textState = SensitivityTextState.FadingOut;

    // Fade-out using curve + slide up
    t = 0f;
    Vector3 fadeOutStart = rt.localPosition;
    Vector3 fadeOutEnd = _initialTextPosition + _fadeOutOffset;

    while (t < fadeTime)
    {
        t += Time.deltaTime;
        float alpha = _fadeOutCurve.Evaluate(t / fadeTime);
        _holdDistanceSensitivityGroup.alpha = alpha;
        rt.localPosition = Vector3.Lerp(fadeOutStart, fadeOutEnd, t / fadeTime);
        yield return null;
    }

    _holdDistanceSensitivityGroup.alpha = 0f;
    rt.localPosition = _initialTextPosition;
    _textState = SensitivityTextState.Hidden;
    _fadeCoroutine = null;
}


    // Coroutine to keep the text visible without moving it
    private IEnumerator KeepTextVisible(float duration)
    {
        _textState = SensitivityTextState.Visible;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // After timer, fade out normally
        _fadeCoroutine = StartCoroutine(FadeSensitivityTextSlideCurve(0f));
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
        if (_isRotatingItem) return;

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

    private void OnModifierPerformed(InputAction.CallbackContext ctx) => _isShiftHeld = true;
    private void OnModifierCanceled(InputAction.CallbackContext ctx) => _isShiftHeld = false;

    private void OnRotateModifierPressed(InputAction.CallbackContext ctx)
    {
        _isRotatingItem = true;
    }

    private void OnRotateModifierReleased(InputAction.CallbackContext ctx)
    {
        _isRotatingItem = false;
    }

    private void OnItemRotate(InputAction.CallbackContext ctx)
    {
        if (!_isRotatingItem) return;
        _itemRotateInput = ctx.ReadValue<Vector2>();
    }

    private void OnItemRoll(InputAction.CallbackContext ctx)
    {
        if (!_isRotatingItem) return;
        _itemRollInput = ctx.ReadValue<float>();
    }

    private void OnDrop(InputAction.CallbackContext ctx)
    {
        if (_heldItem != null)
        {
            // Implement item drop logic here
            Debug.Log("Dropped: " + _heldItem.name);
            _heldItem.transform.SetParent(_itemDefaultLocation);
            Rigidbody rb = _heldItem.GetComponent<Rigidbody>();
            rb.isKinematic = false;
            foreach (var col in _heldItem.GetComponentsInChildren<Collider>())
            {
                col.enabled = true;
            }
            _heldItem = null;
        }
    }

    private void OnSprintPerformed(InputAction.CallbackContext ctx)
    {
        _moveSpeed = _initialMoveSpeed * _sprintMultiplier;
    }

    private void OnSprintCanceled(InputAction.CallbackContext ctx)
    {
        _moveSpeed = _initialMoveSpeed;
    }

    // --------------------
    // Coroutines
    // --------------------

    private IEnumerator FadeSensitivityTextSlideCurve(float visibleDuration, float fadeTime = 0.25f)
    {
        _textState = SensitivityTextState.FadingIn;

        _holdDistanceSensitivityText.gameObject.SetActive(true);
        RectTransform rt = _holdDistanceSensitivityText.rectTransform;

        // Base position = where the text normally sits in the UI
        Vector3 basePos = _initialTextPosition;

        // Start slightly below for fade-in
        Vector3 startPos = basePos + _fadeInOffset;
        Vector3 endPos = basePos;

        float startAlpha = _holdDistanceSensitivityGroup.alpha;
        float t = 0f;

        // Fade-in + slide
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Lerp(startAlpha, 1f, t / fadeTime);
            _holdDistanceSensitivityGroup.alpha = alpha;
            rt.localPosition = Vector3.Lerp(startPos, endPos, t / fadeTime);
            yield return null;
        }

        _holdDistanceSensitivityGroup.alpha = 1f;
        rt.localPosition = endPos;
        _textState = SensitivityTextState.Visible;

        // Stay visible
        yield return new WaitForSeconds(visibleDuration);

        _textState = SensitivityTextState.FadingOut;

        // Fade-out using curve + slide up
        t = 0f;
        Vector3 fadeOutStart = rt.localPosition;
        Vector3 fadeOutEnd = basePos + _fadeOutOffset;

        while (t < fadeTime)
        {
            t += Time.deltaTime;
            float alpha = _fadeOutCurve.Evaluate(t / fadeTime);
            _holdDistanceSensitivityGroup.alpha = alpha;
            rt.localPosition = Vector3.Lerp(fadeOutStart, fadeOutEnd, t / fadeTime);
            yield return null;
        }

        _holdDistanceSensitivityGroup.alpha = 0f;
        rt.localPosition = basePos;
        _textState = SensitivityTextState.Hidden;
        _fadeCoroutine = null;
    }
}
