using System;
using HollowTide.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HollowTide
{
    /// <summary>
    /// Abertura do baú: o baú treme, abre com um clarão e as recompensas aparecem uma a uma. Roda em tempo não
    /// escalado (o jogo fica pausado). Confirmar (clique, Enter, Espaço ou botão do gamepad) primeiro revela tudo
    /// e, na segunda vez, fecha.
    /// </summary>
    public sealed class ChestPanel : MonoBehaviour
    {
        const int MaxRows = 8;
        const float ShakeTime = 1.0f;
        const float FlashTime = 0.25f;
        const float RevealInterval = 0.4f;

        sealed class Row
        {
            public GameObject Root;
            public Image Icon;
            public Text Title, Tag, Description;
        }

        public event Action Closed;

        readonly Row[] _rows = new Row[MaxRows];
        GameObject _root;
        Image _chestImage, _flash;
        RectTransform _chestRect;
        Text _title;
        Button _continue;
        Text _continueLabel;
        Sprite _closedSprite, _openSprite;
        int _count;
        int _revealed;
        float _time;
        bool _opened;
        bool _evolution;

        public bool IsOpen => _root != null && _root.activeSelf;

        public void Build(Transform canvas, Sprite closed, Sprite open)
        {
            _closedSprite = closed;
            _openSprite = open;

            var dim = UiFactory.MakeImage(canvas, "ChestPanel", new Color(0f, 0f, 0f, 0.82f));
            UiFactory.Stretch(dim.rectTransform);
            _root = dim.gameObject;

            _title = UiFactory.MakeLabel(_root.transform, "Title", "BAÚ", 56, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter, FontStyle.Bold);
            var tr = _title.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
            tr.sizeDelta = new Vector2(0f, 90f);
            tr.anchoredPosition = new Vector2(0f, -40f);

            _chestImage = UiFactory.MakeImage(_root.transform, "Chest", Color.white);
            _chestImage.preserveAspect = true;
            _chestImage.raycastTarget = false;
            _chestRect = _chestImage.rectTransform;
            UiFactory.Center(_chestRect, new Vector2(200f, 200f), new Vector2(0f, 230f));

            var list = UiFactory.NewRect("Rewards", _root.transform);
            UiFactory.Center(list, new Vector2(900f, 470f), new Vector2(0f, -70f));
            var vl = list.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 8f;
            vl.childAlignment = TextAnchor.UpperCenter;
            vl.childControlWidth = vl.childControlHeight = true;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;
            for (int i = 0; i < MaxRows; i++) _rows[i] = BuildRow(list, i);

            _continue = UiFactory.MakeTextButton(_root.transform, "Continue", "", 420f, 64f);
            _continueLabel = UiFactory.LabelOf(_continue);
            UiFactory.Center((RectTransform)_continue.transform, new Vector2(420f, 64f), new Vector2(0f, -400f));
            _continue.onClick.AddListener(Advance);

            _flash = UiFactory.MakeImage(_root.transform, "Flash", new Color(1f, 1f, 1f, 0f));
            _flash.raycastTarget = false;
            UiFactory.Stretch(_flash.rectTransform);

            _root.SetActive(false);
        }

        static Row BuildRow(Transform parent, int index)
        {
            var bg = UiFactory.MakeImage(parent, "Row" + index, new Color(0.16f, 0.19f, 0.28f, 0.95f));
            UiFactory.Size(bg.gameObject, preferredHeight: 84f);
            var hl = bg.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(14, 14, 8, 8);
            hl.spacing = 16f;
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = true;

            var row = new Row { Root = bg.gameObject };
            row.Icon = UiFactory.MakeImage(bg.transform, "Icon", Color.white);
            row.Icon.preserveAspect = true;
            UiFactory.Size(row.Icon.gameObject, preferredWidth: 64f, preferredHeight: 64f);

            var texts = UiFactory.NewRect("Texts", bg.transform);
            UiFactory.Size(texts.gameObject, flexibleHeight: 1f).flexibleWidth = 1f;
            var tvl = texts.gameObject.AddComponent<VerticalLayoutGroup>();
            tvl.childAlignment = TextAnchor.MiddleLeft;
            tvl.childControlWidth = tvl.childControlHeight = true;
            tvl.childForceExpandWidth = true;
            tvl.childForceExpandHeight = false;

            row.Title = UiFactory.MakeLabel(texts, "Title", "", 28, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.Size(row.Title.gameObject, preferredHeight: 34f);
            row.Tag = UiFactory.MakeLabel(texts, "Tag", "", 18, new Color(1f, 0.82f, 0.35f), TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.Size(row.Tag.gameObject, preferredHeight: 22f);
            row.Description = UiFactory.MakeLabel(texts, "Description", "", 18, new Color(0.85f, 0.88f, 0.95f), TextAnchor.MiddleLeft);
            UiFactory.Size(row.Description.gameObject, preferredHeight: 22f);
            return row;
        }

        public void Show(OptionView[] views, int count, bool hasEvolution)
        {
            _count = Mathf.Min(count, MaxRows);
            _revealed = 0;
            _time = 0f;
            _opened = false;
            _evolution = hasEvolution;

            for (int i = 0; i < MaxRows; i++)
            {
                var row = _rows[i];
                row.Root.SetActive(false);
                if (i >= _count) continue;

                OptionView v = views[i];
                row.Title.text = v.Title;
                row.Tag.text = v.Tag;
                row.Tag.color = v.Tag == "EVOLUÇÃO" ? new Color(1f, 0.55f, 0.95f) : v.Tag == "OURO" ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.82f, 0.35f);
                row.Description.text = v.Description;
                row.Icon.sprite = v.Icon;
                row.Icon.enabled = v.Icon != null;
            }

            _title.text = hasEvolution ? "EVOLUÇÃO!" : "BAÚ";
            _chestImage.sprite = _closedSprite;
            _chestRect.anchoredPosition = new Vector2(0f, 230f);
            _chestRect.localScale = Vector3.one;
            _continue.gameObject.SetActive(false);
            _flash.color = new Color(1f, 1f, 1f, 0f);
            _root.SetActive(true);
        }

        void Advance()
        {
            if (!IsOpen) return;

            if (_revealed < _count || !_opened)
            {
                // Primeiro confirmar: pula a animação e mostra tudo.
                _opened = true;
                _chestImage.sprite = _openSprite;
                _chestRect.anchoredPosition = new Vector2(0f, 230f);
                for (int i = 0; i < _count; i++) _rows[i].Root.SetActive(true);
                _revealed = _count;
                ShowContinue();
                return;
            }

            _root.SetActive(false);
            Closed?.Invoke();
        }

        void ShowContinue()
        {
            _continue.gameObject.SetActive(true);
            _continueLabel.text = "Continuar";
            UiFactory.Select(_continue.gameObject);
        }

        void Update()
        {
            if (!IsOpen) return;
            _time += Time.unscaledDeltaTime;

            if (!_opened)
            {
                if (_time < ShakeTime)
                {
                    float k = _time / ShakeTime;
                    float amp = 4f + 22f * k;
                    _chestRect.anchoredPosition = new Vector2(Mathf.Sin(_time * 55f) * amp, 230f + Mathf.Cos(_time * 47f) * amp * 0.4f);
                    float s = 1f + 0.12f * k;
                    _chestRect.localScale = new Vector3(s, s, 1f);
                }
                else
                {
                    _opened = true;
                    _chestImage.sprite = _openSprite;
                    _chestRect.anchoredPosition = new Vector2(0f, 230f);
                    _chestRect.localScale = new Vector3(1.2f, 1.2f, 1f);
                    _time = ShakeTime;
                }
            }

            float sinceOpen = _time - ShakeTime;
            if (_opened && sinceOpen >= 0f && sinceOpen < FlashTime)
                _flash.color = new Color(1f, 1f, 1f, 0.9f * (1f - sinceOpen / FlashTime));
            else
                _flash.color = new Color(1f, 1f, 1f, 0f);

            if (_opened && sinceOpen >= FlashTime && _revealed < _count)
            {
                int shouldBe = Mathf.Min(_count, 1 + Mathf.FloorToInt((sinceOpen - FlashTime) / RevealInterval));
                while (_revealed < shouldBe)
                {
                    _rows[_revealed].Root.SetActive(true);
                    _revealed++;
                }
                if (_revealed >= _count) ShowContinue();
            }

            // Com o botão "Continuar" visível, Enter/Espaço/gamepad já o acionam pela UI (evita disparar duas vezes).
            if (!_continue.gameObject.activeSelf)
            {
                var kb = Keyboard.current;
                var mouse = Mouse.current;
                bool key = kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame);
                bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;
                if (key || click) Advance();
            }
        }
    }
}
