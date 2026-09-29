using System.Collections.Generic;
using HollowTide.Core;
using UnityEngine;

namespace HollowTide
{
    public enum RunOutcome { None, Defeat, Victory }

    /// <summary>
    /// Dono da simulação de uma partida. É o único Update do gameplay: chama cada subsistema numa ordem
    /// explícita (spawn > movimento dos inimigos > grid > contato > armas > projéteis > coleta), em vez de depender
    /// da ordem de execução entre vários MonoBehaviours. Também gerencia os estados que pausam o jogo
    /// (level up, baú, pausa, fim de partida) e grava o resultado no save.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public sealed class GameSession : MonoBehaviour
    {
        public const float PlayerRadius = 0.3f;

        public static GameSession Instance { get; private set; }

        [Header("Referências")]
        [SerializeField] PlayerController player;
        [SerializeField] CameraFollow cameraFollow;
        [SerializeField] EnemyViews enemyViews;
        [SerializeField] GemViews gemViews;
        [SerializeField] GemViews coinViews;
        [SerializeField] ProjectileSystem projectiles;
        [SerializeField] PickupSystem pickups;
        [SerializeField] Feedback feedback;
        [SerializeField] EnemySpawner spawner;
        [SerializeField] PlayerWeapons weapons;
        [SerializeField] WorldHealthBar healthBar;
        [SerializeField] GameUi ui;
        [SerializeField] ContentCatalog catalog;
        [SerializeField] EnemyData reaperData;

        [Header("Partida")]
        [Tooltip("Duração da partida. Ao chegar aqui surge o Ceifador, que encerra a run.")]
        [SerializeField] float runLengthSeconds = 1800f;
        [SerializeField] float baseMaxHp = 100f;
        [SerializeField] uint seed = 20240611;

        [Header("Simulação")]
        [SerializeField] int enemyCapacity = 2048;
        [SerializeField] int gemCapacity = 1024;
        [SerializeField] float gridCellSize = 1.5f;
        [Tooltip("Inimigos além (raio do anel + isto) são realocados para o outro lado do jogador.")]
        [SerializeField] float relocateExtraDistance = 8f;

        [Header("Coleta e drops")]
        [Tooltip("Raio base de coleta (multiplicado pelo passivo de coleta).")]
        [SerializeField] float pickupRadius = 1.6f;
        [SerializeField] float collectRadius = 0.4f;
        [Tooltip("Chance base de um inimigo derrubar uma moeda de ouro (multiplicada pela sorte).")]
        [SerializeField] float coinDropChance = 0.05f;
        [SerializeField] int startingRerolls = 2;
        [SerializeField] int startingSkips = 2;

        public PlayerController Player => player;
        public CameraFollow CameraFollow => cameraFollow;
        public PlayerWeapons Weapons => weapons;
        public EnemySpawner Spawner => spawner;
        public ContentCatalog Catalog => catalog;
        public ProjectileSystem Projectiles => projectiles;
        public GameUi Ui => ui;
        public CharacterData Character { get; private set; }
        public float RunLengthSeconds => runLengthSeconds;

        public EnemyWorld Enemies { get; private set; }
        public GemWorld Gems { get; private set; }
        public GemWorld Coins { get; private set; }
        public SpatialHashGrid Grid { get; private set; }
        public PlayerHealth Health { get; private set; }
        public XpTracker Xp { get; private set; }
        public PlayerPassives Passives { get; private set; }
        public LevelUpAllowance Allowance { get; private set; }
        public PlayerStats Stats;

        public int Kills { get; private set; }
        /// <summary>Ouro coletado NESTA partida (vai para o save ao terminar).</summary>
        public int Gold { get; private set; }
        public float Elapsed { get; private set; }
        public bool IsGameOver { get; private set; }
        public bool IsLevelUpOpen { get; private set; }
        public bool IsChestOpen { get; private set; }
        public bool IsPaused { get; private set; }
        public bool ReaperSpawned { get; private set; }
        public RunOutcome Outcome { get; private set; }
        /// <summary>Atalho de teste: o jogador não sofre dano.</summary>
        public bool GodMode;
        /// <summary>Atalho de teste (benchmark): level ups e baús não pausam o jogo.</summary>
        public bool SuppressLevelUps;

        public string Notice { get; private set; } = "";
        public float NoticeTime { get; private set; } = -100f;

        /// <summary>Buffer compartilhado das consultas ao grid (a simulação é single-thread).</summary>
        public readonly int[] QueryBuffer = new int[1024];

        LevelUpController _levelUp;
        SimRandom _rng;
        SaveData _save;
        bool _committed;
        readonly OptionView[] _views = new OptionView[8];
        readonly List<KeyValuePair<WeaponData, WeaponData>> _evolutions = new List<KeyValuePair<WeaponData, WeaponData>>(6);

        // ---- Ciclo de vida -----------------------------------------------------------------------

        void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
            _save = SaveSystem.Current;
            _rng = new SimRandom(seed ^ 0xA5A5A5A5u);
            Character = ChooseCharacter();

            Passives = new PlayerPassives();
            Xp = new XpTracker();
            Enemies = new EnemyWorld(enemyCapacity);
            Gems = new GemWorld(gemCapacity);
            Coins = new GemWorld(256);
            Grid = new SpatialHashGrid(gridCellSize, 4096, enemyCapacity);
            Health = new PlayerHealth(baseMaxHp);
            Stats = PlayerStats.Default;

            var scratch = PlayerStats.Default;
            int rerolls = startingRerolls, skips = startingSkips;
            ApplyPermanentBonuses(ref scratch, ref rerolls, ref skips);
            Allowance = new LevelUpAllowance(rerolls, skips);

            enemyViews.Init(enemyCapacity);
            gemViews.Init(gemCapacity);
            coinViews.Init(256);
            projectiles.Init();
            pickups.Init();
        }

