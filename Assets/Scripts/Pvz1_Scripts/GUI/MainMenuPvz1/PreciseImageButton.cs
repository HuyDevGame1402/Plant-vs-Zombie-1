using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Image))]
public class PreciseImageButton : MonoBehaviour, IPointerClickHandler
{
    protected Image targetImage;

    protected virtual void Awake()
    {
        targetImage = GetComponent<Image>();
        targetImage.raycastTarget = true; // Đảm bảo bắt raycast
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Chuyển đổi vị trí click màn hình sang tọa độ cục bộ của RectTransform
        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetImage.rectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out localPoint))
        {
            return;
        }

        Rect rect = targetImage.rectTransform.rect;
        Texture2D tex = targetImage.sprite.texture;
        Rect spriteRect = targetImage.sprite.textureRect;

        // Tính toán tọa độ pixel tương ứng trên Texture gốc
        float normalizedX = (localPoint.x - rect.x) / rect.width;
        float normalizedY = (localPoint.y - rect.y) / rect.height;

        int pixelX = Mathf.FloorToInt(spriteRect.x + normalizedX * spriteRect.width);
        int pixelY = Mathf.FloorToInt(spriteRect.y + normalizedY * spriteRect.height);

        // Kiểm tra xem điểm bấm có nằm trong vùng ảnh không
        if (pixelX >= 0 && pixelX < tex.width && pixelY >= 0 && pixelY < tex.height)
        {
            try
            {
                Color pixelColor = tex.GetPixel(pixelX, pixelY);

                // Nếu độ trong suốt lớn hơn 0.1 (tức là bấm trúng phần hình ảnh hiển thị, bỏ qua vùng trống)
                if (pixelColor.a > 0.1f)
                {
                    OnButtonClicked(); // Gọi hàm thực thi riêng của lớp con
                }
            }
            catch (UnityException)
            {
                Debug.LogWarning($"Hãy chọn file ảnh gốc '{tex.name}' trong Project -> Tích chọn 'Read/Write Enabled' ở Inspector!");
            }
        }
    }

    // Hàm ảo để các lớp con kế thừa và tự định nghĩa nội dung thực thi riêng
    protected virtual void OnButtonClicked()
    {
        // Mặc định để trống, lớp con sẽ override lại
    }
}