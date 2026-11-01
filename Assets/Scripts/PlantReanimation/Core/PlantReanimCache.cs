
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlantReanimation.Core
{
    public static class PlantReanimCache
    {
        private static readonly Dictionary<string, PlantReanimData>
            cache = new Dictionary<string, PlantReanimData>();

        // =========================================
        // GET DATA
        // =========================================

        public static PlantReanimData Get(
            TextAsset atlasJson,
            TextAsset reanimJson)
        {
            if (atlasJson == null)
                throw new ArgumentNullException(nameof(atlasJson));

            if (reanimJson == null)
                throw new ArgumentNullException(nameof(reanimJson));

            string key = GetCacheKey(atlasJson, reanimJson);

            // Đã có dữ liệu thì sử dụng lại.
            if (cache.TryGetValue(key, out PlantReanimData cachedData))
            {
                return cachedData;
            }

            // Chưa có thì parse JSON.
            PlantReanimData data = new PlantReanimData();

            PlantReanimLoader.LoadAtlas(atlasJson, data);
            PlantReanimLoader.LoadReanim(reanimJson, data);

            // Lưu vào Cache.
            cache.Add(key, data);

            Debug.Log(
                $"[PlantReanimCache] Cached data: {atlasJson.name} + {reanimJson.name}");

            return data;
        }

        // =========================================
        // CACHE KEY
        // =========================================

        private static string GetCacheKey(
            TextAsset atlasJson,
            TextAsset reanimJson)
        {
            return $"{atlasJson.GetInstanceID()}_{reanimJson.GetInstanceID()}";
        }

        // =========================================
        // CLEAR CACHE
        // =========================================

        public static void Clear()
        {
            cache.Clear();

            Debug.Log("[PlantReanimCache] Cache cleared.");
        }

        public static int Count
        {
            get { return cache.Count; }
        }
    }
}
