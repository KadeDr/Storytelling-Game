using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] CinemachinePanTilt _cineCamera;
    [SerializeField] InputActionReference _moveInputAction;
    [SerializeField] float _moveSpeed = 5f;

    private void OnEnable()
    {
        _moveInputAction.action.Enable();
    }

    private void OnDisable()
    {
        _moveInputAction.action.Disable();
    }

    void Update()
    {
        // Get movement input and convert it to a 3D vector
        Vector2 moveInput = _moveInputAction.action.ReadValue<Vector2>();
        Vector3 moveInputIn3D = new Vector3(moveInput.x, 0, moveInput.y);

        // Take the pan angle from the CinemachinePanTilt component
        float panAngle = _cineCamera.PanAxis.Value;
        Quaternion panRotation = Quaternion.Euler(0, panAngle, 0);

        // Rotate the movement input based on the pan angle
        Vector3 moveDirection = panRotation * moveInputIn3D;

        // Move the player, and update the direction they're facing
        transform.Translate(moveDirection * Time.deltaTime * _moveSpeed, Space.World);
        transform.localRotation = panRotation;
    }
}