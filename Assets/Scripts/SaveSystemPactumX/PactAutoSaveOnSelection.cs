using System.Collections;
using Injections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace SaveSystem
{
    public sealed class PactAutoSaveOnSelection : MonoBehaviour
    {
        [SerializeField] private RoomSpawnEvent roomSpawnEvent;
        [SerializeField] private string slotName = "slot_0";
        [SerializeField] private bool logAutoSave;

        private IGameSaveService gameSaveService;
        private bool autoSaveReady;
        private bool hasQueuedRoomSave;
        private string queuedSceneName;

        [Inject]
        private void Construct([InjectOptional] IGameSaveService injectedSaveService)
        {
            gameSaveService = injectedSaveService;
        }

        private void Awake()
        {
            if (gameSaveService == null)
                gameSaveService = RuntimeServiceFallback.GameSaveService;

            autoSaveReady = !PendingGameSaveState.TryGet(out _);
            EnsureRoomSpawnEventBinding();

            if (roomSpawnEvent == null)
            {
                Debug.LogWarning(
                    "PactAutoSaveOnSelection: RoomSpawnEvent is not assigned in inspector. Retrying auto-bind at runtime.",
                    this);
            }
        }

        private IEnumerator Start()
        {
            EnsureRoomSpawnEventBinding();
            if (roomSpawnEvent != null)
            {
                roomSpawnEvent.OnRoomSpawn -= HandleRoomSpawn;
                roomSpawnEvent.OnRoomSpawn += HandleRoomSpawn;
            }
            else
            {
                Debug.LogWarning("PactAutoSaveOnSelection: RoomSpawnEvent is unresolved. Autosave is disabled.", this);
            }

            const int maxFrames = 120;
            for (int i = 0; i < maxFrames; i++)
            {
                if (gameSaveService == null)
                {
                    yield return null;
                    continue;
                }

                if (!PendingGameSaveState.TryGet(out _))
                {
                    autoSaveReady = true;
                    break;
                }

                if (gameSaveService.TryApplyPendingState())
                {
                    autoSaveReady = true;
                    break;
                }

                yield return null;
            }

            if (!autoSaveReady && PendingGameSaveState.TryGet(out _))
            {
                Debug.LogWarning(
                    "PactAutoSaveOnSelection: pending save state could not be applied in time. Autosave was skipped to avoid overwriting loaded data.",
                    this);
                yield break;
            }

            autoSaveReady = true;
            if (hasQueuedRoomSave && !string.IsNullOrWhiteSpace(queuedSceneName))
                SaveCurrentRoomState(queuedSceneName);
        }

        private void OnEnable()
        {
            EnsureRoomSpawnEventBinding();

            if (roomSpawnEvent != null)
                roomSpawnEvent.OnRoomSpawn += HandleRoomSpawn;
        }

        private void OnDisable()
        {
            if (roomSpawnEvent != null)
                roomSpawnEvent.OnRoomSpawn -= HandleRoomSpawn;

            hasQueuedRoomSave = false;
            queuedSceneName = string.Empty;
        }

        private void HandleRoomSpawn(Vector3 _)
        {
            if (gameSaveService == null)
                return;

            if (!IsNpcRoomEntry())
                return;

            string sceneName = ResolveSceneNameForSave();
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning("PactAutoSaveOnSelection: could not resolve scene name for autosave on room entry.", this);
                return;
            }

            if (!autoSaveReady)
            {
                hasQueuedRoomSave = true;
                queuedSceneName = sceneName;
                return;
            }

            SaveCurrentRoomState(sceneName);
        }

        private void SaveCurrentRoomState(string sceneName)
        {
            gameSaveService?.SaveCurrentState(slotName, sceneName);

            if (logAutoSave)
            {
                Debug.Log($"PactAutoSaveOnSelection: autosave on room entry in scene '{sceneName}'.", this);
            }

            hasQueuedRoomSave = false;
            queuedSceneName = string.Empty;
        }

        private string ResolveSceneNameForSave()
        {
            string ownerScene = gameObject.scene.name;
            if (!string.IsNullOrWhiteSpace(ownerScene))
                return ownerScene;

            return SceneManager.GetActiveScene().name;
        }

        private static bool IsNpcRoomEntry()
        {
            return RoomBuilder.Current != null && RoomBuilder.Current.IsCurrentNpcRoom;
        }

        private void EnsureRoomSpawnEventBinding()
        {
            if (roomSpawnEvent != null)
                return;

            RoomBuilder builder = RoomBuilder.Current;
            if (builder == null)
                builder = FindFirstObjectByType<RoomBuilder>();

            if (builder != null)
                roomSpawnEvent = builder.RoomSpawnEvent;
        }
    }
}
