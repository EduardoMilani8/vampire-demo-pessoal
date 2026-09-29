using System.Collections.Generic;
using HollowTide.Core;
using UnityEngine;

namespace HollowTide
{
    /// <summary>Armas equipadas do jogador (máximo 6). Os disparos são dirigidos pelo GameSession via <see cref="Tick"/>.</summary>
    public sealed class PlayerWeapons : MonoBehaviour
    {
        public const int MaxWeapons = 6;

        readonly List<WeaponInstance> _weapons = new List<WeaponInstance>(MaxWeapons);
        GameSession _session;

        public IReadOnlyList<WeaponInstance> Weapons => _weapons;
        public int Count => _weapons.Count;
        public bool IsFull => _weapons.Count >= MaxWeapons;

        public void Init(GameSession session) => _session = session;

        public WeaponInstance Find(WeaponData data)
        {
            for (int i = 0; i < _weapons.Count; i++)
                if (_weapons[i].Data == data) return _weapons[i];
            return null;
        }

        /// <summary>Equipa a arma; retorna null se já está cheio ou já possui a arma.</summary>
        public WeaponInstance Add(WeaponData data)
        {
            if (IsFull || Find(data) != null) return null;

            var instance = new WeaponInstance(data);
            instance.Behavior = WeaponBehaviorFactory.Create(data.behavior);
            _weapons.Add(instance);
            instance.Behavior.Init(_session, instance, transform);
            return instance;
        }

        /// <summary>
        /// Troca uma arma pela sua forma evoluída, na mesma posição da lista e já no nível máximo.
        /// Retorna null se a arma não está equipada ou a evolução já existe.
        /// </summary>
        public WeaponInstance Replace(WeaponInstance old, WeaponData evolved)
        {
            int index = _weapons.IndexOf(old);
            if (index < 0 || Find(evolved) != null) return null;

            old.Behavior.OnRemoved();
            var instance = new WeaponInstance(evolved) { Level = WeaponProgression.MaxLevel };
            instance.Behavior = WeaponBehaviorFactory.Create(evolved.behavior);
            _weapons[index] = instance;
            instance.Behavior.Init(_session, instance, transform);
            return instance;
        }

        public void Tick(float dt)
        {
            for (int i = 0; i < _weapons.Count; i++)
                _weapons[i].Behavior.Tick(dt);
        }
    }
}
