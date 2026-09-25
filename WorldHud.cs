using UnityEngine;

namespace AiLab01
{
    /// <summary>
    /// Мировой HUD (TextMesh): HP игрока и подсказка управления.
    /// Метки создаются в рантайме, поэтому видны и на скриншотах, и при записи видео.
    /// </summary>
    public class WorldHud : MonoBehaviour
    {
        public Transform hpLabel;
        TextMesh hpText;
        Health hp;

        void Awake()
        {
            if (hpLabel == null)
            {
                hpText = WorldText.Create(new Vector3(-12.4f, 7.15f, 0f),
                    "HP ИГРОКА: 100 / 100", TextAnchor.MiddleLeft, goName: "HpLabel");
                hpLabel = hpText.transform;
            }
            WorldText.Create(new Vector3(0f, -7.15f, 0f),
                "WASD / СТРЕЛКИ — ДВИЖЕНИЕ   ·   ПРОБЕЛ / ЛКМ — АТАКА   ·   F1 — АВТО-ДЕМО",
                TextAnchor.MiddleCenter, goName: "HintLabel");
        }

        void Start()
        {
            hp = Object.FindFirstObjectByType<Health>();
            if (hpLabel != null) hpText = hpLabel.GetComponent<TextMesh>();
        }

        void Update()
        {
            if (hpText == null || hp == null) return;
            hpText.text = $"HP ИГРОКА: {Mathf.CeilToInt(hp.Hp)} / {Mathf.CeilToInt(hp.MaxHp)}";
        }
    }
}
