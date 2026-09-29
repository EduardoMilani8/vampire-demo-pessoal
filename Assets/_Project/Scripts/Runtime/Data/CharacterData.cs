using HollowTide.Core;
using UnityEngine;

namespace HollowTide
{
    /// <summary>Personagem jogável: arma inicial, bônus fixos e custo de desbloqueio. Assets > Create > Hollow Tide > Character.</summary>
    [CreateAssetMenu(menuName = "Hollow Tide/Character", fileName = "Character_New")]
    public sealed class CharacterData : ScriptableObject
    {
        public string characterId = "new_character";
        public string displayName = "New Character";
        [TextArea] public string description;
        public Sprite sprite;
        public WeaponData startingWeapon;
        [Tooltip("Bônus fixos sobre os atributos (dano, armadura, vida...).")]
        public StatBonus[] bonuses = new StatBonus[0];
        [Tooltip("Ouro para desbloquear. Ignorado se 'unlockedByDefault'.")]
        public int unlockCost = 300;
        public bool unlockedByDefault;
    }
}
