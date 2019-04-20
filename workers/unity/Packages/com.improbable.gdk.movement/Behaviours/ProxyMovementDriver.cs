using Improbable.Gdk.Subscriptions;
using Improbable.Gdk.StandardTypes;
using Improbable.Worker.CInterop;
using UnityEngine;

namespace Improbable.Gdk.Movement
{
    public class ProxyMovementDriver : GroundCheckingDriver
    {
        [Require] private ServerMovementReader server;
        [Require] private ClientRotationReader client;

        private LinkedEntityComponent LinkedEntityComponent;
        private Vector3 origin;

        private void OnEnable()
        {
            LinkedEntityComponent = GetComponent<LinkedEntityComponent>();
            origin = LinkedEntityComponent.Worker.Origin;

            server.OnLatestUpdate += OnServerUpdate;
            client.OnLatestUpdate += OnClientUpdate;

            OnClientUpdate(client.Data.Latest);
            OnServerUpdate(server.Data.Latest);
        }

        private void OnClientUpdate(RotationUpdate rotation)
        {
            var rot = new Vector3(
                rotation.Pitch.ToFloat1k(),
                rotation.Yaw.ToFloat1k(),
                rotation.Roll.ToFloat1k());

            
            motor.FilterRotation(ref rot);
            motor.InterpolateTo(Quaternion.Euler(rot), rotation.TimeDelta);
        }

        private void OnServerUpdate(ServerResponse movement)
        {
            if (server.Authority != Authority.NotAuthoritative)
            {
                return;
            }

            motor.InterpolateTo(movement.Position.ToVector3() + origin, movement.TimeDelta);
        }
    }
}
