using System;
using System.IO;
using HollowTide.Core;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HollowTide.EditorTools
{
    /// <summary>
    /// Gera o conteúdo padrão como ScriptableObjects em Assets/_Project/Content.
    /// Um asset que já existe NUNCA é sobrescrito, então o que você editar no Inspector sobrevive a um novo
    /// "Build Demo". Para voltar aos valores originais use Tools > Reset Content.
    /// </summary>
    static class ContentBuilder
    {
        public const string Root = "Assets/_Project/Content";
        public const string CatalogPath = Root + "/ContentCatalog.asset";

        public static T GetOrCreate<T>(string folder, string assetName, Action<T> init) where T : ScriptableObject
        {
            string dir = string.IsNullOrEmpty(folder) ? Root : $"{Root}/{folder}";
            Directory.CreateDirectory(dir);
            string path = $"{dir}/{assetName}.asset";

            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            init(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        public static void ResetAll()
        {
            if (AssetDatabase.IsValidFolder(Root)) AssetDatabase.DeleteAsset(Root);
        }

        // ---- Inimigos ----------------------------------------------------------------------------

        /// <summary>Cria/atualiza os inimigos. Retorna todos (incluindo elite e ceifador).</summary>
        public static EnemyData[] BuildEnemies() => new[]
        {
            Enemy("Enemy_Crawler", "Crawler", "enemy_crawler", 8f, 2.4f, 4f, 0.28f, 0f, 1, order: 10, color: new Color(0.65f, 0.4f, 0.85f)),
            Enemy("Enemy_Shambler", "Shambler", "enemy_shambler", 20f, 1.5f, 7f, 0.42f, 0.1f, 2, order: 11, color: new Color(0.55f, 0.8f, 0.4f)),
            Enemy("Enemy_Swarmer", "Swarmer", "enemy_swarmer", 5f, 2.0f, 3f, 0.2f, 0f, 1, order: 12, color: new Color(0.98f, 0.78f, 0.25f)),
            Enemy("Enemy_Brute", "Brute", "enemy_brute", 90f, 1.0f, 14f, 0.7f, 0.7f, 6, order: 13, color: new Color(0.85f, 0.35f, 0.3f)),
            Enemy("Enemy_HollowKnight", "Hollow Knight", "enemy_elite", 380f, 1.25f, 18f, 0.75f, 0.9f, 30, order: 14,
                  color: new Color(0.7f, 0.75f, 0.85f), dropsChest: true, isBoss: true),
            Enemy("Enemy_Reaper", "Ceifador", "enemy_reaper", 999999f, 4.6f, 9999f, 0.6f, 1f, 0, order: 15,
                  color: new Color(0.6f, 0.2f, 0.9f), isBoss: true),
        };

        static EnemyData Enemy(string assetName, string display, string spriteName, float hp, float speed, float dmg,
                               float radius, float kb, int xp, int order, Color color, bool dropsChest = false, bool isBoss = false)
        {
            var e = GetOrCreate<EnemyData>("Enemies", assetName, d =>
            {
                d.displayName = display;
                d.stats = new EnemyStats { MaxHp = hp, Speed = speed, ContactDamage = dmg, Radius = radius, KnockbackResistance = kb };
                d.xpValue = xp;
                d.sprite = PlaceholderSprites.Load(spriteName);
                d.flashSprite = PlaceholderSprites.Load(spriteName + "_flash");
                d.dropsChest = dropsChest;
                d.isBoss = isBoss;
                d.sortingOrder = order;
                d.deathColor = color;
            });

            // Assets de marcos anteriores não tinham estes campos: só preenche o que ainda está no valor padrão.
            bool dirty = false;
            if (e.sortingOrder == 10 && order != 10) { e.sortingOrder = order; dirty = true; }
            if (e.deathColor == Color.white) { e.deathColor = color; dirty = true; }
            if (dirty) EditorUtility.SetDirty(e);
            return e;
        }

        // ---- Armas -------------------------------------------------------------------------------

        public static WeaponData BuildCrescentBlade()
        {
            var weapon = CreateCrescentBlade();
            if (weapon.icon == null) // asset criado por um marco anterior, antes de existirem ícones
            {
                weapon.icon = PlaceholderSprites.Load("icon_crescent_blade");
                EditorUtility.SetDirty(weapon);
            }
            return weapon;
        }

        static WeaponData CreateCrescentBlade() => GetOrCreate<WeaponData>("Weapons", "Weapon_CrescentBlade", w =>
        {
            w.weaponId = "crescent_blade";
            w.displayName = "Crescent Blade";
            w.description = "Corta em arco na direção em que você anda.";
            w.behavior = WeaponBehaviorType.ArcSlash;
            w.baseStats = new WeaponLevelStats
            {
                Damage = 10f, Area = 1f, Cooldown = 1.4f, Duration = 1f, ProjectileSpeed = 1f, Knockback = 6f, Amount = 1, Pierce = 0
            };
            w.levelUps = new[]
            {
                new StatDelta { DamageAdd = 5f },                    // nível 2
                new StatDelta { AmountAdd = 1 },                     // 3: golpe extra para o outro lado
                new StatDelta { AreaPct = 0.2f },                    // 4
                new StatDelta { DamageAdd = 5f },                    // 5
                new StatDelta { CooldownPct = -0.1f },               // 6
                new StatDelta { AreaPct = 0.2f },                    // 7
                new StatDelta { AmountAdd = 1, DamageAdd = 5f },     // 8
            };
            w.baseSize = 1.7f;
            w.arcAngleDegrees = 100f;
            w.effectSprite = PlaceholderSprites.Load("slash_arc");
            w.icon = PlaceholderSprites.Load("icon_crescent_blade");
        });

        // ---- Outras armas e evoluções -------------------------------------------------------------

        /// <summary>Cria as demais armas e suas evoluções e liga cada evolução ao passivo exigido. Chame depois de BuildPassives.</summary>
        public static void BuildOtherWeaponsAndEvolutions()
        {
            var tintWisp = new Color(0.45f, 0.85f, 1f);
            var tintShards = new Color(0.7f, 0.9f, 1f);

            var wisp = Weapon("Weapon_SeekerWisp", "seeker_wisp", "Seeker Wisp", "Orbes que perseguem o inimigo mais próximo.",
                WeaponBehaviorType.HomingBolt,
                new WeaponLevelStats { Damage = 8f, Area = 1f, Cooldown = 1.3f, Duration = 1f, ProjectileSpeed = 1f, Knockback = 2f, Amount = 1, Pierce = 0 },
                new[]
                {
                    new StatDelta { AmountAdd = 1 },
                    new StatDelta { DamageAdd = 4f },
                    new StatDelta { AmountAdd = 1 },
                    new StatDelta { PierceAdd = 1 },
                    new StatDelta { DamageAdd = 4f },
                    new StatDelta { AmountAdd = 1 },
                    new StatDelta { PierceAdd = 1, DamageAdd = 4f },
                },
                size: 0.25f, speed: 7f, duration: 2.2f, sprite: "bolt_wisp", tint: tintWisp, icon: "icon_seeker_wisp");

            var wispStorm = Weapon("Weapon_WispStorm", "wisp_storm", "Wisp Storm", "Uma tempestade de orbes que atravessa inimigos.",
                WeaponBehaviorType.HomingBolt,
                new WeaponLevelStats { Damage = 15f, Area = 1.4f, Cooldown = 0.9f, Duration = 1f, ProjectileSpeed = 1.3f, Knockback = 3f, Amount = 4, Pierce = 2 },
                null, size: 0.25f, speed: 7f, duration: 2.6f, sprite: "bolt_wisp", tint: new Color(1f, 0.55f, 0.95f), icon: "icon_wisp_storm", evolutionOnly: true);

            var shards = Weapon("Weapon_HaloShards", "halo_shards", "Halo Shards", "Cristais giram em volta de você.",
                WeaponBehaviorType.Orbit,
                new WeaponLevelStats { Damage = 6f, Area = 1f, Cooldown = 2f, Duration = 1f, ProjectileSpeed = 1f, Knockback = 3f, Amount = 2, Pierce = 0 },
                new[]
                {
                    new StatDelta { AmountAdd = 1 },
                    new StatDelta { DamageAdd = 3f },
                    new StatDelta { SpeedPct = 0.2f },
                    new StatDelta { AmountAdd = 1 },
                    new StatDelta { DurationPct = 0.25f },
                    new StatDelta { DamageAdd = 3f },
                    new StatDelta { AmountAdd = 1, DamageAdd = 3f },
                },
                size: 1.7f, speed: 170f, duration: 3f, sprite: "shard", tint: tintShards, icon: "icon_halo_shards");

            var tempest = Weapon("Weapon_HaloTempest", "halo_tempest", "Halo Tempest", "Um anel denso de cristais, quase sempre ativo.",
                WeaponBehaviorType.Orbit,
                new WeaponLevelStats { Damage = 13f, Area = 1.4f, Cooldown = 1.2f, Duration = 1.7f, ProjectileSpeed = 1.5f, Knockback = 4f, Amount = 5, Pierce = 0 },
                null, size: 2f, speed: 170f, duration: 3f, sprite: "shard", tint: new Color(1f, 0.8f, 0.4f), icon: "icon_halo_tempest", evolutionOnly: true);

            Weapon("Weapon_WitheringAura", "withering_aura", "Withering Aura", "Uma aura que corrói tudo por perto.",
                WeaponBehaviorType.Aura,
                new WeaponLevelStats { Damage = 4f, Area = 1f, Cooldown = 0.7f, Duration = 1f, ProjectileSpeed = 1f, Knockback = 1.5f, Amount = 1, Pierce = 0 },
                new[]
                {
                    new StatDelta { AreaPct = 0.2f },
                    new StatDelta { DamageAdd = 2f },
                    new StatDelta { CooldownPct = -0.1f },
                    new StatDelta { AreaPct = 0.2f },
                    new StatDelta { DamageAdd = 2f },
                    new StatDelta { AreaPct = 0.2f },
                    new StatDelta { DamageAdd = 4f, CooldownPct = -0.1f },
                },
                size: 1.6f, speed: 0f, duration: 1f, sprite: "aura_disc", tint: new Color(0.8f, 0.3f, 0.9f), icon: "icon_withering_aura");

            Weapon("Weapon_MeteorFlask", "meteor_flask", "Meteor Flask", "Frascos que explodem ao cair sobre os inimigos.",
                WeaponBehaviorType.LobbedProjectile,
                new WeaponLevelStats { Damage = 20f, Area = 1f, Cooldown = 2.4f, Duration = 1f, ProjectileSpeed = 1f, Knockback = 5f, Amount = 1, Pierce = 0 },
                new[]
                {
                    new StatDelta { AmountAdd = 1 },
                    new StatDelta { DamageAdd = 8f },
                    new StatDelta { AreaPct = 0.2f },
                    new StatDelta { AmountAdd = 1 },
                    new StatDelta { DamageAdd = 8f },
                    new StatDelta { CooldownPct = -0.1f },
                    new StatDelta { AmountAdd = 1, DamageAdd = 8f },
                },
                size: 1.3f, speed: 8f, duration: 1f, sprite: "flask", tint: new Color(1f, 0.6f, 0.3f), icon: "icon_meteor_flask");

            var eclipse = Weapon("Weapon_EclipseEdge", "eclipse_edge", "Eclipse Edge", "Um corte enorme e triplo.",
                WeaponBehaviorType.ArcSlash,
                new WeaponLevelStats { Damage = 32f, Area = 1.5f, Cooldown = 1f, Duration = 1f, ProjectileSpeed = 1f, Knockback = 9f, Amount = 3, Pierce = 0 },
                null, size: 1.7f, speed: 0f, duration: 1f, sprite: "slash_arc", tint: new Color(0.85f, 0.55f, 1f), icon: "icon_eclipse_edge", evolutionOnly: true);

            var crescent = AssetDatabase.LoadAssetAtPath<WeaponData>($"{Root}/Weapons/Weapon_CrescentBlade.asset");
            LinkEvolution(crescent, eclipse, "Passive_WrathSigil");
            LinkEvolution(wisp, wispStorm, "Passive_QuickeningGear");
            LinkEvolution(shards, tempest, "Passive_LingeringEmber");
            AssetDatabase.SaveAssets();
        }

        static WeaponData Weapon(string assetName, string id, string display, string description, WeaponBehaviorType behavior,
                                 WeaponLevelStats stats, StatDelta[] levelUps, float size, float speed, float duration,
                                 string sprite, Color tint, string icon, bool evolutionOnly = false)
        {
            return GetOrCreate<WeaponData>("Weapons", assetName, w =>
            {
                w.weaponId = id;
                w.displayName = display;
                w.description = description;
                w.behavior = behavior;
                w.evolutionOnly = evolutionOnly;
                w.baseStats = stats;
                w.levelUps = levelUps ?? new StatDelta[0];
                w.baseSize = size;
                w.baseSpeed = speed;
                w.baseDuration = duration;
                w.effectSprite = PlaceholderSprites.Load(sprite);
                w.effectTint = tint;
                w.icon = PlaceholderSprites.Load(icon);
            });
        }

        /// <summary>Liga a evolução se ainda não estiver ligada (não sobrescreve o que você configurou no Inspector).</summary>
        static void LinkEvolution(WeaponData baseWeapon, WeaponData evolved, string passiveAsset)
        {
            if (baseWeapon == null || evolved == null) return;
            var passive = AssetDatabase.LoadAssetAtPath<PassiveData>($"{Root}/Passives/{passiveAsset}.asset");
            if (baseWeapon.evolvesInto == null) baseWeapon.evolvesInto = evolved;
            if (baseWeapon.requiredPassive == null) baseWeapon.requiredPassive = passive;
            EditorUtility.SetDirty(baseWeapon);
        }

        // ---- Passivos ----------------------------------------------------------------------------

        public static void BuildPassives()
        {
            Passive("Passive_WrathSigil", "wrath_sigil", "Wrath Sigil", "Golpes mais fortes.", PassiveStat.Might, 0.10f);
            Passive("Passive_BulwarkPlate", "bulwark_plate", "Bulwark Plate", "Reduz todo dano recebido.", PassiveStat.Armor, 1f);
            Passive("Passive_VitalBloom", "vital_bloom", "Vital Bloom", "Mais vida máxima.", PassiveStat.MaxHp, 0.20f);
            Passive("Passive_MendingThread", "mending_thread", "Mending Thread", "Regenera vida com o tempo.", PassiveStat.Recovery, 0.2f);
            Passive("Passive_QuickeningGear", "quickening_gear", "Quickening Gear", "Armas recarregam mais rápido.", PassiveStat.Cooldown, 0.08f);
            Passive("Passive_WideLens", "wide_lens", "Wide Lens", "Ataques maiores.", PassiveStat.Area, 0.10f);
            Passive("Passive_SwiftFeather", "swift_feather", "Swift Feather", "Projéteis mais rápidos.", PassiveStat.ProjectileSpeed, 0.10f);
            Passive("Passive_LingeringEmber", "lingering_ember", "Lingering Ember", "Efeitos duram mais.", PassiveStat.Duration, 0.15f);
            Passive("Passive_FleetBoots", "fleet_boots", "Fleet Boots", "Anda mais rápido.", PassiveStat.MoveSpeed, 0.10f);
            Passive("Passive_Lodestone", "lodestone", "Lodestone", "Atrai gemas de mais longe.", PassiveStat.Magnet, 0.25f);
            Passive("Passive_ScholarsInk", "scholars_ink", "Scholar's Ink", "Ganha mais XP.", PassiveStat.Growth, 0.08f);
            Passive("Passive_FortuneDie", "fortune_die", "Fortune Die", "Mais sorte: melhores escolhas e drops.", PassiveStat.Luck, 0.10f);
        }

        static void Passive(string assetName, string id, string display, string description, PassiveStat stat, float perLevel)
        {
            GetOrCreate<PassiveData>("Passives", assetName, p =>
            {
                p.passiveId = id;
                p.displayName = display;
                p.description = description;
                p.stat = stat;
                p.amountPerLevel = perLevel;
                p.maxLevel = 5;
                p.icon = PlaceholderSprites.Load(PlaceholderSprites.PassiveIconName(stat.ToString()));
            });
        }

        // ---- Personagens -------------------------------------------------------------------------

        public static void BuildCharacters()
        {
            Character("Character_Warden", "warden", "Aldric, o Guardião", "Firme como uma muralha. Começa com a Crescent Blade.",
                "player", "Weapon_CrescentBlade", 0, true,
                new StatBonus { Stat = PassiveStat.Armor, Amount = 1f }, new StatBonus { Stat = PassiveStat.MaxHp, Amount = 0.1f });

            Character("Character_Adept", "lyra_adept", "Lyra, Adepta dos Orbes", "Aprende depressa e dispara mais rápido. Começa com o Seeker Wisp.",
                "player_adept", "Weapon_SeekerWisp", 250, false,
                new StatBonus { Stat = PassiveStat.Growth, Amount = 0.1f }, new StatBonus { Stat = PassiveStat.Cooldown, Amount = 0.05f });

            Character("Character_Monk", "cinder_monk", "Irmão Cinza", "Resistente e regenerador. Começa com a Withering Aura.",
                "player_monk", "Weapon_WitheringAura", 500, false,
                new StatBonus { Stat = PassiveStat.MaxHp, Amount = 0.2f }, new StatBonus { Stat = PassiveStat.Recovery, Amount = 0.3f });
        }

        static void Character(string assetName, string id, string display, string description, string sprite, string weaponAsset,
                              int cost, bool byDefault, params StatBonus[] bonuses)
        {
            GetOrCreate<CharacterData>("Characters", assetName, c =>
            {
                c.characterId = id;
                c.displayName = display;
                c.description = description;
                c.sprite = PlaceholderSprites.Load(sprite);
                c.startingWeapon = AssetDatabase.LoadAssetAtPath<WeaponData>($"{Root}/Weapons/{weaponAsset}.asset");
                c.bonuses = bonuses;
                c.unlockCost = cost;
                c.unlockedByDefault = byDefault;
            });
        }

        // ---- Melhorias permanentes ---------------------------------------------------------------

        public static void BuildMetaUpgrades()
        {
            Meta("Meta_Might", "might", "Força", "Golpes mais fortes.", MetaStat.Might, 0.05f, 5, 120, 1.55f);
            Meta("Meta_MaxHp", "maxhp", "Vitalidade", "Mais vida máxima.", MetaStat.MaxHp, 0.10f, 5, 100, 1.5f);
            Meta("Meta_Armor", "armor", "Armadura", "Reduz o dano recebido.", MetaStat.Armor, 1f, 3, 200, 2f);
            Meta("Meta_Recovery", "recovery", "Regeneração", "Recupera vida com o tempo.", MetaStat.Recovery, 0.1f, 5, 110, 1.5f);
            Meta("Meta_Cooldown", "cooldown", "Recarga", "Armas disparam mais rápido.", MetaStat.Cooldown, 0.03f, 5, 150, 1.6f);
            Meta("Meta_Area", "area", "Alcance", "Ataques maiores.", MetaStat.Area, 0.05f, 5, 140, 1.55f);
            Meta("Meta_MoveSpeed", "movespeed", "Agilidade", "Anda mais rápido.", MetaStat.MoveSpeed, 0.05f, 5, 90, 1.5f);
            Meta("Meta_Magnet", "magnet", "Atração", "Coleta gemas de mais longe.", MetaStat.Magnet, 0.15f, 5, 80, 1.45f);
            Meta("Meta_Growth", "growth", "Sabedoria", "Ganha mais XP.", MetaStat.Growth, 0.05f, 5, 130, 1.55f);
            Meta("Meta_Luck", "luck", "Sorte", "Mais drops raros e melhores baús.", MetaStat.Luck, 0.05f, 5, 160, 1.6f);
            Meta("Meta_Reroll", "reroll", "Rerrolagem", "Mais rerolls por partida.", MetaStat.Rerolls, 1f, 3, 250, 2f);
            Meta("Meta_Skip", "skip", "Pulo", "Mais skips por partida.", MetaStat.Skips, 1f, 3, 200, 2f);
        }

        static void Meta(string assetName, string id, string display, string description, MetaStat stat, float perLevel,
                         int maxLevel, int baseCost, float growth)
        {
            GetOrCreate<MetaUpgradeData>("Meta", assetName, m =>
            {
                m.upgradeId = id;
                m.displayName = display;
                m.description = description;
                m.stat = stat;
                m.amountPerLevel = perLevel;
                m.maxLevel = maxLevel;
                m.baseCost = baseCost;
                m.costGrowth = growth;
            });
        }

        // ---- Ondas -------------------------------------------------------------------------------

        /// <summary>
        /// Gera 30 ondas, uma por minuto (Wave_00 ... Wave_29). Ritmo, teto de inimigos e vida crescem a cada minuto;
        /// tipos novos entram aos poucos; elites a cada 3 minutos e cercos de swarmers a cada 4.
        /// Depois de geradas, edite-as no Inspector ou crie novas ondas: o catálogo as reencontra sozinho.
        /// </summary>
        public static void BuildWaves(EnemyData crawler, EnemyData shambler, EnemyData swarmer, EnemyData brute, EnemyData elite)
        {
            for (int m = 0; m < 30; m++)
            {
                int minute = m;
                GetOrCreate<WaveData>("Waves", $"Wave_{m:00}", w =>
                {
                    w.startMinute = minute;
                    w.spawnsPerSecond = 1.3f + 0.5f * minute;
                    w.maxAlive = Mathf.Min(700, 110 + 22 * minute);
                    w.hpMultiplier = 1f + 0.06f * minute;

                    var list = new System.Collections.Generic.List<WaveEnemy>
                    {
                        new WaveEnemy { enemy = crawler, weight = Mathf.Max(0.2f, 3f - 0.09f * minute) },
                    };
                    if (minute >= 1) list.Add(new WaveEnemy { enemy = swarmer, weight = 1.2f + 0.04f * (minute - 1) });
                    if (minute >= 2) list.Add(new WaveEnemy { enemy = shambler, weight = 1.4f + 0.12f * (minute - 2) });
                    if (minute >= 5) list.Add(new WaveEnemy { enemy = brute, weight = 0.25f + 0.1f * (minute - 5) });
                    w.enemies = list.ToArray();

                    var bursts = new System.Collections.Generic.List<WaveBurstData>();
                    if (minute > 0 && minute % 3 == 0)
                        bursts.Add(new WaveBurstData { enemy = elite, count = 1 + minute / 9, atSecond = 10f, surround = false });
                    if (minute > 0 && minute % 4 == 0)
                        bursts.Add(new WaveBurstData { enemy = swarmer, count = Mathf.Min(80, 24 + minute * 2), atSecond = 30f, surround = true });
                    w.bursts = bursts.ToArray();
                });
            }
        }

        // ---- Catálogo ----------------------------------------------------------------------------

        /// <summary>Cria o catálogo se faltar e o reconstrói varrendo a pasta Content (novos assets entram sozinhos).</summary>
        public static ContentCatalog BuildCatalog()
        {
            var catalog = GetOrCreate<ContentCatalog>("", "ContentCatalog", _ => { });
            catalog.weapons = FindAll<WeaponData>();
            catalog.passives = FindAll<PassiveData>();
            catalog.enemies = FindAll<EnemyData>();
            catalog.waves = FindAll<WaveData>();
            catalog.characters = FindAll<CharacterData>();
            catalog.metaUpgrades = FindAll<MetaUpgradeData>();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        /// <summary>Avisa no Console se a tabela de ondas não "sempre cresce" (ritmo ou teto de inimigos diminuindo).</summary>
        public static void ValidateWaves(ContentCatalog catalog)
        {
            var defs = new WaveDefinition[catalog.waves.Length];
            for (int i = 0; i < defs.Length; i++)
            {
                var w = catalog.waves[i];
                defs[i] = new WaveDefinition { StartSeconds = w.startMinute * 60f, SpawnsPerSecond = w.spawnsPerSecond, MaxAlive = w.maxAlive };
            }

            if (!WaveTableValidator.IsGrowing(defs, out string problem))
                Debug.LogWarning("[Build Demo] A tabela de ondas não cresce: " + problem);
        }

        static T[] FindAll<T>() where T : ScriptableObject
        {
            var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { Root });
            var paths = new string[guids.Length];
            for (int i = 0; i < guids.Length; i++) paths[i] = AssetDatabase.GUIDToAssetPath(guids[i]);
            Array.Sort(paths, StringComparer.Ordinal);

            var result = new T[paths.Length];
            for (int i = 0; i < paths.Length; i++) result[i] = AssetDatabase.LoadAssetAtPath<T>(paths[i]);
            return result;
        }
    }
}
