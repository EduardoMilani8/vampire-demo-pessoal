using System;
using UnityEngine;
using UnityEngine.UI;

namespace HollowTide
{
    /// <summary>Menu de pausa: continuar, reiniciar, voltar ao menu e opções de som (gravadas no save).</summary>
    public sealed class PausePanel
    {
        public event Action Resume, Restart, Quit;

        GameObject _root;
        Button _resumeButton;
        Text _soundLabel, _volumeLabel;

        public void Build(Transform canvas)
        {
            var dim = UiFactory.MakeImage(canvas, "PausePanel", new Color(0f, 0f, 0f, 0.78f));
            UiFactory.Stretch(dim.rectTransform);
            _root = dim.gameObject;

            var col = UiFactory.NewRect("Column", _root.transform);
            UiFactory.Center(col, new Vector2(520f, 640f));
            var vl = col.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 16f;
            vl.childAlignment = TextAnchor.MiddleCenter;
            vl.childControlWidth = vl.childControlHeight = true;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;

            var title = UiFactory.MakeLabel(col, "Title", "PAUSADO", 60, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Size(title.gameObject, preferredHeight: 100f);

            _resumeButton = UiFactory.MakeTextButton(col, "Resume", "Continuar", 520f, 68f);
            _resumeButton.onClick.AddListener(() => Resume?.Invoke());
            var restart = UiFactory.MakeTextButton(col, "Restart", "Reiniciar partida", 520f, 68f);
            restart.onClick.AddListener(() => Restart?.Invoke());
            var quit = UiFactory.MakeTextButton(col, "Quit", "Menu principal", 520f, 68f);
            quit.onClick.AddListener(() => Quit?.Invoke());

            var sound = UiFactory.MakeTextButton(col, "Sound", "", 520f, 60f, 24);
            _soundLabel = UiFactory.LabelOf(sound);
            sound.onClick.AddListener(() => { SaveSystem.Current.SoundOn = !SaveSystem.Current.SoundOn; SaveSystem.Save(); Refresh(); });

            var row = UiFactory.NewRect("VolumeRow", col);
            UiFactory.Size(row.gameObject, preferredHeight: 60f);
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 12f;
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = false;

            var down = UiFactory.MakeTextButton(row, "VolDown", "-", 90f, 60f);
            down.onClick.AddListener(() => ChangeVolume(-0.1f));
            _volumeLabel = UiFactory.MakeLabel(row, "Volume", "", 26, Color.white);
            UiFactory.Size(_volumeLabel.gameObject, preferredWidth: 300f, preferredHeight: 60f);
            var up = UiFactory.MakeTextButton(row, "VolUp", "+", 90f, 60f);
            up.onClick.AddListener(() => ChangeVolume(0.1f));

            _root.SetActive(false);
        }

        void ChangeVolume(float delta)
        {
            var save = SaveSystem.Current;
            save.Volume = Mathf.Clamp01(Mathf.Round((save.Volume + delta) * 10f) / 10f);
            SaveSystem.Save();
            Refresh();
            AudioManager.Play(Sfx.Click);
        }

        void Refresh()
        {
            var save = SaveSystem.Current;
            _soundLabel.text = save.SoundOn ? "Som: ligado" : "Som: desligado";
            _volumeLabel.text = "Volume " + Mathf.RoundToInt(save.Volume * 100f) + "%";
        }

        public void Show(bool show)
        {
            _root.SetActive(show);
            if (!show) return;
            Refresh();
            UiFactory.Select(_resumeButton.gameObject);
        }
    }
}
