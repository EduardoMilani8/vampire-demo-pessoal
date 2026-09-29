using HollowTide.Core;
using UnityEngine;

namespace HollowTide
{
    /// <summary>Uma arma equipada: dados + nível + cooldown + o comportamento que a executa.</summary>
    public sealed class WeaponInstance
    {
        public readonly WeaponData Data;
        public int Level = 1;
        public WeaponTimer Timer;   // campo (não propriedade) para o struct ser mutado no lugar
        public WeaponBehavior Behavior;

        public WeaponInstance(WeaponData data) => Data = data;

        public bool IsMaxLevel => Level >= WeaponProgression.MaxLevel;

        /// <summary>Estatísticas efetivas agora: nível da arma + passivos do jogador.</summary>
        public WeaponLevelStats Resolved(in PlayerStats player)
            => WeaponMath.Resolve(Data.StatsAtLevel(Level), player);

        public bool TryLevelUp()
        {
            if (IsMaxLevel) return false;
            Level++;
            Behavior?.OnLevelUp();
            return true;
        }
    }

    /// <summary>
    /// Base dos comportamentos de arma. Para criar um novo: herde, implemente Tick e registre em
    /// <see cref="WeaponBehaviorFactory"/>. Todo o estado mutável vive no comportamento; os números vêm do WeaponData.
    /// </summary>
    public abstract class WeaponBehavior
    {
        protected GameSession Session { get; private set; }
        protected WeaponInstance Weapon { get; private set; }
        protected Transform FxRoot { get; private set; }

        public void Init(GameSession session, WeaponInstance weapon, Transform fxRoot)
        {
            Session = session;
            Weapon = weapon;
            FxRoot = fxRoot;
            OnInit();
        }

        protected virtual void OnInit() { }
        public virtual void OnLevelUp() { }

        /// <summary>Chamado quando a arma sai do jogador (evolução): libere efeitos visuais e objetos criados.</summary>
        public virtual void OnRemoved() { }
        public abstract void Tick(float dt);
    }

    public static class WeaponBehaviorFactory
    {
        public static WeaponBehavior Create(WeaponBehaviorType type)
        {
            switch (type)
            {
                case WeaponBehaviorType.ArcSlash: return new ArcSlashBehavior();
                case WeaponBehaviorType.HomingBolt: return new HomingBoltBehavior();
                case WeaponBehaviorType.Orbit: return new OrbitBehavior();
                case WeaponBehaviorType.Aura: return new AuraBehavior();
                case WeaponBehaviorType.LobbedProjectile: return new LobbedBehavior();
                default:
                    throw new System.NotImplementedException($"Comportamento '{type}' não implementado.");
            }
        }
    }
}
