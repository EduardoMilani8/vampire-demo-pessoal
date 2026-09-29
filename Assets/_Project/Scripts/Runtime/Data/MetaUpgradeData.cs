using HollowTide.Core;
using UnityEngine;

namespace HollowTide
{
    /// <summary>Melhoria permanente da loja, paga com ouro entre partidas. Assets > Create > Hollow Tide > Meta Upgrade.</summary>
    [CreateAssetMenu(menuName = "Hollow Tide/Meta Upgrade", fileName = "Meta_New")]
    public sealed class MetaUpgradeData : ScriptableObject
    {
        public string upgradeId = "new_upgrade";
        public string displayName = "New Upgrade";
        [TextArea] public string description;
        public MetaStat stat;
        [Tooltip("Efeito por nível. Percentuais em fração (0.05 = +5%).")]
        public float amountPerLevel = 0.05f;
        public int maxLevel = 5;
        public int baseCost = 100;
        [Tooltip("O custo do nível N é baseCost * costGrowth^N.")]
        public float costGrowth = 1.5f;

        public int CostAtLevel(int currentLevel) => MetaMath.Cost(baseCost, costGrowth, currentLevel);

        public string DescribeLevel()
        {
            switch (stat)
            {
                case MetaStat.Armor: return $"+{amountPerLevel:0.##} armadura";
                case MetaStat.Recovery: return $"+{amountPerLevel:0.##} vida/s";
                case MetaStat.Rerolls: return $"+{amountPerLevel:0} reroll";
                case MetaStat.Skips: return $"+{amountPerLevel:0} skip";
                case MetaStat.Cooldown: return $"-{Mathf.RoundToInt(amountPerLevel * 100f)}% recarga";
                default: return $"+{Mathf.RoundToInt(amountPerLevel * 100f)}% {displayName}";
            }
        }
    }
}
