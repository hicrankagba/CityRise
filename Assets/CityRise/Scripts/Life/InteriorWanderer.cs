using System.Collections.Generic;
using UnityEngine;

namespace CityRise.Life
{
    /// <summary>
    /// Dükkan içinde müşteri: boş noktalar arasında (raflara çarpmadan) dolaşır, arada durup raftan ürün alır,
    /// bazen kasaya gider. Çalışanlar (idleOnly) yerinde durur, arada telefonla konuşur.
    /// Mixamo karakterlerinde Animator durumları (Idle, Walk, PickUp, Phone, Sit) kullanılır;
    /// basit karakterlerde hafif zıplama ve sallanma ile taklit edilir.
    /// </summary>
    public class InteriorWanderer : MonoBehaviour
    {
        public List<Vector3> spots = new List<Vector3>();
        public Vector3 register;
        public bool idleOnly;
        /// <summary>Oturan müşteri (kafede): yalnızca oturma animasyonu.</summary>
        public bool seated;
        public float speed = 1.0f;

        Transform body;
        Animator animator;
        string current;
        Vector3 goal;
        float pause, phase, stateTimer;
        System.Random rng;

        void Start()
        {
            body = transform.childCount > 0 ? transform.GetChild(0) : transform;
            animator = GetComponentInChildren<Animator>();
            rng = new System.Random(Random.Range(0, int.MaxValue));
            phase = (float)rng.NextDouble() * 10f;
            goal = transform.localPosition;
            pause = (float)rng.NextDouble() * 2f;
            if (seated) SetState("Sit");
            else SetState("Idle");
            // Herkes aynı karede başlamasın.
            if (animator != null) animator.Update((float)rng.NextDouble() * 3f);
        }

        /// <summary>
        /// Yeni hedef: bazen kasa, çoğunlukla boş bir nokta. Başka bir müşterinin bulunduğu ya da gittiği
        /// yere (0,9 m içi) gidilmez; böylece iki kişi üst üste binmez.
        /// </summary>
        Vector3 PickGoal()
        {
            var others = transform.parent != null ? transform.parent.GetComponentsInChildren<InteriorWanderer>() : new InteriorWanderer[0];
            bool Free(Vector3 p)
            {
                foreach (var o in others)
                {
                    if (o == this) continue;
                    if (Flat(o.transform.localPosition - p) < 0.9f || Flat(o.goal - p) < 0.9f) return false;
                }
                return true;
            }
            if (rng.NextDouble() < 0.2 && Free(register)) return register;
            for (int i = 0; i < 8; i++)
            {
                var p = spots[rng.Next(spots.Count)];
                if (Free(p)) return p;
            }
            return transform.localPosition;
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        void SetState(string state)
        {
            if (animator == null || current == state) return;
            current = state;
            animator.CrossFadeInFixedTime(state, 0.25f);
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            if (seated) return;
            if (idleOnly || spots.Count == 0)
            {
                if (animator != null)
                {
                    stateTimer -= dt;
                    if (stateTimer <= 0)
                    {
                        SetState(rng.NextDouble() < 0.25 ? "Phone" : "Idle");
                        stateTimer = 6f + (float)rng.NextDouble() * 8f;
                    }
                    return;
                }
                phase += dt * 2f;
                body.localRotation = Quaternion.Euler(0, Mathf.Sin(phase) * 12f, 0);
                return;
            }
            if (pause > 0)
            {
                pause -= dt;
                if (animator == null) body.localPosition = Vector3.zero;
                if (pause <= 0) goal = PickGoal();
                return;
            }
            var pos = transform.localPosition;
            var to = goal - pos;
            to.y = 0;
            float step = speed * dt;
            if (to.magnitude <= step)
            {
                transform.localPosition = new Vector3(goal.x, pos.y, goal.z);
                // Durunca: raftan ürün alır ya da biraz bekler.
                bool pick = rng.NextDouble() < 0.5;
                SetState(pick ? "PickUp" : "Idle");
                pause = pick ? 2.6f : 1.5f + (float)rng.NextDouble() * 3f;
                return;
            }
            SetState("Walk");
            var dir = to.normalized;
            transform.localPosition = pos + dir * step;
            transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.LookRotation(dir), 8f * dt);
            if (animator != null) return;
            phase += dt * 9f;
            body.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(phase)) * 0.05f, 0);
            body.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(phase) * 6f);
        }
    }
}
