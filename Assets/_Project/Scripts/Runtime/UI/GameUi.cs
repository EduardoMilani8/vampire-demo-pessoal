using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HollowTide
{
    /// <summary>
    /// Canvas do jogo, construído em código no Awake: HUD (XP/nível, vida, tempo, ouro, abates, ícones de armas e
    /// passivos, avisos, clarão de tela) e os painéis de level up, baú, pausa e fim de partida.
    /// Os textos só são reescritos quando o valor muda, para não gerar lixo por frame.
    /// </summary>
    public sealed class GameUi : MonoBehaviour
    {
        const int Slots = 6;

        sealed class Slot
        {
            public GameObject Root;
            public Image Icon;
            public Text Level;
        }

        [SerializeField] Sprite chestClosed;
        [SerializeField] Sprite chestOpen;

        public event Action PauseResume, PauseRestart, PauseQuit, EndRetry, EndMenu;

        public LevelUpPanel LevelUp { get; private set; }
        public ChestPanel Chest { get; private set; }

        RectTransform _xpFill, _hpFill;
        Text _levelText, _hpText, _timerText, _goldText, _killsText, _noticeText;
        Image _flash;
        readonly Slot[] _weaponSlots = new Slot[Slots];
        readonly Slot[] _passiveSlots = new Slot[Slots];
        PausePanel _pause;
        EndPanel _end;

        int _shownLevel = -1, _shownSeconds = -1, _shownGold = -1, _shownKills = -1, _shownHp = -1, _shownMaxHp = -1;
        float _noticeTime;
        Color _flashColor;
        float _flashTime, _flashDuration = 1f;

        void Awake()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var root = canvasGo.transform;
            BuildXpBar(root);
            BuildHealth(root);
            BuildTimer(root);
            BuildCounters(root);
            BuildLoadout(root);
            BuildNotice(root);

            _flash = UiFactory.MakeImage(root, "ScreenFlash", new Color(1f, 1f, 1f, 0f));
            _flash.raycastTarget = false;
            UiFactory.Stretch(_flash.rectTransform);

            LevelUp = new GameObject("LevelUpPanelRoot", typeof(RectTransform)).AddComponent<LevelUpPanel>();
            LevelUp.transform.SetParent(transform, false);
            LevelUp.Build(root);

            Chest = new GameObject("ChestPanelRoot", typeof(RectTransform)).AddComponent<ChestPanel>();
            Chest.transform.SetParent(transform, false);
            Chest.Build(root, chestClosed, chestOpen);

            _pause = new PausePanel();
            _pause.Build(root);
            _pause.Resume += () => PauseResume?.Invoke();
            _pause.Restart += () => PauseRestart?.Invoke();
            _pause.Quit += () => PauseQuit?.Invoke();

            _end = new EndPanel();
            _end.Build(root);
            _end.Retry += () => EndRetry?.Invoke();
            _end.Menu += () => EndMenu?.Invoke();

            UiFactory.EnsureEventSystem();
        }

        // ---- Construção --------------------------------------------------------------------------

        static void Anchor(RectTransform rt, Vector2 anchorAndPivot, Vector2 size, Vector2 position)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchorAndPivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
        }

        static void AddOutline(Graphic g)
        {
            var o = g.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.85f);
            o.effectDistance = new Vector2(2f, -2f);
        }

        void BuildXpBar(Transform canvas)
        {
            var back = UiFactory.MakeImage(canvas, "XpBar", new Color(0f, 0f, 0f, 0.6f));
            var rt = back.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, 26f);
            rt.anchoredPosition = Vector2.zero;

            var fill = UiFactory.MakeImage(back.transform, "Fill", new Color(0.30f, 0.70f, 1f, 1f));
            _xpFill = fill.rectTransform;
            _xpFill.anchorMin = Vector2.zero;
            _xpFill.anchorMax = new Vector2(0f, 1f);
            _xpFill.offsetMin = _xpFill.offsetMax = Vector2.zero;

            _levelText = UiFactory.MakeLabel(back.transform, "Level", "NV 1", 20, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Stretch(_levelText.rectTransform);
        }

        void BuildHealth(Transform canvas)
        {
            var back = UiFactory.MakeImage(canvas, "HpBar", new Color(0f, 0f, 0f, 0.65f));
            Anchor(back.rectTransform, new Vector2(0f, 1f), new Vector2(320f, 28f), new Vector2(16f, -38f));

            var fill = UiFactory.MakeImage(back.transform, "Fill", new Color(0.85f, 0.2f, 0.25f, 1f));
            _hpFill = fill.rectTransform;
            _hpFill.anchorMin = Vector2.zero;
            _hpFill.anchorMax = Vector2.one;
            _hpFill.offsetMin = new Vector2(2f, 2f);
            _hpFill.offsetMax = new Vector2(-2f, -2f);

            _hpText = UiFactory.MakeLabel(back.transform, "Text", "", 18, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Stretch(_hpText.rectTransform);
            AddOutline(_hpText);
        }

        void BuildTimer(Transform canvas)
        {
            _timerText = UiFactory.MakeLabel(canvas, "Timer", "00:00", 44, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(_timerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(300f, 56f), new Vector2(0f, -32f));
            AddOutline(_timerText);
        }

        void BuildCounters(Transform canvas)
        {
            _goldText = BuildCounter(canvas, "Gold", "OURO", new Color(1f, 0.85f, 0.3f), -38f);
            _killsText = BuildCounter(canvas, "Kills", "ABATES", Color.white, -74f);
        }

        static Text BuildCounter(Transform canvas, string name, string caption, Color valueColor, float y)
        {
            var row = UiFactory.NewRect(name, canvas);
            Anchor(row, new Vector2(1f, 1f), new Vector2(300f, 32f), new Vector2(-16f, y));
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 10f;
            hl.childAlignment = TextAnchor.MiddleRight;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = false;

            var cap = UiFactory.MakeLabel(row, "Caption", caption, 18, new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleRight);
            UiFactory.Size(cap.gameObject, preferredWidth: 110f, preferredHeight: 32f);
            var value = UiFactory.MakeLabel(row, "Value", "0", 30, valueColor, TextAnchor.MiddleRight, FontStyle.Bold);
            UiFactory.Size(value.gameObject, preferredWidth: 150f, preferredHeight: 32f);
            AddOutline(value);
            return value;
        }

        void BuildLoadout(Transform canvas)
        {
            BuildSlotRow(canvas, "Weapons", -74f, _weaponSlots);
            BuildSlotRow(canvas, "Passives", -128f, _passiveSlots);
        }

        static void BuildSlotRow(Transform canvas, string name, float y, Slot[] slots)
        {
            var row = UiFactory.NewRect(name, canvas);
            Anchor(row, new Vector2(0f, 1f), new Vector2(Slots * 50f, 46f), new Vector2(16f, y));
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 6f;
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = hl.childControlHeight = false;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;

            for (int i = 0; i < slots.Length; i++)
            {
                var bg = UiFactory.MakeImage(row, "Slot" + i, new Color(0f, 0f, 0f, 0.55f));
                bg.rectTransform.sizeDelta = new Vector2(44f, 44f);

                var icon = UiFactory.MakeImage(bg.transform, "Icon", Color.white);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                UiFactory.Stretch(icon.rectTransform);
                icon.rectTransform.offsetMin = new Vector2(3f, 3f);
                icon.rectTransform.offsetMax = new Vector2(-3f, -3f);

                var level = UiFactory.MakeLabel(bg.transform, "Level", "", 15, Color.white, TextAnchor.LowerRight, FontStyle.Bold);
                UiFactory.Stretch(level.rectTransform);
                level.rectTransform.offsetMax = new Vector2(-3f, 0f);
                AddOutline(level);

                slots[i] = new Slot { Root = bg.gameObject, Icon = icon, Level = level };
                bg.gameObject.SetActive(false);
            }
        }

        void BuildNotice(Transform canvas)
        {
            _noticeText = UiFactory.MakeLabel(canvas, "Notice", "", 38, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(_noticeText.rectTransform, new Vector2(0.5f, 1f), new Vector2(1400f, 60f), new Vector2(0f, -110f));
            AddOutline(_noticeText);
            _noticeText.enabled = false;
        }

        // ---- Atualização -------------------------------------------------------------------------

        public void SetXp(float ratio, int level)
        {
            _xpFill.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
            if (level != _shownLevel)
            {
                _shownLevel = level;
                _levelText.text = "NV " + UiFactory.Num(level);
            }
        }

        public void SetHud(float elapsed, int kills, int gold, float hp, float maxHp)
        {
            int seconds = (int)elapsed;
            if (seconds != _shownSeconds)
            {
                _shownSeconds = seconds;
                _timerText.text = EndPanel.FormatTime(elapsed);
            }
            if (kills != _shownKills) { _shownKills = kills; _killsText.text = UiFactory.Num(kills); }
            if (gold != _shownGold) { _shownGold = gold; _goldText.text = UiFactory.Num(gold); }

            _hpFill.anchorMax = new Vector2(Mathf.Clamp01(maxHp > 0f ? hp / maxHp : 0f), 1f);
            int hpInt = Mathf.CeilToInt(hp), maxInt = Mathf.RoundToInt(maxHp);
            if (hpInt != _shownHp || maxInt != _shownMaxHp)
            {
                _shownHp = hpInt;
                _shownMaxHp = maxInt;
                _hpText.text = UiFactory.Num(hpInt) + " / " + UiFactory.Num(maxInt);
            }
        }

        public void SetLoadout(IReadOnlyList<WeaponInstance> weapons, IReadOnlyList<PassiveInstance> passives)
        {
            for (int i = 0; i < Slots; i++)
            {
                var slot = _weaponSlots[i];
                bool has = i < weapons.Count;
                slot.Root.SetActive(has);
                if (!has) continue;
                slot.Icon.sprite = weapons[i].Data.icon;
                slot.Icon.enabled = slot.Icon.sprite != null;
                slot.Level.text = weapons[i].IsMaxLevel ? "MAX" : UiFactory.Num(weapons[i].Level);
            }

            for (int i = 0; i < Slots; i++)
            {
                var slot = _passiveSlots[i];
                bool has = i < passives.Count;
                slot.Root.SetActive(has);
                if (!has) continue;
                slot.Icon.sprite = passives[i].Data.icon;
                slot.Icon.enabled = slot.Icon.sprite != null;
                slot.Level.text = passives[i].IsMaxLevel ? "MAX" : UiFactory.Num(passives[i].Level);
            }
        }

        public void ShowNotice(string text)
        {
            _noticeText.text = text;
            _noticeText.color = new Color(1f, 0.85f, 0.4f, 1f);
            _noticeText.enabled = true;
            _noticeTime = 3f;
        }

        /// <summary>Clarão de tela (dano, bomba). A cor traz o alfa inicial; some linearmente em <paramref name="duration"/>.</summary>
        public void Flash(Color color, float duration)
        {
            _flashColor = color;
            _flashDuration = Mathf.Max(0.01f, duration);
            _flashTime = _flashDuration;
        }

        public void ShowPause(bool show) => _pause.Show(show);
        public void ShowEnd(EndSummary summary) => _end.Show(summary);

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (_flashTime > 0f)
            {
                _flashTime -= dt;
                var c = _flashColor;
                c.a *= Mathf.Clamp01(_flashTime / _flashDuration);
                _flash.color = c;
            }

            if (_noticeTime > 0f)
            {
                _noticeTime -= dt;
                var c = _noticeText.color;
                c.a = Mathf.Clamp01(_noticeTime / 0.6f);
                _noticeText.color = c;
                if (_noticeTime <= 0f) _noticeText.enabled = false;
            }

            _end.Tick();
        }
    }
}
