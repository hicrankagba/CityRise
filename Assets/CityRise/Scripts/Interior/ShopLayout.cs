using System.Collections.Generic;
using UnityEngine;

namespace CityRise.Interior
{
    /// <summary>İç görünümün yürünebilir noktaları, kasa ve kapı (odanın yerel koordinatlarında). Canlı satış bunları kullanır.</summary>
    public class ShopLayout : MonoBehaviour
    {
        public List<Vector3> spots = new List<Vector3>();
        /// <summary>Kasada ödeme yapan müşterinin durduğu nokta.</summary>
        public Vector3 register;
        /// <summary>Sıranın uzadığı yön (kasadan uzağa).</summary>
        public Vector3 queueDir = Vector3.back;
        /// <summary>Müşterilerin girip çıktığı kapı.</summary>
        public Vector3 door;
        public List<GameObject> customers = new List<GameObject>();
        public List<GameObject> waiting = new List<GameObject>();
    }
}
