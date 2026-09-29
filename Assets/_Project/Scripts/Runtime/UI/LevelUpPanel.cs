using System;
using HollowTide.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HollowTide
{
    /// <summary>
    /// Painel de escolha ao subir de nível: 3–4 cartas, reroll e skip. Funciona com mouse, teclado
    /// (1–4 escolhem, E rerrola, X pula) e gamepad (navegação + confirmar). Não conhece regras de jogo:
    /// só exibe OptionViews e dispara eventos.
    /// </summary>
    public sealed class LevelUpPanel : MonoBehaviour
    {
        sealed class Card
        {
            public Button Button;
            public Image Icon;
            public Text Title, Tag, Description;
        }

        static readonly Color CardColor = new Color(0.20f, 0.24f, 0.34f, 1f);
        static readonly Color NewColor = new Color(0.45f, 0.85f, 0.55f);
        static readonly Color UpgradeColor = new Color(1f, 0.82f, 0.35f);

        public event Action<int> OptionChosen;
        public event Action RerollClicked;
        public event Action SkipClicked;

        readonly Card[] _cards = new Card[LevelUpRoller.MaxOptions];
        GameObject _root;
        Button _rerollButton, _skipButton;
        Text _rerollLabel, _skipLabel;
        int _shown;

        public bool IsOpen => _root != null && _root.activeSelf;

        public void Build(Transform canvas)
        {
            var dim = UiFactory.MakeImage(canvas, "LevelUpPanel", new Color(0f, 0f, 0f, 0.78f));
            UiFactory.Stretch(dim.rectTransform);
            _root = dim.gameObject;

            var title = UiFactory.MakeLabel(_root.transform, "Title", "NÍVEL AUMENTOU!", 64, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.sizeDelta = new Vector2(0f, 120f);
            titleRt.anchoredPosition = new Vector2(0f, -70f);

            var row = UiFactory.NewRect("Cards", _root.transform);
            UiFactory.Center(row, new Vector2(1600f, 460f), new Vector2(0f, 20f));
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 28f;
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;

            for (int i = 0; i < _cards.Length; i++) _cards[i] = BuildCard(row, i);

            var buttons = UiFactory.NewRect("Actions", _root.transform);
            UiFactory.Center(buttons, new Vector2(760f, 70f), new Vector2(0f, -300f));
            var bl = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            bl.spacing = 40f;
            bl.childAlignment = TextAnchor.MiddleCenter;
            bl.childControlWidth = bl.childControlHeight = true;
            bl.childForceExpandWidth = bl.childForceExpandHeight = false;

            _rerollButton = BuildActionButton(buttons, "Reroll", out _rerollLabel);
            _rerollButton.onClick.AddListener(() => RerollClicked?.Invoke());
            _skipButton = BuildActionButton(buttons, "Skip", out _skipLabel);
            _skipButton.onClick.AddListener(() => SkipClicked?.Invoke());

            _root.SetActive(false);
        }

        Card BuildCard(Transform parent, int index)
        {
            var btn = UiFactory.MakeButton(parent, "Card" + index, CardColor);
            UiFactory.Size(btn.gameObject, preferredWidth: 340f, preferredHeight: 430f);

            var vl = btn.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(20, 20, 24, 16);
            vl.spacing = 10f;
            vl.childAlignment = TextAnchor.UpperCenter;
            vl.childControlWidth = vl.childControlHeight = true;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;

            var card = new Card { Button = btn };

            card.Icon = UiFactory.MakeImage(btn.transform, "Icon", Color.white);
            card.Icon.preserveAspect = true;
            card.Icon.raycastTarget = false;
            UiFactory.Size(card.Icon.gameObject, preferredHeight: 96f);

            card.Title = UiFactory.MakeLabel(btn.transform, "Title", "", 30, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Size(card.Title.gameObject, preferredHeight: 44f);

            card.Tag = UiFactory.MakeLabel(btn.transform, "Tag", "", 20, UpgradeColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Size(card.Tag.gameObject, preferredHeight: 30f);

            card.Description = UiFactory.MakeLabel(btn.transform, "Description", "", 22, new Color(0.88f, 0.9f, 0.95f), TextAnchor.UpperCenter);
            UiFactory.Size(card.Description.gameObject, flexibleHeight: 1f);

            var hotkey = UiFactory.MakeLabel(btn.transform, "Hotkey", "[" + (index + 1) + "]", 18, new Color(1f, 1f, 1f, 0.45f));
            UiFactory.Size(hotkey.gameObject, preferredHeight: 24f);

            int captured = index;
            btn.onClick.AddListener(() => OptionChosen?.Invoke(captured));
            return card;
        }

        static Button BuildActionButton(Transform parent, string name, out Text label)
        {
            var b = UiFactory.MakeButton(parent, name, new Color(0.26f, 0.30f, 0.42f, 1f));
            UiFactory.Size(b.gameObject, preferredWidth: 360f, preferredHeight: 64f);
            label = UiFactory.MakeLabel(b.transform, "Label", "", 26, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Stretch(label.rectTransform);
            return b;
        }

        public void Show(OptionView[] views, int count, int rerolls, int skips)
        {
            _shown = count;
            _root.SetActive(true);

            for (int i = 0; i < _cards.Length; i++)
            {
                var card = _cards[i];
                bool active = i < count;
                card.Button.gameObject.SetActive(active);
                if (!active) continue;

                OptionView v = views[i];
                card.Title.text = v.Title;
                card.Tag.text = v.Tag;
                card.Tag.color = v.Kind == OptionKind.NewWeapon || v.Kind == OptionKind.NewPassive ? NewColor : UpgradeColor;
                card.Description.text = v.Description;
                card.Icon.sprite = v.Icon;
                card.Icon.enabled = v.Icon != null;
            }

            _rerollLabel.text = "[E] Rerrolar (" + rerolls + ")";
            _skipLabel.text = "[X] Pular (" + skips + ")";
            _rerollButton.interactable = rerolls > 0;
            _skipButton.interactable = skips > 0;

            // Foco inicial na primeira carta, para teclado/gamepad já funcionarem.
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(_cards[0].Button.gameObject);
            }
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        void Update()
        {
            if (!IsOpen) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            if (_shown > 0 && kb.digit1Key.wasPressedThisFrame) OptionChosen?.Invoke(0);
            else if (_shown > 1 && kb.digit2Key.wasPressedThisFrame) OptionChosen?.Invoke(1);
            else if (_shown > 2 && kb.digit3Key.wasPressedThisFrame) OptionChosen?.Invoke(2);
            else if (_shown > 3 && kb.digit4Key.wasPressedThisFrame) OptionChosen?.Invoke(3);
            else if (kb.eKey.wasPressedThisFrame && _rerollButton.interactable) RerollClicked?.Invoke();
            else if (kb.xKey.wasPressedThisFrame && _skipButton.interactable) SkipClicked?.Invoke();
        }
    }
}
