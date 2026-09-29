using HollowTide.Core;
using UnityEngine;

namespace HollowTide
{
    /// <summary>Definição de um passivo. Crie novos em Assets > Create > Hollow Tide > Passive.</summary>
    [CreateAssetMenu(menuName = "Hollow Tide/Passive", fileName = "Passive_New")]
    public sealed class PassiveData : ScriptableObject
    {
        public string passiveId = "new_passive";
        public string displayName = "New Passive";
        [TextArea] public string description;
        public Sprite icon;

        [Tooltip("Qual atributo do jogador este passivo altera.")]
        public PassiveStat stat;
        [Tooltip("Efeito por nível. Percentuais em fração (0.1 = +10%); armadura e regeneração em valor absoluto.")]
        public float amountPerLevel = 0.1f;
        public int maxLevel = 5;

        public string DescribeLevel() => StatText.DescribePassive(stat, amountPerLevel);
    }
}
