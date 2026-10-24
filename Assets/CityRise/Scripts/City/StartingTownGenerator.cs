using System.Collections.Generic;
using CityRise.Core;
using UnityEngine;

namespace CityRise.City
{
    /// <summary>Bir bloğun arsalara nasıl bölüneceği.</summary>
    public enum BlockTemplate
    {
        /// <summary>Tek parça, aşama açılınca bölünür.</summary>
        Locked,
        /// <summary>Tüm blok tek meydan.</summary>
        Plaza,
        /// <summary>Çevrede 12 küçük dükkan arsası (1×1), ortada 2×2 büyük arsa.</summary>
        Commercial,
        /// <summary>Dört 2×2 ev arsası.</summary>
        Residential,
        /// <summary>Dört 2×2 satılık arsa.</summary>
        ForSale,
        /// <summary>Tüm blok park.</summary>
        Park,
        /// <summary>Yarısı kamu binası, yarısı park.</summary>
        Civic,
    }

    /// <summary>
    /// Varsayılan başlangıç kasabasını üretir: ortada meydan, çevresinde birkaç dükkan, ev ve park;
    /// dış halkalar kilitli. Yerleşim sabit tohumla üretildiği için her seferinde aynıdır.
    /// </summary>
    public static class StartingTownGenerator
    {
        public const int OpenRings = 1;

        struct Occupant
        {
            public PlotType type;
            public string id;
            public string style;
            public Occupant(PlotType type, string id, string style) { this.type = type; this.id = id; this.style = style; }
        }

        static Occupant Shop(string id, string style) => new Occupant(PlotType.Business, id, style);
        static Occupant Home(string style) => new Occupant(PlotType.House, "ev", style);

        public static void Fill(CityLayout layout)
        {
            layout.plots.Clear();
            layout.roads.Clear();
            var g = layout.grid;

            // Yollar: tüm ızgara; her yol kenarındaki en iç halkayla birlikte açılır.
            var bounds = g.Bounds;
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                var c = new Vector2Int(x, y);
                if (!g.IsRoadCell(c)) continue;
                var center = g.BlockRect(Vector2Int.zero);
                bool main = x == center.xMin - 1 || x == center.xMax || y == center.yMin - 1 || y == center.yMax;
                layout.roads.Add(new RoadCell { cell = c, main = main, unlockStage = CityGrid.StageForRing(g.RoadRing(c)) });
            }

            // Bloklar.
            for (int bx = -g.radiusBlocks; bx <= g.radiusBlocks; bx++)
            for (int by = -g.radiusBlocks; by <= g.radiusBlocks; by++)
            {
                var block = new Vector2Int(bx, by);
                if (CityGrid.Ring(block) > OpenRings)
                {
                    AddBlockPlots(layout, block, BlockTemplate.Locked, unlocked: false);
                    continue;
                }
                AddStartingBlock(layout, block);
            }
        }

        /// <summary>Başlangıç kasabasının elle tasarlanmış blokları.</summary>
        static void AddStartingBlock(CityLayout layout, Vector2Int block)
        {
            // Dükkanların sırası: güneyden başlayıp saat yönünün tersine bloğun çevresi (bkz. PerimeterCells).
            var none = new Occupant[0];
            switch ((block.x, block.y))
            {
                case (0, 0):
                    AddBlockPlots(layout, block, BlockTemplate.Plaza, true, new[] { new Occupant(PlotType.Plaza, "meydan", "") });
                    break;
                case (0, 1): // Meydanın kuzeyi: çarşı. Güney sırası meydana bakar.
                    AddBlockPlots(layout, block, BlockTemplate.Commercial, true, new[]
                    {
                        Shop("simit_bufesi", "commercial/building-c"),
                        Shop("bakkal", "commercial/building-a"),
                        Shop("kafe", "commercial/building-h"),
                        Shop("firin", "commercial/building-d"),
                    });
                    break;
                case (0, -1): // Meydanın güneyi: kuzey sırası meydana bakar.
                    AddBlockPlots(layout, block, BlockTemplate.Commercial, true, new[]
                    {
                        default, default, default, default, default, default,
                        Shop("manav", "commercial/building-b"),
                        Shop("berber", "commercial/building-g"),
                        Shop("eczane", "commercial/building-f"),
                    });
                    break;
                case (1, 0):
                    AddBlockPlots(layout, block, BlockTemplate.Civic, true, new[] { new Occupant(PlotType.Civic, "belediye", "commercial/building-i") });
                    break;
                case (-1, 1):
                case (-1, -1):
                    AddBlockPlots(layout, block, BlockTemplate.Park, true, new[] { new Occupant(PlotType.Park, "park", "") });
                    break;
                case (-1, 0):
                    AddBlockPlots(layout, block, BlockTemplate.Residential, true, new[]
                    {
                        Home("suburban/building-type-a"), Home("suburban/building-type-k"), Home("suburban/building-type-e"),
                    });
                    break;
                case (1, 1):
                    AddBlockPlots(layout, block, BlockTemplate.Residential, true, new[]
                    {
                        Home("suburban/building-type-c"), default, default, Home("suburban/building-type-p"),
                    });
                    break;
                default:
                    AddBlockPlots(layout, block, BlockTemplate.ForSale, true, none);
                    break;
            }
        }

