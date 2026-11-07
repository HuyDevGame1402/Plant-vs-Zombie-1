using System;
using System.Collections.Generic;
using UnityEngine;

public class ReanimPartRenderer
{
    private const float PositionScale = 2f;

    private sealed class Part
    {
        public ReanimData.ReanimTrack track;
        public GameObject gameObject;
        public MeshFilter meshFilter;
        public MeshRenderer meshRenderer;
        public Mesh mesh;

        public readonly Vector3[] vertices = new Vector3[4];
        public readonly Vector2[] uvs = new Vector2[4];
        public readonly Vector2[] corners = new Vector2[4];
        public readonly Vector2[] blendFrom = new Vector2[4];

        public string currentImageId;
        public bool lastVisible;
        public bool blendValid;
        public int sortingIndex;

        // ---- Override do gameplay điều khiển ----
        public bool hidden;               // ẩn hẳn bộ phận (tay rụng, đầu bật ra)
        public string imageOverride;      // đổi ảnh (bắp tay -> tay cụt)
        public bool attached;             // dính cứng theo thân (đầu lúc chết còn đầu)

        // ---- Overlay (lớp phủ gắn vào một track khác, ví dụ lá cờ gắn vào bàn tay) ----
        public ReanimAnimationPlayer overlayPlayer;   // player riêng của overlay
        public Part overlayAttachPart;                // track của zombie mà overlay bám theo
        public int overlayAttachRefFrame;             // frame mà overlay được thiết kế (toạ độ gốc)
    }

    private struct Pose
    {
        public float x, y, kx, ky, sx, sy;
        public bool visible;
        public string imageId;
    }

    private struct Affine
    {
        public float m00, m01, m10, m11;
        public float tx, ty;

        public static Affine Identity
        {
            get { return new Affine { m00 = 1f, m11 = 1f }; }
        }
    }

    private readonly List<Part> parts = new List<Part>();
    private readonly List<Part> footParts = new List<Part>();
    private readonly List<string> partNames = new List<string>();

    private readonly Dictionary<string, Part> partsByName =
        new Dictionary<string, Part>(StringComparer.OrdinalIgnoreCase);

    private ReanimAnimationPlayer animationPlayer;
    private ReanimData data;

    private Transform parent;
    private Material sharedMaterial;

    private Part shadowPart;
    private Part shadowPart2;

    private Vector2 anchorPixel;
    private float pixelPerUnit;
    private Vector2 globalScale;
    private Vector2 pivotOffset;
    private Vector2 shadowOffset;

    private bool anchorToFeet;
    private bool initialized;

    private float currentAlpha = 1f;
    private string shadowImageId;

    // Dính cứng một nhóm bộ phận vào thân (đầu lúc chết còn đầu).
    private bool attachActive;
    private Part attachBodyPart;
    private int attachRefFrame;

    // Tư thế giơ cờ: tay thẳng giơ lên + cụm bàn tay cờ/cán/lá cờ đi theo cổ tay.
    private bool raiseActive;
    private Part raiseUpper, raiseLower, raiseWrist, raiseFlagHand;
    private float raiseArmDegrees, raiseWaveDegrees;

    // null = không giới hạn Image ID.
    private HashSet<string> allowedImageIds;

    public int PartCount => parts.Count;
    public bool IsInitialized => initialized;

    /// <summary>Danh sách tên track đang được vẽ, theo thứ tự trong file Reanim.</summary>
    public IReadOnlyList<string> PartNames => partNames;

    // =========================================================
    // INITIALIZE
    // =========================================================

    public bool Initialize(
        ReanimAnimationPlayer player,
        Transform parentTransform,
        Material material,
        float pixelPerUnit,
        Vector2 globalScale,
        bool anchorToFeet,
        Vector2 pivotOffset,
        string shadowImageId = null,
        Vector2 shadowOffset = default(Vector2),
        HashSet<string> allowedImageIds = null)
    {
        if (initialized)
        {
            Debug.LogWarning("ReanimPartRenderer: Đã Initialize.");
            return false;
        }

        if (player == null || player.Data == null)
        {
            Debug.LogError("ReanimPartRenderer: AnimationPlayer hoặc Data null.");
            return false;
        }

        if (parentTransform == null || material == null)
        {
            Debug.LogError("ReanimPartRenderer: Parent hoặc Material null.");
            return false;
        }

        if (pixelPerUnit <= 0f)
        {
            Debug.LogError("ReanimPartRenderer: pixelPerUnit phải > 0.");
            return false;
        }

        animationPlayer = player;
        data = player.Data;
        parent = parentTransform;
        sharedMaterial = material;

        this.pixelPerUnit = pixelPerUnit;
        this.globalScale = globalScale;
        this.anchorToFeet = anchorToFeet;
        this.pivotOffset = pivotOffset;
        this.shadowOffset = shadowOffset;
        this.shadowImageId = shadowImageId;
        this.allowedImageIds = allowedImageIds;

        BuildParts();

        if (parts.Count == 0)
        {
            Debug.LogError("ReanimPartRenderer: Không có track hợp lệ.");
            Dispose();
            return false;
        }

        int referenceFrame = GetReferenceFrame();

        CalculateAnchor(referenceFrame);
        BuildShadow(referenceFrame);

        animationPlayer.OnBlendStarted += CaptureBlendPose;

        initialized = true;

        Render();
        SetAlpha(currentAlpha);

        return true;
    }

