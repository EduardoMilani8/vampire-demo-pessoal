namespace HollowTide.Core
{
    /// <summary>XP necessário por nível. Estritamente crescente, com saltos de dificuldade nos níveis 20 e 40.</summary>
    public static class XpCurve
    {
        /// <summary>XP para sair do nível <paramref name="level"/> (>= 1) e chegar no seguinte.</summary>
        public static int RequiredForNext(int level)
        {
            if (level < 1) level = 1;
            int req = 5 + 9 * (level - 1);
            if (level >= 20) req += 300 + 6 * (level - 20);
            if (level >= 40) req += 900 + 20 * (level - 40);
            return req;
        }

        /// <summary>XP total acumulado para alcançar <paramref name="level"/> (nível 1 = 0).</summary>
        public static long CumulativeToReach(int level)
        {
            long total = 0;
            for (int l = 1; l < level; l++) total += RequiredForNext(l);
            return total;
        }
    }

    /// <summary>
    /// Nível e XP do jogador. O nível sobe imediatamente ao acumular XP; cada subida fica como
    /// "level up pendente" até a UI abrir a escolha correspondente (várias subidas num frame viram várias escolhas).
    /// </summary>
    public sealed class XpTracker
    {
        public int Level { get; private set; } = 1;
        public float Current { get; private set; }
        public int PendingLevelUps { get; private set; }

        public int Required => XpCurve.RequiredForNext(Level);
        public float Ratio => Current / Required;

        /// <summary>Soma XP; retorna quantos níveis foram ganhos.</summary>
        public int AddXp(float amount)
        {
            if (amount <= 0f) return 0;

            Current += amount;
            int gained = 0;
            while (Current >= Required)
            {
                Current -= Required;
                Level++;
                PendingLevelUps++;
                gained++;
            }
            return gained;
        }

        /// <summary>Consome uma escolha pendente; false se não há nenhuma.</summary>
        public bool ConsumePending()
        {
            if (PendingLevelUps <= 0) return false;
            PendingLevelUps--;
            return true;
        }
    }
}