        void Start()
        {
            weapons.Init(this);
            if (Character.startingWeapon != null) weapons.Add(Character.startingWeapon);
            player.SetSprite(Character.sprite);

            _levelUp = new LevelUpController(catalog, weapons, Passives, seed);
            spawner.Init(catalog);

            ui.LevelUp.OptionChosen += OnOptionChosen;
            ui.LevelUp.RerollClicked += OnReroll;
            ui.LevelUp.SkipClicked += OnSkip;
            ui.Chest.Closed += OnChestClosed;
            ui.PauseResume += () => SetPaused(false);
            ui.PauseRestart += () => { CommitRun(); GameFlow.ReloadGame(); };
            ui.PauseQuit += () => { CommitRun(); GameFlow.ToMenu(); };
            ui.EndRetry += GameFlow.ReloadGame;
            ui.EndMenu += GameFlow.ToMenu;

            RecalculateStats();
            Health.SetMax(baseMaxHp * Stats.MaxHpMult);
            ui.SetXp(Xp.Ratio, Xp.Level);
            ui.SetLoadout(weapons.Weapons, Passives.Items);
            _ = AudioManager.Instance; // gera os clipes agora, não no meio da primeira luta
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f; // não deixar o próximo carregamento de cena congelado
        }

        CharacterData ChooseCharacter()
        {
            var list = catalog.characters;
            for (int i = 0; i < list.Length; i++)
                if (list[i].characterId == GameFlow.SelectedCharacterId) return list[i];
            for (int i = 0; i < list.Length; i++)
                if (list[i].unlockedByDefault) return list[i];
            return list[0];
        }

        // ---- Loop principal ----------------------------------------------------------------------

        void Update()
        {
            if (!IsGameOver && !IsLevelUpOpen && !IsChestOpen && player.PausePressedThisFrame) SetPaused(!IsPaused);
            if (IsGameOver || IsLevelUpOpen || IsChestOpen || IsPaused) return;

            if (Xp.PendingLevelUps > 0)
            {
                if (SuppressLevelUps) Xp.ConsumePending();
                else
                {
                    OpenLevelUp();
                    return;
                }
            }

            float dt = Mathf.Min(Time.deltaTime, 0.05f); // um hitch não deve teleportar inimigos
            Elapsed += dt;

            player.MoveSpeedMultiplier = Stats.MoveSpeed;
            Health.Armor = Stats.Armor;
            Health.RegenPerSecond = Stats.Recovery;
            Health.Tick(dt);

            spawner.Tick(dt);
            CheckReaper();

            Vector2 p = player.Position;
            Vector2 half = cameraFollow.HalfExtents;
            float ring = SpawnRing.RingRadius(half.x, half.y, spawner.RingMargin);
            Enemies.Step(dt, p.x, p.y, ring + relocateExtraDistance, ring);
            Enemies.RebuildGrid(Grid);
            Enemies.Separate(Grid, dt, QueryBuffer);

            ResolveContact(p);
            weapons.Tick(dt);
            projectiles.Tick(dt);
            CollectGems(dt, p);
            pickups.Tick(dt, p, this);

            healthBar.SetRatio(Health.Ratio);
            ui.SetXp(Xp.Ratio, Xp.Level);
            ui.SetHud(Elapsed, Kills, Gold, Health.Current, Health.Max);

            if (Health.IsDead) EndRun();
        }