    // =========================================================
    // IMAGE FILTER
    // =========================================================

    private bool IsAllowedImage(string imageId)
    {
        if (string.IsNullOrEmpty(imageId))
            return false;

        if (allowedImageIds != null &&
            !allowedImageIds.Contains(imageId))
        {
            return false;
        }

        return data != null &&
               data.AtlasParts.ContainsKey(imageId);
    }

    // =========================================================
    // BUILD PARTS
    // =========================================================

    private void BuildParts()
    {
        int sortingIndex = 0;

        foreach (ReanimData.ReanimTrack track in data.Tracks)
        {
            if (track == null ||
                track.frames == null ||
                string.IsNullOrEmpty(track.name))
            {
                continue;
            }

            bool hasValidSprite = false;

            foreach (ReanimData.ReanimFrame frame in track.frames)
            {
                if (frame == null)
                    continue;

                if (frame.visible && IsAllowedImage(frame.imageId))
                {
                    hasValidSprite = true;
                    break;
                }
            }

            if (!hasValidSprite)
                continue;

            Part part = CreatePart(track);

            part.sortingIndex = sortingIndex++;

            parts.Add(part);
            partNames.Add(track.name);
            partsByName[track.name] = part;

            if (track.name.IndexOf(
                "foot",
                StringComparison.OrdinalIgnoreCase) >= 0)
            {
                footParts.Add(part);
            }
        }
    }

    private Part CreatePart(ReanimData.ReanimTrack track)
    {
        GameObject go = new GameObject(track.name);

        go.transform.SetParent(parent, false);

        MeshFilter filter = go.AddComponent<MeshFilter>();
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();

        renderer.sharedMaterial = sharedMaterial;
        renderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        Mesh mesh = new Mesh();
        mesh.name = track.name;
        mesh.MarkDynamic();

        mesh.vertices = new Vector3[4];
        mesh.uv = new Vector2[4];

        mesh.colors32 = CreateWhiteColors();

        mesh.triangles = new[]
        {
            0, 1, 2,
            0, 2, 3
        };

        filter.sharedMesh = mesh;

        return new Part
        {
            track = track,
            gameObject = go,
            meshFilter = filter,
            meshRenderer = renderer,
            mesh = mesh
        };
    }

    private static Color32[] CreateWhiteColors()
    {
        return new[]
        {
            new Color32(255,255,255,255),
            new Color32(255,255,255,255),
            new Color32(255,255,255,255),
            new Color32(255,255,255,255)
        };
    }

    // =========================================================
    // REFERENCE FRAME / ANCHOR
    // =========================================================

    private int GetReferenceFrame()
    {
        ReanimData.ReanimClip clip;

        if (data.Clips.TryGetValue("idle", out clip))
            return clip.start;

        foreach (ReanimData.ReanimClip item in data.Clips.Values)
            return item.start;

        return 0;
    }

    private void CalculateAnchor(int referenceFrame)
    {
        anchorPixel = Vector2.zero;

        if (!anchorToFeet || footParts.Count == 0)
            return;

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;

        foreach (Part foot in footParts)
        {
            if (referenceFrame < 0 ||
                referenceFrame >= foot.track.frames.Length)
                continue;

            ReanimData.ReanimFrame frame =
                foot.track.frames[referenceFrame];

            ReanimData.AtlasPart atlasPart;

            if (frame == null ||
                !IsAllowedImage(frame.imageId) ||
                !data.AtlasParts.TryGetValue(frame.imageId, out atlasPart))
                continue;

            Vector2[] corners = new Vector2[4];

            ComputeCorners(
                ToAffine(FromFrame(frame)),
                atlasPart,
                corners);

            AccumulateBounds(
                corners,
                ref minX,
                ref maxX,
                ref maxY);
        }

        if (minX <= maxX)
        {
            anchorPixel = new Vector2(
                (minX + maxX) * 0.5f,
                maxY);
        }
    }

    private static void AccumulateBounds(
        Vector2[] corners,
        ref float minX,
        ref float maxX,
        ref float maxY)
    {
        for (int i = 0; i < 4; i++)
        {
            minX = Mathf.Min(minX, corners[i].x);
            maxX = Mathf.Max(maxX, corners[i].x);
            maxY = Mathf.Max(maxY, corners[i].y);
        }
    }

    // =========================================================
    // SHADOW
    // =========================================================

