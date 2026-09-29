using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HollowTide.EditorTools
{
    /// <summary>
    /// Ponto de entrada: Tools > Build Demo. Regera sprites, URP, conteúdo (sem sobrescrever o que já existe) e cenas.
    /// Também chamável em batchmode: -executeMethod HollowTide.EditorTools.BuildDemo.BuildAll
    /// </summary>
    public static class BuildDemo
    {
        [MenuItem("Tools/Build Demo")]
        public static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            UrpSetup.Ensure();
            PlaceholderSprites.GenerateAll();
            var catalog = SceneBuilder.BuildContent();
            SceneBuilder.BuildGameScene(catalog);
            SceneBuilder.BuildMenuScene(catalog);
            SceneBuilder.RegisterScenesInBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(SceneBuilder.MenuScenePath);
            Debug.Log("[Build Demo] Pronto. A cena MainMenu está aberta: aperte Play para jogar (o menu carrega a cena Game). " +
                      "Para testar só a partida, abra Assets/_Project/Scenes/Game.unity.");
        }

        [MenuItem("Tools/Save/Reset Save Data")]
        public static void ResetSave()
        {
            if (!EditorUtility.DisplayDialog("Reset Save Data", "Apaga ouro, melhorias e personagens desbloqueados. Continuar?", "Apagar", "Cancelar")) return;
            SaveSystem.Reset();
            Debug.Log("[Save] Progresso apagado: " + SaveSystem.FilePath);
        }

        [MenuItem("Tools/Save/Reveal Save File")]
        public static void RevealSave()
        {
            SaveSystem.Save();
            EditorUtility.RevealInFinder(SaveSystem.FilePath);
        }

        [MenuItem("Tools/Save/Give 5000 Gold (teste)")]
        public static void GiveGold()
        {
            SaveSystem.Current.Gold += 5000;
            SaveSystem.Save();
            Debug.Log("[Save] +5000 de ouro. Total: " + SaveSystem.Current.Gold);
        }

        [MenuItem("Tools/Reset Content (apaga os ScriptableObjects e reconstrói)")]
        public static void ResetContent()
        {
            if (!EditorUtility.DisplayDialog("Reset Content",
                    "Isto apaga Assets/_Project/Content (suas edições nos ScriptableObjects) e reconstrói tudo com os valores padrão. Continuar?",
                    "Apagar e reconstruir", "Cancelar")) return;

            ContentBuilder.ResetAll();
            BuildAll();
        }
    }
}