        void CheckReaper()
        {
            if (ReaperSpawned || Elapsed < runLengthSeconds || reaperData == null) return;
            ReaperSpawned = true;
            spawner.Spawn(reaperData);
            PostNotice("O CEIFADOR CHEGOU");
            cameraFollow.AddTrauma(0.7f);
            AudioManager.Play(Sfx.Bomb);
        }

        // ---- Fim de partida ----------------------------------------------------------------------

        void EndRun()
        {
            if (IsGameOver) return;
            IsGameOver = true;
            Outcome = ReaperSpawned ? RunOutcome.Victory : RunOutcome.Defeat;
            CommitRun();
            AudioManager.Play(Sfx.GameOver);

            var names = new System.Text.StringBuilder();
            for (int i = 0; i < weapons.Count; i++)
            {
                if (i > 0) names.Append(", ");
                names.Append(weapons.Weapons[i].Data.displayName);
            }

            Time.timeScale = 0f;
            ui.ShowEnd(new EndSummary
            {
                Victory = Outcome == RunOutcome.Victory,
                Seconds = Elapsed,
                Kills = Kills,
                Gold = Gold,
                Level = Xp.Level,
                Weapons = names.ToString(),
                NewBestTime = Elapsed > _bestBeforeRun,
            });
        }

        float _bestBeforeRun = -1f;

        /// <summary>Soma o resultado ao save (uma única vez por partida) e grava em disco.</summary>
        void CommitRun()
        {
            if (_committed) return;
            _committed = true;

            _bestBeforeRun = _save.BestTimeSeconds;
            _save.Gold += Gold;
            _save.TotalKills += Kills;
            _save.TotalRuns++;
            if (Outcome == RunOutcome.Victory) _save.Victories++;
            if (Elapsed > _save.BestTimeSeconds) _save.BestTimeSeconds = Elapsed;
            SaveSystem.Save();
        }

        public void SetPaused(bool paused)
        {
            if (IsGameOver || IsPaused == paused) return;
            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            ui.ShowPause(paused);
            AudioManager.Play(Sfx.Click);
        }

        // ---- Inimigos e dano ---------------------------------------------------------------------

        public bool SpawnEnemy(EnemyData data, float x, float y, float hpMultiplier = 1f)
        {
            var stats = data.stats;
            stats.MaxHp *= hpMultiplier;
            int id = Enemies.Spawn(stats, x, y);
            if (id < 0) return false;
            enemyViews.Attach(id, data, x, y);
            return true;
        }

        /// <summary>Aplica dano e knockback (direção normalizada). Mata, derruba loot e libera o inimigo se a vida acabar.</summary>
        public void HitEnemy(int id, float damage, float dirX, float dirY, float knockback)
        {
            if (!Enemies.Alive[id]) return;

            float x = Enemies.X[id], y = Enemies.Y[id];
            float dealt = Mathf.Min(damage, Enemies.Hp[id]);
            bool killed = Enemies.Damage(id, damage);
            feedback.DamageNumber(x, y, dealt, killed ? new Color(1f, 0.85f, 0.3f) : Color.white);

            if (killed)
            {
                Kills++;
                var data = enemyViews.DataOf(id);
                DropLoot(id, data);
                feedback.Burst(x, y, 7, data != null ? data.deathColor : Color.white, 4.5f, 0.45f, 0.16f);
                AudioManager.Play(Sfx.Kill, 0.7f);
                enemyViews.Release(id);
                Enemies.Despawn(id);
            }
            else
            {
                if (knockback > 0f) Enemies.Knockback(id, dirX, dirY, knockback);
                AudioManager.Play(Sfx.Hit, 0.5f);
            }
        }

