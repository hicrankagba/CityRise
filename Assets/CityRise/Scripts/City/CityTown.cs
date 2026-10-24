using CityRise.Core;
using UnityEngine;

namespace CityRise.City
{
    /// <summary>
    /// Sahnedeki şehir. Düzen verisini tutar ve dünyadaki bir noktanın hangi arsaya denk geldiğini söyler
    /// (ileride arsaya dokununca satın alma / yükseltme penceresi açmak için).
    /// </summary>
    public class CityTown : MonoBehaviour
    {
        public CityLayout layout;
        public CityStage stage = CityStage.Seyyar;

        public CityGrid Grid => layout.grid;

        public Plot PlotAt(Vector3 worldPoint)
        {
            if (layout == null) return null;
            return layout.FindPlot(Grid.WorldToCell(worldPoint));
        }

        public bool IsUnlocked(Plot plot) => plot != null && plot.unlockStage <= stage;

        void OnDrawGizmosSelected()
        {
            if (layout == null) return;
            foreach (var p in layout.plots)
            {
                Gizmos.color = GizmoColor(p.type);
                var size = new Vector3(p.cells.width * Grid.cellSize, 0.2f, p.cells.height * Grid.cellSize);
                Gizmos.DrawWireCube(Grid.RectCenter(p.cells) + Vector3.up * 0.3f, size * 0.96f);
            }
        }

        static Color GizmoColor(PlotType t)
        {
            switch (t)
            {
                case PlotType.Business: return new Color(1f, 0.6f, 0.1f);
                case PlotType.House: return new Color(0.3f, 0.7f, 1f);
                case PlotType.Park: return Color.green;
                case PlotType.Civic: return Color.magenta;
                case PlotType.Plaza: return Color.white;
                case PlotType.ForSale: return Color.yellow;
                default: return new Color(0.4f, 0.4f, 0.4f);
            }
        }
    }
}