    private void BuildShadow(int referenceFrame)
    {
        if (string.IsNullOrEmpty(shadowImageId))
            return;

        ReanimData.AtlasPart shadowAtlas;

        if (!data.AtlasParts.TryGetValue(
            shadowImageId,
            out shadowAtlas))
        {
            return;
        }

        shadowPart = CreatePart(CreateShadowTrack("Shadow_1"));
        shadowPart2 = CreatePart(CreateShadowTrack("Shadow_2"));

        SetPartUvs(shadowPart, shadowAtlas, shadowImageId);
        SetPartUvs(shadowPart2, shadowAtlas, shadowImageId);

        UpdateShadow(referenceFrame, shadowAtlas);
    }

    private static ReanimData.ReanimTrack CreateShadowTrack(string name)
    {
        return new ReanimData.ReanimTrack
        {
            name = name,
            frames = new ReanimData.ReanimFrame[0]
        };
    }

    private void UpdateShadow(
        int referenceFrame,
        ReanimData.AtlasPart shadowAtlas)
    {
        if (shadowPart == null ||
            shadowPart2 == null ||
            footParts.Count == 0)
            return;

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;

        foreach (Part foot in footParts)
        {
            if (referenceFrame < 0 ||
                referenceFrame >= foot.track.frames.Length)
                continue;

            ReanimData.ReanimFrame frame =
                foot.track.frames[referenceFrame];

            ReanimData.AtlasPart footAtlas;

            if (frame == null ||
                !IsAllowedImage(frame.imageId) ||
                !data.AtlasParts.TryGetValue(
                    frame.imageId,
                    out footAtlas))
            {
                continue;
            }

            Vector2[] corners = new Vector2[4];

            ComputeCorners(
                ToAffine(FromFrame(frame)),
                footAtlas,
                corners);

            AccumulateBounds(
                corners,
                ref minX,
                ref maxX,
                ref maxY);
        }

        if (minX > maxX)
            return;

        float halfW = shadowAtlas.width * globalScale.x * 0.5f;
        float halfH = shadowAtlas.height * globalScale.y * 0.5f;

        float centerX =
            (minX + maxX) * 0.5f +
            shadowOffset.x * PositionScale;

        float centerY =
            maxY + shadowOffset.y * PositionScale;

        Vector3 a = ToUnity(centerX - halfW, centerY - halfH);
        Vector3 b = ToUnity(centerX + halfW, centerY - halfH);
        Vector3 c = ToUnity(centerX + halfW, centerY + halfH);
        Vector3 d = ToUnity(centerX - halfW, centerY + halfH);

        SetQuadVertices(shadowPart, a, b, c, d);
        SetQuadVertices(shadowPart2, a, b, c, d);

        shadowPart.meshRenderer.enabled = true;
        shadowPart2.meshRenderer.enabled = true;
    }

    private void SetQuadVertices(
        Part part,
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 d)
    {
        part.vertices[0] = a;
        part.vertices[1] = b;
        part.vertices[2] = c;
        part.vertices[3] = d;

        part.mesh.vertices = part.vertices;
        part.mesh.RecalculateBounds();
    }

    // =========================================================
    // OVERRIDE: ẨN / ĐỔI ẢNH / DÍNH THEO THÂN
    // =========================================================

    /// <summary>Ẩn hoặc hiện lại một bộ phận (ví dụ cẳng tay + bàn tay khi rụng tay).</summary>
    public bool SetPartHidden(string trackName, bool hidden)
    {
        Part part;

        if (string.IsNullOrEmpty(trackName) ||
            !partsByName.TryGetValue(trackName, out part))
            return false;

        part.hidden = hidden;
        return true;
    }

    /// <summary>
    /// Đổi ảnh của một bộ phận (ví dụ bắp tay -> tay cụt).
    /// Truyền null để trả về ảnh gốc của animation.
    /// </summary>
    public bool SetPartImageOverride(string trackName, string imageId)
    {
        Part part;

        if (string.IsNullOrEmpty(trackName) ||
            !partsByName.TryGetValue(trackName, out part))
            return false;

        if (!string.IsNullOrEmpty(imageId) && !IsAllowedImage(imageId))
            return false;

        part.imageOverride = string.IsNullOrEmpty(imageId) ? null : imageId;
        return true;
    }

    /// <summary>
    /// Cho một nhóm bộ phận dính cứng vào thân: chúng giữ nguyên vị trí so với thân
    /// ở frame tham chiếu rồi đi theo chuyển động của thân.
    /// Dùng cho đầu khi zombie chết còn đầu (clip chết ẩn track đầu).
    /// </summary>
    public bool SetAttachment(
        IEnumerable<string> trackNames,
        string bodyTrackName,
        int referenceFrame)
    {
        Part body;

        if (trackNames == null ||
            !partsByName.TryGetValue(bodyTrackName, out body))
            return false;

        ClearAttachment();

        attachBodyPart = body;
        attachRefFrame = referenceFrame;

        foreach (string name in trackNames)
        {
            Part part;

            if (partsByName.TryGetValue(name, out part))
                part.attached = true;
        }

        attachActive = true;
        return true;
    }

