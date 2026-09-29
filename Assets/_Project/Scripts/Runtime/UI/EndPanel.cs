using System;
using UnityEngine;
using UnityEngine.UI;

namespace HollowTide
{
    /// <summary>Resumo mostrado ao terminar a partida.</summary>
    public struct EndSummary
    {
        public bool Victory;
        public float Seconds;
        public int Kills;
        public int Gold;
        public int Level;
        public string Weapons;
        public bool NewBestTime;
    }

    /// <summary>Tela de fim de partida (derrota ou vitória) com o resumo da run.</summary>
    public sealed class EndPanel
    {
        public event Action Retry, Menu;

        GameObject _root;
        CanvasGroup _group;
        Text _title, _subtitle, _stats;
        Button _retryButton;
        float _fade;

        public static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, (int)seconds);
            return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
        }

        public void Build(Transform canvas)
        {
            var dim = UiFactory.MakeImage(canvas, "EndPanel", new Color(0f, 0f, 0f, 0.85f));
            UiFactory.Stretch(dim.rectTransform);
            _root = dim.gameObject;
            _group = _root.AddComponent<CanvasGroup>();

            var col = UiFactory.NewRect("Column", _root.transform);
            UiFactory.Center(col, new Vector2(760f, 800f));
            var vl = col.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 14f;
            vl.childAlignment = TextAnchor.MiddleCenter;
            vl.childControlWidth = vl.childControlHeight = true;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;

            _title = UiFactory.MakeLabel(col, "Title", "", 84, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Size(_title.gameObject, preferredHeight: 130f);
            _subtitle = UiFactory.MakeLabel(col, "Subtitle", "", 26, new Color(0.85f, 0.88f, 0.95f), TextAnchor.MiddleCenter);
            UiFactory.Size(_subtitle.gameObject, preferredHeight: 60f);
            _stats = UiFactory.MakeLabel(col, "Stats", "", 32, Color.white, TextAnchor.UpperCenter);
            UiFactory.Size(_stats.gameObject, preferredHeight: 300f);

            _retryButton = UiFactory.MakeTextButton(col, "Retry", "Jogar novamente", 520f, 70f);
            _retryButton.onClick.AddListener(() => Retry?.Invoke());
            var menu = UiFactory.MakeTextButton(col, "Menu", "Menu principal", 520f, 70f);
            menu.onClick.AddListener(() => Menu?.Invoke());

            _root.SetActive(false);
        }

        public void Show(EndSummary s)
        {
            _title.text = s.Victory ? "VITÓRIA" : "DERROTA";
            _title.color = s.Victory ? new Color(1f, 0.85f, 0.35f) : new Color(1f, 0.4f, 0.4f);
            _subtitle.text = s.Victory
                ? "Você sobreviveu à noite inteira. O Ceifador levou a sua alma... mas não a sua glória."
                : "Você caiu antes do amanhecer.";

            _stats.text =
                "Tempo: " + FormatTime(s.Seconds) + (s.NewBestTime ? "   (novo recorde!)" : "") + "\n" +
                "Abates: " + s.Kills + "\n" +
                "Ouro coletado: " + s.Gold + "\n" +
                "Nível: " + s.Level + "\n" +
                "Armas: " + (string.IsNullOrEmpty(s.Weapons) ? "-" : s.Weapons);

            _fade = 0f;
            _group.alpha = 0f;
            _root.SetActive(true);
            UiFactory.Select(_retryButton.gameObject);
        }

        /// <summary>Fade-in em tempo não escalado (o jogo está com timeScale 0).</summary>
        public void Tick()
        {
            if (!_root.activeSelf || _fade >= 1f) return;
            _fade = Mathf.Min(1f, _fade + Time.unscaledDeltaTime * 1.6f);
            _group.alpha = _fade;
        }
    }
}
