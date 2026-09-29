using HollowTide.Core;
using UnityEngine;

namespace HollowTide
{
    public enum WeaponBehaviorType
    {
        ArcSlash,          // golpe em arco na direção do movimento
        HomingBolt,        // projétil teleguiado ao inimigo mais próximo
        Orbit,             // orbitais ao redor do jogador
        Aura,              // aura de dano contínuo
        LobbedProjectile,  // projétil em arco que cai e explode
    }

    /// <summary>Definição de uma arma. Crie novas em Assets > Create > Hollow Tide > Weapon.</summary>
    [CreateAssetMenu(menuName = "Hollow Tide/Weapon", fileName = "Weapon_New")]
    public sealed class WeaponData : ScriptableObject
    {
        public string weaponId = "new_weapon";
        public string displayName = "New Weapon";
        [TextArea] public string description;
        public Sprite icon;
        public WeaponBehaviorType behavior;
        [Tooltip("Armas que só existem como resultado de uma evolução (marco 4) não aparecem no level up.")]
        public bool evolutionOnly;

        [Header("Nível 1")]
        public WeaponLevelStats baseStats = WeaponLevelStats.Default;

        [Header("Melhorias (índice 0 = ao chegar no nível 2 ... índice 6 = nível 8)")]
        public StatDelta[] levelUps = new StatDelta[WeaponProgression.MaxLevel - 1];

        [Header("Comportamento")]
        [Tooltip("Tamanho base em unidades do mundo: raio do arco / da aura / da explosão, raio do projétil, distância da órbita. Multiplicado por Area (exceto a órbita).")]
        public float baseSize = 1.5f;
        [Tooltip("Velocidade base: unidades/s dos projéteis, graus/s dos orbitais. Multiplicada por ProjectileSpeed.")]
        public float baseSpeed = 7f;
        [Tooltip("Duração base em segundos: vida do projétil teleguiado, tempo ativo dos orbitais. Multiplicada por Duration.")]
        public float baseDuration = 1f;
        [Tooltip("Abertura total do arco em graus (só ArcSlash). O sprite do efeito foi desenhado para 100°.")]
        public float arcAngleDegrees = 100f;
        public Sprite effectSprite;
        public Color effectTint = Color.white;

        [Header("Evolução")]
        [Tooltip("Arma em que esta evolui ao abrir um baú (precisa estar no nível máximo e o jogador ter o passivo abaixo).")]
        public WeaponData evolvesInto;
        public PassiveData requiredPassive;

        public WeaponLevelStats StatsAtLevel(int level) => WeaponProgression.StatsAtLevel(baseStats, levelUps, level);
    }
}
