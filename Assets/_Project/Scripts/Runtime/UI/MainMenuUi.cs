using System.Text;
using HollowTide.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HollowTide
{
    /// <summary>
    /// Menu principal (cena MainMenu), construído em código: início, seleção de personagem (com desbloqueio por ouro),
    /// loja de melhorias permanentes e opções. Todo o progresso vem de e vai para o save (<see cref="SaveSystem"/>).
    /// </summary>
    public sealed class MainMenuUi : MonoBehaviour
    {
        enum Page { Main, Characters, Shop, Options }

        sealed class CharacterCard
        {
            public Image Icon;
            public Text Name, Description, Info;
            public Button Button;
            public Text ButtonLabel;
        }

        sealed class ShopRow
        {
            public Text Name, Description;
            public Button Button;
            public Text ButtonLabel;
        }

        static readonly Color Gold = new Color(1f, 0.85f, 0.3f);

        [SerializeField] ContentCatalog catalog;

        SaveData _save;
        GameObject[] _screens;
        Page _current;
        Text _goldText, _statsText;
        Button _firstMainButton, _shopBack, _optionsBack, _charactersBack;
        CharacterCard[] _cards;
        ShopRow[] _rows;
        Text _soundLabel, _volumeLabel, _resetLabel;
        bool _resetArmed;

        void Awake()
        {
            Time.timeScale = 1f;
            _save = SaveSystem.Current;

            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var bg = UiFactory.MakeImage(canvasGo.transform, "Background", new Color(0.06f, 0.08f, 0.11f, 1f));
            UiFactory.Stretch(bg.rectTransform);

            _screens = new GameObject[4];
            _screens[(int)Page.Main] = BuildMain(canvasGo.transform);
            _screens[(int)Page.Characters] = BuildCharacters(canvasGo.transform);
            _screens[(int)Page.Shop] = BuildShop(canvasGo.transform);
            _screens[(int)Page.Options] = BuildOptions(canvasGo.transform);

            _goldText = UiFactory.MakeLabel(canvasGo.transform, "Gold", "", 36, Gold, TextAnchor.MiddleRight, FontStyle.Bold);
            var gr = _goldText.rectTransform;
            gr.anchorMin = gr.anchorMax = gr.pivot = new Vector2(1f, 1f);
            gr.sizeDelta = new Vector2(500f, 50f);
            gr.anchoredPosition = new Vector2(-28f, -20f);

            UiFactory.EnsureEventSystem();
        }

        void Start()
        {
            _ = AudioManager.Instance;
            Show(Page.Main);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && _current != Page.Main) Show(Page.Main);
        }

        // ---- Navegação ---------------------------------------------------------------------------

        void Show(Page screen)
        {
            _current = screen;
            for (int i = 0; i < _screens.Length; i++) _screens[i].SetActive(i == (int)screen);
            _goldText.text = "OURO  " + _save.Gold;
            _resetArmed = false;

            switch (screen)
            {
                case Page.Main: RefreshMain(); UiFactory.Select(_firstMainButton.gameObject); break;
                case Page.Characters: RefreshCharacters(); break;
                case Page.Shop: RefreshShop(); break;
                case Page.Options: RefreshOptions(); UiFactory.Select(_optionsBack.gameObject); break;
            }
        }

        static RectTransform Column(Transform parent, Vector2 size, float spacing)
        {
            var col = UiFactory.NewRect("Column", parent);
            UiFactory.Center(col, size);
            var vl = col.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = spacing;
            vl.childAlignment = TextAnchor.MiddleCenter;
            vl.childControlWidth = vl.childControlHeight = true;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;
            return col;
        }

        static GameObject NewScreen(Transform canvas, string name)
        {
            var rt = UiFactory.NewRect(name, canvas);
            UiFactory.Stretch(rt);
            return rt.gameObject;
        }

        Button MenuButton(Transform parent, string text, System.Action onClick, float width = 520f, float height = 72f, int font = 30)
        {
            var b = UiFactory.MakeTextButton(parent, text, text, width, height, font);
            b.onClick.AddListener(() =>
            {
                AudioManager.Play(Sfx.Click);
                onClick();
            });
            return b;
        }

        // ---- Tela principal ----------------------------------------------------------------------

        GameObject BuildMain(Transform canvas)
        {
            var screen = NewScreen(canvas, "Main");
            var col = Column(screen.transform, new Vector2(620f, 800f), 18f);

            var title = UiFactory.MakeLabel(col, "Title", "HOLLOW TIDE", 110, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Size(title.gameObject, preferredHeight: 150f);
            var sub = UiFactory.MakeLabel(col, "Subtitle", "Sobreviva até o amanhecer", 30, new Color(0.7f, 0.78f, 0.9f), TextAnchor.MiddleCenter);
            UiFactory.Size(sub.gameObject, preferredHeight: 70f);

            _firstMainButton = MenuButton(col, "Jogar", () => Show(Page.Characters));
            MenuButton(col, "Melhorias", () => Show(Page.Shop));
            MenuButton(col, "Opções", () => Show(Page.Options));
            MenuButton(col, "Sair", Quit);

            _statsText = UiFactory.MakeLabel(screen.transform, "Stats", "", 24, new Color(1f, 1f, 1f, 0.6f), TextAnchor.LowerCenter);
            var sr = _statsText.rectTransform;
            sr.anchorMin = new Vector2(0f, 0f); sr.anchorMax = new Vector2(1f, 0f); sr.pivot = new Vector2(0.5f, 0f);
            sr.sizeDelta = new Vector2(0f, 60f);
            sr.anchoredPosition = new Vector2(0f, 30f);
            return screen;
        }

        void RefreshMain()
        {
            _statsText.text = "Melhor tempo: " + EndPanel.FormatTime(_save.BestTimeSeconds) +
                              "     Partidas: " + _save.TotalRuns +
                              "     Vitórias: " + _save.Victories +
                              "     Abates: " + _save.TotalKills;
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---- Seleção de personagem ---------------------------------------------------------------

        GameObject BuildCharacters(Transform canvas)
        {
            var screen = NewScreen(canvas, "Characters");

            var title = UiFactory.MakeLabel(screen.transform, "Title", "ESCOLHA SEU HERÓI", 64, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            var tr = title.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
            tr.sizeDelta = new Vector2(0f, 100f);
            tr.anchoredPosition = new Vector2(0f, -50f);

            var row = UiFactory.NewRect("Cards", screen.transform);
            UiFactory.Center(row, new Vector2(1500f, 600f), new Vector2(0f, -10f));
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 32f;
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;

            int count = catalog.characters.Length;
            _cards = new CharacterCard[count];
            for (int i = 0; i < count; i++) _cards[i] = BuildCard(row, i);

            _charactersBack = BackButton(screen.transform);
            return screen;
        }

        CharacterCard BuildCard(Transform parent, int index)
        {
            var bg = UiFactory.MakeImage(parent, "Card" + index, new Color(0.15f, 0.18f, 0.26f, 1f));
            UiFactory.Size(bg.gameObject, preferredWidth: 400f, preferredHeight: 580f);
            var vl = bg.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(24, 24, 24, 24);
            vl.spacing = 10f;
            vl.childAlignment = TextAnchor.UpperCenter;
            vl.childControlWidth = vl.childControlHeight = true;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;

            var card = new CharacterCard();
            card.Icon = UiFactory.MakeImage(bg.transform, "Icon", Color.white);
            card.Icon.preserveAspect = true;
            UiFactory.Size(card.Icon.gameObject, preferredHeight: 130f);
            card.Name = UiFactory.MakeLabel(bg.transform, "Name", "", 34, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Size(card.Name.gameObject, preferredHeight: 48f);
            card.Description = UiFactory.MakeLabel(bg.transform, "Description", "", 22, new Color(0.85f, 0.88f, 0.95f), TextAnchor.UpperCenter);
            UiFactory.Size(card.Description.gameObject, preferredHeight: 110f);
            card.Info = UiFactory.MakeLabel(bg.transform, "Info", "", 22, Gold, TextAnchor.UpperCenter);
            UiFactory.Size(card.Info.gameObject, flexibleHeight: 1f);

            card.Button = UiFactory.MakeTextButton(bg.transform, "Play", "", 300f, 68f, 26);
            card.ButtonLabel = UiFactory.LabelOf(card.Button);
            int captured = index;
            card.Button.onClick.AddListener(() => OnCharacterClicked(captured));
            return card;
        }

        bool IsUnlocked(CharacterData c) => c.unlockedByDefault || _save.IsUnlocked(c.characterId);

        void RefreshCharacters()
        {
            var sb = new StringBuilder(160);
            Button focus = null;

            for (int i = 0; i < _cards.Length; i++)
            {
                var c = catalog.characters[i];
                var card = _cards[i];
                bool unlocked = IsUnlocked(c);

                card.Icon.sprite = c.sprite;
                card.Icon.color = unlocked ? Color.white : new Color(0.25f, 0.25f, 0.3f, 1f);
                card.Name.text = c.displayName;
                card.Description.text = c.description;

                sb.Clear();
                if (c.startingWeapon != null) sb.Append("Arma: ").Append(c.startingWeapon.displayName);
                foreach (var b in c.bonuses) sb.Append('\n').Append(StatText.DescribePassive(b.Stat, b.Amount));
                card.Info.text = sb.ToString();

                if (unlocked)
                {
                    card.ButtonLabel.text = "Jogar";
                    card.Button.interactable = true;
                    focus ??= card.Button;
                }
                else
                {
                    card.ButtonLabel.text = "Desbloquear (" + c.unlockCost + ")";
                    card.Button.interactable = _save.Gold >= c.unlockCost;
                }
            }

            _goldText.text = "OURO  " + _save.Gold;
            UiFactory.Select((focus != null ? focus.gameObject : _charactersBack.gameObject));
        }

        void OnCharacterClicked(int index)
        {
            var c = catalog.characters[index];
            if (!IsUnlocked(c))
            {
                if (MetaShop.TryUnlockCharacter(_save, c.characterId, c.unlockCost))
                {
                    SaveSystem.Save();
                    AudioManager.Play(Sfx.Chest);
                    RefreshCharacters();
                }
                return;
            }

            AudioManager.Play(Sfx.Click);
            GameFlow.StartRun(c.characterId);
        }

        // ---- Loja --------------------------------------------------------------------------------

        GameObject BuildShop(Transform canvas)
        {
            var screen = NewScreen(canvas, "Shop");

            var title = UiFactory.MakeLabel(screen.transform, "Title", "MELHORIAS PERMANENTES", 60, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            var tr = title.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
            tr.sizeDelta = new Vector2(0f, 90f);
            tr.anchoredPosition = new Vector2(0f, -40f);

            var grid = UiFactory.NewRect("Grid", screen.transform);
            UiFactory.Center(grid, new Vector2(1520f, 720f), new Vector2(0f, -20f));
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(740f, 104f);
            gl.spacing = new Vector2(16f, 12f);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = 2;
            gl.childAlignment = TextAnchor.UpperCenter;

            int count = catalog.metaUpgrades.Length;
            _rows = new ShopRow[count];
            for (int i = 0; i < count; i++) _rows[i] = BuildRow(grid, i);

            _shopBack = BackButton(screen.transform);
            return screen;
        }

        ShopRow BuildRow(Transform parent, int index)
        {
            var bg = UiFactory.MakeImage(parent, "Row" + index, new Color(0.15f, 0.18f, 0.26f, 1f));
            var hl = bg.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(18, 18, 8, 8);
            hl.spacing = 16f;
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = true;

            var row = new ShopRow();
            var texts = UiFactory.NewRect("Texts", bg.transform);
            UiFactory.Size(texts.gameObject, flexibleHeight: 1f).flexibleWidth = 1f;
            var tvl = texts.gameObject.AddComponent<VerticalLayoutGroup>();
            tvl.childAlignment = TextAnchor.MiddleLeft;
            tvl.childControlWidth = tvl.childControlHeight = true;
            tvl.childForceExpandWidth = true;
            tvl.childForceExpandHeight = false;

            row.Name = UiFactory.MakeLabel(texts, "Name", "", 26, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.Size(row.Name.gameObject, preferredHeight: 34f);
            row.Description = UiFactory.MakeLabel(texts, "Description", "", 20, new Color(0.8f, 0.85f, 0.95f), TextAnchor.MiddleLeft);
            UiFactory.Size(row.Description.gameObject, preferredHeight: 28f);

            row.Button = UiFactory.MakeTextButton(bg.transform, "Buy", "", 250f, 72f, 22);
            row.ButtonLabel = UiFactory.LabelOf(row.Button);
            int captured = index;
            row.Button.onClick.AddListener(() => OnBuyClicked(captured));
            return row;
        }

        void RefreshShop()
        {
            for (int i = 0; i < _rows.Length; i++)
            {
                var m = catalog.metaUpgrades[i];
                var row = _rows[i];
                int level = _save.GetUpgradeLevel(m.upgradeId);

                row.Name.text = m.displayName + "   " + level + "/" + m.maxLevel;
                row.Description.text = m.DescribeLevel() + " por nível";

                if (level >= m.maxLevel)
                {
                    row.ButtonLabel.text = "MÁXIMO";
                    row.Button.interactable = false;
                }
                else
                {
                    int cost = m.CostAtLevel(level);
                    row.ButtonLabel.text = "Comprar (" + cost + ")";
                    row.Button.interactable = _save.Gold >= cost;
                }
            }

            _goldText.text = "OURO  " + _save.Gold;
            Button focus = _shopBack;
            for (int i = 0; i < _rows.Length; i++)
                if (_rows[i].Button.interactable) { focus = _rows[i].Button; break; }
            UiFactory.Select(focus.gameObject);
        }

        void OnBuyClicked(int index)
        {
            var m = catalog.metaUpgrades[index];
            if (!MetaShop.TryBuyUpgrade(_save, m.upgradeId, m.baseCost, m.costGrowth, m.maxLevel)) return;

            SaveSystem.Save();
            AudioManager.Play(Sfx.Pickup);
            RefreshShop();
        }

        // ---- Opções ------------------------------------------------------------------------------

        GameObject BuildOptions(Transform canvas)
        {
            var screen = NewScreen(canvas, "Options");
            var col = Column(screen.transform, new Vector2(560f, 720f), 16f);

            var title = UiFactory.MakeLabel(col, "Title", "OPÇÕES", 64, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Size(title.gameObject, preferredHeight: 110f);

            var sound = MenuButton(col, "Som", () =>
            {
                _save.SoundOn = !_save.SoundOn;
                SaveSystem.Save();
                RefreshOptions();
            }, 560f, 68f, 26);
            _soundLabel = UiFactory.LabelOf(sound);

            var row = UiFactory.NewRect("VolumeRow", col);
            UiFactory.Size(row.gameObject, preferredHeight: 68f);
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 12f;
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = false;
            MenuButton(row, "-", () => ChangeVolume(-0.1f), 100f, 68f);
            _volumeLabel = UiFactory.MakeLabel(row, "Volume", "", 28, Color.white);
            UiFactory.Size(_volumeLabel.gameObject, preferredWidth: 320f, preferredHeight: 68f);
            MenuButton(row, "+", () => ChangeVolume(0.1f), 100f, 68f);

            var reset = MenuButton(col, "Apagar progresso", OnResetClicked, 560f, 68f, 24);
            _resetLabel = UiFactory.LabelOf(reset);

            _optionsBack = BackButton(col);
            return screen;
        }

        void ChangeVolume(float delta)
        {
            _save.Volume = Mathf.Clamp01(Mathf.Round((_save.Volume + delta) * 10f) / 10f);
            SaveSystem.Save();
            RefreshOptions();
            AudioManager.Play(Sfx.Click);
        }

        void OnResetClicked()
        {
            if (!_resetArmed)
            {
                _resetArmed = true;
                RefreshOptions();
                return;
            }

            SaveSystem.Reset();
            _save = SaveSystem.Current;
            _resetArmed = false;
            RefreshOptions();
        }

        void RefreshOptions()
        {
            _soundLabel.text = _save.SoundOn ? "Som: ligado" : "Som: desligado";
            _volumeLabel.text = "Volume " + Mathf.RoundToInt(_save.Volume * 100f) + "%";
            _resetLabel.text = _resetArmed ? "Tem certeza? Clique de novo" : "Apagar progresso";
            _goldText.text = "OURO  " + _save.Gold;
        }

        Button BackButton(Transform parent)
        {
            var b = MenuButton(parent, "Voltar", () => Show(Page.Main), 360f, 64f, 26);
            if (parent.GetComponent<LayoutGroup>() == null)
            {
                var rt = (RectTransform)b.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
                rt.sizeDelta = new Vector2(360f, 64f);
                rt.anchoredPosition = new Vector2(0f, 36f);
            }
            return b;
        }
    }
}
