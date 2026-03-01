using UnityEngine;

namespace SoR.Gameplay
{
    /// <summary>
    /// Reads legacy UnityEngine.Input and drives PlayerController each frame.
    /// Drop-in replacement for the InputSystem-based InputReader when no
    /// .inputactions asset is available.
    /// </summary>
    public class SimpleInputHandler : MonoBehaviour
    {
        private PlayerController _controller;
        private CharacterController _characterController;
        private float _verticalVelocity;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _characterController = GetComponent<CharacterController>();
        }

        private void Update()
        {
            if (_controller == null) return;

            // Movement — WASD / arrow keys
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            _controller.InputDirection = new Vector3(h, 0f, v).normalized;

            // Attack — left mouse button
            if (Input.GetMouseButtonDown(0))
                _controller.HandleAttackInput();

            // Dodge — Space
            if (Input.GetKeyDown(KeyCode.Space))
                _controller.HandleDodgeInput();

            // Skills — 1, 2, 3, 4
            if (Input.GetKeyDown(KeyCode.Alpha1)) _controller.HandleSkillInput(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) _controller.HandleSkillInput(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) _controller.HandleSkillInput(2);
            if (Input.GetKeyDown(KeyCode.Alpha4)) _controller.HandleSkillInput(3);

            // Gravity
            ApplyGravity();
        }

        private void ApplyGravity()
        {
            if (_characterController == null) return;

            if (_characterController.isGrounded)
                _verticalVelocity = -0.5f;
            else
                _verticalVelocity += Physics.gravity.y * Time.deltaTime;

            _characterController.Move(new Vector3(0f, _verticalVelocity * Time.deltaTime, 0f));
        }
    }
}