        void DropLoot(int id, EnemyData data)
        {
            float x = Enemies.X[id], y = Enemies.Y[id];
            if (data != null && data.xpValue > 0) SpawnGem(x, y, data.xpValue);

            if (data != null && data.dropsChest)
            {
                pickups.Spawn(PickupKind.Chest, x, y);
                return;
            }
            if (data != null && data.isBoss) return;

            if (_rng.NextFloat() < coinDropChance * Stats.Luck)
            {
                int cid = Coins.Spawn(x + _rng.Range(-0.3f, 0.3f), y + _rng.Range(-0.3f, 0.3f), 1f);
                if (cid >= 0) coinViews.Attach(cid, 1f, Coins.X[cid], Coins.Y[cid]);
            }

            switch (RareDropRoller.Roll(_rng, Stats.Luck))
            {
                case RareDrop.Heal: pickups.Spawn(PickupKind.Heal, x, y); break;
                case RareDrop.Magnet: pickups.Spawn(PickupKind.Magnet, x, y); break;
                case RareDrop.Bomb: pickups.Spawn(PickupKind.Bomb, x, y); break;
            }
        }

        public void SpawnGem(float x, float y, float value)
        {
            int gid = Gems.Spawn(x, y, value);
            if (gid >= 0) gemViews.Attach(gid, value, x, y);
        }

        /// <summary>Dano em área instantâneo (explosões, pulsos de aura): acerta todo inimigo cujo corpo toca o círculo.</summary>
        public void DamageArea(float x, float y, float radius, float damage, float knockback)
        {
            int n = Grid.Query(x, y, radius + Enemies.MaxRadius, QueryBuffer);
            for (int i = 0; i < n; i++)
            {
                int id = QueryBuffer[i];
                if (!Enemies.Alive[id]) continue;

                float dx = Enemies.X[id] - x, dy = Enemies.Y[id] - y;
                float reach = radius + Enemies.Radius[id];
                float d2 = dx * dx + dy * dy;
                if (d2 > reach * reach) continue;

                float d = Mathf.Sqrt(d2);
                if (d > 1e-4f) { dx /= d; dy /= d; } else { dx = 0f; dy = 1f; }
                HitEnemy(id, damage, dx, dy, knockback);
            }
            if (radius >= 0.9f) AudioManager.Play(Sfx.Explosion, 0.6f);
        }

        void ResolveContact(Vector2 p)
        {
            int n = Grid.Query(p.x, p.y, PlayerRadius + Enemies.MaxRadius, QueryBuffer);
            float worst = 0f;
            for (int i = 0; i < n; i++)
            {
                int id = QueryBuffer[i];
                float dx = Enemies.X[id] - p.x, dy = Enemies.Y[id] - p.y;
                float reach = PlayerRadius + Enemies.Radius[id];
                if (dx * dx + dy * dy <= reach * reach && Enemies.ContactDamage[id] > worst)
                    worst = Enemies.ContactDamage[id];
            }

            if (worst > 0f && !GodMode && Health.TryHit(worst))
            {
                player.Flash(new Color(1f, 0.3f, 0.3f));
                feedback.DamageNumber(p.x, p.y + 0.3f, Mathf.Max(1f, worst - Health.Armor), new Color(1f, 0.35f, 0.35f));
                feedback.Burst(p.x, p.y, 8, new Color(1f, 0.3f, 0.3f), 5f, 0.4f, 0.14f);
                cameraFollow.AddTrauma(0.32f);
                ui.Flash(new Color(1f, 0f, 0f, 0.22f), 0.25f);
                AudioManager.Play(Sfx.Hurt);
            }
        }

        // ---- XP, ouro e itens do chão ------------------------------------------------------------

        void CollectGems(float dt, Vector2 p)
        {
            float radius = pickupRadius * Stats.Magnet;

            float xp = Gems.Step(dt, p.x, p.y, radius, collectRadius);
            for (int i = 0; i < Gems.CollectedCount; i++) gemViews.Release(Gems.CollectedIds[i]);
            if (xp > 0f)
            {
                Xp.AddXp(xp * Stats.Growth);
                AudioManager.Play(Sfx.Gem, 0.5f);
            }

            float coins = Coins.Step(dt, p.x, p.y, radius, collectRadius);
            for (int i = 0; i < Coins.CollectedCount; i++) coinViews.Release(Coins.CollectedIds[i]);
            if (coins > 0f)
            {
                Gold += Mathf.RoundToInt(coins);
                AudioManager.Play(Sfx.Coin, 0.6f);
            }
        }

