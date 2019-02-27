using System.Collections;
using Improbable;
using Improbable.Gdk.Core;
using Improbable.Gdk.GameObjectRepresentation;
using Improbable.PlayerLifecycle;
using Improbable.Worker.CInterop;
using UnityEngine;

namespace Fps
{
    public class ConnectionController : MonoBehaviour
    {
        [Require] private PlayerCreator.Requirable.CommandRequestSender commandSender;
        [Require] private PlayerCreator.Requirable.CommandResponseHandler responseHandler;

        private WorkerConnector clientWorkerConnector;

        private void Start()
        {
            ConnectionStateReporter.InformOfConnectionController(this);
            clientWorkerConnector = gameObject.GetComponent<WorkerConnector>();
            clientWorkerConnector.OnWorkerCreationFinished += OnWorkerCreated;
        }

        private void OnWorkerCreated(Worker worker)
        {
            if (worker.Connection.GetConnectionStatusCode() == ConnectionStatusCode.Success)
            {
                StartCoroutine(nameof(DelayedConnectedMessage));
            }
            else
            {
                ConnectionStateReporter.SetState(ConnectionStateReporter.State.ConnectionFailed,
                    worker.Connection.GetConnectionStatusCode().ToString());
            }
        }

        private IEnumerator DelayedConnectedMessage()
        {
            // 1 frame delay necessary to allow [Require] components to active on ConnectionController
            yield return null;
            ConnectionStateReporter.SetState(ConnectionStateReporter.State.Connected);
        }

        private void OnEnable()
        {
            if (responseHandler != null)
            {
                responseHandler.OnCreatePlayerResponse += OnCreatePlayerResponse;
            }
        }

        private void OnCreatePlayerResponse(PlayerCreator.CreatePlayer.ReceivedResponse obj)
        {
            if (obj.StatusCode == StatusCode.Success)
            {
                ConnectionStateReporter.SetState(ConnectionStateReporter.State.Spawned);
            }
            else
            {
                ConnectionStateReporter.SetState(ConnectionStateReporter.State.SpawningFailed,
                    obj.StatusCode.ToString());
            }
        }

        public void OnFailedToConnect(string errorMessage)
        {
            ConnectionStateReporter.SetState(ConnectionStateReporter.State.ConnectionFailed, errorMessage);
        }

        public void OnDisconnected()
        {
            ConnectionStateReporter.SetState(ConnectionStateReporter.State.WorkerDisconnected);
        }

        public void ConnectAction()
        {
            ClientWorkerHandler.CreateClient();
        }

        public void DisconnectAction()
        {
            // TODO Disconnect?
            if (clientWorkerConnector != null)
            {
                Destroy(clientWorkerConnector.gameObject);
            }
        }

        public void SpawnPlayerAction()
        {
            ConnectionStateReporter.SetState(ConnectionStateReporter.State.Spawning);
            var request = new CreatePlayerRequestType(new Vector3f { X = 0, Y = 0, Z = 0 });
            Debug.Log($"commandSender: {commandSender}\n" +
                $"request: {request}");
            commandSender.SendCreatePlayerRequest(new EntityId(1), request);
        }
    }
}
