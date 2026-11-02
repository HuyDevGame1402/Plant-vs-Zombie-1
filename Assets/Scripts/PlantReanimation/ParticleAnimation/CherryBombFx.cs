using System.Collections.Generic;

namespace PvzFx
{
    /// <summary>
    /// Hiệu ứng nổ Cherry Bomb.
    /// Ảnh trong atlas (EXPLOSIONCLOUD, BOSSEXPLOSION...) là ảnh trắng/vàng nhạt,
    /// engine PvZ nhuộm màu bằng các track red/green/blue theo thời gian của particle
    /// (vàng -> cam -> đỏ đậm -> xám đen). Ở đây làm đúng như vậy bằng Track.Curve.
    /// Toạ độ: pixel atlas 960, (0,0) = tâm vụ nổ, Y dương = xuống.
    /// </summary>
    public static class CherryBombFx
    {
        public static List<EmitterDef> Create()
        {
            var list = new List<EmitterDef>();

            // 0) Khói đen bốc lên sau vụ nổ (vẽ dưới cùng)
            list.Add(new EmitterDef
            {
                name = "Smoke",
                image = "IMAGE_DUST_PUFFS",
                sortingOffset = 0,
                randomLaunchSpin = true,
                systemDuration = 2,
                spawnRate = Track.Const(100000),
                spawnMaxLaunched = 10,
                particleDuration = Track.Range(90, 140),
                launchSpeed = Track.Range(60, 260),
                emitterRadius = Track.Range(0, 40),
                red = Track.Const(0.22f),
                green = Track.Const(0.19f),
                blue = Track.Const(0.17f),
                alpha = Track.Curve((0f, 0f), (0.2f, 0f), (0.35f, 0.7f), (1f, 0f)), // xuất hiện sau lửa
                scale = Track.CurveRange((0f, 0.8f, 1.1f), (1f, 2.2f, 3.2f)),
                spinSpeed = Track.Range(-60, 60),
                fields =
                {
                    new Field(FieldType.Friction, Track.Const(0.04f), Track.Const(0.04f)),
                    new Field(FieldType.Acceleration, Track.Const(0), Track.Const(-4)),
                },
            });

            // 1) Các cụm mây lửa (ảnh trắng viền đen -> nhuộm vàng -> cam -> đỏ đậm -> đen xám)
            list.Add(new EmitterDef
            {
                name = "FireClouds",
                image = "IMAGE_EXPLOSIONCLOUD",
                sortingOffset = 1,
                randomLaunchSpin = true,
                systemDuration = 2,
                spawnRate = Track.Const(100000),
                spawnMaxLaunched = 16,
                particleDuration = Track.Range(55, 85),
                launchSpeed = Track.Range(150, 520),          // bay tỏa 360° (launchAngle = null)
                emitterRadius = Track.Range(0, 28),
                red = Track.Curve((0f, 1f), (0.4f, 1f), (0.75f, 0.65f), (1f, 0.3f)),
                green = Track.Curve((0f, 0.95f), (0.2f, 0.6f), (0.5f, 0.2f), (0.8f, 0.1f), (1f, 0.08f)),
                blue = Track.Curve((0f, 0.45f), (0.25f, 0.1f), (1f, 0.05f)),
                alpha = Track.Curve((0f, 0f), (0.05f, 1f), (0.65f, 0.95f), (1f, 0f)),
                scale = Track.CurveRange((0f, 0.7f, 1.1f), (1f, 1.8f, 2.7f)),
                spinSpeed = Track.Range(-120, 120),
                fields = { new Field(FieldType.Friction, Track.Const(0.05f), Track.Const(0.05f)) },
            });

            // 2) Quầng sáng chói ở tâm (cộng sáng)
            list.Add(new EmitterDef
            {
                name = "Core",
                image = "IMAGE_BOSSEXPLOSION1",
                sortingOffset = 2,
                additive = true,
                systemDuration = 2,
                spawnRate = Track.Const(100000),
                spawnMaxLaunched = 1,
                particleDuration = Track.Const(40),
                scale = Track.Curve((0f, 1.3f), (1f, 3.4f)),
                alpha = Track.Curve((0f, 1f), (0.4f, 0.7f), (1f, 0f)),
                red = Track.Const(1f),
                green = Track.Curve((0f, 0.75f), (1f, 0.25f)),
                blue = Track.Curve((0f, 0.3f), (1f, 0.05f)),
            });

            // 3) Tia sáng xoay (cộng sáng)
            list.Add(new EmitterDef
            {
                name = "Rays",
                image = "IMAGE_BOSSEXPLOSION3",
                sortingOffset = 3,
                additive = true,
                systemDuration = 2,
                spawnRate = Track.Const(100000),
                spawnMaxLaunched = 1,
                particleDuration = Track.Const(28),
                scale = Track.Curve((0f, 1.0f), (1f, 3.6f)),
                alpha = Track.Curve((0f, 0.9f), (0.5f, 0.6f), (1f, 0f)),
                spinSpeed = Track.Const(300),
                red = Track.Const(1f),
                green = Track.Const(0.6f),
                blue = Track.Const(0.15f),
            });

            // 4) Tia lửa nhỏ bắn ra, rơi xuống
            list.Add(new EmitterDef
            {
                name = "Sparks",
                image = "IMAGE_ZOMBIE_BOSS_FIREBALL_ADDITIVEPARTICLE",
                sortingOffset = 4,
                additive = true,
                systemDuration = 2,
                spawnRate = Track.Const(100000),
                spawnMaxLaunched = 28,
                particleDuration = Track.Range(40, 75),
                launchSpeed = Track.Range(350, 950),
                scale = Track.CurveRange((0f, 1.2f, 2.2f), (1f, 0.3f, 0.6f)),
                red = Track.Const(1f),
                green = Track.Curve((0f, 0.8f), (1f, 0.2f)),
                blue = Track.Const(0.1f),
                alpha = Track.Curve((0f, 1f), (0.7f, 0.8f), (1f, 0f)),
                fields =
                {
                    new Field(FieldType.Friction, Track.Const(0.03f), Track.Const(0.03f)),
                    new Field(FieldType.Acceleration, Track.Const(0), Track.Const(12)),
                },
            });

            return list;
        }
    }
}