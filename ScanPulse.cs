using UnityEngine;

namespace AiLab01
{
    /// <summary>
    /// Визуальный импульс 360°-сканирования Призрака:
    /// расширяющееся и затухающее кольцо радиусом scanRadius.
    /// </summary>
    public class ScanPulse : MonoBehaviour
    {
        SpriteRenderer sr;
        float radius = 1f;
        float interval = 0.5f;
        float t = 999f;
        static readonly Color Base = new Color(0.75f, 0.5f, 1f);

        public void Init(float scanRadius, float scanInterval)
        {
            radius = scanRadius;
            interval = scanInterval;
            sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Ring;
            sr.sharedMaterial = SpriteFactory.SpriteMaterial;
            sr.color = Base;
            sr.sortingOrder = 5;
            sr.enabled = false;
        }

        public void Play() => t = 0f;

        void Update()
        {
            if (t > interval)
            {
                if (sr != null) sr.enabled = false;
                return;
            }
            t += Time.deltaTime;
            if (sr == null) return;
            float k = Mathf.Clamp01(t / interval);
            sr.enabled = true;
            transform.localScale = Vector3.one * Mathf.Lerp(0.12f, radius * 2f, k);
            sr.color = new Color(Base.r, Base.g, Base.b, Mathf.Lerp(0.85f, 0f, k * k));
        }
    }
}
