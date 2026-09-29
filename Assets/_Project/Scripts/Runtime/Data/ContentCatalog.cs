using UnityEngine;

namespace HollowTide
{
    /// <summary>
    /// Lista de todo o conteúdo do jogo. As listas são preenchidas por Tools > Build Demo, que varre a pasta
    /// Content: para adicionar uma arma ou passivo, crie o asset e rode Build Demo (ou arraste-o aqui à mão).
    /// A posição de cada item é o índice usado pelo sorteio de level up.
    /// </summary>
    [CreateAssetMenu(menuName = "Hollow Tide/Content Catalog", fileName = "ContentCatalog")]
    public sealed class ContentCatalog : ScriptableObject
    {
        public WeaponData[] weapons = new WeaponData[0];
        public PassiveData[] passives = new PassiveData[0];
        public EnemyData[] enemies = new EnemyData[0];
        public WaveData[] waves = new WaveData[0];
        public CharacterData[] characters = new CharacterData[0];
        public MetaUpgradeData[] metaUpgrades = new MetaUpgradeData[0];
    }
}
