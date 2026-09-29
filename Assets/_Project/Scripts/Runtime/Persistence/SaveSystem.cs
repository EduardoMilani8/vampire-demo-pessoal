using System;
using System.IO;
using HollowTide.Core;
using UnityEngine;

namespace HollowTide
{
    /// <summary>
    /// Save em JSON em Application.persistentDataPath/save.json. A escrita é atômica (arquivo temporário + troca),
    /// então uma queda no meio do save não corrompe o anterior; um arquivo ilegível vira um save novo
    /// (o original é guardado como save.json.corrupt para inspeção).
    /// </summary>
    public static class SaveSystem
    {
        const string FileName = "save.json";

        static SaveData _current;

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public static SaveData Current => _current ??= Load();

        public static SaveData Load()
        {
            string path = FilePath;
            try
            {
                if (File.Exists(path))
                {
                    var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                    if (data != null)
                    {
                        data.Sanitize();
                        return data;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] Não consegui ler {path}: {e.Message}. Começando um save novo.");
                TryPreserveCorrupt(path);
            }

            var fresh = new SaveData();
            fresh.Sanitize();
            return fresh;
        }

        public static void Save()
        {
            if (_current == null) return;
            string path = FilePath;
            string temp = path + ".tmp";
            try
            {
                _current.Sanitize();
                File.WriteAllText(temp, JsonUtility.ToJson(_current, true));
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] Falha ao salvar {path}: {e.Message}");
            }
        }

        /// <summary>Apaga o progresso (ferramenta de teste).</summary>
        public static void Reset()
        {
            _current = new SaveData();
            Save();
        }

        static void TryPreserveCorrupt(string path)
        {
            try { File.Copy(path, path + ".corrupt", true); } catch { /* melhor esforço */ }
        }
    }
}
