using System.IO;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace HollowTide.EditorTools
{
    /// <summary>
    /// Gera os sprites placeholder proceduralmente e os salva como PNG em Assets/_Project/Art/Generated.
    /// Cada marco acrescenta sprites aqui (inimigos, gemas, projéteis...). Regerar é idempotente.
    /// </summary>
    static class PlaceholderSprites
    {
        public const string Dir = "Assets/_Project/Art/Generated";
        public const int Ppu = 32;

        public static string PathOf(string name) => $"{Dir}/{name}.png";

        public static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(PathOf(name));

        public static void GenerateAll()
        {
            Directory.CreateDirectory(Dir);
            SavePng("player", DrawPlayer());
            for (int i = 0; i < 4; i++) SavePng($"ground_{i}", DrawGround(i));

            SaveWithFlash("enemy_crawler", DrawCrawler());
            SaveWithFlash("enemy_shambler", DrawShambler());
            SaveWithFlash("enemy_brute", DrawBrute());
            SaveWithFlash("enemy_swarmer", DrawSwarmer());

            SavePng("slash_arc", DrawSlashArc(), Ppu, new Vector2(0f, 0.5f)); // pivô no centro do arco
            SavePng("pixel", new PixelCanvas(4, 4, new Color32(255, 255, 255, 255)), 4);

            SavePng("gem_0", DrawGem(new Color32(70, 150, 255, 255)));
            SavePng("gem_1", DrawGem(new Color32(80, 215, 120, 255)));
            SavePng("gem_2", DrawGem(new Color32(245, 85, 85, 255)));

            SavePng("player_adept", DrawPlayer(new Color32(150, 95, 215, 255), new Color32(45, 25, 75, 255), new Color32(210, 175, 255, 255)));
            SavePng("player_monk", DrawPlayer(new Color32(230, 130, 55, 255), new Color32(80, 40, 15, 255), new Color32(255, 200, 130, 255)));
            SaveWithFlash("enemy_elite", DrawElite());
            SaveWithFlash("enemy_reaper", DrawReaper());
            SavePng("chest_closed", DrawChest(false));
            SavePng("chest_open", DrawChest(true));
            SavePng("pickup_heal", DrawHeart());
            SavePng("pickup_magnet", DrawMagnet());
            SavePng("pickup_bomb", DrawBomb());
            SavePng("coin", DrawCoin());
            SavePng("bolt_wisp", DrawWisp());
            SavePng("shard", DrawShard());
            SavePng("aura_disc", DrawAuraDisc());
            SavePng("flask", DrawFlask());
            SavePng("explosion_ring", DrawExplosionRing());

            SavePng("icon_crescent_blade", DrawIcon(new Color32(70, 110, 190, 255), IconShape.Crescent));
            SavePng("icon_seeker_wisp", DrawIcon(new Color32(60, 150, 200, 255), IconShape.Circle));
            SavePng("icon_halo_shards", DrawIcon(new Color32(90, 130, 200, 255), IconShape.Ring));
            SavePng("icon_withering_aura", DrawIcon(new Color32(140, 70, 170, 255), IconShape.HalfFill));
            SavePng("icon_meteor_flask", DrawIcon(new Color32(210, 120, 50, 255), IconShape.Triangle));
            SavePng("icon_eclipse_edge", DrawIcon(new Color32(120, 60, 170, 255), IconShape.Crescent));
            SavePng("icon_wisp_storm", DrawIcon(new Color32(190, 70, 160, 255), IconShape.Dots));
            SavePng("icon_halo_tempest", DrawIcon(new Color32(210, 150, 50, 255), IconShape.X));
            foreach (var (stat, color, shape) in PassiveIcons)
                SavePng("icon_passive_" + stat, DrawIcon(color, shape));
            AssetDatabase.Refresh();
        }

        static PixelCanvas DrawPlayer() => DrawPlayer(new Color32(74, 138, 230, 255), new Color32(20, 30, 60, 255), new Color32(140, 190, 255, 255));

        static PixelCanvas DrawPlayer(Color32 body, Color32 outline, Color32 light)
        {
            var c = new PixelCanvas(32, 32);

            c.Ellipse(16, 4, 10, 3, new Color32(0, 0, 0, 90));      // sombra
            c.Circle(16, 15, 12.5f, outline);
            c.Circle(16, 15, 10.5f, body);
            c.Circle(12.5f, 19f, 4f, light);                         // brilho
            c.Rect(11, 15, 3, 4, Color.white);                       // olhos
            c.Rect(19, 15, 3, 4, Color.white);
            c.Rect(12, 15, 2, 3, outline);
            c.Rect(20, 15, 2, 3, outline);
            return c;
        }

        /// <summary>
        /// Tile 128x128 sem detalhes junto da borda (exceto uma linha de grade de 1px na base/esquerda,
        /// que repete perfeitamente), para emendar sem costura e dar noção de deslocamento.
        /// </summary>
        static PixelCanvas DrawGround(int variant)
        {
            const int size = 128;
            var baseCol = new Color32(34, 48, 46, 255);
            var c = new PixelCanvas(size, size, baseCol);
            var rng = new Random(1234 + variant * 77);

            // manchas suaves de 8x8
            for (int by = 0; by < size; by += 8)
                for (int bx = 0; bx < size; bx += 8)
                {
                    int d = rng.Next(-3, 4);
                    if (d == 0) continue;
                    c.Rect(bx, by, 8, 8, new Color32((byte)(baseCol.r + d), (byte)(baseCol.g + d), (byte)(baseCol.b + d), 255));
                }

            // pequenos detalhes, longe da borda
            for (int i = 0; i < 10; i++)
            {
                int x = rng.Next(10, size - 12), y = rng.Next(10, size - 12);
                bool bright = rng.Next(2) == 0;
                var col = bright ? new Color32(58, 78, 70, 255) : new Color32(24, 34, 34, 255);
                c.Rect(x, y, 2, 2, col);
                if (rng.Next(3) == 0) c.Rect(x + 2, y, 2, 1, col);
            }

            var line = new Color32(28, 40, 39, 255);
            c.Rect(0, 0, size, 1, line);
            c.Rect(0, 0, 1, size, line);
            return c;
        }

        static PixelCanvas DrawGem(Color32 color)
        {
            var c = new PixelCanvas(12, 12);
            var dark = new Color32((byte)(color.r * 0.45f), (byte)(color.g * 0.45f), (byte)(color.b * 0.45f), 255);
            var light = new Color32((byte)Mathf.Min(255, color.r + 90), (byte)Mathf.Min(255, color.g + 90), (byte)Mathf.Min(255, color.b + 90), 255);
            for (int y = 0; y < 12; y++)
                for (int x = 0; x < 12; x++)
                {
                    float d = Mathf.Abs(x + 0.5f - 6f) + Mathf.Abs(y + 0.5f - 6f);
                    if (d <= 6f) c.Set(x, y, dark);
                    if (d <= 4.6f) c.Set(x, y, color);
                }
            c.Rect(4, 7, 2, 2, light);
            return c;
        }

        static PixelCanvas DrawElite()
        {
            var c = new PixelCanvas(48, 48);
            var outline = new Color32(25, 28, 35, 255);
            var armor = new Color32(120, 130, 150, 255);
            var dark = new Color32(80, 88, 105, 255);
            c.Rect(20, 38, 8, 9, new Color32(190, 50, 50, 255));    // penacho
            c.Circle(24, 22, 21, outline);
            c.Circle(24, 22, 18.5f, armor);
            c.Ellipse(24, 11, 13, 6, dark);
            c.Rect(10, 21, 28, 9, outline);                          // viseira
            c.Rect(12, 23, 24, 5, new Color32(20, 20, 26, 255));
            c.Rect(16, 24, 5, 3, new Color32(255, 70, 60, 255));     // olhos brilhando
            c.Rect(27, 24, 5, 3, new Color32(255, 70, 60, 255));
            c.Rect(22, 8, 4, 12, dark);
            return c;
        }

        static PixelCanvas DrawReaper()
        {
            const int size = 64;
            var c = new PixelCanvas(size, size);
            var outline = new Color32(8, 8, 14, 255);
            var robe = new Color32(34, 30, 52, 255);
            var robeLight = new Color32(58, 52, 84, 255);

            // Manto: trapézio que alarga para baixo.
            for (int y = 2; y < 42; y++)
            {
                float half = 8f + (42 - y) * 0.42f;
                c.Rect((int)(30 - half) - 1, y, (int)(half * 2) + 4, 1, outline);
            }
            for (int y = 3; y < 41; y++)
            {
                float half = 8f + (42 - y) * 0.42f;
                c.Rect((int)(30 - half) + 1, y, (int)(half * 2), 1, y % 7 == 0 ? robeLight : robe);
            }

            c.Circle(30, 46, 15f, outline);                          // capuz
            c.Circle(30, 46, 13f, robe);
            c.Circle(30, 44, 9.5f, new Color32(4, 4, 10, 255));      // rosto vazio
            c.Rect(24, 44, 4, 3, new Color32(255, 60, 60, 255));     // olhos
            c.Rect(32, 44, 4, 3, new Color32(255, 60, 60, 255));

            c.Rect(52, 4, 3, 56, new Color32(110, 80, 50, 255));     // cabo da foice
            c.Ellipse(46, 58, 10, 4, outline);                       // lâmina
            c.Ellipse(46, 58, 8.5f, 2.6f, new Color32(210, 220, 235, 255));
            c.Rect(38, 55, 8, 2, outline);
            return c;
        }

        static PixelCanvas DrawChest(bool open)
        {
            var c = new PixelCanvas(32, 28);
            var outline = new Color32(45, 25, 10, 255);
            var wood = new Color32(150, 95, 45, 255);
            var woodDark = new Color32(115, 70, 30, 255);
            var gold = new Color32(250, 205, 60, 255);

            c.Ellipse(16, 2, 13, 2.5f, new Color32(0, 0, 0, 80));
            c.Rect(2, 3, 28, 13, outline);
            c.Rect(3, 4, 26, 11, wood);
            c.Rect(3, 4, 26, 3, woodDark);
            c.Rect(14, 4, 4, 11, gold);                              // cinta

            if (!open)
            {
                c.Rect(2, 15, 28, 9, outline);
                c.Rect(3, 16, 26, 7, woodDark);
                c.Rect(14, 16, 4, 7, gold);
                c.Rect(13, 13, 6, 5, gold);                          // fechadura
                c.Rect(15, 14, 2, 2, outline);
            }
            else
            {
                c.Rect(3, 15, 26, 5, new Color32(255, 235, 140, 255)); // brilho lá dentro
                c.Rect(5, 17, 22, 3, new Color32(255, 250, 200, 255));
                c.Rect(2, 21, 28, 6, outline);                       // tampa aberta, atrás
                c.Rect(3, 22, 26, 4, woodDark);
            }
            return c;
        }

        static PixelCanvas DrawHeart()
        {
            var c = new PixelCanvas(16, 16);
            var outline = new Color32(90, 15, 25, 255);
            var red = new Color32(235, 60, 80, 255);
            for (int pass = 0; pass < 2; pass++)
            {
                float grow = pass == 0 ? 1.2f : 0f;
                var col = pass == 0 ? outline : red;
                c.Circle(5, 10, 4f + grow, col);
                c.Circle(11, 10, 4f + grow, col);
                for (int y = 1; y < 10; y++)
                {
                    float half = (y - 0.5f) * 0.72f + (pass == 0 ? 1.2f : 0f);
                    c.Rect((int)(8 - half), y, (int)(half * 2), 1, col);
                }
            }
            c.Rect(4, 11, 2, 2, new Color32(255, 170, 180, 255));
            return c;
        }

        static PixelCanvas DrawMagnet()
        {
            var c = new PixelCanvas(16, 16);
            var outline = new Color32(20, 25, 60, 255);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    float dx = x + 0.5f - 8f, dy = y + 0.5f - 9f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dy < -0.5f) { if (Mathf.Abs(dx) > 2.5f && Mathf.Abs(dx) < 7.5f && y > 1) c.Set(x, y, outline); }
                    else if (r > 2.6f && r < 7.6f && dy > -1f) c.Set(x, y, outline);
                }
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    float dx = x + 0.5f - 8f, dy = y + 0.5f - 9f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    bool arm = dy < -0.5f && Mathf.Abs(dx) > 3.5f && Mathf.Abs(dx) < 6.5f && y > 2;
                    bool arc = r > 3.6f && r < 6.6f && dy > -1f;
                    if (!arm && !arc) continue;
                    bool tip = y <= 5 && dy < -0.5f;
                    c.Set(x, y, tip ? new Color32(235, 240, 250, 255) : new Color32(220, 60, 60, 255));
                }
            return c;
        }

        static PixelCanvas DrawBomb()
        {
            var c = new PixelCanvas(16, 16);
            c.Circle(8, 6.5f, 6.5f, new Color32(15, 15, 22, 255));
            c.Circle(8, 6.5f, 5.2f, new Color32(50, 52, 66, 255));
            c.Circle(6, 8.5f, 1.6f, new Color32(150, 155, 175, 255));
            c.Rect(7, 12, 3, 2, new Color32(120, 90, 50, 255));      // pavio
            c.Rect(9, 13, 2, 2, new Color32(255, 220, 70, 255));     // faísca
            c.Rect(10, 14, 1, 1, new Color32(255, 130, 40, 255));
            return c;
        }

        static PixelCanvas DrawCoin()
        {
            var c = new PixelCanvas(10, 10);
            c.Circle(5, 5, 5f, new Color32(120, 80, 10, 255));
            c.Circle(5, 5, 4f, new Color32(250, 205, 60, 255));
            c.Rect(4, 3, 2, 4, new Color32(200, 150, 30, 255));
            c.Rect(3, 6, 2, 2, new Color32(255, 240, 160, 255));
            return c;
        }

        /// <summary>Orbe claro para receber cor (tint) da arma: base = ciano, evoluída = rosa.</summary>
        static PixelCanvas DrawWisp()
        {
            var c = new PixelCanvas(16, 16);
            c.Circle(8, 8, 7.5f, new Color32(255, 255, 255, 70));
            c.Circle(8, 8, 5.5f, new Color32(255, 255, 255, 150));
            c.Circle(8, 8, 3.5f, new Color32(255, 255, 255, 255));
            return c;
        }

        static PixelCanvas DrawShard()
        {
            var c = new PixelCanvas(16, 16);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    float d = Mathf.Abs(x + 0.5f - 8f) * 1.6f + Mathf.Abs(y + 0.5f - 8f) * 0.85f;
                    if (d <= 8f) c.Set(x, y, new Color32(170, 190, 220, 255));
                    if (d <= 6.2f) c.Set(x, y, new Color32(255, 255, 255, 255));
                }
            return c;
        }

        static PixelCanvas DrawAuraDisc()
        {
            const int size = 128;
            var c = new PixelCanvas(size, size);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - size / 2f) / (size / 2f), dy = (y + 0.5f - size / 2f) / (size / 2f);
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (r > 1f) continue;
                    byte a = r > 0.94f ? (byte)255 : (byte)(70 + 60 * r * r);
                    c.Set(x, y, new Color32(255, 255, 255, a));
                }
            return c;
        }

        static PixelCanvas DrawFlask()
        {
            var c = new PixelCanvas(14, 18);
            var glass = new Color32(200, 225, 235, 255);
            var liquid = new Color32(255, 255, 255, 255);
            c.Rect(5, 12, 4, 5, glass);      // gargalo
            c.Rect(4, 16, 6, 2, new Color32(150, 110, 70, 255)); // rolha
            c.Circle(7, 6.5f, 6.5f, glass);
            c.Circle(7, 6f, 5f, liquid);
            c.Rect(4, 8, 2, 3, new Color32(255, 255, 255, 255));
            return c;
        }

        static PixelCanvas DrawExplosionRing()
        {
            const int size = 64;
            var c = new PixelCanvas(size, size);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - size / 2f) / (size / 2f), dy = (y + 0.5f - size / 2f) / (size / 2f);
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (r > 1f) continue;
                    byte a = r > 0.86f ? (byte)255 : (byte)(60 + 90 * r);
                    c.Set(x, y, new Color32(255, 255, 255, a));
                }
            return c;
        }

        public enum IconShape { Circle, Cross, Diamond, Triangle, Ring, Bars, X, Frame, Dots, Crescent, HalfFill, Wave }

        /// <summary>Um ícone por atributo de passivo: cor + forma distintas (arte placeholder).</summary>
        static readonly (string stat, Color32 color, IconShape shape)[] PassiveIcons =
        {
            ("might", new Color32(200, 70, 60, 255), IconShape.Diamond),
            ("armor", new Color32(120, 140, 170, 255), IconShape.Frame),
            ("maxhp", new Color32(220, 90, 130, 255), IconShape.Cross),
            ("recovery", new Color32(80, 190, 110, 255), IconShape.Wave),
            ("cooldown", new Color32(70, 170, 210, 255), IconShape.Ring),
            ("area", new Color32(150, 100, 210, 255), IconShape.Circle),
            ("projectilespeed", new Color32(230, 200, 70, 255), IconShape.Triangle),
            ("duration", new Color32(230, 140, 60, 255), IconShape.Bars),
            ("movespeed", new Color32(60, 190, 170, 255), IconShape.X),
            ("magnet", new Color32(80, 110, 220, 255), IconShape.HalfFill),
            ("growth", new Color32(150, 210, 70, 255), IconShape.Dots),
            ("luck", new Color32(215, 175, 50, 255), IconShape.Crescent),
        };

        public static string PassiveIconName(string stat) => "icon_passive_" + stat.ToLowerInvariant();

        static PixelCanvas DrawIcon(Color32 bg, IconShape shape)
        {
            const int size = 32;
            var c = new PixelCanvas(size, size);
            var border = new Color32((byte)(bg.r * 0.4f), (byte)(bg.g * 0.4f), (byte)(bg.b * 0.4f), 255);
            var fg = new Color32(250, 250, 255, 255);

            c.Rect(0, 0, size, size, border);
            c.Rect(2, 2, size - 4, size - 4, bg);

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f - size / 2f) / (size / 2f);   // -1..1
                    float v = (y + 0.5f - size / 2f) / (size / 2f);
                    if (InShape(shape, u, v)) c.Set(x, y, fg);
                }
            return c;
        }

        static bool InShape(IconShape shape, float u, float v)
        {
            float au = Mathf.Abs(u), av = Mathf.Abs(v), r2 = u * u + v * v;
            switch (shape)
            {
                case IconShape.Circle: return r2 < 0.30f;
                case IconShape.Cross: return (au < 0.17f && av < 0.62f) || (av < 0.17f && au < 0.62f);
                case IconShape.Diamond: return au + av < 0.68f;
                case IconShape.Triangle:
                    return v > -0.55f && v < 0.6f && au < 0.62f * (0.6f - v) / 1.15f;
                case IconShape.Ring: return r2 > 0.22f && r2 < 0.46f;
                case IconShape.Bars: return av < 0.55f && (Mathf.Abs(u + 0.4f) < 0.11f || au < 0.11f || Mathf.Abs(u - 0.4f) < 0.11f);
                case IconShape.X: return (Mathf.Abs(u - v) < 0.16f || Mathf.Abs(u + v) < 0.16f) && au < 0.62f && av < 0.62f;
                case IconShape.Frame: return Mathf.Max(au, av) < 0.6f && Mathf.Max(au, av) > 0.4f;
                case IconShape.Dots:
                    return (Mathf.Abs(au - 0.32f) < 0.16f) && (Mathf.Abs(av - 0.32f) < 0.16f);
                case IconShape.Crescent:
                    return r2 < 0.42f && (u + 0.22f) * (u + 0.22f) + v * v > 0.30f;
                case IconShape.HalfFill: return r2 < 0.42f && v < 0.05f;
                case IconShape.Wave: return au < 0.65f && Mathf.Abs(v - 0.28f * Mathf.Sin(u * 5.5f)) < 0.13f;
                default: return false;
            }
        }

        static void SaveWithFlash(string name, PixelCanvas canvas)
        {
            SavePng(name, canvas);
            SavePng(name + "_flash", canvas.Silhouette());
        }

        static PixelCanvas DrawCrawler()
        {
            var c = new PixelCanvas(24, 24);
            var outline = new Color32(30, 10, 40, 255);
            var body = new Color32(130, 70, 180, 255);
            var wing = new Color32(95, 45, 140, 255);
            c.Ellipse(6, 13, 6.5f, 4.5f, outline);
            c.Ellipse(18, 13, 6.5f, 4.5f, outline);
            c.Ellipse(6, 13, 5f, 3f, wing);
            c.Ellipse(18, 13, 5f, 3f, wing);
            c.Circle(12, 11, 6.5f, outline);
            c.Circle(12, 11, 5f, body);
            c.Rect(9, 12, 2, 2, new Color32(255, 80, 80, 255));
            c.Rect(13, 12, 2, 2, new Color32(255, 80, 80, 255));
            return c;
        }

        static PixelCanvas DrawShambler()
        {
            var c = new PixelCanvas(32, 32);
            var outline = new Color32(20, 40, 20, 255);
            var body = new Color32(115, 165, 85, 255);
            c.Rect(0, 10, 7, 6, outline);
            c.Rect(25, 10, 7, 6, outline);
            c.Circle(16, 15, 13, outline);
            c.Rect(1, 11, 5, 4, body);
            c.Rect(26, 11, 5, 4, body);
            c.Circle(16, 15, 11, body);
            var eye = new Color32(250, 230, 80, 255);
            c.Rect(11, 17, 3, 3, eye);
            c.Rect(18, 17, 3, 3, eye);
            c.Rect(12, 17, 1, 2, outline);
            c.Rect(19, 17, 1, 2, outline);
            c.Rect(12, 9, 8, 2, new Color32(40, 20, 20, 255));
            return c;
        }

        static PixelCanvas DrawBrute()
        {
            var c = new PixelCanvas(48, 48);
            var outline = new Color32(50, 20, 20, 255);
            var body = new Color32(175, 75, 65, 255);
            var horn = new Color32(235, 225, 195, 255);
            c.Rect(8, 35, 8, 10, outline);
            c.Rect(32, 35, 8, 10, outline);
            c.Circle(24, 22, 21, outline);
            c.Rect(9, 36, 6, 8, horn);
            c.Rect(33, 36, 6, 8, horn);
            c.Circle(24, 22, 18.5f, body);
            c.Ellipse(24, 12, 12, 6, new Color32(145, 58, 52, 255));
            c.Rect(15, 26, 6, 5, Color.white);
            c.Rect(27, 26, 6, 5, Color.white);
            c.Rect(17, 26, 3, 4, outline);
            c.Rect(29, 26, 3, 4, outline);
            c.Rect(13, 32, 9, 2, outline);
            c.Rect(26, 32, 9, 2, outline);
            return c;
        }

        static PixelCanvas DrawSwarmer()
        {
            var c = new PixelCanvas(16, 16);
            c.Circle(8, 8, 7, new Color32(90, 60, 10, 255));
            c.Circle(8, 8, 5.5f, new Color32(240, 190, 40, 255));
            c.Rect(5, 8, 2, 2, new Color32(40, 25, 5, 255));
            c.Rect(9, 8, 2, 2, new Color32(40, 25, 5, 255));
            return c;
        }

        /// <summary>
        /// Crescente de 100° apontando para +X, com o centro do arco na borda esquerda (pivô 0, 0.5).
        /// 64px a 32 PPU = raio de 2 unidades; o comportamento escala por baseSize * Area / 2.
        /// </summary>
        static PixelCanvas DrawSlashArc()
        {
            const int size = 64;
            var c = new PixelCanvas(size, size);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f, dy = y + 0.5f - size / 2f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > 63f) continue;
                    if (Mathf.Abs(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg) > 50f) continue;
                    float ix = dx + 20f; // círculo interno deslocado recorta o crescente
                    if (Mathf.Sqrt(ix * ix + dy * dy) <= 60f) continue;

                    float t = Mathf.InverseLerp(40f, 63f, d);
                    var col = Color.Lerp(new Color(0.43f, 0.63f, 1f), new Color(0.92f, 0.96f, 1f), t);
                    c.Set(x, y, new Color32((byte)(col.r * 255), (byte)(col.g * 255), (byte)(col.b * 255), 235));
                }
            return c;
        }

        static void SavePng(string name, PixelCanvas canvas, int ppu = Ppu, Vector2? pivot = null)
        {
            var tex = canvas.ToTexture();
            string path = PathOf(name);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = ppu;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();

            if (pivot.HasValue)
            {
                imp = (TextureImporter)AssetImporter.GetAtPath(path);
                var settings = new TextureImporterSettings();
                imp.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot.Value;
                imp.SetTextureSettings(settings);
                imp.SaveAndReimport();
            }
        }
    }
}
