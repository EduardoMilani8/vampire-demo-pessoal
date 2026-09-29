using System.Collections.Generic;
using System.Globalization;
using HollowTide.Core;
using UnityEngine;

namespace HollowTide
{
    /// <summary>Textos de descrição de melhorias, gerados a partir dos números (só usados ao abrir a UI, não em código quente).</summary>
    public static class StatText
    {
        static string Pct(float f) => Mathf.RoundToInt(f * 100f).ToString(CultureInfo.InvariantCulture) + "%";
        static string Num(float f) => f.ToString("0.##", CultureInfo.InvariantCulture);

        public static string DescribePassive(PassiveStat stat, float perLevel)
        {
            switch (stat)
            {
                case PassiveStat.Might: return $"+{Pct(perLevel)} de dano";
                case PassiveStat.Armor: return $"+{Num(perLevel)} de armadura";
                case PassiveStat.MaxHp: return $"+{Pct(perLevel)} de vida máxima";
                case PassiveStat.Recovery: return $"+{Num(perLevel)} de vida por segundo";
                case PassiveStat.Cooldown: return $"-{Pct(perLevel)} de recarga";
                case PassiveStat.Area: return $"+{Pct(perLevel)} de área";
                case PassiveStat.ProjectileSpeed: return $"+{Pct(perLevel)} de velocidade de projétil";
                case PassiveStat.Duration: return $"+{Pct(perLevel)} de duração";
                case PassiveStat.MoveSpeed: return $"+{Pct(perLevel)} de velocidade de movimento";
                case PassiveStat.Magnet: return $"+{Pct(perLevel)} de raio de coleta";
                case PassiveStat.Growth: return $"+{Pct(perLevel)} de XP recebido";
                case PassiveStat.Luck: return $"+{Pct(perLevel)} de sorte";
                default: return "";
            }
        }

        static readonly List<string> Parts = new List<string>(8);

        public static string DescribeDelta(in StatDelta d)
        {
            Parts.Clear();
            if (d.DamageAdd != 0f) Parts.Add($"{Signed(d.DamageAdd)} de dano");
            if (d.AmountAdd != 0) Parts.Add($"{Signed(d.AmountAdd)} quantidade");
            if (d.AreaPct != 0f) Parts.Add($"{SignedPct(d.AreaPct)} de área");
            if (d.CooldownPct != 0f) Parts.Add($"{SignedPct(d.CooldownPct)} de recarga");
            if (d.DurationPct != 0f) Parts.Add($"{SignedPct(d.DurationPct)} de duração");
            if (d.SpeedPct != 0f) Parts.Add($"{SignedPct(d.SpeedPct)} de velocidade");
            if (d.PierceAdd != 0) Parts.Add($"{Signed(d.PierceAdd)} perfuração");
            if (d.KnockbackAdd != 0f) Parts.Add($"{Signed(d.KnockbackAdd)} de repulsão");
            return string.Join("\n", Parts);
        }

        static string Signed(float v) => (v > 0 ? "+" : "") + Num(v);
        static string SignedPct(float v) => (v > 0 ? "+" : "") + Pct(v);
    }
}
