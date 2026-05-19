using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        public int CurrentLevelIndex => currentLevelIndex;
        public string CurrentLevelName =>
            (currentLevelIndex >= 0 && currentLevelIndex < levelSceneNames.Length)
                ? levelSceneNames[currentLevelIndex] : null;

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
                OnAllLevelsComplete?.Invoke();
                return;
            }
            StartCoroutine(LoadLevelRoutine(levelSceneNames[currentLevelIndex]));
        }

        IEnumerator LoadLevelRoutine(string sceneName)
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
        }
    }
}
