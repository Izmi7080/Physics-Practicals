using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using VirtualLab.Azm.UI;

namespace VirtualLab.Azm.Core
{
    /*
     * Layer: Core (app-level singleton, persists across scenes - one of only two allowed by
     * CLAUDE.md; the other is SettingsManager_Azm).
     * Purpose: App flow. Holds the current session (which practical, which mode), loads scenes
     * asynchronously behind a fade-to-black with a small loading label (never an instant cut), and
     * forwards app pause/resume. It knows NOTHING about any specific practical - a practical is just
     * a PracticalInfo_Azm with a scene name, so adding a teammate's practical never touches this file.
     *
     * Public API: GoToMainMenu(), StartPractical(info, mode), ReloadCurrentPractical(),
     * LoadScene(name), PreloadScene(name) (used by the splash), Quit().
     * It does NOT store practical state (trials, counters) - those live in the scene-local
     * practical controller so nothing leaks between sessions.
     * Talks to: SettingsManager_Azm is separate; ScreenFader_Azm is created and driven here; scenes
     * and menus call the public API above.
     */
    public class AppManager_Azm : MonoBehaviour
    {
        public const string SplashSceneName = "Splash_Azm";
        public const string MainMenuSceneName = "MainMenu_Azm";

        public static AppManager_Azm Instance { get; private set; }

        [Tooltip("Seconds for each fade to/from black.")]
        public float fadeSeconds = 0.35f;
        [Tooltip("Minimum time the black/loading screen stays up, so a fast load doesn't flash.")]
        public float minimumLoadSeconds = 0.3f;

        public PracticalCatalog_Azm Catalog { get; private set; }
        public UITheme_Azm Theme { get; private set; }
        public SessionInfo_Azm CurrentSession { get; private set; }
        public bool IsLoading { get; private set; }

        // true = app went to the background, false = it came back. Practical scenes subscribe so
        // they can stop their simulation clock.
        public event Action<bool> AppPauseChanged;

        private ScreenFader_Azm fader;
        private AsyncOperation preloadOperation;
        private string preloadSceneName;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[AppManager_Azm] Duplicate on '{name}' - keeping the existing one on '{Instance.name}'.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            fader = ScreenFader_Azm.Create(transform);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Called by SceneBootstrap_Azm. Only fills what is still empty, so the first scene to
        // provide a catalog/theme wins and later scenes cannot silently replace them.
        public void Configure(PracticalCatalog_Azm catalog, UITheme_Azm theme)
        {
            if (Catalog == null) Catalog = catalog;
            if (Theme == null) Theme = theme;
        }

        public bool IsSceneInBuild(string sceneName)
        {
            return !string.IsNullOrEmpty(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);
        }

        // --- Public API ---

        public void GoToMainMenu()
        {
            CurrentSession = null;
            LoadScene(MainMenuSceneName);
        }

        // Returns false (and logs a warning) instead of throwing if the practical can't be started,
        // e.g. its scene is not in Build Settings. The menu uses this to grey such cards out.
        public bool StartPractical(PracticalInfo_Azm practical, PracticalMode_Azm mode)
        {
            if (practical == null)
            {
                Debug.LogWarning("[AppManager_Azm] StartPractical called with no practical.");
                return false;
            }
            if (!IsSceneInBuild(practical.sceneName))
            {
                Debug.LogWarning($"[AppManager_Azm] Scene '{practical.sceneName}' for '{practical.displayName}' is not in Build Settings.");
                return false;
            }

            CurrentSession = new SessionInfo_Azm(practical, mode);
            LoadScene(practical.sceneName);
            return true;
        }

        public void ReloadCurrentPractical()
        {
            if (CurrentSession == null)
            {
                Debug.LogWarning("[AppManager_Azm] ReloadCurrentPractical called with no session.");
                return;
            }
            LoadScene(CurrentSession.Practical.sceneName);
        }

        // Starts loading a scene in the background WITHOUT activating it; a later LoadScene of the
        // same name reuses it. The splash uses this so the main menu is already loaded when the
        // splash sequence ends.
        public void PreloadScene(string sceneName)
        {
            if (preloadOperation != null || !IsSceneInBuild(sceneName)) return;
            preloadSceneName = sceneName;
            preloadOperation = SceneManager.LoadSceneAsync(sceneName);
            if (preloadOperation != null) preloadOperation.allowSceneActivation = false;
        }

        public void LoadScene(string sceneName)
        {
            if (IsLoading) return;
            StartCoroutine(LoadRoutine(sceneName));
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // --- Internals ---

        private IEnumerator LoadRoutine(string sceneName)
        {
            IsLoading = true;
            yield return fader.FadeTo(1f, fadeSeconds);
            fader.SetLoadingVisible(true);

            AsyncOperation operation;
            if (preloadOperation != null && preloadSceneName == sceneName)
            {
                operation = preloadOperation;
                preloadOperation = null;
                preloadSceneName = null;
            }
            else
            {
                operation = IsSceneInBuild(sceneName) ? SceneManager.LoadSceneAsync(sceneName) : null;
            }

            if (operation == null)
            {
                // Fail soft: go back to visible UI instead of leaving the student on black.
                Debug.LogWarning($"[AppManager_Azm] Could not load scene '{sceneName}'.");
                fader.SetLoadingVisible(false);
                yield return fader.FadeTo(0f, fadeSeconds);
                IsLoading = false;
                yield break;
            }

            operation.allowSceneActivation = true;
            float started = Time.unscaledTime;
            while (!operation.isDone || Time.unscaledTime - started < minimumLoadSeconds)
                yield return null;

            yield return null; // let the new scene's Awake/Start settle before revealing it
            fader.SetLoadingVisible(false);
            yield return fader.FadeTo(0f, fadeSeconds);
            IsLoading = false;
        }

        // Android/iOS: fired when the app goes to / returns from the background.
        private void OnApplicationPause(bool paused)
        {
            AudioListener.pause = paused;
            AppPauseChanged?.Invoke(paused);
        }
    }
}
