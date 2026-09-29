using UnityEngine.SceneManagement;

namespace HollowTide
{
    /// <summary>Estado que atravessa as cenas (Menu → Jogo) e navegação entre elas.</summary>
    public static class GameFlow
    {
        public const string MenuScene = "MainMenu";
        public const string GameScene = "Game";

        /// <summary>Personagem escolhido no menu. Nulo (ex.: rodando Game.unity direto) = o primeiro do catálogo.</summary>
        public static string SelectedCharacterId;

        public static void StartRun(string characterId)
        {
            SelectedCharacterId = characterId;
            SceneManager.LoadScene(GameScene);
        }

        public static void ReloadGame() => SceneManager.LoadScene(GameScene);

        public static void ToMenu() => SceneManager.LoadScene(MenuScene);
    }
}
