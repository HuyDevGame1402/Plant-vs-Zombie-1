
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chứa dữ liệu đã được parse từ một bộ Atlas JSON + Reanim JSON.
/// Mỗi bộ Reanim sẽ có một instance ReanimData riêng.
/// </summary>
public class ReanimData
{
    // =========================================================
    // 1. DỮ LIỆU ATLAS
    // =========================================================

    public sealed class AtlasPart
    {
        public float width;
        public float height;

        public Vector2 uvMin;
        public Vector2 uvMax;
    }

    // =========================================================
    // 2. DỮ LIỆU REANIM FRAME
    // =========================================================

    public sealed class ReanimFrame
    {
        public float x;
        public float y;

        public float kx;
        public float ky;

        public float sx = 1f;
        public float sy = 1f;

        public bool visible = true;

        public string imageId;
    }

    // =========================================================
    // 3. DỮ LIỆU REANIM TRACK
    // =========================================================

    public sealed class ReanimTrack
    {
        public string name;

        public bool hasImage;

        public ReanimFrame[] frames;
    }

    // =========================================================
    // 4. DỮ LIỆU ANIMATION CLIP
    // =========================================================

    public sealed class ReanimClip
    {
        public string name;

        public int start;
        public int end;

        public int Length
        {
            get { return end - start; }
        }
    }

    // =========================================================
    // 5. DỮ LIỆU CỦA MỘT BỘ REANIM
    // =========================================================

    public readonly Dictionary<string, AtlasPart> AtlasParts =
        new Dictionary<string, AtlasPart>(StringComparer.OrdinalIgnoreCase);

    public readonly List<ReanimTrack> Tracks =
        new List<ReanimTrack>();

    /// <summary>Root/ground motion track, kept separate from drawable parts.</summary>
    public ReanimTrack GroundTrack { get; internal set; }

    public readonly Dictionary<string, ReanimClip> Clips =
        new Dictionary<string, ReanimClip>(StringComparer.OrdinalIgnoreCase);

    public int Fps { get; internal set; }

    public int IdleFrameCount { get; internal set; }

    public int ImageIndexOffset { get; internal set; }

    public bool IsValid { get; internal set; }
}
