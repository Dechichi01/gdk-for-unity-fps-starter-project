using UnityEngine;

namespace Improbable.Gdk.Movement
{
    [RequireComponent(typeof(CharacterControllerMotor))]
    public class GroundCheckingDriver : MonoBehaviour
    {
        // Ground checking
        public bool IsGrounded { get; private set; }

        [Tooltip("Uses an overlap sphere of this radius from the character's feet to check for collision.")]
        [SerializeField]
        private float groundedRadius = 0.05f;

        [SerializeField] private LayerMask groundLayerMask = ~0;
        private readonly Collider[] groundedOverlapSphereArray = new Collider[1];

        protected CharacterControllerMotor motor;

        protected virtual void Awake()
        {
            motor = GetComponent<CharacterControllerMotor>();
        }

        private void CheckGrounded()
        {
            IsGrounded = Physics.OverlapSphereNonAlloc(transform.position, groundedRadius, groundedOverlapSphereArray,
                groundLayerMask) > 0;

            CheckExtensionsForOverrides();
        }

        //TODO: Possibly remove parameters and find cleaner way to CheckGrounded on every movement
        public virtual void ApplyMovement(Vector3 movement, Quaternion rotation, MovementSpeed movementSpeed, bool startJump)
        {
            CheckGrounded();
        }

        private void CheckExtensionsForOverrides()
        {
            foreach (var extension in motor.MotorExtensions)
            {
                if (extension.IsOverrideAir())
                {
                    IsGrounded = false;
                    return;
                }
            }
        }
    }
}
