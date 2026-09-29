using System;
using System.Collections.Generic;

namespace HollowTide.Core
{
    /// <summary>O que uma melhoria permanente da loja altera: os 12 atributos dos passivos e os recursos de level up.</summary>
    public enum MetaStat
    {
        Might, Armor, MaxHp, Recovery, Cooldown, Area, ProjectileSpeed, Duration, MoveSpeed, Magnet, Growth, Luck,
        Rerolls, Skips,
    }

    /// <summary>Bônus fixo de um personagem sobre um atributo.</summary>
    [Serializable]
    public struct StatBonus
    {
        public PassiveStat Stat;
        public float Amount;
    }

    public static class MetaMath
    {
        /// <summary>Custo de subir do nível <paramref name="currentLevel"/> para o próximo: base * crescimento^nível.</summary>
        public static int Cost(int baseCost, float growth, int currentLevel)
            => (int)Math.Round(baseCost * Math.Pow(growth, currentLevel));

        /// <summary>Aplica uma melhoria permanente às estatísticas iniciais da partida (ou aos recursos de level up).</summary>
        public static void Apply(ref PlayerStats stats, ref int rerolls, ref int skips, MetaStat stat, float amount)
        {
            switch (stat)
            {
                case MetaStat.Rerolls: rerolls += (int)Math.Round(amount); return;
                case MetaStat.Skips: skips += (int)Math.Round(amount); return;
                case MetaStat.Might: PassiveMath.Apply(ref stats, PassiveStat.Might, amount); return;
                case MetaStat.Armor: PassiveMath.Apply(ref stats, PassiveStat.Armor, amount); return;
                case MetaStat.MaxHp: PassiveMath.Apply(ref stats, PassiveStat.MaxHp, amount); return;
                case MetaStat.Recovery: PassiveMath.Apply(ref stats, PassiveStat.Recovery, amount); return;
                case MetaStat.Cooldown: PassiveMath.Apply(ref stats, PassiveStat.Cooldown, amount); return;
                case MetaStat.Area: PassiveMath.Apply(ref stats, PassiveStat.Area, amount); return;
                case MetaStat.ProjectileSpeed: PassiveMath.Apply(ref stats, PassiveStat.ProjectileSpeed, amount); return;
                case MetaStat.Duration: PassiveMath.Apply(ref stats, PassiveStat.Duration, amount); return;
                case MetaStat.MoveSpeed: PassiveMath.Apply(ref stats, PassiveStat.MoveSpeed, amount); return;
                case MetaStat.Magnet: PassiveMath.Apply(ref stats, PassiveStat.Magnet, amount); return;
                case MetaStat.Growth: PassiveMath.Apply(ref stats, PassiveStat.Growth, amount); return;
                case MetaStat.Luck: PassiveMath.Apply(ref stats, PassiveStat.Luck, amount); return;
                default: throw new ArgumentOutOfRangeException(nameof(stat));
            }
        }
    }

    /// <summary>
    /// Progresso persistente (ouro, melhorias, personagens, recordes, opções). Só campos públicos serializáveis, para o
    /// JsonUtility da Unity salvar/carregar. Melhorias e personagens são guardados por id (texto), não por posição,
    /// para o save sobreviver a mudanças na ordem do conteúdo.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public int Gold;
        public List<string> UpgradeIds = new List<string>();
        public List<int> UpgradeLevels = new List<int>();
        public List<string> UnlockedCharacters = new List<string>();
        public float BestTimeSeconds;
        public int TotalKills;
        public int TotalRuns;
        public int Victories;
        public bool SoundOn = true;
        public float Volume = 0.8f;

        public int GetUpgradeLevel(string id)
        {
            int i = UpgradeIds.IndexOf(id);
            return i >= 0 ? UpgradeLevels[i] : 0;
        }

        public void SetUpgradeLevel(string id, int level)
        {
            int i = UpgradeIds.IndexOf(id);
            if (i >= 0) { UpgradeLevels[i] = level; return; }
            UpgradeIds.Add(id);
            UpgradeLevels.Add(level);
        }

        public bool IsUnlocked(string characterId) => UnlockedCharacters.Contains(characterId);

        public void Unlock(string characterId)
        {
            if (!UnlockedCharacters.Contains(characterId)) UnlockedCharacters.Add(characterId);
        }

        /// <summary>Repara um save incompleto ou corrompido (listas nulas, tamanhos diferentes, valores negativos).</summary>
        public void Sanitize()
        {
            if (UpgradeIds == null) UpgradeIds = new List<string>();
            if (UpgradeLevels == null) UpgradeLevels = new List<int>();
            if (UnlockedCharacters == null) UnlockedCharacters = new List<string>();

            int n = Math.Min(UpgradeIds.Count, UpgradeLevels.Count);
            if (UpgradeIds.Count > n) UpgradeIds.RemoveRange(n, UpgradeIds.Count - n);
            if (UpgradeLevels.Count > n) UpgradeLevels.RemoveRange(n, UpgradeLevels.Count - n);
            for (int i = 0; i < n; i++) if (UpgradeLevels[i] < 0) UpgradeLevels[i] = 0;

            if (Gold < 0) Gold = 0;
            if (TotalKills < 0) TotalKills = 0;
            if (TotalRuns < 0) TotalRuns = 0;
            if (Victories < 0) Victories = 0;
            if (BestTimeSeconds < 0f || float.IsNaN(BestTimeSeconds)) BestTimeSeconds = 0f;
            if (float.IsNaN(Volume)) Volume = 0.8f;
            Volume = Math.Max(0f, Math.Min(1f, Volume));
            Version = CurrentVersion;
        }
    }

    /// <summary>Regras de compra da loja (sem UI): checa ouro, nível máximo e debita.</summary>
    public static class MetaShop
    {
        public static bool TryBuyUpgrade(SaveData save, string id, int baseCost, float growth, int maxLevel)
        {
            int level = save.GetUpgradeLevel(id);
            if (level >= maxLevel) return false;

            int cost = MetaMath.Cost(baseCost, growth, level);
            if (save.Gold < cost) return false;

            save.Gold -= cost;
            save.SetUpgradeLevel(id, level + 1);
            return true;
        }

        public static bool TryUnlockCharacter(SaveData save, string characterId, int cost)
        {
            if (save.IsUnlocked(characterId)) return false;
            if (save.Gold < cost) return false;

            save.Gold -= cost;
            save.Unlock(characterId);
            return true;
        }
    }
}
