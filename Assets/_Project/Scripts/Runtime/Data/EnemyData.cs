using HollowTide.Core;
using UnityEngine;

namespace HollowTide
{
    /// <summary>Definição de um tipo de inimigo. Crie novos em Assets > Create > Hollow Tide > Enemy.</summary>
    [CreateAssetMenu(menuName = "Hollow Tide/Enemy", fileName = "Enemy_New")]
    public sealed class EnemyData : ScriptableObject
    {
        public string displayName = "New Enemy";

        [Tooltip("Vida, velocidade, dano de contato, raio de colisão (unidades) e resistência a knockback (0..1).")]
        public EnemyStats stats = new EnemyStats { MaxHp = 10f, Speed = 1.5f, ContactDamage = 5f, Radius = 0.35f, KnockbackResistance = 0f };

        [Tooltip("Valor da gema de XP derrubada ao morrer.")]
        public int xpValue = 1;

        [Header("Comportamento especial")]
        [Tooltip("Derruba um baú ao morrer (inimigos de elite e chefes).")]
        public bool dropsChest;
        [Tooltip("Chefes sofrem só dano simbólico da bomba e não derrubam drops raros.")]
        public bool isBoss;

        [Header("Visual")]
        public Sprite sprite;
        [Tooltip("Silhueta branca usada no flash de dano. Se vazio, não pisca.")]
        public Sprite flashSprite;
        public float visualScale = 1f;
        [Tooltip("Ordem de desenho. Use um valor diferente por tipo (10..19): o renderer agrupa por textura e o batching melhora muito.")]
        public int sortingOrder = 10;
        [Tooltip("Cor das partículas ao morrer.")]
        public Color deathColor = Color.white;
    }
}
