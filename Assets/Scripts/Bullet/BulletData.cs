using UnityEngine;

public class BulletData : MonoBehaviour
{
    [SerializeField] private int damage;
    public int Damage => damage;

    public void SetUpDataBullet(int damage)
    {
        this.damage = damage;
        //damage = 1;
    }
}
