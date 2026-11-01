using System.Collections.Generic;

/// <summary>
/// Zombie thường. Toàn bộ logic nằm trong BaseZombieReanim,
/// class này chỉ khai báo danh sách ảnh được phép vẽ.
/// </summary>
public class NormalZombieReanim : BaseZombieReanim
{
    protected override HashSet<string> AllowedImageIds
    {
        get { return CommonZombieImageIds; }
    }
}