using UnityEngine;

namespace CityRise.Environment
{
    /// <summary>
    /// Su yüzeyindeki iki normal haritasını farklı yönlere kaydırarak dalgalanma hissi verir.
    /// Ana normal haritası _BaseMap'in, ince dalgalar ise detay haritasının kaydırmasını kullanır.
    /// Yalnızca oyun çalışırken çalışır; malzeme dosyasını değiştirmemek için kopya malzeme kullanır.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class WaterScroll : MonoBehaviour
    {
        public Vector2 mainSpeed = new Vector2(0.012f, 0.006f);
        public Vector2 detailSpeed = new Vector2(-0.02f, 0.015f);

        Material mat;
        static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");
        static readonly int DetailSt = Shader.PropertyToID("_DetailAlbedoMap_ST");
        Vector4 baseSt, detailSt;

        void Start()
        {
            mat = GetComponent<Renderer>().material;
            baseSt = mat.GetVector(BaseMapSt);
            detailSt = mat.HasProperty(DetailSt) ? mat.GetVector(DetailSt) : Vector4.zero;
        }

        void Update()
        {
            if (mat == null) return;
            float t = Time.time;
            mat.SetVector(BaseMapSt, new Vector4(baseSt.x, baseSt.y, baseSt.z + mainSpeed.x * t, baseSt.w + mainSpeed.y * t));
            if (detailSt != Vector4.zero)
                mat.SetVector(DetailSt, new Vector4(detailSt.x, detailSt.y, detailSt.z + detailSpeed.x * t, detailSt.w + detailSpeed.y * t));
        }

        void OnDestroy()
        {
            if (mat != null) Destroy(mat);
        }
    }
}