    /// <summary>
    /// Bật tư thế giơ cờ. Bắp tay + cẳng tay + cổ tay xoay quanh vai,
    /// bàn tay cầm cờ và overlay bám theo nó dời tới cổ tay rồi vẫy.
    /// Góc được set qua SetRaisedFlagAngles.
    /// </summary>
    public bool SetRaisedFlagPose(
        string upperTrack,
        string lowerTrack,
        string wristTrack,
        string flagHandTrack)
    {
        Part u, l, w, f;

        if (!partsByName.TryGetValue(upperTrack, out u) ||
            !partsByName.TryGetValue(lowerTrack, out l) ||
            !partsByName.TryGetValue(wristTrack, out w) ||
            !partsByName.TryGetValue(flagHandTrack, out f))
            return false;

        raiseUpper = u;
        raiseLower = l;
        raiseWrist = w;
        raiseFlagHand = f;
        raiseArmDegrees = 0f;
        raiseWaveDegrees = 0f;
        raiseActive = true;
        return true;
    }

    /// <param name="armDegrees">Góc xoay cả cánh tay quanh vai (180 = giơ thẳng lên).</param>
    /// <param name="waveDegrees">Góc xoay thêm của cụm cờ quanh cổ tay (vẫy).</param>
    public void SetRaisedFlagAngles(float armDegrees, float waveDegrees)
    {
        raiseArmDegrees = armDegrees;
        raiseWaveDegrees = waveDegrees;
    }

    public void ClearRaisedFlagPose()
    {
        raiseActive = false;
        raiseUpper = raiseLower = raiseWrist = raiseFlagHand = null;
    }

    public void ClearAttachment()
    {
        attachActive = false;
        attachBodyPart = null;

        foreach (Part part in parts)
            part.attached = false;
    }

    /// <summary>Xoá toàn bộ override (ẩn, đổi ảnh, dính thân). Dùng khi hồi sinh zombie.</summary>
    public void ClearOverrides()
    {
        ClearAttachment();
        ClearRaisedFlagPose();

        foreach (Part part in parts)
        {
            part.hidden = false;
            part.imageOverride = null;
        }
    }

    // =========================================================
    // OVERLAY (lớp phủ bám theo một bộ phận, ví dụ lá cờ bám bàn tay)
    // =========================================================

    /// <summary>
    /// Thêm một bộ Reanim thứ 2 (có animation riêng) bám theo một track của zombie.
    /// Toạ độ trong file overlay được thiết kế ở tư thế của attachReferenceFrame,
    /// nên mỗi frame overlay được kéo theo chuyển động của track đó so với tư thế gốc.
    /// Tên các bộ phận overlay được chèn vào PartNames ngay sau track mà nó bám,
    /// nên thứ tự layer giống game gốc.
    /// </summary>
    public bool AddOverlay(
        ReanimData overlayData,
        ReanimAnimationPlayer overlayPlayer,
        string attachTrackName,
        int attachReferenceFrame)
    {
        if (!initialized || overlayData == null || overlayPlayer == null)
            return false;

        Part attachPart;

        if (string.IsNullOrEmpty(attachTrackName) ||
            !partsByName.TryGetValue(attachTrackName, out attachPart))
        {
            Debug.LogWarning(
                "ReanimPartRenderer: Không tìm thấy track '" +
                attachTrackName + "' để gắn overlay.");

            return false;
        }

        int insertAt = partNames.IndexOf(attachPart.track.name) + 1;
        int added = 0;

        foreach (ReanimData.ReanimTrack track in overlayData.Tracks)
        {
            if (track == null ||
                track.frames == null ||
                string.IsNullOrEmpty(track.name) ||
                partsByName.ContainsKey(track.name))
                continue;

            bool hasValidSprite = false;

            foreach (ReanimData.ReanimFrame frame in track.frames)
            {
                if (frame != null &&
                    frame.visible &&
                    IsAllowedImage(frame.imageId))
                {
                    hasValidSprite = true;
                    break;
                }
            }

            if (!hasValidSprite)
                continue;

            Part part = CreatePart(track);

            part.sortingIndex = parts.Count;
            part.overlayPlayer = overlayPlayer;
            part.overlayAttachPart = attachPart;
            part.overlayAttachRefFrame = attachReferenceFrame;

            parts.Add(part);
            partNames.Insert(insertAt + added, track.name);
            partsByName[track.name] = part;
            added++;
        }

        SetAlpha(currentAlpha);
        return added > 0;
    }

    // =========================================================
    // RENDER
    // =========================================================

