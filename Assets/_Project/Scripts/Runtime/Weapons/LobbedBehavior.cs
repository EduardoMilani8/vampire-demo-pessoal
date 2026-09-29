using HollowTide.Core;
using UnityEngine;

namespace HollowTide
{
    /// <summary>
    /// Projétil em arco: é lançado no inimigo mais próximo (ou à frente do jogador se não houver), passa por cima
    /// de tudo no voo e explode em área ao cair. Frascos extras caem espalhados ao redor do primeiro alvo.
    /// </summary>
    public sealed class LobbedBehavior : WeaponBehavior
    {
        const float ThrowInterval = 0.15f;
        const float SearchRadius = 11f;
        const float FallbackDistance = 4f;
        const float ScatterRadius = 1.6f;
        const float MinFlightTime = 0.4f;
        const float MaxFlightTime = 1.4f;
        const float ArcHeight = 1.8f;

        SimRandom _rng;
        int _pending;
        int _throwIndex;
        float _throwDelay;
        Vector2 _aim;

        protected override void OnInit() => _rng = new SimRandom(0xC0FFEEu);

        public override void Tick(float dt)
        {
            WeaponLevelStats stats = Weapon.Resolved(Session.Stats);

            if (_pending == 0 && Weapon.Timer.Tick(dt, stats.Cooldown))
            {
                _pending = Mathf.Max(1, stats.Amount);
                _throwIndex = 0;
                _throwDelay = 0f;
                _aim = PickAim();
            }

            if (_pending > 0)
            {
                _throwDelay -= dt;
                if (_throwDelay <= 0f)
                {
                    Throw(stats, _throwIndex++);
                    _pending--;
                    _throwDelay = ThrowInterval;
                }
            }
        }

        Vector2 PickAim()
        {
            Vector2 origin = Session.Player.Position;
            if (Session.Grid.FindNearest(origin.x, origin.y, SearchRadius, out int target, out _))
                return new Vector2(Session.Enemies.X[target], Session.Enemies.Y[target]);
            return origin + Session.Player.Facing * FallbackDistance;
        }

        void Throw(in WeaponLevelStats stats, int index)
        {
            Vector2 start = Session.Player.Position;
            Vector2 landing = _aim;
            if (index > 0)
            {
                float a = _rng.NextFloat() * Mathf.PI * 2f;
                float r = ScatterRadius * Mathf.Sqrt(_rng.NextFloat());
                landing += new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }

            var data = Weapon.Data;
            Vector2 delta = landing - start;
            float flight = Mathf.Clamp(delta.magnitude / (data.baseSpeed * stats.ProjectileSpeed), MinFlightTime, MaxFlightTime);

            var spawn = new ProjectileSpawn
            {
                X = start.x, Y = start.y, Vx = delta.x / flight, Vy = delta.y / flight,
                Radius = 0.2f, Damage = stats.Damage, Knockback = stats.Knockback,
                Life = flight, ExplodeRadius = data.baseSize * stats.Area, Arc = ArcHeight,
            };
            Session.Projectiles.Spawn(spawn, data.effectSprite, 1f, data.effectTint);
        }
    }
}
