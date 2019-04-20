using UnityEngine;

namespace Improbable.Gdk.Movement
{
    public class ClientMovementDriver : GroundCheckingDriver
    {
        [SerializeField] private MovementSettings movementSettings = new MovementSettings
        {
            MovementSpeed = new MovementSpeedSettings
            {
                WalkSpeed = 2.5f,
                RunSpeed = 4.0f,
                SprintSpeed = 6.0f
            },
            SprintCooldown = 0.1f,
            Gravity = 9.81f,
            StartingJumpSpeed = 10.0f,
            TerminalVelocity = 100.0f,
            GroundedFallSpeed = 1.0f,
            AirControlModifier = 0.0f,
            InAirDamping = 1.0f
        };

        private const float FloatErrorTolerance = 0.01f;

        private bool didJump;
        private float verticalVelocity;
        private Vector3 lastDirection;

        private float sprintCooldownExpires;

        public bool HasSprintedRecently => Time.time < sprintCooldownExpires;

        protected override void Awake()
        {
            base.Awake();
            // There will only be one client movement driver, but there will always be one.
            // Therefore it should be safe to set shared movement settings here.
            MovementSpeedSettings.SharedSettings = movementSettings.MovementSpeed;
        }

        #region Client Movement
        public override void ApplyMovement(Vector3 movement, Quaternion rotation, MovementSpeed movementSpeed, bool startJump)
        {
            ProcessInput(movement, rotation, movementSpeed, startJump);
            base.ApplyMovement(movement, rotation, movementSpeed, startJump);
        }

        public void ProcessInput(Vector3 movement, Quaternion rotation, MovementSpeed movementSpeed, bool startJump)
        {
            UpdateVerticalVelocity();

            UpdateSprintCooldown(movementSpeed, movement);

            var toMove = GetGroundedMovement(movement, movementSpeed);
            ApplyAirMovement(ref toMove);
            ApplyJumpMovement(ref toMove, startJump);

            ApplyFrameMovement(toMove);

            motor.Rotate(rotation.eulerAngles);
        }

        private void ApplyFrameMovement(Vector3 toMove)
        {
            // Inform the motor.
            var oldPosition = transform.position;
            motor.MoveFrame(toMove * Time.deltaTime, didJump);

            // Stop vertical velocity (upwards) if blocked
            if (verticalVelocity > 0 &&
                transform.position.y - oldPosition.y < toMove.y * Time.deltaTime - FloatErrorTolerance)
            {
                verticalVelocity = 0;
            }
        }

        private bool IsJumping()
        {
            return verticalVelocity > 0;
        }

        private void UpdateVerticalVelocity()
        {
            verticalVelocity = Mathf.Clamp(verticalVelocity - movementSettings.Gravity * Time.deltaTime,
                -movementSettings.TerminalVelocity, verticalVelocity);

            if (IsGrounded && verticalVelocity < -movementSettings.GroundedFallSpeed)
            {
                verticalVelocity = -movementSettings.GroundedFallSpeed;
            }
        }

        private void UpdateSprintCooldown(MovementSpeed movementSpeed, Vector3 movementInput)
        {
            var isSprinting = movementSpeed == MovementSpeed.Sprint && movementInput.sqrMagnitude > 0 && IsGrounded;
            if (isSprinting)
            {
                sprintCooldownExpires = Time.time + movementSettings.SprintCooldown;
            }
        }

        private Vector3 GetGroundedMovement(Vector3 movementInput, MovementSpeed movementSpeed)
        {
            // Grounded motion
            // Strafe in the direction given.
            var speed = GetSpeed(movementSpeed);
            return movementInput * speed;
        }

        private void ApplyAirMovement(ref Vector3 toMove)
        {
            var inAir = IsJumping() || !IsGrounded;
            if (!inAir)
            {
                if (IsGrounded) { lastDirection = toMove; }
                return;
            }

            // Keep your last direction, with some damping.
            var momentumMovement = Vector3.Lerp(lastDirection, Vector3.zero,
                Time.deltaTime * movementSettings.InAirDamping);

            // Update the last direction (reduced by the air damping)
            lastDirection = momentumMovement;

            // Can only accelerate up to the movement speed.
            var maxAirSpeed = Mathf.Max(momentumMovement.magnitude, movementSettings.MovementSpeed.RunSpeed);

            var aerialMovement = Vector3.ClampMagnitude(
                momentumMovement + toMove,
                maxAirSpeed
            );
            // Lerp between just momentum, and the momentum with full movement
            toMove = Vector3.Lerp(momentumMovement, aerialMovement, movementSettings.AirControlModifier);
        }

        private void ApplyJumpMovement(ref Vector3 toMove, bool startJump)
        {
            if (startJump && IsGrounded && !IsJumping())
            {
                verticalVelocity = movementSettings.StartingJumpSpeed;
                didJump = true;
            }

            toMove += Vector3.up * verticalVelocity;
        }

        public float GetSpeed(MovementSpeed requestedSpeed)
        {
            switch (requestedSpeed)
            {
                case MovementSpeed.Sprint:
                    return movementSettings.MovementSpeed.SprintSpeed;
                case MovementSpeed.Run:
                    return movementSettings.MovementSpeed.RunSpeed;
                case MovementSpeed.Walk:
                    return movementSettings.MovementSpeed.WalkSpeed;
                default:
                    return 0;
            }
        }

        #endregion
    }
}
