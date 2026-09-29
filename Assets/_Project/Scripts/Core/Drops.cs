using System;

namespace HollowTide.Core
{
    public enum RareDrop : byte { None, Heal, Magnet, Bomb }

    /// <summary>Drops raros de inimigos comuns: cura, ímã (puxa todas as gemas) e bomba (limpa a tela). A sorte multiplica as chances.</summary>
    public static class RareDropRoller
    {
        public const float HealChance = 0.006f;
        public const float MagnetChance = 0.003f;
        public const float BombChance = 0.002f;

        public static RareDrop Roll(SimRandom rng, float luck)
        {
            float scale = Math.Max(0.1f, luck);
            float heal = HealChance * scale, magnet = MagnetChance * scale, bomb = BombChance * scale;

            float r = rng.NextFloat();
            if (r < heal) return RareDrop.Heal;
            r -= heal;
            if (r < magnet) return RareDrop.Magnet;
            r -= magnet;
            if (r < bomb) return RareDrop.Bomb;
            return RareDrop.None;
        }
    }

    /// <summary>Recompensas de baú: 1, 3 ou 5 melhorias (mais chance de 3/5 com sorte) e uma quantia de ouro.</summary>
    public static class ChestRewards
    {
        public static float ChanceOfFive(float luck) => Math.Max(0f, Math.Min(0.25f, (luck - 1f) * 0.2f));
        public static float ChanceOfThree(float luck) => 0.2f + Math.Max(0f, Math.Min(0.4f, (luck - 1f) * 0.4f));

        public static int ItemCount(SimRandom rng, float luck)
        {
            float r = rng.NextFloat();
            float five = ChanceOfFive(luck);
            if (r < five) return 5;
            if (r < five + ChanceOfThree(luck)) return 3;
            return 1;
        }

        /// <summary>Ouro do baú: 15–40, escalado pela sorte e pelo minuto da partida.</summary>
        public static int GoldAmount(SimRandom rng, float luck, float runMinutes)
        {
            float baseGold = rng.Range(15f, 40f);
            return Math.Max(1, (int)(baseGold * Math.Max(0.1f, luck) * (1f + 0.05f * Math.Max(0f, runMinutes))));
        }
    }
}
