using System;
using UnityEngine;

namespace HollowTide
{
    [Serializable]
    public struct WaveEnemy
    {
        public EnemyData enemy;
        [Tooltip("Peso no sorteio: 3 aparece o triplo de vezes que 1.")]
        public float weight;
    }

    [Serializable]
    public struct WaveBurstData
    {
        public EnemyData enemy;
        public int count;
        [Tooltip("Segundos depois do início desta onda.")]
        public float atSecond;
        [Tooltip("Marcado: distribui igualmente ao redor do jogador (cerco). Desmarcado: posições aleatórias.")]
        public bool surround;
    }

    /// <summary>
    /// Uma faixa da tabela de ondas: vale do seu início até o início da próxima onda. Crie novas em
    /// Assets > Create > Hollow Tide > Wave e rode Tools > Build Demo para entrar no catálogo.
    /// A tabela deve sempre crescer (ritmo e teto de inimigos não podem diminuir); o Build Demo avisa se não.
    /// </summary>
    [CreateAssetMenu(menuName = "Hollow Tide/Wave", fileName = "Wave_New")]
    public sealed class WaveData : ScriptableObject
    {
        [Tooltip("Minuto da partida em que esta onda começa.")]
        public float startMinute;
        [Tooltip("Inimigos por segundo (spawn contínuo).")]
        public float spawnsPerSecond = 1.5f;
        [Tooltip("Teto de inimigos vivos para o spawn contínuo. Rajadas ignoram o teto.")]
        public int maxAlive = 150;
        [Tooltip("Multiplicador de vida dos inimigos que nascem nesta onda.")]
        public float hpMultiplier = 1f;
        public WaveEnemy[] enemies = new WaveEnemy[0];
        public WaveBurstData[] bursts = new WaveBurstData[0];
    }
}
