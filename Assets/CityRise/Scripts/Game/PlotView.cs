using UnityEngine;

namespace CityRise.Game
{
    /// <summary>
    /// Sahnede bir arsanın kök nesnesi. "Susler" altındaki tabela, çiçek ve çimler
    /// arsaya bina kurulunca kaldırılır; kaldırım ağaçları kalır.
    /// </summary>
    public class PlotView : MonoBehaviour
    {
        public int plotId;
        /// <summary>Kurulan işletmenin binası (yoksa boş).</summary>
        public GameObject building;

        public Transform Decor => transform.Find("Susler");

        public void ClearDecor()
        {
            var d = Decor;
            if (d != null) Destroy(d.gameObject);
        }
    }
}
