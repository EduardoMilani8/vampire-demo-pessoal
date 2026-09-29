using UnityEngine;
using UnityEngine.UI;

namespace HollowTide
{
    /// <summary>
    /// Helpers para montar uGUI por código. A UI do jogo é construída em runtime para que o projeto funcione sem
    /// prefabs montados à mão; usa a fonte embutida (Legacy Text) para não exigir o import do TextMeshPro.
    /// </summary>
    public static class UiFactory
    {
        static Font _font;

        public static Font DefaultFont
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>Ancora o retângulo no centro da tela com tamanho fixo e deslocamento opcional.</summary>
        public static void Center(RectTransform rt, Vector2 size, Vector2 offset = default)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
        }

        public static Image MakeImage(Transform parent, string name, Color color)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text MakeLabel(Transform parent, string name, string text, int size, Color color,
                                     TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = DefaultFont;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button MakeButton(Transform parent, string name, Color background)
        {
            var img = MakeImage(parent, name, background);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;

            var cb = b.colors;
            cb.normalColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            cb.highlightedColor = Color.white;
            cb.selectedColor = Color.white;
            cb.pressedColor = new Color(0.55f, 0.55f, 0.55f, 1f);
            cb.disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.6f);
            b.colors = cb;
            return b;
        }

        static string[] _numbers;

        /// <summary>Texto de um número pequeno sem alocar a cada chamada (cache preguiçoso até 9999).</summary>
        public static string Num(int n)
        {
            if (n < 0 || n > 9999) return n.ToString();
            _numbers ??= new string[10000];
            return _numbers[n] ??= n.ToString();
        }

        /// <summary>Botão com texto centralizado e tamanho fixo (para colunas com VerticalLayoutGroup).</summary>
        public static Button MakeTextButton(Transform parent, string name, string label, float width, float height, int fontSize = 28)
        {
            var b = MakeButton(parent, name, new Color(0.26f, 0.30f, 0.42f, 1f));
            Size(b.gameObject, preferredWidth: width, preferredHeight: height);
            var t = MakeLabel(b.transform, "Label", label, fontSize, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(t.rectTransform);
            return b;
        }

        public static Text LabelOf(Button b) => b.transform.Find("Label").GetComponent<Text>();

        /// <summary>Garante um EventSystem com o módulo de UI do novo Input System (mouse, teclado e gamepad).</summary>
        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();
            var module = go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        /// <summary>Move o foco de teclado/gamepad para um objeto da UI.</summary>
        public static void Select(GameObject go)
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return;
            es.SetSelectedGameObject(null);
            es.SetSelectedGameObject(go);
        }

        public static LayoutElement Size(GameObject go, float preferredWidth = -1f, float preferredHeight = -1f, float flexibleHeight = -1f)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            if (preferredWidth >= 0f) le.preferredWidth = preferredWidth;
            if (preferredHeight >= 0f) le.preferredHeight = preferredHeight;
            if (flexibleHeight >= 0f) le.flexibleHeight = flexibleHeight;
            return le;
        }
    }
}