        public static void AddBlockPlots(CityLayout layout, Vector2Int block, BlockTemplate template, bool unlocked)
            => AddBlockPlots(layout, block, template, unlocked, new Occupant[0]);

        static void AddBlockPlots(CityLayout layout, Vector2Int block, BlockTemplate template, bool unlocked, Occupant[] occupants)
        {
            var g = layout.grid;
            var r = g.BlockRect(block);
            var stage = CityGrid.StageForRing(CityGrid.Ring(block));
            var rects = new List<(RectInt rect, Facing facing)>();
            int n = g.blockCells;
            int half = n / 2;

            switch (template)
            {
                case BlockTemplate.Locked:
                case BlockTemplate.Plaza:
                case BlockTemplate.Park:
                    rects.Add((r, FacingTowardCenter(block)));
                    break;
                case BlockTemplate.Commercial:
                    foreach (var (cell, facing) in PerimeterCells(r))
                        rects.Add((new RectInt(cell, Vector2Int.one), facing));
                    rects.Add((new RectInt(r.xMin + 1, r.yMin + 1, n - 2, n - 2), FacingTowardCenter(block)));
                    break;
                case BlockTemplate.Residential:
                case BlockTemplate.ForSale:
                    for (int qy = 0; qy < 2; qy++)
                    for (int qx = 0; qx < 2; qx++)
                        rects.Add((new RectInt(r.xMin + qx * half, r.yMin + qy * half, half, half), qy == 0 ? Facing.South : Facing.North));
                    break;
                case BlockTemplate.Civic:
                {
                    // Kamu binası meydana bakan yarıda, diğer yarısı park.
                    var toCenter = FacingTowardCenter(block);
                    bool alongX = toCenter == Facing.East || toCenter == Facing.West;
                    RectInt front, back;
                    if (alongX)
                    {
                        bool west = toCenter == Facing.West;
                        front = new RectInt(west ? r.xMin : r.xMin + half, r.yMin, half, n);
                        back = new RectInt(west ? r.xMin + half : r.xMin, r.yMin, half, n);
                    }
                    else
                    {
                        bool south = toCenter == Facing.South;
                        front = new RectInt(r.xMin, south ? r.yMin : r.yMin + half, n, half);
                        back = new RectInt(r.xMin, south ? r.yMin + half : r.yMin, n, half);
                    }
                    rects.Add((front, toCenter));
                    rects.Add((back, toCenter));
                    occupants = new[] { occupants.Length > 0 ? occupants[0] : default, new Occupant(PlotType.Park, "park", "") };
                    break;
                }
            }

            for (int i = 0; i < rects.Count; i++)
            {
                var plot = new Plot
                {
                    id = layout.NextPlotId(),
                    cells = rects[i].rect,
                    facing = rects[i].facing,
                    block = block,
                    unlockStage = stage,
                    type = unlocked ? PlotType.ForSale : PlotType.Locked,
                };
                if (unlocked && i < occupants.Length && occupants[i].id != null)
                {
                    plot.type = occupants[i].type;
                    plot.occupantId = occupants[i].id;
                    plot.styleId = occupants[i].style ?? "";
                }
                layout.plots.Add(plot);
            }
        }

        /// <summary>
        /// Bloğun çevresindeki hücreler ve baktıkları yön.
        /// Sıra: güney kenarı (batıdan doğuya), doğu, kuzey (doğudan batıya), batı. Köşeler kuzey/güneye bakar.
        /// </summary>
        public static IEnumerable<(Vector2Int cell, Facing facing)> PerimeterCells(RectInt r)
        {
            for (int x = r.xMin; x < r.xMax; x++) yield return (new Vector2Int(x, r.yMin), Facing.South);
            for (int y = r.yMin + 1; y < r.yMax - 1; y++) yield return (new Vector2Int(r.xMax - 1, y), Facing.East);
            for (int x = r.xMax - 1; x >= r.xMin; x--) yield return (new Vector2Int(x, r.yMax - 1), Facing.North);
            for (int y = r.yMax - 2; y > r.yMin; y--) yield return (new Vector2Int(r.xMin, y), Facing.West);
        }

        static Facing FacingTowardCenter(Vector2Int block)
        {
            if (block == Vector2Int.zero) return Facing.South;
            if (Mathf.Abs(block.x) >= Mathf.Abs(block.y)) return block.x > 0 ? Facing.West : Facing.East;
            return block.y > 0 ? Facing.South : Facing.North;
        }
    }
}