        public void MagnetizeAll()
        {
            Gems.MagnetizeAll();
            Coins.MagnetizeAll();
        }

        /// <summary>Limpa a tela: mata tudo à vista, exceto chefes (que só levam dano simbólico).</summary>
        public void Bomb()
        {
            Vector2 p = player.Position;
            Vector2 half = cameraFollow.HalfExtents;
            float rx = half.x + 1.5f, ry = half.y + 1.5f;

            for (int i = Enemies.Count - 1; i >= 0; i--)
            {
                int id = Enemies.Active[i];
                if (Mathf.Abs(Enemies.X[id] - p.x) > rx || Mathf.Abs(Enemies.Y[id] - p.y) > ry) continue;

                var data = enemyViews.DataOf(id);
                HitEnemy(id, data != null && data.isBoss ? 100f : 1e7f, 0f, 1f, 0f);
            }

            ui.Flash(new Color(1f, 1f, 1f, 0.75f), 0.5f);
            cameraFollow.AddTrauma(0.75f);
            AudioManager.Play(Sfx.Bomb);
        }

        public void CollectPickup(PickupKind kind, float x, float y)
        {
            switch (kind)
            {
                case PickupKind.Heal:
                    Health.Heal(Health.Max * 0.3f);
                    feedback.Burst(x, y, 10, new Color(0.4f, 1f, 0.5f), 4f, 0.5f, 0.16f);
                    AudioManager.Play(Sfx.Pickup);
                    PostNotice("Vida recuperada");
                    break;
                case PickupKind.Magnet:
                    MagnetizeAll();
                    feedback.Burst(x, y, 10, new Color(0.5f, 0.6f, 1f), 4f, 0.5f, 0.16f);
                    AudioManager.Play(Sfx.Pickup);
                    PostNotice("Ímã: todas as gemas vêm até você");
                    break;
                case PickupKind.Bomb:
                    Bomb();
                    PostNotice("Bomba!");
                    break;
                case PickupKind.Chest:
                    if (!SuppressLevelUps) OpenChest();
                    break;
            }
        }

        // ---- Baú e evolução ----------------------------------------------------------------------

        /// <summary>
        /// Evolui toda arma elegível (nível máximo + passivo exigido). Retorna quantas evoluíram; os pares ficam em
        /// <see cref="_evolutions"/> até a próxima chamada.
        /// </summary>
        public int TryEvolveAll()
        {
            _evolutions.Clear();
            for (int i = 0; i < weapons.Count; i++)
            {
                var inst = weapons.Weapons[i];
                var data = inst.Data;
                if (data.evolvesInto == null || data.requiredPassive == null) continue;

                bool hasPassive = Passives.Find(data.requiredPassive) != null;
                if (!EvolutionRules.IsEligible(inst.Level, WeaponProgression.MaxLevel, hasPassive)) continue;

                if (weapons.Replace(inst, data.evolvesInto) != null)
                {
                    _evolutions.Add(new KeyValuePair<WeaponData, WeaponData>(data, data.evolvesInto));
                    PostNotice($"EVOLUÇÃO: {data.displayName} → {data.evolvesInto.displayName}!");
                }
            }
            if (_evolutions.Count > 0) ui.SetLoadout(weapons.Weapons, Passives.Items);
            return _evolutions.Count;
        }

