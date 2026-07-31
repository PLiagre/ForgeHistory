using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace VictoriaGame.Presentation.Editor
{
    /// <summary>
    /// Crée scène Main, PanelSettings, et inscrit EditorBuildSettings — headless via -executeMethod.
    /// </summary>
    public static class V1007SceneSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string PanelSettingsPath = "Assets/Resources/UI/VictoriaPanelSettings.asset";
        const string ResourcesUiFolder = "Assets/Resources/UI";

        [MenuItem("Victoria/v1_007 Setup Scene And UI")]
        public static void SetupFromMenu()
        {
            Setup();
            Debug.Log("V1007SceneSetup: DONE (menu)");
        }

        /// <summary>Point d'entrée batchmode : -executeMethod VictoriaGame.Presentation.Editor.V1007SceneSetup.SetupFromCommandLine</summary>
        public static void SetupFromCommandLine()
        {
            try
            {
                Setup();
                Debug.Log("V1007SceneSetup: DONE");
                EditorApplication.Exit(0);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("V1007SceneSetup FAILED: " + ex);
                EditorApplication.Exit(1);
            }
        }

        public static void Setup()
        {
            EnsureFolders();
            EnsurePanelSettings();
            EnsureMainScene();
            EnsureBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            VerifyOnDisk();
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(ResourcesUiFolder))
                AssetDatabase.CreateFolder("Assets/Resources", "UI");
        }

        static void EnsurePanelSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (existing != null)
            {
                Debug.Log("V1007SceneSetup: PanelSettings déjà présent → " + PanelSettingsPath);
                TryAssignDefaultTheme(existing);
                EditorUtility.SetDirty(existing);
                return;
            }

            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "VictoriaPanelSettings";
            TryAssignDefaultTheme(settings);
            AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            Debug.Log("V1007SceneSetup: PanelSettings créé → " + PanelSettingsPath);
        }

        static void TryAssignDefaultTheme(PanelSettings settings)
        {
            // Cherche le thème runtime par défaut du package / builtin.
            var themeGuids = AssetDatabase.FindAssets("t:ThemeStyleSheet UnityDefaultRuntimeTheme");
            ThemeStyleSheet theme = null;
            for (var i = 0; i < themeGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(themeGuids[i]);
                theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(path);
                if (theme != null)
                {
                    Debug.Log("V1007SceneSetup: thème trouvé → " + path);
                    break;
                }
            }

            if (theme == null)
            {
                // Fallback : n'importe quel ThemeStyleSheet.
                themeGuids = AssetDatabase.FindAssets("t:ThemeStyleSheet");
                for (var i = 0; i < themeGuids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(themeGuids[i]);
                    if (path.IndexOf("UnityDefaultRuntimeTheme", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || path.IndexOf("DefaultRuntime", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(path);
                        if (theme != null)
                        {
                            Debug.Log("V1007SceneSetup: thème fallback → " + path);
                            break;
                        }
                    }
                }
            }

            if (theme != null)
                settings.themeStyleSheet = theme;
            else
                Debug.LogWarning(
                    "V1007SceneSetup: aucun ThemeStyleSheet trouvé — " +
                    "panneau bitmap dans la texture (repli sanctionné).");
        }

        static void EnsureMainScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.08f, 1f);
            cam.orthographic = true;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            camGo.AddComponent<AudioListener>();

            var hudGo = new GameObject("InGameHud");
            hudGo.AddComponent<InGameHud>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("V1007SceneSetup: scène sauvée → " + ScenePath);
        }

        static void EnsureBuildSettings()
        {
            var scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
            EditorBuildSettings.scenes = scenes;
            Debug.Log("V1007SceneSetup: EditorBuildSettings ← " + ScenePath);
        }

        static void VerifyOnDisk()
        {
            var sceneOk = System.IO.File.Exists(
                System.IO.Path.Combine(Application.dataPath, "Scenes/Main.unity"));
            var panelOk = System.IO.File.Exists(
                System.IO.Path.Combine(Application.dataPath, "Resources/UI/VictoriaPanelSettings.asset"));
            if (!sceneOk || !panelOk)
                throw new System.IO.FileNotFoundException(
                    $"Vérif disque échouée scene={sceneOk} panel={panelOk}");
            Debug.Log($"V1007SceneSetup: vérif disque OK scene={sceneOk} panel={panelOk}");
        }
    }
}
