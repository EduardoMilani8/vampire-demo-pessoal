using System.IO;
using UnityEditor;
using UnityEngine;

namespace HollowTide.EditorTools
{
    /// <summary>
    /// Um projeto criado sem template vem com o backend de input antigo ligado. O jogo usa só o
    /// novo Input System, então na primeira abertura ligamos o backend novo (Player Settings >
    /// Active Input Handling) e pedimos para reiniciar o editor, que é exigido pela Unity.
    /// </summary>
    [InitializeOnLoad]
    static class ProjectBootstrap
    {
        const string SettingsPath = "ProjectSettings/ProjectSettings.asset";

        static ProjectBootstrap() => EditorApplication.delayCall += EnsureNewInputHandler;

        static void EnsureNewInputHandler()
        {
            if (!File.Exists(SettingsPath)) return;

            var assets = AssetDatabase.LoadAllAssetsAtPath(SettingsPath);
            if (assets == null || assets.Length == 0) return;

            var so = new SerializedObject(assets[0]);
            var prop = so.FindProperty("activeInputHandler");
            if (prop == null || prop.intValue != 0) return; // 0 = só antigo; 1 = novo; 2 = ambos

            prop.intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();

            if (EditorUtility.DisplayDialog("Hollow Tide",
                    "O backend do novo Input System foi ativado. É preciso reiniciar o editor para valer.\n\nReiniciar agora?",
                    "Reiniciar", "Depois"))
            {
                EditorApplication.OpenProject(Directory.GetCurrentDirectory());
            }
        }
    }
}
