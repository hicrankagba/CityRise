using CityRise.Game;
using CityRise.Interior;
using CityRise.Progression;
using UnityEngine;

namespace CityRise.Life
{
    /// <summary>
    /// Canlı satışta bir müşteri: kapıdan girer → başında istediği ürün balonu → rafa gider, ürünü alır →
    /// kasada sıraya girer → öder ve çıkar (ya da ürün yoksa kızgın çıkar) → bir süre sonra yeniden gelir.
    /// </summary>
    public class ShopCustomer : MonoBehaviour
    {
        enum Phase { Outside, ToShelf, Picking, ToQueue, Queue, Leaving }

        ShopFlow flow;
        ShopLayout layout;
        Product[] products;
        InteriorLibrary lib;
        Animator animator;
        string animState;
        Phase phase;
        float timer;
        Vector3 goal;
        int queueIndex = -1;
        System.Random rng;
        Transform bubble;
        Renderer bubbleIcon, bubbleBack;
        bool gotIt;
        const float Speed = 1.15f;

        public Product Wanted { get; private set; }
        public bool AtRegister => phase == Phase.Queue && queueIndex == 0 && Flat(transform.localPosition - goal) < 0.08f;

        public void Init(ShopFlow f, ShopLayout l, Product[] list, InteriorLibrary library, float delay)
        {
            flow = f;
            layout = l;
            products = list;
            lib = library;
            animator = GetComponentInChildren<Animator>();
            rng = new System.Random(GetHashCode());
            BuildBubble();
            // İlk müşteriler zaten içeride: hemen rafa yönelir.
            if (delay <= 0.01f) Begin(false);
            else { phase = Phase.Outside; timer = delay; Show(false); }
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        void Show(bool on)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = on;
            if (!on && bubble != null) bubble.gameObject.SetActive(false);
        }

        void SetAnim(string s)
        {
            if (animator == null || animState == s) return;
            animState = s;
            animator.CrossFadeInFixedTime(s, 0.2f);
        }

        /// <summary>Yeni tur: kapıdan girer (ya da içeriden başlar), istediği ürünü seçer.</summary>
        void Begin(bool fromDoor)
        {
            if (products.Length == 0) return;
            Show(true);
            if (fromDoor) transform.localPosition = layout.door;
            Wanted = flow.PickWanted(products, rng);
            gotIt = false;
            SetBubble(Wanted, false);
            goal = layout.spots.Count > 0 ? layout.spots[rng.Next(layout.spots.Count)] : transform.localPosition;
            phase = Phase.ToShelf;
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            switch (phase)
            {
                case Phase.Outside:
                    timer -= dt;
                    if (timer <= 0) Begin(true);
                    break;
                case Phase.ToShelf:
                    if (Walk(dt)) { phase = Phase.Picking; timer = 2.4f; SetAnim("PickUp"); }
                    break;
                case Phase.Picking:
                    timer -= dt;
                    if (timer <= 0)
                    {
                        if (flow.InStock(Wanted.id))
                        {
                            gotIt = true;
                            queueIndex = flow.JoinQueue(this);
                            phase = Phase.ToQueue;
                        }
                        else
                        {
                            flow.Lost(this, Wanted);
                            LeaveAngry();
                        }
                    }
                    break;
                case Phase.ToQueue:
                case Phase.Queue:
                    goal = flow.QueuePosition(Mathf.Max(0, queueIndex));
                    if (Walk(dt)) { phase = Phase.Queue; SetAnim("Idle"); FaceRegister(); }
                    break;
                case Phase.Leaving:
                    if (Walk(dt))
                    {
                        phase = Phase.Outside;
                        timer = flow.RespawnDelay(rng);
                        Show(false);
                    }
                    break;
            }
            Billboard();
        }

        public void SetQueueIndex(int i)
        {
            if (queueIndex == i) return;
            queueIndex = i;
            if (phase == Phase.Queue) phase = Phase.ToQueue;
        }

        public void Paid()
        {
            queueIndex = -1;
            SetBubble(null, false);
            goal = layout.door;
            phase = Phase.Leaving;
        }