        void OpenChest()
        {
            IsChestOpen = true;
            Time.timeScale = 0f;

            int count = 0;
            int evolved = TryEvolveAll();
            for (int i = 0; i < evolved; i++)
            {
                var pair = _evolutions[i];
                _views[count++] = new OptionView
                {
                    Kind = OptionKind.NewWeapon, Title = pair.Value.displayName, Tag = "EVOLUÇÃO",
                    Description = pair.Key.displayName + " evoluiu!", Icon = pair.Value.icon,
                };
            }

            int items = ChestRewards.ItemCount(_rng, Stats.Luck);
            int upgrades = _levelUp.RollAndApplyUpgrades(this, items);
            for (int i = 0; i < upgrades && count < _views.Length - 1; i++) _views[count++] = _levelUp.GetView(i);

            int gold = ChestRewards.GoldAmount(_rng, Stats.Luck, Elapsed / 60f);
            Gold += gold;
            _views[count++] = new OptionView { Kind = OptionKind.Fallback, Title = gold + " de ouro", Tag = "OURO", Description = "", Icon = null };

            RecalculateStats();
            ui.SetLoadout(weapons.Weapons, Passives.Items);
            ui.Chest.Show(_views, count, evolved > 0);
            AudioManager.Play(evolved > 0 ? Sfx.Evolve : Sfx.Chest);
        }

        void OnChestClosed()
        {
            IsChestOpen = false;
            Time.timeScale = 1f;
        }

        // ---- Estatísticas ------------------------------------------------------------------------

        void ApplyPermanentBonuses(ref PlayerStats stats, ref int rerolls, ref int skips)
        {
            if (Character != null)
                foreach (var b in Character.bonuses) PassiveMath.Apply(ref stats, b.Stat, b.Amount);

            foreach (var m in catalog.metaUpgrades)
            {
                int level = _save.GetUpgradeLevel(m.upgradeId);
                if (level > 0) MetaMath.Apply(ref stats, ref rerolls, ref skips, m.stat, m.amountPerLevel * level);
            }
        }

        /// <summary>Recalcula os atributos: personagem + melhorias permanentes + passivos da partida.</summary>
        public void RecalculateStats()
        {
            var s = PlayerStats.Default;
            int unusedRerolls = 0, unusedSkips = 0;
            ApplyPermanentBonuses(ref s, ref unusedRerolls, ref unusedSkips);
            Passives.Apply(ref s);
            Stats = s;

            float newMax = baseMaxHp * Stats.MaxHpMult;
            if (!Mathf.Approximately(newMax, Health.Max)) Health.SetMax(newMax);
        }

        // ---- Level up ----------------------------------------------------------------------------

        void OpenLevelUp()
        {
            Xp.ConsumePending();
            _levelUp.Roll(Stats.Luck, avoidCurrent: false);
            IsLevelUpOpen = true;
            Time.timeScale = 0f;
            ShowLevelUp();
            AudioManager.Play(Sfx.LevelUp);
        }

        void ShowLevelUp()
        {
            for (int i = 0; i < _levelUp.OptionCount; i++) _views[i] = _levelUp.GetView(i);
            ui.LevelUp.Show(_views, _levelUp.OptionCount, Allowance.Rerolls, Allowance.Skips);
        }

        void OnOptionChosen(int index)
        {
            if (!IsLevelUpOpen || index >= _levelUp.OptionCount) return;
            _levelUp.Apply(this, index);
            RecalculateStats();
            ui.SetLoadout(weapons.Weapons, Passives.Items);
            AudioManager.Play(Sfx.Click);
            CloseLevelUp();
        }

        void OnReroll()
        {
            if (!IsLevelUpOpen || !Allowance.TryReroll()) return;
            _levelUp.Roll(Stats.Luck, avoidCurrent: true);
            ShowLevelUp();
            AudioManager.Play(Sfx.Click);
        }

        void OnSkip()
        {
            if (!IsLevelUpOpen || !Allowance.TrySkip()) return;
            AudioManager.Play(Sfx.Click);
            CloseLevelUp();
        }

        void CloseLevelUp()
        {
            ui.LevelUp.Hide();
            IsLevelUpOpen = false;
            Time.timeScale = 1f;
        }

        // ---- Avisos e atalhos de teste -----------------------------------------------------------

        public void PostNotice(string text)
        {
            Notice = text;
            NoticeTime = Time.unscaledTime;
            ui.ShowNotice(text);
        }

        /// <summary>Atalho de teste: dá XP suficiente para subir um nível agora.</summary>
        public void DebugGrantLevel() => Xp.AddXp(Xp.Required - Xp.Current);

        /// <summary>Atalho de teste: avança o relógio da partida (ex.: para ver o Ceifador sem esperar 30 min).</summary>
        public void DebugSkipTime(float seconds)
        {
            Elapsed += seconds;
            spawner.SkipClock(seconds);
        }
    }
}
