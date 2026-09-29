using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HollowTide.EditorTools
{
    /// <summary>Monta as cenas do jogo (MainMenu e Game) por código.</summary>
    static class SceneBuilder
    {
        public const string ScenesDir = "Assets/_Project/Scenes";
        public const string GameScenePath = ScenesDir + "/Game.unity";
        public const string MenuScenePath = ScenesDir + "/MainMenu.unity";

        /// <summary>Cria todo o conteúdo (ScriptableObjects) e devolve o catálogo.</summary>
        public static ContentCatalog BuildContent()
        {
            var enemies = ContentBuilder.BuildEnemies();          // crawler, shambler, swarmer, brute, elite, ceifador
            ContentBuilder.BuildCrescentBlade();
            ContentBuilder.BuildPassives();
            ContentBuilder.BuildOtherWeaponsAndEvolutions();
            ContentBuilder.BuildCharacters();
            ContentBuilder.BuildMetaUpgrades();
            ContentBuilder.BuildWaves(enemies[0], enemies[1], enemies[2], enemies[3], enemies[4]);

            var catalog = ContentBuilder.BuildCatalog();
            ContentBuilder.ValidateWaves(catalog);
            return catalog;
        }

        // ---- Cena de jogo ------------------------------------------------------------------------

        public static void BuildGameScene(ContentCatalog catalog)
        {
            Directory.CreateDirectory(ScenesDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            catalog = ReloadCatalog();

            var reaper = AssetDatabase.LoadAssetAtPath<EnemyData>($"{ContentBuilder.Root}/Enemies/Enemy_Reaper.asset");

            // Jogador
            var player = new GameObject("Player");
            var visual = new GameObject("Visual");
            visual.transform.SetParent(player.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprites.Load("player");
            sr.sortingOrder = 20;
            var controller = player.AddComponent<PlayerController>();
            EditorWire.SetObject(controller, "visual", visual.transform);
            EditorWire.SetObject(controller, "spriteRenderer", sr);
            var healthBar = BuildHealthBar(player.transform);

            // Câmera
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.14f, 0.13f);
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            camGo.AddComponent<AudioListener>();
            var follow = camGo.AddComponent<CameraFollow>();
            EditorWire.SetObject(follow, "target", player.transform);

            // Fundo infinito
            var bg = new GameObject("Background");
            var bgComp = bg.AddComponent<InfiniteBackground>();
            var tiles = new Object[4];
            for (int i = 0; i < tiles.Length; i++) tiles[i] = PlaceholderSprites.Load($"ground_{i}");
            EditorWire.SetObjectArray(bgComp, "tileVariants", tiles);
            EditorWire.SetObject(bgComp, "followTarget", camGo.transform);

            // Sistemas de jogo
            var systems = new GameObject("GameSystems");
            var session = systems.AddComponent<GameSession>();

            var views = Child<EnemyViews>(systems, "EnemyViews");

            var gemViews = Child<GemViews>(systems, "GemViews");
            EditorWire.SetObjectArray(gemViews, "tierSprites", new Object[]
            {
                PlaceholderSprites.Load("gem_0"), PlaceholderSprites.Load("gem_1"), PlaceholderSprites.Load("gem_2")
            });

            var coinViews = Child<GemViews>(systems, "CoinViews");
            EditorWire.SetObjectArray(coinViews, "tierSprites", new Object[] { PlaceholderSprites.Load("coin") });

            var projectiles = Child<ProjectileSystem>(systems, "Projectiles");
            EditorWire.SetObject(projectiles, "explosionSprite", PlaceholderSprites.Load("explosion_ring"));

            var pickups = Child<PickupSystem>(systems, "Pickups");
            EditorWire.SetObject(pickups, "chestSprite", PlaceholderSprites.Load("chest_closed"));
            EditorWire.SetObject(pickups, "healSprite", PlaceholderSprites.Load("pickup_heal"));
            EditorWire.SetObject(pickups, "magnetSprite", PlaceholderSprites.Load("pickup_magnet"));
            EditorWire.SetObject(pickups, "bombSprite", PlaceholderSprites.Load("pickup_bomb"));

            var feedback = Child<Feedback>(systems, "Feedback");
            EditorWire.SetObject(feedback, "pixelSprite", PlaceholderSprites.Load("pixel"));

            var spawner = Child<EnemySpawner>(systems, "EnemySpawner");
            var weapons = Child<PlayerWeapons>(systems, "PlayerWeapons");
            systems.AddComponent<DebugOverlay>();

            var uiGo = new GameObject("GameUI");
            var gameUi = uiGo.AddComponent<GameUi>();
            EditorWire.SetObject(gameUi, "chestClosed", PlaceholderSprites.Load("chest_closed"));
            EditorWire.SetObject(gameUi, "chestOpen", PlaceholderSprites.Load("chest_open"));

            EditorWire.SetObject(session, "player", controller);
            EditorWire.SetObject(session, "cameraFollow", follow);
            EditorWire.SetObject(session, "enemyViews", views);
            EditorWire.SetObject(session, "gemViews", gemViews);
            EditorWire.SetObject(session, "coinViews", coinViews);
            EditorWire.SetObject(session, "projectiles", projectiles);
            EditorWire.SetObject(session, "pickups", pickups);
            EditorWire.SetObject(session, "feedback", feedback);
            EditorWire.SetObject(session, "spawner", spawner);
            EditorWire.SetObject(session, "weapons", weapons);
            EditorWire.SetObject(session, "healthBar", healthBar);
            EditorWire.SetObject(session, "ui", gameUi);
            EditorWire.SetObject(session, "catalog", catalog);
            EditorWire.SetObject(session, "reaperData", reaper);

            EditorSceneManager.SaveScene(scene, GameScenePath);
        }

        // ---- Cena do menu ------------------------------------------------------------------------

        public static void BuildMenuScene(ContentCatalog catalog)
        {
            Directory.CreateDirectory(ScenesDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            catalog = ReloadCatalog();

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.08f, 0.11f);
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            camGo.AddComponent<AudioListener>();

            var menu = new GameObject("MainMenu").AddComponent<MainMenuUi>();
            EditorWire.SetObject(menu, "catalog", catalog);

            EditorSceneManager.SaveScene(scene, MenuScenePath);
        }

        /// <summary>Menu = índice 0 (primeira cena ao abrir o jogo), Game = índice 1.</summary>
        public static void RegisterScenesInBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true),
            };
        }

        /// <summary>
        /// NewScene(Single) descarrega os assets não usados, e uma referência obtida ANTES dela vira "nula" (destruída).
        /// Por isso o catálogo é sempre recarregado do disco depois de criar a cena.
        /// </summary>
        static ContentCatalog ReloadCatalog() => AssetDatabase.LoadAssetAtPath<ContentCatalog>(ContentBuilder.CatalogPath);

        // ---- Helpers -----------------------------------------------------------------------------

        static T Child<T>(GameObject parent, string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go.AddComponent<T>();
        }

        static WorldHealthBar BuildHealthBar(Transform parent)
        {
            var pixel = PlaceholderSprites.Load("pixel");

            var bar = new GameObject("HealthBar");
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = new Vector3(0f, -0.7f, 0f);

            var back = new GameObject("Back");
            back.transform.SetParent(bar.transform, false);
            back.transform.localScale = new Vector3(1.26f, 0.18f, 1f);
            var backSr = back.AddComponent<SpriteRenderer>();
            backSr.sprite = pixel;
            backSr.color = new Color(0f, 0f, 0f, 0.7f);
            backSr.sortingOrder = 21;

            var fill = new GameObject("Fill");
            fill.transform.SetParent(bar.transform, false);
            var fillSr = fill.AddComponent<SpriteRenderer>();
            fillSr.sprite = pixel;
            fillSr.color = new Color(0.85f, 0.2f, 0.25f);
            fillSr.sortingOrder = 22;

            var comp = bar.AddComponent<WorldHealthBar>();
            EditorWire.SetObject(comp, "fill", fill.transform);
            return comp;
        }
    }
}
