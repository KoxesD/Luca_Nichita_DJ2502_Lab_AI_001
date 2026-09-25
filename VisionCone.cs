using UnityEngine;

namespace AiLab01
{
    /// <summary>
    /// Визуализация поля зрения Стража: веер лучей с обрезкой по стенам (Raycast),
    /// собираемый в полупрозрачную сетку-конус.
    /// Особенности:
    ///  • вершины переводятся в локальные координаты через InverseTransformPoint,
    ///    поэтому поворот корпуса врага не «сдвигает» конус относительно стен;
    ///  • конус рендерится ПОД спрайтами стен (order −9 при стенах −8) — там, где
    ///    между лучами остаётся щель, стену закрывает её собственный спрайт,
    ///    и обрезка выглядит ровной;
    ///  • конус намеренно слабозаметен (низкая прозрачность).
    /// </summary>
    [RequireComponent(typeof(GuardEnemy))]
    public class VisionCone : MonoBehaviour
    {
        public int rayCount = 60;

        static readonly Color Calm = new Color(0.35f, 1f, 0.55f, 0.05f);
        static readonly Color Search = new Color(1f, 0.85f, 0.3f, 0.07f);
        static readonly Color Combat = new Color(1f, 0.3f, 0.2f, 0.10f);

        GuardEnemy enemy;
        Mesh mesh;
        Material mat;
        Transform coneRoot;

        void Start()
        {
            enemy = GetComponent<GuardEnemy>();
            mesh = new Mesh();
            mesh.MarkDynamic();

            var go = new GameObject("VisionCone");
            coneRoot = go.transform;
            coneRoot.SetParent(transform, false);   // наследует поворот корпуса —
            // вершины ниже компенсируют это через InverseTransformPoint
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mat = new Material(Shader.Find("Sprites/Default")) { color = Calm };
            mr.sharedMaterial = mat;
            mr.sortingOrder = -9;   // под стенами (−8): обрезка перекрывается спрайтами стен
        }

        void LateUpdate()
        {
            if (enemy == null || coneRoot == null) return;

            Vector2 o = transform.position;
            float half = enemy.viewAngleDeg * 0.5f;
            float mid = Mathf.Atan2(enemy.Facing.y, enemy.Facing.x) * Mathf.Rad2Deg;
            float a0 = mid - half;
            float step = enemy.viewAngleDeg / rayCount;

            var verts = new Vector3[rayCount + 2];
            var tris = new int[rayCount * 3];
            verts[0] = coneRoot.InverseTransformPoint(o);

            for (int i = 0; i <= rayCount; i++)
            {
                float a = (a0 + step * i) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var hit = Physics2D.Raycast(o, dir, enemy.viewDistance, enemy.obstacleMask);
                Vector2 p = hit.collider != null
                    ? o + dir * hit.distance
                    : o + dir * enemy.viewDistance;
                verts[i + 1] = coneRoot.InverseTransformPoint(p);
                if (i < rayCount)
                {
                    int k = i * 3;
                    tris[k] = 0;
                    tris[k + 1] = i + 2;
                    tris[k + 2] = i + 1;
                }
            }

            mesh.Clear();
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateBounds();

            string s = enemy.CurrentStateName;
            var target = (s == "CHASE" || s == "ATTACK") ? Combat : (s == "SEARCH" ? Search : Calm);
            mat.color = Color.Lerp(mat.color, target, 12f * Time.deltaTime);
        }
    }
}
