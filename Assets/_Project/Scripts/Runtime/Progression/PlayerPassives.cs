using System.Collections.Generic;
using HollowTide.Core;

namespace HollowTide
{
    public sealed class PassiveInstance
    {
        public readonly PassiveData Data;
        public int Level = 1;

        public PassiveInstance(PassiveData data) => Data = data;

        public bool IsMaxLevel => Level >= Data.maxLevel;
    }

    /// <summary>Passivos equipados (máximo 6). Os efeitos são recalculados por completo a cada mudança.</summary>
    public sealed class PlayerPassives
    {
        public const int Max = LevelUpPool.MaxPassives;

        readonly List<PassiveInstance> _list = new List<PassiveInstance>(Max);

        public IReadOnlyList<PassiveInstance> Items => _list;
        public int Count => _list.Count;
        public bool IsFull => _list.Count >= Max;

        public PassiveInstance Find(PassiveData data)
        {
            for (int i = 0; i < _list.Count; i++)
                if (_list[i].Data == data) return _list[i];
            return null;
        }

        public PassiveInstance Add(PassiveData data)
        {
            if (IsFull || Find(data) != null) return null;
            var p = new PassiveInstance(data);
            _list.Add(p);
            return p;
        }

        public bool TryLevelUp(PassiveInstance p)
        {
            if (p.IsMaxLevel) return false;
            p.Level++;
            return true;
        }

        /// <summary>Soma o efeito de todos os passivos em <paramref name="stats"/> (que deve partir de PlayerStats.Default).</summary>
        public void Apply(ref PlayerStats stats)
        {
            for (int i = 0; i < _list.Count; i++)
            {
                var p = _list[i];
                PassiveMath.Apply(ref stats, p.Data.stat, p.Data.amountPerLevel * p.Level);
            }
        }
    }
}
