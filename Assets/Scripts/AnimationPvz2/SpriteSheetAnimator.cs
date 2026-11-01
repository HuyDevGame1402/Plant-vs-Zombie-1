//using System.Collections.Generic;
//using UnityEngine;
//using UnityEngine.UI;

//public class SpriteSheetAnimator : MonoBehaviour
//{
//    [Header("Assets")]
//    public Texture2D atlasTexture; // Texture ảnh tổng
//    public TextAsset atlasJsonText; // File JSON 1 (Atlas)
//    public TextAsset animJsonText;  // File JSON 2 (Anim)

//    [Header("Render Target")]
//    public Image uiImage;
//    public SpriteRenderer spriteRenderer;

//    private Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
//    private List<Sprite> animationFrames = new List<Sprite>();
//    private float frameRate = 30f;
//    private float timer = 0f;
//    private int currentFrameIndex = 0;
//    private bool isPlaying = false;

//    void Start()
//    {
//        Debug.Log("[Animator] --- BẮT ĐẦU NẠP DỮ LIỆU ---");
//        ParseAtlas();
//        ParseAnimation();

//        if (animationFrames.Count > 0)
//        {
//            isPlaying = true;
//            Debug.Log($"[Animator] SUCCESS! Đã nạp thành công {animationFrames.Count} frames. Bắt đầu chạy animation...");
//            UpdateFrame(0); // Hiển thị ngay frame đầu tiên
//        }
//        else
//        {
//            Debug.LogError("[Animator] LỖI: animationFrames bị rỗng! Kiểm tra lại Log phía trên để xem lỗi ở bước Atlas hay bước Anim ID.");
//        }
//    }

//    void Update()
//    {
//        if (!isPlaying || animationFrames.Count == 0) return;

//        timer += Time.deltaTime;
//        float frameInterval = 1f / frameRate;

//        if (timer >= frameInterval)
//        {
//            timer -= frameInterval;
//            currentFrameIndex = (currentFrameIndex + 1) % animationFrames.Count;
//            UpdateFrame(currentFrameIndex);
//        }
//    }

//    void ParseAtlas()
//    {
//        if (atlasJsonText == null || atlasTexture == null)
//        {
//            Debug.LogError("[Animator] Thiếu atlasJsonText hoặc atlasTexture trên Inspector!");
//            return;
//        }

//        AtlasData data = JsonUtility.FromJson<AtlasData>(atlasJsonText.text);
//        if (data == null || data.groups == null)
//        {
//            Debug.LogError("[Animator] Không parse được file Atlas JSON!");
//            return;
//        }

//        int loadedSpritesCount = 0;

//        foreach (var group in data.groups)
//        {
//            // Bỏ qua nhóm không chứa danh sách resources
//            if (group.resources == null || group.resources.Count == 0) continue;

//            foreach (var res in group.resources)
//            {
//                if (res.type == "Image" && res.aw > 0 && res.ah > 0)
//                {
//                    // Lật trục Y vì Unity Sprite tính tọa độ gốc (0,0) ở góc DƯỚI-TRÁI
//                    int rectX = res.ax;
//                    int rectY = atlasTexture.height - res.ay - res.ah;
//                    int rectW = res.aw;
//                    int rectH = res.ah;

//                    Rect rect = new Rect(rectX, rectY, rectW, rectH);
//                    Vector2 pivot = new Vector2(0.5f, 0.5f);

//                    Sprite sprite = Sprite.Create(atlasTexture, rect, pivot);

//                    if (!string.IsNullOrEmpty(res.id))
//                    {
//                        spriteCache[res.id] = sprite;
//                        loadedSpritesCount++;
//                    }
//                }
//            }
//        }

//        Debug.Log($"[Animator] Đã cắt thành công {loadedSpritesCount} sprite từ Atlas và lưu vào Cache.");
//    }

//    void ParseAnimation()
//    {
//        if (animJsonText == null)
//        {
//            Debug.LogError("[Animator] Thiếu animJsonText trên Inspector!");
//            return;
//        }

//        AnimData animData = JsonUtility.FromJson<AnimData>(animJsonText.text);
//        if (animData == null || animData.image == null)
//        {
//            Debug.LogError("[Animator] Không parse được file Anim JSON!");
//            return;
//        }

//        frameRate = animData.frame_rate > 0 ? animData.frame_rate : 30f;

//        foreach (var imgEntry in animData.image)
//        {
//            if (string.IsNullOrEmpty(imgEntry.name)) continue;

//            string spriteId = imgEntry.name;

//            // Nếu chuỗi có chứa dấu '|', lấy phần ID sau dấu '|'
//            if (imgEntry.name.Contains("|"))
//            {
//                string[] parts = imgEntry.name.Split('|');
//                if (parts.Length > 1)
//                {
//                    spriteId = parts[1];
//                }
//            }

//            if (spriteCache.ContainsKey(spriteId))
//            {
//                animationFrames.Add(spriteCache[spriteId]);
//            }
//            else
//            {
//                Debug.LogWarning($"[Animator] KHÔNG TÌM THẤY SPRITE ID: '{spriteId}' trong Atlas Cache! Vui lòng kiểm tra lại ID trong file Anim JSON và Atlas JSON.");
//            }
//        }
//    }

//    void UpdateFrame(int index)
//    {
//        Sprite currentSprite = animationFrames[index];

//        if (uiImage != null)
//        {
//            uiImage.sprite = currentSprite;
//            uiImage.SetNativeSize();
//        }

//        if (spriteRenderer != null)
//        {
//            spriteRenderer.sprite = currentSprite;
//        }
//    }
//}