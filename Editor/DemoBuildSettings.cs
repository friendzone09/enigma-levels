using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Se asegura de que las dos escenas de demostración estén en Build Settings,
/// para que LevelDoor pueda cargarlas con SceneManager.LoadScene.
/// </summary>
[InitializeOnLoad]
public static class DemoBuildSettings
{
    private static readonly string[] DemoScenes =
    {
        "Assets/Scenes/Nivel01_Aprender.unity",
        "Assets/Scenes/Nivel02_SalaVacia.unity",
    };

    static DemoBuildSettings()
    {
        EditorApplication.delayCall += EnsureScenes;
    }

    private static void EnsureScenes()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        bool changed = false;

        foreach (string path in DemoScenes)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                continue;

            if (scenes.Any(scene => scene.path == path))
                continue;

            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            changed = true;
        }

        if (changed)
        {
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("DEMO: los niveles de demostración se añadieron a Build Settings.");
        }
    }
}
