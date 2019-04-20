using Improbable.Gdk.Core;
using Improbable.Gdk.Subscriptions;
using Improbable.Gdk.StandardTypes;
using UnityEngine;

namespace Improbable.Gdk.Movement
{
    public class ReplicatedClientMotor : CharacterControllerMotor
    {
        [Require] private ClientMovementWriter client;
        [Require] private ClientRotationWriter rotation;
        [Require] private ServerMovementReader server;

        [SerializeField] private float transformUpdateHz = 15.0f;
        [SerializeField] private float rotationUpdateHz = 15.0f;
        [SerializeField] [HideInInspector] private float transformUpdateDelta;
        [SerializeField] [HideInInspector] private float rotationUpdateDelta;

        private Vector3 origin;
        private bool lastMovementStationary;

        private float cumulativeRotationTimeDelta;
        private bool yawDirty;
        private bool rollDirty;
        private bool pitchDirty;

        // Cache the update delta values.
        private void OnValidate()
        {
            if (transformUpdateHz > 0.0f)
            {
                transformUpdateDelta = 1.0f / transformUpdateHz;
            }
            else
            {
                transformUpdateDelta = 1.0f;
                Debug.LogError("The Transform Update Hz must be greater than 0.");
            }

            if (rotationUpdateHz > 0.0f)
            {
                rotationUpdateDelta = 1.0f / rotationUpdateHz;
            }
            else
            {
                rotationUpdateDelta = 1.0f;
                Debug.LogError("The Rotation Update Hz must be greater than 0.");
            }
        }

        private void OnEnable()
        {
            var linkedEntityComponent = GetComponent<LinkedEntityComponent>();
            origin = linkedEntityComponent.Worker.Origin;
            server.OnLatestUpdate += OnServerUpdate;
            server.OnForcedRotationEvent += OnForcedRotation;
        }

        protected override void SetRotation(Vector3 rot)
        {
            var currentRot = GetCurrentRotation();
            if (rot.x != currentRot.x)
            {
                pitchDirty = true;
            }
            if (rot.y != currentRot.y)
            {
                yawDirty = true;
            }
            if (rot.z != currentRot.z)
            {
                rollDirty = true;
            }
            base.SetRotation(rot);

            SendRotationUpdate();
        }

        public override void MoveFrame(Vector3 toMove, bool didJump)
        {
            base.MoveFrame(toMove, didJump);
            SendPositionUpdate(didJump);
        }

        #region Server Listeners
        private void OnForcedRotation(RotationUpdate forcedRotation)
        {
            var rotationUpdate = new RotationUpdate
            {
                Pitch = forcedRotation.Pitch,
                Roll = forcedRotation.Roll,
                Yaw = forcedRotation.Yaw,
                TimeDelta = forcedRotation.TimeDelta
            };
            var update = new ClientRotation.Update { Latest = new Option<RotationUpdate>(rotationUpdate) };
            rotation.SendUpdate(update);

            cumulativeRotationTimeDelta = 0;
            pitchDirty = rollDirty = yawDirty = false;

            //Possibly solve previous bug of not updating current yaw/pitch/roll on forced rotation
            Rotate(
                forcedRotation.Pitch.ToFloat1k(),
                forcedRotation.Yaw.ToFloat1k(),
                forcedRotation.Roll.ToFloat1k());
        }

        private void OnServerUpdate(ServerResponse update)
        {
            Reconcile(update.Position.ToVector3() + origin, update.Timestamp);
        }
        #endregion

        #region Send server updates

        private bool HasAllRequirements()
        {
            return client != null && server != null && rotation != null;
        }

        // Returns true if an update is sent.
        private bool SendPositionUpdate(bool didJump)
        {
            //Send network data if required (If moved, or was still moving last update)
            var anyUpdate = false;

            if (HasAllRequirements() && HasEnoughMovement(transformUpdateDelta))
            {
                var syncData = GetMovementSyncData();
                Reset();
                if (syncData.AnyMovement || !lastMovementStationary)
                {
                    var clientRequest = new ClientRequest
                    {
                        IncludesJump = didJump,
                        Movement = syncData.Movement.ToIntDelta(),
                        TimeDelta = syncData.TimeDelta,
                        Timestamp = syncData.MessageStamp
                    };
                    var update = new ClientMovement.Update { Latest = new Option<ClientRequest>(clientRequest) };
                    client.SendUpdate(update);
                    lastMovementStationary = !syncData.AnyMovement;
                    didJump = false;
                    anyUpdate = true;
                }
            }

            return anyUpdate;
        }

        // Returns true if an update needs to be sent.
        private bool SendRotationUpdate()
        {
            //Send network data if required (If moved, or was still moving last update)
            var anyUpdate = false;

            if (HasAllRequirements() && cumulativeRotationTimeDelta > rotationUpdateDelta)
            {
                if (pitchDirty || rollDirty || yawDirty)
                {
                    var currentRot = GetCurrentRotation();
                    var rotationUpdate = new RotationUpdate
                    {
                        Pitch = currentRot.x.ToInt1k(),
                        Roll = currentRot.z.ToInt1k(),
                        Yaw = currentRot.y.ToInt1k(),
                        TimeDelta = cumulativeRotationTimeDelta
                    };
                    var update = new ClientRotation.Update { Latest = new Option<RotationUpdate>(rotationUpdate) };
                    rotation.SendUpdate(update);
                    anyUpdate = true;
                }

                cumulativeRotationTimeDelta = 0;
                pitchDirty = rollDirty = yawDirty = false;
            }
            else
            {
                cumulativeRotationTimeDelta += Time.deltaTime;
            }

            return anyUpdate;
        }
        #endregion
    }
}
