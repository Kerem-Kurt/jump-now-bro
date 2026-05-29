using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using JumpNowBro.Util;

namespace JumpNowBro.Gameplay
{
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        [SerializeField] string[] levelSceneNames;
        [SerializeField] bool loadFirstOnStart = true;
        int currentLevelIndex = -1;
        string currentlyLoadedScene;

        public event Action OnAllLevelsComplete;
        /// Fires with the target sceneIndex BEFORE LoadLevelRoutine starts. NetworkManager subscribes on
        /// the host to send LEVEL_LOAD EVENT; the client subscribes nothing (its own load is driven by
        /// the inbound EVENT, which calls LoadByIndex — the resulting OnBeforeLevelLoad is a no-op there).
        public event Action<int> OnBeforeLevelLoad;

        /// Sentinel sceneIndex meaning "all levels complete" — host pushes it through OnBeforeLevelLoad so
        /// NetworkManager forwards a LEVEL_LOAD EVENT with this byte; client's LoadByIndex translates it
        /// back into OnAllLevelsComplete locally. Keeps the EVENT shape unchanged (1-byte sceneIndex).
        public const byte AllLevelsCompleteSentinel = 0xFE;

        public int CurrentLevelIndex => currentLevelIndex;
        public string CurrentLevelName =>
            (currentLevelIndex >= 0 && currentLevelIndex < levelSceneNames.Length)
                ? levelSceneNames[currentLevelIndex] : null;

        /// True while LoadLevelRoutine is running. NetworkStateBroadcaster suppresses STATE during the
        /// async unload/load so a stale-sceneIndex STATE doesn't race the LEVEL_LOAD EVENT (#76).
        public bool IsLoading { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            if (loadFirstOnStart) LoadFirst();
        }

        /// Called by NetworkManager in Awake when role != SinglePlayer so the connection flow controls
        /// when the game begins; the SinglePlayer path leaves this alone and LoadFirst still runs.
        public void SuppressAutoStart() => loadFirstOnStart = false;

        public void LoadFirst()
        {
            currentLevelIndex = -1;
            currentlyLoadedScene = null;
            LoadNext();
        }

        public void LoadNext()
        {
            currentLevelIndex++;
            if (levelSceneNames == null || currentLevelIndex >= levelSceneNames.Length)
            {
                Debug.Log("LevelManager: all levels complete.");
                OnBeforeLevelLoad?.Invoke(AllLevelsCompleteSentinel);              // host: NetworkManager forwards as LEVEL_LOAD EVENT
                OnAllLevelsComplete?.Invoke();
                return;
            }
            OnBeforeLevelLoad?.Invoke(currentLevelIndex);
            StartCoroutine(LoadLevelRoutine(levelSceneNames[currentLevelIndex]));
        }

        /// Jump straight to a specific level by index — used by the v1.4 client on join (driven by WELCOME)
        /// and by the v1.4 LEVEL_LOAD EVENT receiver. Idempotent on already-current index. 0xFF sentinel
        /// (host pre-load) is treated as a no-op; 0xFE sentinel (host all-levels-complete) fires the
        /// CompleteScreen path without trying to load a scene.
        public void LoadByIndex(int sceneIndex)
        {
            if (sceneIndex < 0 || sceneIndex == 0xFF) return;
            if (sceneIndex == AllLevelsCompleteSentinel)
            {
                OnAllLevelsComplete?.Invoke();
                return;
            }
            if (levelSceneNames == null || sceneIndex >= levelSceneNames.Length)
            {
                Debug.LogError($"LevelManager.LoadByIndex: {sceneIndex} out of range.", this);
                return;
            }
            if (sceneIndex == currentLevelIndex && currentlyLoadedScene == levelSceneNames[sceneIndex]) return;
            currentLevelIndex = sceneIndex;
            OnBeforeLevelLoad?.Invoke(currentLevelIndex);
            StartCoroutine(LoadLevelRoutine(levelSceneNames[sceneIndex]));
        }

        IEnumerator LoadLevelRoutine(string sceneName)
        {
            IsLoading = true;
            try
            {
                if (!string.IsNullOrEmpty(currentlyLoadedScene))
                {
                    var unload = SceneManager.UnloadSceneAsync(currentlyLoadedScene);
                    if (unload != null) yield return unload;
                }

                var load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                if (load == null)
                {
                    Debug.LogError($"LevelManager: scene '{sceneName}' not in Build Settings.", this);
                    yield break;
                }
                yield return load;

                currentlyLoadedScene = sceneName;

                if (ControlMapStore.Instance != null)
                    ControlMapStore.Instance.Apply(ControlMap.Default);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
