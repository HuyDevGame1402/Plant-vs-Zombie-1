using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BananaPlantAnimator : MonoBehaviour
{
    [Header("BANANA assets")]
    [SerializeField] private TextAsset animationJson;
    [SerializeField] private TextAsset atlasJson;
    [SerializeField] private Texture2D atlasTexture;

    [Header("Playback")]
    [SerializeField] private string startAnimation = "idle";
    [SerializeField] private bool loop = true;
    [SerializeField] private float pixelsPerUnit = 100f;
    [SerializeField] private int sortingOrder;

    private BananaAnimationData animationData;
    private BananaTimeline mainTimeline;
    private Dictionary<int, Sprite> imageSprites;
    // Ma tran [a, b, c, d, tx, ty] = image[i].transform * diag(cx, cy) (cx, cy: hieu chinh kich thuoc atlas -> kich thuoc goc)
    private float[][] imageMatrices;
    private Dictionary<string, AnimationRange> animationRanges;
    private AnimationRange activeRange;
    private int currentFrame;
    private float frameTimer;
    private bool initialized;

    private void Awake()
    {
        if (!Initialize())
        {
            enabled = false;
            return;
        }

        Play(startAnimation, loop);
    }

    private void Update()
    {
        if (!initialized || activeRange == null)
            return;

        frameTimer += Time.deltaTime * animationData.main_sprite.frame_rate;
        while (frameTimer >= 1f)
        {
            frameTimer -= 1f;

            if (currentFrame >= activeRange.endFrame)
            {
                if (!loop)
                {
                    frameTimer = 0f;
                    return;
                }

                RebuildToFrame(activeRange.startFrame);
            }
            else
            {
                currentFrame++;
                mainTimeline.ApplyFrame(currentFrame);
            }
        }
    }

    private void OnDestroy()
    {
        mainTimeline?.Clear();

        if (imageSprites == null)
            return;

        foreach (Sprite sprite in imageSprites.Values)
        {
            if (sprite != null)
                Destroy(sprite);
        }
    }

    /// <summary>Plays the segment beginning at the matching label in BANANA.json.</summary>
    public void Play(string animationName, bool shouldLoop = true)
    {
        if (!initialized)
        {
            Debug.LogError("BananaPlantAnimator is not initialized.", this);
            return;
        }

        if (!animationRanges.TryGetValue(animationName, out AnimationRange range))
        {
            Debug.LogError($"BANANA animation label '{animationName}' was not found.", this);
            return;
        }

        activeRange = range;
        loop = shouldLoop;
        frameTimer = 0f;
        RebuildToFrame(range.startFrame);
    }

    private bool Initialize()
    {
        if (animationJson == null || atlasJson == null || atlasTexture == null)
        {
            Debug.LogError("Assign BANANA.json, one PLANTBANANA atlas JSON, and its matching PNG texture.", this);
            return false;
        }

        if (pixelsPerUnit <= 0f)
        {
            Debug.LogError("Pixels Per Unit must be greater than zero.", this);
            return false;
        }

        try
        {
            animationData = JsonUtility.FromJson<BananaAnimationData>(animationJson.text);
            BananaAtlasData atlasData = JsonUtility.FromJson<BananaAtlasData>(atlasJson.text);

            if (animationData == null || animationData.main_sprite == null ||
                animationData.main_sprite.frame == null || atlasData == null || atlasData.sprites == null)
            {
                Debug.LogError("The supplied BANANA or atlas JSON has an unexpected structure.", this);
                return false;
            }

            imageSprites = BuildSpriteMap(animationData, atlasData);
            animationRanges = BuildAnimationRanges(animationData.main_sprite);
            mainTimeline = new BananaTimeline(
                this,
                animationData.main_sprite,
                transform,
                true,
                Color.white,
                sortingOrder);
            initialized = true;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Could not load the BANANA animation data: {exception.Message}", this);
            return false;
        }
    }

    private Dictionary<int, Sprite> BuildSpriteMap(BananaAnimationData data, BananaAtlasData atlasData)
    {
        var atlasSprites = new Dictionary<string, BananaAtlasSprite>(StringComparer.Ordinal);
        foreach (BananaAtlasSprite atlasSprite in atlasData.sprites)
        {
            if (atlasSprite != null && !string.IsNullOrEmpty(atlasSprite.id))
                atlasSprites[atlasSprite.id] = atlasSprite;
        }

        if (data.image == null || data.image.Length == 0)
            throw new InvalidOperationException("BANANA.json does not contain any images.");

        // Buoc 1: tim sprite trong atlas cho tung image va tinh ti le atlas (vd 768/1200 = 0.64).
        var matched = new BananaAtlasSprite[data.image.Length];
        float sumAtlas = 0f;
        float sumSize = 0f;
        for (int i = 0; i < data.image.Length; i++)
        {
            BananaImage image = data.image[i];
            if (image.size == null || image.size.Length < 2 || image.size[0] <= 0f || image.size[1] <= 0f)
                throw new InvalidOperationException($"BANANA image {i} has no usable size.");

            string id = image.name == null ? string.Empty : image.name.Substring(image.name.LastIndexOf('|') + 1);
            if (!atlasSprites.TryGetValue(id, out BananaAtlasSprite atlasSprite))
                throw new InvalidOperationException($"Atlas sprite '{id}' is missing.");

            if (atlasSprite.aw <= 0 || atlasSprite.ah <= 0 ||
                atlasSprite.ax < 0 || atlasSprite.ay < 0 ||
                atlasSprite.ax + atlasSprite.aw > atlasTexture.width ||
                atlasSprite.ay + atlasSprite.ah > atlasTexture.height)
            {
                throw new InvalidOperationException($"Atlas rectangle for '{id}' is outside the assigned texture.");
            }

            matched[i] = atlasSprite;
            sumAtlas += atlasSprite.aw;
            sumSize += image.size[0];
        }

        float atlasScale = sumAtlas / sumSize;
        float spritePixelsPerUnit = pixelsPerUnit * atlasScale;

        // Buoc 2: tao sprite. PIVOT O GOC TREN-TRAI vi image[i].transform (tx, ty) la vi tri goc tren-trai cua anh.
        imageSprites = new Dictionary<int, Sprite>();
        imageMatrices = new float[data.image.Length][];
        var result = new Dictionary<int, Sprite>();
        for (int i = 0; i < data.image.Length; i++)
        {
            BananaImage image = data.image[i];
            BananaAtlasSprite atlasSprite = matched[i];

            var rect = new Rect(
                atlasSprite.ax,
                atlasTexture.height - atlasSprite.ay - atlasSprite.ah,
                atlasSprite.aw,
                atlasSprite.ah);

            result.Add(i, Sprite.Create(
                atlasTexture,
                rect,
                new Vector2(0f, 1f),
                spritePixelsPerUnit,
                0,
                SpriteMeshType.FullRect));

            // Hieu chinh lam tron: dua kich thuoc sprite ve dung image.size (don vi stage).
            float cx = image.size[0] * atlasScale / atlasSprite.aw;
            float cy = image.size[1] * atlasScale / atlasSprite.ah;

            float ia = 1f, ib = 0f, ic = 0f, id = 1f, itx = 0f, ity = 0f;
            float[] t = image.transform;
            if (t != null && t.Length >= 6)
            {
                ia = t[0]; ib = t[1]; ic = t[2]; id = t[3]; itx = t[4]; ity = t[5];
            }

            imageMatrices[i] = new[] { ia * cx, ib * cx, ic * cy, id * cy, itx, ity };
        }

        return result;
    }

    private static Dictionary<string, AnimationRange> BuildAnimationRanges(BananaTimelineData timeline)
    {
        var ranges = new Dictionary<string, AnimationRange>(StringComparer.Ordinal);
        var labels = new List<KeyValuePair<string, int>>();

        for (int i = 0; i < timeline.frame.Length; i++)
        {
            string label = timeline.frame[i].label;
            if (!string.IsNullOrEmpty(label))
                labels.Add(new KeyValuePair<string, int>(label, i));
        }

        for (int i = 0; i < labels.Count; i++)
        {
            int end = i + 1 < labels.Count
                ? labels[i + 1].Value - 1
                : timeline.frame.Length - 1;
            ranges.Add(labels[i].Key, new AnimationRange(labels[i].Value, end));
        }

        return ranges;
    }

    private void RebuildToFrame(int targetFrame)
    {
        mainTimeline.Clear();
        mainTimeline.ApplyFrame(targetFrame); // tu replay tu frame 0 toi targetFrame
        currentFrame = targetFrame;
    }

    private Sprite GetImageSprite(int resource)
    {
        if (!imageSprites.TryGetValue(resource, out Sprite sprite))
            throw new InvalidOperationException($"BANANA image resource {resource} is out of range.");
        return sprite;
    }

    private float[] GetImageTransform(int resource)
    {
        if (resource < 0 || resource >= imageMatrices.Length)
            throw new InvalidOperationException($"BANANA image resource {resource} is out of range.");
        return imageMatrices[resource];
    }

    private BananaTimelineData GetSpriteTimeline(int resource)
    {
        if (resource < 0 || resource >= animationData.sprite.Length)
            throw new InvalidOperationException($"BANANA sprite resource {resource} is out of range.");
        return animationData.sprite[resource];
    }

    private sealed class BananaTimeline
    {
        private readonly BananaPlantAnimator owner;
        private readonly BananaTimelineData data;
        private readonly Transform container;
        private readonly bool isMainTimeline;
        private readonly Dictionary<int, BananaVisual> children = new Dictionary<int, BananaVisual>();
        private readonly int sortingBase;
        private Color tint;
        private int lastFrame = -1;

        public BananaTimeline(
            BananaPlantAnimator owner,
            BananaTimelineData data,
            Transform container,
            bool isMainTimeline,
            Color tint,
            int sortingBase)
        {
            this.owner = owner;
            this.data = data;
            this.container = container;
            this.isMainTimeline = isMainTimeline;
            this.tint = tint;
            this.sortingBase = sortingBase;
        }

        public void Clear()
        {
            foreach (BananaVisual visual in children.Values)
                visual.Destroy();

            children.Clear();
            lastFrame = -1;
        }

        public void SetTint(Color value)
        {
            tint = value;
            foreach (BananaVisual visual in children.Values)
                visual.SetTint(value);
        }

        /// <summary>Dua timeline toi frameIndex: replay cac frame bi nhay qua; quay lui thi Clear roi replay.</summary>
        public void ApplyFrame(int frameIndex)
        {
            if (data.frame == null || frameIndex < 0 || frameIndex >= data.frame.Length || frameIndex == lastFrame)
                return;

            if (frameIndex < lastFrame)
                Clear();

            for (int f = lastFrame + 1; f <= frameIndex; f++)
                ApplyFrameInternal(f);

            lastFrame = frameIndex;
        }

        private void ApplyFrameInternal(int frameIndex)
        {
            BananaFrame frame = data.frame[frameIndex];

            if (frame.remove != null)
            {
                foreach (BananaRemove remove in frame.remove)
                {
                    if (remove != null && children.TryGetValue(remove.index, out BananaVisual removed))
                    {
                        removed.Destroy();
                        children.Remove(remove.index);
                    }
                }
            }

            if (frame.append != null)
            {
                foreach (BananaAppend append in frame.append)
                {
                    if (append == null)
                        continue;

                    if (children.TryGetValue(append.index, out BananaVisual replaced))
                        replaced.Destroy();

                    children[append.index] = CreateVisual(append);
                }
            }

            if (frame.change != null)
            {
                foreach (BananaChange change in frame.change)
                {
                    if (change == null || !children.TryGetValue(change.index, out BananaVisual visual))
                        continue;

                    visual.SetTransform(change.transform, isMainTimeline);
                    if (change.color != null && change.color.Length >= 4)
                    {
                        visual.SetTint(new Color(
                            change.color[0],
                            change.color[1],
                            change.color[2],
                            change.color[3]));
                    }

                    if (visual.Timeline != null)
                        visual.Timeline.ApplyFrame(change.anim_frame_num);
                }
            }
        }

        private BananaVisual CreateVisual(BananaAppend append)
        {
            BananaTimelineData nestedTimeline = append.sprite
                ? owner.GetSpriteTimeline(append.resource)
                : null;

            Sprite sprite = append.sprite ? null : owner.GetImageSprite(append.resource);
            string label = append.sprite
                ? nestedTimeline.name
                : $"image_{append.resource}";
            int visualSortingOrder = isMainTimeline
                ? sortingBase + append.index * 100
                : sortingBase + append.index;

            var childObject = new GameObject($"BANANA_{append.index}_{label}");
            childObject.transform.SetParent(container, false);

            SpriteRenderer renderer = null;
            if (sprite != null)
            {
                renderer = childObject.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = visualSortingOrder;
            }

            float[] imageTransform = append.sprite ? null : owner.GetImageTransform(append.resource);
            var visual = new BananaVisual(owner, childObject.transform, renderer, tint, imageTransform);
            if (nestedTimeline != null)
            {
                visual.Timeline = new BananaTimeline(
                    owner,
                    nestedTimeline,
                    childObject.transform,
                    false,
                    tint,
                    visualSortingOrder);
                visual.Timeline.ApplyFrame(0);
            }

            return visual;
        }
    }

    private sealed class BananaVisual
    {
        private readonly BananaPlantAnimator owner;
        private readonly Transform visualTransform;
        private readonly SpriteRenderer renderer;
        private readonly float[] imageTransform;
        private Color tint;

        public BananaTimeline Timeline { get; set; }

        public BananaVisual(
            BananaPlantAnimator owner,
            Transform visualTransform,
            SpriteRenderer renderer,
            Color initialTint,
            float[] imageTransform)
        {
            this.owner = owner;
            this.visualTransform = visualTransform;
            this.renderer = renderer;
            this.imageTransform = imageTransform;
            SetTint(initialTint);
        }

        public void SetTransform(float[] values, bool centerOnStage)
        {
            if (values == null || values.Length == 0)
                return;

            float a = 1f;
            float b = 0f;
            float c = 0f;
            float d = 1f;
            float x;
            float y;

            if (values.Length >= 6)
            {
                a = values[0];
                b = values[1];
                c = values[2];
                d = values[3];
                x = values[4];
                y = values[5];
            }
            else if (values.Length == 3)
            {
                // [goc xoay (radian), x, y]
                float angle = values[0];
                a = Mathf.Cos(angle);
                b = Mathf.Sin(angle);
                c = -Mathf.Sin(angle);
                d = Mathf.Cos(angle);
                x = values[1];
                y = values[2];
            }
            else if (values.Length == 2)
            {
                x = values[0];
                y = values[1];
            }
            else
            {
                return;
            }

            if (imageTransform != null && imageTransform.Length >= 6)
            {
                float imageA = imageTransform[0];
                float imageB = imageTransform[1];
                float imageC = imageTransform[2];
                float imageD = imageTransform[3];
                float imageX = imageTransform[4];
                float imageY = imageTransform[5];

                x += a * imageX + c * imageY;
                y += b * imageX + d * imageY;

                float combinedA = a * imageA + c * imageB;
                float combinedB = b * imageA + d * imageB;
                float combinedC = a * imageC + c * imageD;
                float combinedD = b * imageC + d * imageD;
                a = combinedA;
                b = combinedB;
                c = combinedC;
                d = combinedD;
            }

            float scaleX = Mathf.Sqrt(a * a + b * b);
            float determinant = a * d - b * c;
            float scaleY = scaleX > 0f ? determinant / scaleX : 1f;
            float rotation = -Mathf.Atan2(b, a) * Mathf.Rad2Deg;

            if (centerOnStage)
            {
                float stageWidth = owner.animationData.size != null && owner.animationData.size.Length > 0
                    ? owner.animationData.size[0]
                    : 0f;
                float stageHeight = owner.animationData.size != null && owner.animationData.size.Length > 1
                    ? owner.animationData.size[1]
                    : 0f;
                float originX = owner.animationData.position != null && owner.animationData.position.Length > 0
                    ? owner.animationData.position[0]
                    : 0f;
                float originY = owner.animationData.position != null && owner.animationData.position.Length > 1
                    ? owner.animationData.position[1]
                    : 0f;
                x -= originX + stageWidth * 0.5f;
                y = originY + stageHeight * 0.5f - y;
            }
            else
            {
                y = -y;
            }

            visualTransform.localPosition = new Vector3(
                x / owner.pixelsPerUnit,
                y / owner.pixelsPerUnit,
                0f);
            visualTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            visualTransform.localScale = new Vector3(scaleX, scaleY, 1f);
        }

        public void SetTint(Color value)
        {
            tint = value;
            if (renderer != null)
                renderer.color = value;

            Timeline?.SetTint(value);
        }

        public void Destroy()
        {
            if (visualTransform != null)
            {
                visualTransform.gameObject.SetActive(false);
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(visualTransform.gameObject);
                else
                    UnityEngine.Object.DestroyImmediate(visualTransform.gameObject);
            }
        }
    }

    private sealed class AnimationRange
    {
        public readonly int startFrame;
        public readonly int endFrame;

        public AnimationRange(int startFrame, int endFrame)
        {
            this.startFrame = startFrame;
            this.endFrame = endFrame;
        }
    }

    [Serializable]
    private sealed class BananaAnimationData
    {
        public float frame_rate;
        public float[] position;
        public float[] size;
        public BananaImage[] image;
        public BananaTimelineData[] sprite;
        public BananaTimelineData main_sprite;
    }

    [Serializable]
    private sealed class BananaImage
    {
        public string name;
        public float[] size;
        public float[] transform;
    }

    [Serializable]
    private sealed class BananaTimelineData
    {
        public string name;
        public float frame_rate;
        public int[] work_area;
        public BananaFrame[] frame;
    }

    [Serializable]
    private sealed class BananaFrame
    {
        public string label;
        public bool stop;
        public BananaRemove[] remove;
        public BananaAppend[] append;
        public BananaChange[] change;
    }

    [Serializable]
    private sealed class BananaRemove
    {
        public int index;
    }

    [Serializable]
    private sealed class BananaAppend
    {
        public int index;
        public int resource;
        public bool sprite;
    }

    [Serializable]
    private sealed class BananaChange
    {
        public int index;
        public float[] transform;
        public float[] color;
        public int anim_frame_num;
    }

    [Serializable]
    private sealed class BananaAtlasData
    {
        public BananaAtlasSprite[] sprites;
    }

    [Serializable]
    private sealed class BananaAtlasSprite
    {
        public string id;
        public int ax;
        public int ay;
        public int aw;
        public int ah;
    }
}