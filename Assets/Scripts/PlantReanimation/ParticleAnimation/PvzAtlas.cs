using System;
using System.Collections.Generic;
using UnityEngine;

namespace PvzFx
{
    /// <summary>
    /// ??c file JSON c?t atlas c?a PvZ (LoadingImages_Particles_960.json) và t?o Sprite[] cho t?ng ?nh.
    /// ?nh có "cols" (vd POTATOMINE_PARTICLES cols=6) s? ???c chia thành nhi?u frame b?ng nhau.
    /// </summary>
    public sealed class PvzAtlas
    {
        // ---- class ph? ?? JsonUtility ??c ???c file JSON ----
        [Serializable] class JsonRoot { public JsonRes[] resources; }
        [Serializable] class JsonRes { public JsonAttr attributes; public JsonRect atlasRect; }
        [Serializable] class JsonAttr { public string id; public string cols; public string rows; }
        [Serializable] class JsonRect { public int x, y, width, height; }

        public readonly float PixelsPerUnit;
        readonly Dictionary<string, Sprite[]> _sprites = new Dictionary<string, Sprite[]>();

        public PvzAtlas(Texture2D texture, TextAsset json, float pixelsPerUnit = 100f)
        {
            PixelsPerUnit = pixelsPerUnit;

            string text = json.text.TrimStart('\uFEFF'); // b? BOM n?u có
            JsonRoot root = JsonUtility.FromJson<JsonRoot>(text);

            foreach (JsonRes res in root.resources)
            {
                if (res.atlasRect == null || res.atlasRect.width <= 0) continue; // dòng ??u là atlas g?c, b? qua

                // "IMAGE_POTATOMINE_PARTICLES|960" -> "IMAGE_POTATOMINE_PARTICLES"
                string id = res.attributes.id.Split('|')[0];

                int cols = 1;
                if (!string.IsNullOrEmpty(res.attributes.cols)) int.TryParse(res.attributes.cols, out cols);
                cols = Mathf.Max(1, cols);

                int cellW = res.atlasRect.width / cols;
                int cellH = res.atlasRect.height;

                var frames = new Sprite[cols];
                for (int i = 0; i < cols; i++)
                {
                    // JSON: g?c (0,0) ? góc TRÊN-trái. Unity: g?c ? góc D??I-trái -> ph?i l?t tr?c Y.
                    float x = res.atlasRect.x + i * cellW;
                    float y = texture.height - (res.atlasRect.y + cellH);
                    frames[i] = Sprite.Create(texture, new Rect(x, y, cellW, cellH),
                        new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
                    frames[i].name = id + "_" + i;
                }
                _sprites[id] = frames;
            }
        }

        public Sprite[] Get(string id)
        {
            if (_sprites.TryGetValue(id, out var s)) return s;
            Debug.LogError("[PvzAtlas] Không tìm th?y ?nh: " + id);
            return null;
        }
    }
}