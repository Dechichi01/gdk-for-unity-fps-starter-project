using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Improbable.Gdk.Movement
{
    [RequireComponent(typeof(CharacterController))]
    public class CharacterControllerMotor : MonoBehaviour
    {
        //CSP
        private readonly Queue<MovementRequest> requests = new Queue<MovementRequest>();
        private Vector3 cumulativeMovement;
        private float cumulativeTimeDelta;
        private bool anyMovement;

        //Interpolation: Movement
        private bool hasMovementLeft;
        private float timeLeftToMove;
        private Vector3 distanceLeftToMove;
        private int messageStamp;

        //Interpolation: Rotation
        private float timeLeftToRotate;
        private float lastFullTime;
        private Quaternion source;
        private Quaternion target;
        private bool hasRotationLeft;

        [Tooltip(
            "When interpolating motion, if the (squared) distance travelled is greater than this, teleport instead of interpolating.")]
        [SerializeField]
        private float teleportDistanceSqrd = 25f;

        [SerializeField]
        private RotationConstraints rotationConstraints = new RotationConstraints
        {
            XAxisRotation = true,
            YAxisRotation = true,
            ZAxisRotation = true
        };

        private CharacterController characterController;
        public IMotorExtension[] MotorExtensions;
        private Vector3 currentRotation;

        protected virtual void Awake()
        {
            characterController = GetComponent<CharacterController>();
            MotorExtensions = GetComponents<IMotorExtension>();
        }

        protected virtual void Update()
        {
            InterpolatePosition();
            InterpolateRotation();
        }

        #region Rotation
        public void Rotate(Vector3 rot)
        {
            FilterRotation(ref rot);
            SetRotation(rot);
        }

        public void Rotate(float x, float y, float z)
        {
            Rotate(new Vector3(x, y, z));
        }

        public void FilterRotation(ref Vector3 rot)
        {
            rot.x = rotationConstraints.XAxisRotation ? rot.x : 0;
            rot.y = rotationConstraints.YAxisRotation ? rot.y : 0;
            rot.z = rotationConstraints.ZAxisRotation ? rot.z : 0;
        }

        protected virtual void SetRotation(Vector3 rot)
        {
            transform.rotation = Quaternion.Euler(rot);
            currentRotation = rot;
        }

        protected Vector3 GetCurrentRotation()
        {
            return currentRotation;
        }
        #endregion

        #region Movement
        //TODO: Cleaner way to avoid adding didJump to base class method
        public virtual void MoveFrame(Vector3 toMove, bool didJump)
        {
            cumulativeTimeDelta += Time.deltaTime;
            var before = transform.position;
            MoveWithExtensions(toMove);
            var delta = transform.position - before;
            anyMovement |= delta.sqrMagnitude / Time.deltaTime > 0;
            cumulativeMovement += delta;
        }

        protected void Move(Vector3 toMove)
        {
            if (characterController.enabled)
            {
                characterController.Move(toMove);
            }
        }

        // Separate from regular move, as we only want the extensions applied to the movement once.
        private void MoveWithExtensions(Vector3 toMove)
        {
            if (!characterController.enabled)
            {
                return;
            }

            foreach (var extension in MotorExtensions)
            {
                extension.BeforeMove(toMove);
            }

            Move(toMove);
        }
        #endregion

        #region ClientSync
        public bool HasEnoughMovement(float threshold)
        {
            return cumulativeTimeDelta > threshold;
        }

        public MovementSyncData GetMovementSyncData()
        {
            //TODO: Possibly ditch variables and just keep a MovementSyncData member variable
            return new MovementSyncData(
                cumulativeMovement,
                cumulativeTimeDelta,
                anyMovement,
                messageStamp);
        }

        public void Reset()
        {
            requests.Enqueue(new MovementRequest(messageStamp, cumulativeMovement));
            cumulativeMovement = Vector3.zero;
            cumulativeTimeDelta = 0;
            anyMovement = false;
            messageStamp++;
        }

        public void Reconcile(Vector3 position, int timestamp)
        {
            transform.position = position;
            foreach (var each in requests.ToList())
            {
                if (each.Timestamp <= timestamp)
                {
                    requests.Dequeue();
                }
                else
                {
                    Move(each.Movement);
                }
            }

            Move(cumulativeMovement);
        }
        #endregion

        #region Interpolation
        public void InterpolateTo(Vector3 target, float timeDelta)
        {
            distanceLeftToMove = target - transform.position;
            var sqrMagnitude = distanceLeftToMove.sqrMagnitude;
            hasMovementLeft = sqrMagnitude < teleportDistanceSqrd || timeDelta != 0;
            if (hasMovementLeft)
            {
                timeLeftToMove = timeDelta;
            }
            else
            {
                transform.position = target;
            }
        }

        public void InterpolateTo(Quaternion targetQuaternion, float timeDelta)
        {
            hasRotationLeft = true;
            lastFullTime = timeLeftToRotate = timeDelta;
            target = targetQuaternion;
            source = transform.rotation;
        }

        protected virtual void InterpolatePosition()
        {
            if (hasMovementLeft)
            {
                if (Time.deltaTime < timeLeftToMove)
                {
                    var percentageToMove = Time.deltaTime / timeLeftToMove;
                    var distanceToMove = distanceLeftToMove * percentageToMove;
                    Move(distanceToMove);
                    distanceLeftToMove -= distanceToMove;
                    timeLeftToMove -= Time.deltaTime;
                }
                else
                {
                    Move(distanceLeftToMove);
                    hasMovementLeft = false;
                }
            }
        }

        protected virtual void InterpolateRotation()
        {
            if (!hasRotationLeft)
            {
                return;
            }

            if (Time.deltaTime < timeLeftToRotate)
            {
                transform.rotation =
                    Quaternion.Lerp(source, target, 1 - timeLeftToRotate / lastFullTime);
                timeLeftToRotate -= Time.deltaTime;
            }
            else
            {
                transform.rotation = target;
                hasRotationLeft = false;
            }
        }
        #endregion

        public struct MovementSyncData
        {
            public readonly Vector3 Movement;
            public readonly float TimeDelta;
            public readonly bool AnyMovement;
            public readonly int MessageStamp;

            public MovementSyncData(Vector3 movement, float timeDelta, bool anyMovement, int messageStamp)
            {
                Movement = movement;
                TimeDelta = timeDelta;
                AnyMovement = anyMovement;
                MessageStamp = messageStamp;
            }
        }
    }
}