    public void Render()
    {
        if (!initialized ||
            animationPlayer == null ||
            animationPlayer.CurrentClip == null)
            return;

        Render(
            animationPlayer.FrameA,
            animationPlayer.FrameB,
            animationPlayer.FrameLerp,
            animationPlayer.BlendWeight,
            animationPlayer.IsBlending);
    }

    public void Render(
        int frameA,
        int frameB,
        float frameLerp,
        float blendWeight = 1f,
        bool blending = false)
    {
        if (data == null)
            return;

        // Ma trận kéo các bộ phận dính thân theo chuyển động của thân.
        bool attachReady = false;
        Affine attachMatrix = Affine.Identity;

        if (attachActive &&
            attachBodyPart != null &&
            attachRefFrame >= 0 &&
            attachRefFrame < attachBodyPart.track.frames.Length &&
            frameA >= 0 && frameB >= 0 &&
            frameA < attachBodyPart.track.frames.Length &&
            frameB < attachBodyPart.track.frames.Length)
        {
            Affine bodyNow = ToAffine(Sample(
                attachBodyPart.track, frameA, frameB, frameLerp));

            Affine bodyRef = ToAffine(FromFrame(
                attachBodyPart.track.frames[attachRefFrame]));

            attachMatrix = Multiply(bodyNow, Invert(bodyRef));
            attachReady = true;
        }

        // Tư thế giơ cờ.
        bool raiseReady = false;
        Affine armExtra = Affine.Identity;
        Affine flagExtra = Affine.Identity;

        if (raiseActive &&
            frameA >= 0 && frameB >= 0 &&
            frameA < raiseUpper.track.frames.Length &&
            frameB < raiseUpper.track.frames.Length &&
            frameA < raiseWrist.track.frames.Length &&
            frameB < raiseWrist.track.frames.Length &&
            frameA < raiseFlagHand.track.frames.Length &&
            frameB < raiseFlagHand.track.frames.Length)
        {
            Pose shoulder = Sample(raiseUpper.track, frameA, frameB, frameLerp);
            Pose wrist = Sample(raiseWrist.track, frameA, frameB, frameLerp);
            Pose flagHand = Sample(raiseFlagHand.track, frameA, frameB, frameLerp);

            armExtra = MakeRotateAbout(
                shoulder.x * PositionScale,
                shoulder.y * PositionScale,
                raiseArmDegrees * Mathf.Deg2Rad);

            Vector2 wristNow = ApplyAffine(
                armExtra,
                wrist.x * PositionScale,
                wrist.y * PositionScale);

            // Dời gốc bàn tay cờ tới cổ tay (giữ nguyên hướng gốc: cán cờ thẳng đứng),
            // rồi xoay quanh cổ tay để vẫy.
            flagExtra = Multiply(
                MakeRotateAbout(
                    wristNow.x,
                    wristNow.y,
                    raiseWaveDegrees * Mathf.Deg2Rad),
                MakeTranslate(
                    wristNow.x - flagHand.x * PositionScale,
                    wristNow.y - flagHand.y * PositionScale));

            raiseReady = true;
        }

        foreach (Part part in parts)
        {
            // Overlay có animation riêng (frame riêng), bộ phận zombie dùng frame chung.
            int fa = frameA;
            int fb = frameB;
            float ft = frameLerp;

            if (part.overlayPlayer != null)
            {
                fa = part.overlayPlayer.FrameA;
                fb = part.overlayPlayer.FrameB;
                ft = part.overlayPlayer.FrameLerp;
            }

            if (fa < 0 ||
                fb < 0 ||
                fa >= part.track.frames.Length ||
                fb >= part.track.frames.Length)
            {
                part.meshRenderer.enabled = false;
                part.lastVisible = false;
                continue;
            }

            Pose pose = Sample(
                part.track,
                fa,
                fb,
                ft);

            bool visible = pose.visible;
            string imageId = pose.imageId;
            Affine matrix = ToAffine(pose);

            // Overlay: kéo theo chuyển động của track mà nó bám (so với tư thế gốc).
            if (part.overlayAttachPart != null)
            {
                Part ap = part.overlayAttachPart;
                int refF = part.overlayAttachRefFrame;

                if (refF >= 0 &&
                    refF < ap.track.frames.Length &&
                    frameA >= 0 && frameB >= 0 &&
                    frameA < ap.track.frames.Length &&
                    frameB < ap.track.frames.Length)
                {
                    Pose apPose = Sample(ap.track, frameA, frameB, frameLerp);

                    Affine apNow = ToAffine(apPose);
                    Affine apRef = ToAffine(FromFrame(ap.track.frames[refF]));

                    matrix = Multiply(Multiply(apNow, Invert(apRef)), matrix);

                    // Track mà overlay bám đang ẩn (ví dụ bàn tay cầm cờ lúc chết) thì overlay cũng ẩn.
                    if (!apPose.visible)
                        visible = false;
                }
            }

            if (raiseReady)
            {
                if (part == raiseUpper || part == raiseLower || part == raiseWrist)
                    matrix = Multiply(armExtra, matrix);
                else if (part == raiseFlagHand || part.overlayAttachPart == raiseFlagHand)
                    matrix = Multiply(flagExtra, matrix);
            }

            if (part.attached &&
                attachReady &&
                attachRefFrame < part.track.frames.Length)
            {
                Pose refPose = FromFrame(part.track.frames[attachRefFrame]);

                visible = refPose.visible;
                imageId = refPose.imageId;
                matrix = Multiply(attachMatrix, ToAffine(refPose));
            }

            if (part.imageOverride != null)
                imageId = part.imageOverride;

            if (part.hidden)
                visible = false;

            ReanimData.AtlasPart atlasPart = null;

            // LỌC LẦN 2: chặn phụ kiện khác zombie xuất hiện
            // khi track đổi Image ID trong animation.
            bool hasSprite =
                visible &&
                IsAllowedImage(imageId) &&
                data.AtlasParts.TryGetValue(
                    imageId,
                    out atlasPart);

            part.meshRenderer.enabled = hasSprite;
            part.lastVisible = hasSprite;

            if (!hasSprite)
                continue;

            SetPartUvs(part, atlasPart, imageId);

            ComputeCorners(
                matrix,
                atlasPart,
                part.corners);

            if (blending && part.blendValid)
            {
                for (int i = 0; i < 4; i++)
                {
                    part.corners[i] = Vector2.Lerp(
                        part.blendFrom[i],
                        part.corners[i],
                        blendWeight);
                }
            }
            else if (!blending)
            {
                part.blendValid = false;
            }

            for (int i = 0; i < 4; i++)
            {
                part.vertices[i] = ToUnity(
                    part.corners[i].x,
                    part.corners[i].y);
            }

            part.mesh.vertices = part.vertices;
            part.mesh.RecalculateBounds();
        }
    }

