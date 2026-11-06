using UnityEngine;

/// <summary>
/// Phím test nhanh cho mọi loại zombie Reanim: Normal, Flag... (cần Input Manager cũ).
///
///  1 = Idle              6 = Rụng tay
///  2 = Đi (walk)         7 = Chết còn đầu
///  3 = Đi (walk2)        8 = Chết mất đầu thường
///  4 = Bắt đầu ăn        9 = Chết mất đầu hiếm (Flag Zombie: superlongdeath)
///  5 = Ngừng ăn          T = Trừ máu
///                        R = Hồi sinh / reset
/// </summary>
public class NormalZombieAnimationTester : MonoBehaviour
{
    //[SerializeField] private BaseZombieReanim zombie;
    //[SerializeField] private float testDamage = 50f;

    //private void Update()
    //{
    //    if (zombie == null) return;

    //    if (Input.GetKeyDown(KeyCode.Alpha1)) zombie.Idle();

    //    if (Input.GetKeyDown(KeyCode.Alpha2))
    //    {
    //        zombie.SetWalkVariant(false);
    //        zombie.Walk();
    //    }

    //    if (Input.GetKeyDown(KeyCode.Alpha3))
    //    {
    //        zombie.SetWalkVariant(true);
    //        zombie.Walk();
    //    }

    //    if (Input.GetKeyDown(KeyCode.Alpha4)) zombie.StartEating();
    //    if (Input.GetKeyDown(KeyCode.Alpha5)) zombie.StopEating();
    //    if (Input.GetKeyDown(KeyCode.Alpha6)) zombie.LoseArm();
    //    if (Input.GetKeyDown(KeyCode.Alpha7)) zombie.Die();
    //    if (Input.GetKeyDown(KeyCode.Alpha8))
    //    {
    //        FlagZombieReanim flagZombie = zombie as FlagZombieReanim;

    //        if (flagZombie != null)
    //            flagZombie.DieHeadlessStandard();
    //        else
    //            zombie.DieHeadless();
    //    }

    //    // Chạy trực tiếp animation chết hiếm, không phụ thuộc tỉ lệ random.
    //    if (Input.GetKeyDown(KeyCode.Alpha9))
    //    {
    //        FlagZombieReanim flagZombie = zombie as FlagZombieReanim;

    //        if (flagZombie != null)
    //            flagZombie.DieHeadlessRare();
    //        else
    //            Debug.LogWarning(
    //                "Animation superlongdeath chỉ có trên Flag Zombie.",
    //                this);
    //    }

    //    if (Input.GetKeyDown(KeyCode.T)) zombie.TakeDamage(testDamage);
    //    if (Input.GetKeyDown(KeyCode.R)) zombie.ResetZombie();
    //}
}
