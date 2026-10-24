using System.Collections.Generic;
using CityRise.Core;
using UnityEngine;

namespace CityRise.City
{
    /// <summary>
    /// Şehrin arsa ve yol verisi. Görünümden bağımsızdır: sahne bu veriden kurulur,
    /// ileride aynı veri sunucudan da gelebilir.
    /// Proje penceresinde sağ tık > Create > CityRise > Şehir Düzeni ile oluşturulur.
    /// </summary>
    [CreateAssetMenu(menuName = "CityRise/Şehir Düzeni", fileName = "SehirDuzeni")]
    public class CityLayout : ScriptableObject
    {
        public CityGrid grid = new CityGrid(8f, 4, 5);
        public List<Plot> plots = new List<Plot>();
        public List<RoadCell> roads = new List<RoadCell>();

        public Plot FindPlot(Vector2Int cell)
        {
            foreach (var p in plots)
                if (p.Contains(cell)) return p;
            return null;
        }

        public Plot FindPlot(int id) => plots.Find(p => p.id == id);

        public bool IsRoad(Vector2Int cell) => roads.Exists(r => r.cell == cell);

        public int NextPlotId()
        {
            int max = 0;
            foreach (var p in plots) max = Mathf.Max(max, p.id);
            return max + 1;
        }

        /// <summary>Bir aşamada açık olan yollar.</summary>
        public IEnumerable<RoadCell> OpenRoads(CityStage stage)
        {
            foreach (var r in roads)
                if (r.unlockStage <= stage) yield return r;
        }

        /// <summary>
        /// İki arsayı birleştirir (ör. büyük bir bina için). İkinci arsa silinir.
        /// Başarılıysa birleşmiş arsayı döndürür.
        /// </summary>
        public Plot Merge(Plot a, Plot b)
        {
            if (a == null || !a.CanMergeWith(b) || !a.IsEmpty || !b.IsEmpty) return null;
            var min = Vector2Int.Min(a.cells.min, b.cells.min);
            var max = Vector2Int.Max(a.cells.max, b.cells.max);
            a.cells = new RectInt(min, max - min);
            a.type = PlotType.ForSale;
            plots.Remove(b);
            return a;
        }

        /// <summary>
        /// Kilitli bir bloğu satılık arsalara böler (aşama açılınca çağrılır).
        /// </summary>
        public void UnlockBlock(Vector2Int block)
        {
            var locked = plots.FindAll(p => p.block == block && p.type == PlotType.Locked);
            foreach (var p in locked) plots.Remove(p);
            StartingTownGenerator.AddBlockPlots(this, block, BlockTemplate.ForSale, unlocked: true);
        }
    }
}