    // =========================================================
    // BLEND
    // =========================================================

    private void CaptureBlendPose()
    {
        foreach (Part part in parts)
        {
            part.blendValid = part.lastVisible;

            if (part.blendValid)
            {
                Array.Copy(
                    part.corners,
                    part.blendFrom,
                    4);
            }
        }
    }

    // =========================================================
    // DEBRIS (mảnh rơi: đầu bật ra, tay rụng)
    // =========================================================

    /// <summary>
    /// Tạo mảnh rơi từ các bộ phận đang hiển thị, đúng tại vị trí hiện tại của chúng.
    /// Trả về GameObject gốc của mảnh rơi (null nếu không có bộ phận nào đang hiện).
    /// </summary>
    public GameObject SpawnDebris(
        IList<string> trackNames,
        Vector3 velocity,
        float gravity,
        float angularVelocity,
        float lifetime,
        float fadeTime,
        float groundLinger)
    {
        if (!initialized || trackNames == null)
            return null;

        List<Part> sources = new List<Part>();

        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, 0f);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, 0f);

        foreach (string name in trackNames)
        {
            Part part;

            if (!partsByName.TryGetValue(name, out part) ||
                !part.lastVisible ||
                string.IsNullOrEmpty(part.currentImageId))
                continue;

            sources.Add(part);

            for (int i = 0; i < 4; i++)
            {
                Vector3 v = ToUnity(part.corners[i].x, part.corners[i].y);
                min = Vector3.Min(min, v);
                max = Vector3.Max(max, v);
            }
        }

        if (sources.Count == 0)
            return null;

        Vector3 pivot = (min + max) * 0.5f;

        GameObject root = new GameObject("Debris");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = pivot;
        // Particle PvZ được tách khỏi zombie ngay khi sinh ra. Giữ nguyên
        // pose/thang đo thế giới hiện tại để debris không bị kéo theo zombie.
        root.transform.SetParent(null, true);

        List<Mesh> meshes = new List<Mesh>();

        foreach (Part source in sources)
        {
            ReanimData.AtlasPart atlasPart;

            if (!data.AtlasParts.TryGetValue(source.currentImageId, out atlasPart))
                continue;

            GameObject go = new GameObject(source.track.name + "_piece");
            go.transform.SetParent(root.transform, false);

            MeshFilter filter = go.AddComponent<MeshFilter>();
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();

            renderer.sharedMaterial = sharedMaterial;
            renderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = source.meshRenderer.sortingOrder;

            Vector3[] verts = new Vector3[4];

            for (int i = 0; i < 4; i++)
            {
                verts[i] = ToUnity(
                    source.corners[i].x,
                    source.corners[i].y) - pivot;
            }

            Mesh mesh = new Mesh();
            mesh.name = go.name;
            mesh.vertices = verts;
            mesh.uv = new[]
            {
                new Vector2(atlasPart.uvMin.x, atlasPart.uvMax.y),
                new Vector2(atlasPart.uvMax.x, atlasPart.uvMax.y),
                new Vector2(atlasPart.uvMax.x, atlasPart.uvMin.y),
                new Vector2(atlasPart.uvMin.x, atlasPart.uvMin.y)
            };
            mesh.colors32 = CreateWhiteColors();
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();

            filter.sharedMesh = mesh;
            meshes.Add(mesh);
        }

        ReanimDebris debris = root.AddComponent<ReanimDebris>();

        float groundY = parent.position.y +
            (pivot.y - min.y) * Mathf.Abs(parent.lossyScale.y);

        debris.Init(
            velocity,
            gravity,
            angularVelocity,
            lifetime,
            fadeTime,
            groundY,
            groundLinger,
            meshes.ToArray());

        return root;
    }

    // =========================================================
    // SAMPLE / TRANSFORM
    // =========================================================

    private static Pose Sample(
        ReanimData.ReanimTrack track,
        int frameA,
        int frameB,
        float t)
    {
        ReanimData.ReanimFrame a = track.frames[frameA];
        ReanimData.ReanimFrame b = track.frames[frameB];

        return new Pose
        {
            x = Mathf.Lerp(a.x, b.x, t),
            y = Mathf.Lerp(a.y, b.y, t),

            kx = a.kx + Mathf.DeltaAngle(a.kx, b.kx) * t,
            ky = a.ky + Mathf.DeltaAngle(a.ky, b.ky) * t,

            sx = Mathf.Lerp(a.sx, b.sx, t),
            sy = Mathf.Lerp(a.sy, b.sy, t),

            visible = a.visible,
            imageId = a.imageId
        };
    }

    private static Pose FromFrame(ReanimData.ReanimFrame frame)
    {
        return new Pose
        {
            x = frame.x,
            y = frame.y,
            kx = frame.kx,
            ky = frame.ky,
            sx = frame.sx,
            sy = frame.sy,
            visible = frame.visible,
            imageId = frame.imageId
        };
    }

    private static Affine ToAffine(Pose pose)
    {
        float kx = pose.kx * Mathf.Deg2Rad;
        float ky = pose.ky * Mathf.Deg2Rad;

        return new Affine
        {
            m00 = Mathf.Cos(kx) * pose.sx,
            m01 = -Mathf.Sin(ky) * pose.sy,

            m10 = Mathf.Sin(kx) * pose.sx,
            m11 = Mathf.Cos(ky) * pose.sy,

            tx = pose.x * PositionScale,
            ty = pose.y * PositionScale
        };
    }

    /// <summary>Kết quả = a ∘ b (áp dụng b trước, rồi a).</summary>
    private static Affine Multiply(Affine a, Affine b)
    {
        return new Affine
        {
            m00 = a.m00 * b.m00 + a.m01 * b.m10,
            m01 = a.m00 * b.m01 + a.m01 * b.m11,
            m10 = a.m10 * b.m00 + a.m11 * b.m10,
            m11 = a.m10 * b.m01 + a.m11 * b.m11,
            tx = a.m00 * b.tx + a.m01 * b.ty + a.tx,
            ty = a.m10 * b.tx + a.m11 * b.ty + a.ty
        };
    }

    private static Affine MakeTranslate(float tx, float ty)
    {
        return new Affine { m00 = 1f, m11 = 1f, tx = tx, ty = ty };
    }

    private static Affine MakeRotateAbout(float cx, float cy, float radians)
    {
        float c = Mathf.Cos(radians);
        float s = Mathf.Sin(radians);

        return new Affine
        {
            m00 = c,
            m01 = -s,
            m10 = s,
            m11 = c,
            tx = cx - (c * cx - s * cy),
            ty = cy - (s * cx + c * cy)
        };
    }

    private static Vector2 ApplyAffine(Affine a, float x, float y)
    {
        return new Vector2(
            a.m00 * x + a.m01 * y + a.tx,
            a.m10 * x + a.m11 * y + a.ty);
    }

    private static Affine Invert(Affine m)
    {
        float det = m.m00 * m.m11 - m.m01 * m.m10;

        if (Mathf.Abs(det) < 1e-6f)
            return Affine.Identity;

        float i00 = m.m11 / det;
        float i01 = -m.m01 / det;
        float i10 = -m.m10 / det;
        float i11 = m.m00 / det;

        return new Affine
        {
            m00 = i00,
            m01 = i01,
            m10 = i10,
            m11 = i11,
            tx = -(i00 * m.tx + i01 * m.ty),
            ty = -(i10 * m.tx + i11 * m.ty)
        };
    }

    private void ComputeCorners(
        Affine matrix,
        ReanimData.AtlasPart atlasPart,
        Vector2[] corners)
    {
        float width = atlasPart.width;
        float height = atlasPart.height;

        float gx = globalScale.x;
        float gy = globalScale.y;

        corners[0] = new Vector2(
            matrix.tx * gx,
            matrix.ty * gy);

        corners[1] = new Vector2(
            (matrix.tx + matrix.m00 * width) * gx,
            (matrix.ty + matrix.m10 * width) * gy);

        corners[2] = new Vector2(
            (matrix.tx + matrix.m00 * width + matrix.m01 * height) * gx,
            (matrix.ty + matrix.m10 * width + matrix.m11 * height) * gy);

        corners[3] = new Vector2(
            (matrix.tx + matrix.m01 * height) * gx,
            (matrix.ty + matrix.m11 * height) * gy);
    }

    // =========================================================
    // UV
    // =========================================================

    private static void SetPartUvs(
        Part part,
        ReanimData.AtlasPart atlasPart,
        string imageId)
    {
        if (part.currentImageId == imageId)
            return;

        part.uvs[0] = new Vector2(
            atlasPart.uvMin.x,
            atlasPart.uvMax.y);

        part.uvs[1] = new Vector2(
            atlasPart.uvMax.x,
            atlasPart.uvMax.y);

        part.uvs[2] = new Vector2(
            atlasPart.uvMax.x,
            atlasPart.uvMin.y);

        part.uvs[3] = new Vector2(
            atlasPart.uvMin.x,
            atlasPart.uvMin.y);

        part.mesh.uv = part.uvs;
        part.currentImageId = imageId;
    }

    // =========================================================
    // SORTING
    // =========================================================

    public void SetSortingOrders(
        int baseSortingOrder,
        int sortingStep = 1)
    {
        foreach (Part part in parts)
        {
            if (part.meshRenderer != null)
            {
                part.meshRenderer.sortingOrder =
                    baseSortingOrder +
                    part.sortingIndex * sortingStep;
            }
        }
    }

    public bool SetPartSortingOrder(
        string trackName,
        int sortingOrder)
    {
        Part part;

        if (!partsByName.TryGetValue(trackName, out part))
            return false;

        part.meshRenderer.sortingOrder = sortingOrder;
        return true;
    }

    public void SetShadowSortingOrder(int sortingOrder)
    {
        if (shadowPart != null)
            shadowPart.meshRenderer.sortingOrder = sortingOrder;

        if (shadowPart2 != null)
            shadowPart2.meshRenderer.sortingOrder = sortingOrder;
    }

    // =========================================================
    // ALPHA
    // =========================================================

    public void SetAlpha(float alpha)
    {
        currentAlpha = Mathf.Clamp01(alpha);

        byte a = (byte)Mathf.RoundToInt(
            currentAlpha * 255f);

        Color32[] colors =
        {
            new Color32(255,255,255,a),
            new Color32(255,255,255,a),
            new Color32(255,255,255,a),
            new Color32(255,255,255,a)
        };

        foreach (Part part in parts)
        {
            if (part.mesh != null)
                part.mesh.colors32 = colors;
        }

        if (shadowPart != null)
            shadowPart.mesh.colors32 = colors;

        if (shadowPart2 != null)
            shadowPart2.mesh.colors32 = colors;
    }

    // =========================================================
    // COORDINATE
    // =========================================================

    private Vector3 ToUnity(float pixelX, float pixelY)
    {
        return new Vector3(
            (pixelX - anchorPixel.x) / pixelPerUnit + pivotOffset.x,
            -(pixelY - anchorPixel.y) / pixelPerUnit + pivotOffset.y,
            0f);
    }

    // =========================================================
    // PUBLIC ACCESS
    // =========================================================

    public MeshRenderer GetPartRenderer(string trackName)
    {
        Part part;

        if (partsByName.TryGetValue(trackName, out part))
            return part.meshRenderer;

        return null;
    }

    public bool TryGetPartCorners(
        string trackName,
        out Vector2[] corners)
    {
        Part part;

        if (partsByName.TryGetValue(trackName, out part))
        {
            corners = part.corners;
            return true;
        }

        corners = null;
        return false;
    }

    // =========================================================
    // DISPOSE
    // =========================================================

    public void Dispose()
    {
        if (animationPlayer != null)
            animationPlayer.OnBlendStarted -= CaptureBlendPose;

        foreach (Part part in parts)
            DestroyPart(part);

        parts.Clear();
        footParts.Clear();
        partNames.Clear();
        partsByName.Clear();

        if (shadowPart != null)
            DestroyPart(shadowPart);

        if (shadowPart2 != null)
            DestroyPart(shadowPart2);

        shadowPart = null;
        shadowPart2 = null;

        attachActive = false;
        attachBodyPart = null;

        animationPlayer = null;
        data = null;
        parent = null;
        sharedMaterial = null;
        allowedImageIds = null;

        initialized = false;
    }

    private static void DestroyPart(Part part)
    {
        if (part == null)
            return;

        if (part.mesh != null)
            UnityEngine.Object.Destroy(part.mesh);

        if (part.gameObject != null)
            UnityEngine.Object.Destroy(part.gameObject);
    }
}
