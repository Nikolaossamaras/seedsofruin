using UnityEngine;

namespace SoR.Gameplay
{
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float _moveSpeed = 6f;
        [SerializeField] private float _dodgeSpeed = 14f;
        [SerializeField] private float _dodgeDistance = 4f;
        [SerializeField] private float _rotationSpeed = 720f;

        [Header("References")]
        [SerializeField] private CharacterController _characterController;

        public float MoveSpeed => _moveSpeed;
        public float DodgeSpeed => _dodgeSpeed;
        public float DodgeDistance => _dodgeDistance;
        public bool IsGrounded => _characterController != null && _characterController.isGrounded;

        public void Initialize(float moveSpeed, float dodgeSpeed, float dodgeDistance)
        {
            _moveSpeed = moveSpeed;
            _dodgeSpeed = dodgeSpeed;
            _dodgeDistance = dodgeDistance;
        }

        /// <summary>
        /// Allows runtime-created objects to wire the CharacterController from code.
        /// </summary>
        public void SetCharacterController(CharacterController cc)
        {
            _characterController = cc;
        }

        public void Move(Vector3 direction)
        {
            if (_characterController == null || direction.sqrMagnitude < 0.01f)
                return;

            Vector3 velocity = direction.normalized * _moveSpeed;
            _characterController.Move(velocity * Time.deltaTime);
            FaceDirection(direction);
        }

        public void Dodge(Vector3 direction)
        {
            if (_characterController == null)
                return;

            Vector3 dodgeDirection = direction.sqrMagnitude > 0.01f
                ? direction.normalized
                : transform.forward;

            Vector3 velocity = dodgeDirection * _dodgeSpeed;
            _characterController.Move(velocity * Time.deltaTime);
        }

        public void FaceDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.01f)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                _rotationSpeed * Time.deltaTime);
        }
    }
}
