using System;
using CityRise.Core;
using UnityEngine;

namespace CityRise.City
{
    /// <summary>
    /// Şehir ızgarası. Hücre (i, j) dünyada [i*cellSize, (i+1)*cellSize) aralığını kaplar; Y = 0 zemindir.
    /// Şehir bloklara bölünür: her blok blockCells × blockCells hücredir ve blokların arasında
    /// bir hücre genişliğinde yol vardır. Blok (0,0) dünyanın ortasındaki meydandır.
    /// Bloklar merkeze uzaklıklarına göre halkalara ayrılır; her halka bir şehir aşamasında açılır.
    /// </summary>
    [Serializable]
    public struct CityGrid
    {
        public float cellSize;
        public int blockCells;
        /// <summary>Merkezden kenara kaç blok (halka) var.</summary>
        public int radiusBlocks;

        public CityGrid(float cellSize, int blockCells, int radiusBlocks)
        {
            this.cellSize = cellSize;
            this.blockCells = blockCells;
            this.radiusBlocks = radiusBlocks;
        }

        /// <summary>Bir blok ve yanındaki yol: blok tekrar aralığı.</summary>
        public int Period => blockCells + 1;

        /// <summary>Blok (0,0)'ın ilk hücresi; meydanın dünya merkezinde durması için.</summary>
        int Offset => -blockCells / 2;

        public RectInt BlockRect(Vector2Int block) =>
            new RectInt(block.x * Period + Offset, block.y * Period + Offset, blockCells, blockCells);

        /// <summary>Şehrin tamamı (en dış yollar dahil).</summary>
        public RectInt Bounds
        {
            get
            {
                var min = BlockRect(new Vector2Int(-radiusBlocks, -radiusBlocks)).min - Vector2Int.one;
                var max = BlockRect(new Vector2Int(radiusBlocks, radiusBlocks)).max + Vector2Int.one;
                return new RectInt(min, max - min);
            }
        }

        static int Mod(int a, int m) => ((a % m) + m) % m;

        /// <summary>Hücre bir blokların arasındaki yol çizgisinde mi (X ya da Z ekseninde)?</summary>
        public bool IsRoadColumn(int i) => Mod(i - Offset, Period) == blockCells;

        public bool IsRoadCell(Vector2Int c) => IsRoadColumn(c.x) || IsRoadColumn(c.y);

        public Vector2Int BlockOf(Vector2Int c) =>
            new Vector2Int(Mathf.FloorToInt((c.x - Offset) / (float)Period), Mathf.FloorToInt((c.y - Offset) / (float)Period));

        /// <summary>Halka: merkez blok 0, çevresindeki 8 blok 1, ...</summary>
        public static int Ring(Vector2Int block) => Mathf.Max(Mathf.Abs(block.x), Mathf.Abs(block.y));

        /// <summary>
        /// Bir yol hücresinin halkası: yanındaki blokların en içtekisi.
        /// Böylece bir halkayı çevreleyen yol o halkayla birlikte açılır.
        /// </summary>
        public int RoadRing(Vector2Int c)
        {
            int best = int.MaxValue;
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                var n = new Vector2Int(c.x + dx, c.y + dy);
                if (IsRoadCell(n)) continue;
                best = Mathf.Min(best, Ring(BlockOf(n)));
            }
            return best == int.MaxValue ? Ring(BlockOf(c)) : best;
        }

        public Vector3 CellCenter(Vector2Int c) => new Vector3((c.x + 0.5f) * cellSize, 0f, (c.y + 0.5f) * cellSize);

        public Vector3 RectCenter(RectInt r) => new Vector3(r.center.x * cellSize, 0f, r.center.y * cellSize);

        public Vector2Int WorldToCell(Vector3 p) =>
            new Vector2Int(Mathf.FloorToInt(p.x / cellSize), Mathf.FloorToInt(p.z / cellSize));

        /// <summary>
        /// Halkanın açıldığı aşama: başlangıç kasabası (halka 0-1) Seyyar, sonra her halka bir aşama.
        /// </summary>
        public static CityStage StageForRing(int ring)
        {
            int stage = Mathf.Clamp(ring, 1, (int)CityStage.Metropol);
            return (CityStage)stage;
        }
    }
}
