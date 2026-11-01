
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý nhiều ReanimData độc lập.
/// Mỗi cặp Atlas JSON + Reanim JSON có một dữ liệu riêng.
/// </summary>
public static class ReanimDataCache
{
    /// <summary>
    /// Khóa cache được tạo từ Instance ID của hai TextAsset.
    /// </summary>
    private struct CacheKey : IEquatable<CacheKey>
    {
        private readonly int atlasId;
        private readonly int reanimId;

        public CacheKey(TextAsset atlas, TextAsset reanim)
        {
            atlasId = atlas.GetInstanceID();
            reanimId = reanim.GetInstanceID();
        }

        public bool Equals(CacheKey other)
        {
            return atlasId == other.atlasId
                && reanimId == other.reanimId;
        }

        public override bool Equals(object obj)
        {
            return obj is CacheKey && Equals((CacheKey)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (atlasId * 397) ^ reanimId;
            }
        }
    }

    private static readonly Dictionary<CacheKey, ReanimData> cache =
        new Dictionary<CacheKey, ReanimData>();

    /// <summary>
    /// Số bộ ReanimData hiện đang được lưu trong cache.
    /// </summary>
    public static int CachedCount
    {
        get { return cache.Count; }
    }

    /// <summary>
    /// Lấy dữ liệu từ cache.
    /// Nếu chưa có thì parse JSON và lưu kết quả mới.
    /// </summary>
    public static bool GetOrLoad(
        TextAsset atlasJsonAsset,
        TextAsset reanimJsonAsset,
        out ReanimData data)
    {
        data = null;

        if (atlasJsonAsset == null || reanimJsonAsset == null)
        {
            Debug.LogError(
                "ReanimDataCache: Atlas JSON hoặc Reanim JSON đang bị null.");

            return false;
        }

        CacheKey key = new CacheKey(
            atlasJsonAsset,
            reanimJsonAsset);

        // Đã có dữ liệu thì sử dụng lại.
        if (cache.TryGetValue(key, out data))
        {
            if (data != null && data.IsValid)
                return true;

            // Nếu dữ liệu không hợp lệ thì bỏ khỏi cache.
            cache.Remove(key);
            data = null;
        }

        // Chưa có thì parse dữ liệu mới.
        ReanimData parsedData;

        if (!ReanimDataParser.TryParse(
            atlasJsonAsset,
            reanimJsonAsset,
            out parsedData))
        {
            return false;
        }

        if (parsedData == null || !parsedData.IsValid)
        {
            Debug.LogError(
                "ReanimDataCache: Parser trả về dữ liệu không hợp lệ.");

            return false;
        }

        cache.Add(key, parsedData);

        data = parsedData;
        return true;
    }

    /// <summary>
    /// Xóa toàn bộ dữ liệu đang lưu trong cache.
    /// </summary>
    public static void Clear()
    {
        cache.Clear();
    }

    /// <summary>
    /// Reset cache khi Unity bắt đầu Play session mới.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        Clear();
    }
}
