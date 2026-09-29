namespace HollowTide.Core
{
    /// <summary>
    /// Limita a frequência com que uma arma contínua (orbitais) acerta o mesmo inimigo. Indexada pelo id do
    /// inimigo no EnemyWorld; um id reaproveitado herda no máximo um intervalo de espera, o que é irrelevante na prática.
    /// </summary>
    public sealed class HitCooldowns
    {
        readonly float[] _nextHitTime;

        public HitCooldowns(int enemyCapacity) => _nextHitTime = new float[enemyCapacity];

        /// <summary>True (e arma a espera) se o inimigo pode ser acertado em <paramref name="now"/>.</summary>
        public bool TryHit(int enemyId, float now, float interval)
        {
            if (now < _nextHitTime[enemyId]) return false;
            _nextHitTime[enemyId] = now + interval;
            return true;
        }

        public void Clear()
        {
            System.Array.Clear(_nextHitTime, 0, _nextHitTime.Length);
        }
    }

    public static class EvolutionRules
    {
        /// <summary>Uma arma evolui quando está no nível máximo e o jogador tem o passivo exigido.</summary>
        public static bool IsEligible(int weaponLevel, int maxLevel, bool hasRequiredPassive)
            => hasRequiredPassive && weaponLevel >= maxLevel;
    }
}