        public void LeaveAngry()
        {
            queueIndex = -1;
            SetBubble(Wanted, true);
            goal = layout.door;
            phase = Phase.Leaving;
        }

        void FaceRegister()
        {
            var to = layout.register - layout.queueDir.normalized * 2f - transform.localPosition;
            to.y = 0;
            if (to.sqrMagnitude > 0.001f) transform.localRotation = Quaternion.LookRotation(to);
        }

        /// <summary>Hedefe yürür; vardıysa true.</summary>
        bool Walk(float dt)
        {
            var pos = transform.localPosition;
            var to = goal - pos;
            to.y = 0;
            float step = Speed * dt;
            if (to.magnitude <= step)
            {
                transform.localPosition = new Vector3(goal.x, pos.y, goal.z);
                return true;
            }
            SetAnim("Walk");
            var dir = to.normalized;
            transform.localPosition = pos + dir * step;
            transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.LookRotation(dir), 8f * dt);
            return false;
        }

        // ------------------------------------------------------------ İstek balonu

        static Material backMat, angryMat;

        void BuildBubble()
        {
            bubble = new GameObject("IstekBalonu").transform;
            bubble.SetParent(transform, false);
            bubble.localPosition = new Vector3(0, 2.25f, 0);
            if (backMat == null)
            {
                backMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(1f, 1f, 0.97f) };
                angryMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.95f, 0.3f, 0.25f) };
            }
            var back = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(back.GetComponent<Collider>());
            back.transform.SetParent(bubble, false);
            back.transform.localScale = new Vector3(0.62f, 0.62f, 1f);
            bubbleBack = back.GetComponent<Renderer>();
            bubbleBack.sharedMaterial = backMat;
            var icon = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(icon.GetComponent<Collider>());
            icon.transform.SetParent(bubble, false);
            icon.transform.localPosition = new Vector3(0, 0, -0.01f);
            icon.transform.localScale = new Vector3(0.52f, 0.52f, 1f);
            bubbleIcon = icon.GetComponent<Renderer>();
            bubbleIcon.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            foreach (var r in new[] { bubbleBack, bubbleIcon }) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Balonun kuyruğu.
            var tail = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(tail.GetComponent<Collider>());
            tail.transform.SetParent(bubble, false);
            tail.transform.localPosition = new Vector3(0, -0.34f, 0.005f);
            tail.transform.localRotation = Quaternion.Euler(0, 0, 45f);
            tail.transform.localScale = new Vector3(0.14f, 0.14f, 1f);
            var tr = tail.GetComponent<Renderer>();
            tr.sharedMaterial = backMat;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bubble.gameObject.SetActive(false);
        }

        void SetBubble(Product p, bool angry)
        {
            if (bubble == null) return;
            if (p == null) { bubble.gameObject.SetActive(false); return; }
            bubble.gameObject.SetActive(true);
            bubbleBack.sharedMaterial = angry ? angryMat : backMat;
            foreach (Transform c in bubble) if (c.GetComponent<Renderer>() != bubbleIcon) c.GetComponent<Renderer>().sharedMaterial = angry ? angryMat : backMat;
            var tex = ProductIcons.Get(lib, p.model);
            bubbleIcon.material.mainTexture = tex;
            bubbleIcon.material.SetTexture("_BaseMap", tex);
            bubbleIcon.material.color = angry ? new Color(1f, 0.75f, 0.75f) : Color.white;
        }

        void Billboard()
        {
            if (bubble == null || !bubble.gameObject.activeSelf) return;
            var cam = Camera.main;
            if (cam == null) return;
            bubble.rotation = Quaternion.LookRotation(bubble.position - cam.transform.position, cam.transform.up);
            // Elinde ürün varken balon hafif zıplar (ödemeye gidiyor).
            float bob = gotIt ? Mathf.Abs(Mathf.Sin(Time.time * 4f)) * 0.06f : 0f;
            bubble.localPosition = new Vector3(0, 2.25f + bob, 0);
        }
    }
}
