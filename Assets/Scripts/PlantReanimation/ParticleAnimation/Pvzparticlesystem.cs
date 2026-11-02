using System;
using System.Collections.Generic;
using UnityEngine;

namespace PvzFx
{
    // =====================================================================
    //  Track: giá trị thay đổi theo thời gian 0..1, mỗi keyframe có khoảng [lo..hi]
    //  mỗi particle bốc ngẫu nhiên 1 số "interp" trong 0..1 để chọn giữa lo và hi
    //  (giống FloatTrackEvaluate trong PvzpParticle.cpp)
    // =====================================================================
    public sealed class Track
    {
        struct Key { public float t, lo, hi; }
        Key[] _keys;

        public static Track Const(float v) => new Track { _keys = new[] { new Key { t = 0, lo = v, hi = v } } };
        public static Track Range(float lo, float hi) => new Track { _keys = new[] { new Key { t = 0, lo = lo, hi = hi } } };
        /// <summary>Đường cong: (thời gian 0..1, giá trị)</summary>
        public static Track Curve(params (float t, float v)[] k)
        {
            var keys = new Key[k.Length];
            for (int i = 0; i < k.Length; i++) keys[i] = new Key { t = k[i].t, lo = k[i].v, hi = k[i].v };
            return new Track { _keys = keys };
        }
        /// <summary>Đường cong có khoảng ngẫu nhiên: (thời gian, lo, hi)</summary>
        public static Track CurveRange(params (float t, float lo, float hi)[] k)
        {
            var keys = new Key[k.Length];
            for (int i = 0; i < k.Length; i++) keys[i] = new Key { t = k[i].t, lo = k[i].lo, hi = k[i].hi };
            return new Track { _keys = keys };
        }

        static float V(Key k, float r) => Mathf.Lerp(k.lo, k.hi, r);

        public float Eval(float time, float interp)
        {
            int n = _keys.Length;
            if (n == 1 || time <= _keys[0].t) return V(_keys[0], interp);
            for (int i = 1; i < n; i++)
            {
                if (time <= _keys[i].t)
                {
                    Key a = _keys[i - 1], b = _keys[i];
                    float f = (time - a.t) / Mathf.Max(1e-6f, b.t - a.t);
                    return Mathf.Lerp(V(a, interp), V(b, interp), f);
                }
            }
            return V(_keys[n - 1], interp);
        }
    }

    // =====================================================================
    //  Định nghĩa emitter (tương đương PvzpEmitterDefinition trong file C++)
    // =====================================================================
    public enum FieldType { Acceleration, Friction, GroundConstraint }

    public sealed class Field
    {
        public FieldType type;
        public Track x = Track.Const(0), y = Track.Const(0);
        public Field(FieldType t, Track x, Track y) { type = t; this.x = x; this.y = y; }
    }

    public sealed class EmitterDef
    {
        public string name;
        public string image;               // vd "IMAGE_POTATOMINE_PARTICLES"
        public bool additive;              // cộng sáng (flash, lửa...)
        public bool randomLaunchSpin;      // góc xoay ban đầu ngẫu nhiên
        public bool animated;              // true: frame chạy theo tuổi particle; false: chọn frame ngẫu nhiên 1 lần
        public int sortingOffset;          // thứ tự vẽ (lớn = vẽ đè lên trên)

        public int systemDuration = 100;   // số "tick" (1 tick = 0.01s, PvZ chạy 100 tick/giây)
        public bool systemLoops;
        public Track spawnRate = Track.Const(0);   // số particle/giây (theo thời gian của system)
        public int spawnMaxLaunched = -1;          // tổng số particle tối đa emitter được bắn (-1 = vô hạn)

        public Track particleDuration = Track.Const(100); // tuổi thọ particle (tick)
        public Track launchSpeed = Track.Const(0);        // tốc độ ban đầu (x0.01 px/tick)
        public Track launchAngle;                          // độ. 0 = hướng XUỐNG, 180 = hướng LÊN. null = ngẫu nhiên 360
        public Track emitterRadius = Track.Const(0);
        public Track emitterOffsetX = Track.Const(0), emitterOffsetY = Track.Const(0);

        public Track red = Track.Const(1), green = Track.Const(1), blue = Track.Const(1), alpha = Track.Const(1);
        public Track scale = Track.Const(1), stretch = Track.Const(1);
        public Track spinSpeed = Track.Const(0), spinAngle = Track.Const(0);
        public Track collisionReflect = Track.Const(0), collisionSpin = Track.Const(0);

        public List<Field> fields = new List<Field>();
    }

    // =====================================================================
    //  Runtime
    // =====================================================================
    sealed class Particle
    {
        public Vector2 pos, vel;
        public int age, dur;
        public float t;                 // thời gian chuẩn hoá 0..1
        public float spin, spinVel, lastSpinAngle;
        public int frame;
        public float[] r = new float[10];       // số ngẫu nhiên cho từng track
        public float[] fi;                      // số ngẫu nhiên cho từng field (x,y)
        public SpriteRenderer sr;
    }

    sealed class Emitter
    {
        const int R_RED = 0, R_GREEN = 1, R_BLUE = 2, R_ALPHA = 3, R_SCALE = 4, R_STRETCH = 5,
                  R_SPINSPEED = 6, R_SPINANGLE = 7, R_REFLECT = 8, R_COLLSPIN = 9;

        readonly EmitterDef _def;
        readonly Sprite[] _sprites;
        readonly Transform _parent;
        readonly float _ppu;
        readonly Material _material;
        readonly int _sortingOrder;
        readonly string _sortingLayer;

        readonly List<Particle> _ps = new List<Particle>();
        readonly Stack<SpriteRenderer> _free = new Stack<SpriteRenderer>();

        int _age = -1;
        float _accum, _sysT;
        int _launched;
        bool _spawnEnded;
        readonly float _sysInterp = UnityEngine.Random.value;

        public bool Dead => _spawnEnded && _ps.Count == 0;

        public Emitter(EmitterDef def, PvzAtlas atlas, Transform parent, Material additiveMat, int baseOrder, string sortingLayer)
        {
            _def = def; _parent = parent; _ppu = atlas.PixelsPerUnit;
            _sprites = atlas.Get(def.image);
            if (_sprites == null) _spawnEnded = true;
            _material = def.additive ? additiveMat : null;
            _sortingOrder = baseOrder + def.sortingOffset;
            _sortingLayer = sortingLayer;
        }

        SpriteRenderer GetRenderer()
        {
            if (_free.Count > 0) { var s = _free.Pop(); s.enabled = true; return s; }
            var go = new GameObject("p_" + _def.name);
            go.transform.SetParent(_parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            if (_material != null) sr.sharedMaterial = _material;
            sr.sortingLayerName = _sortingLayer;
            sr.sortingOrder = _sortingOrder;
            return sr;
        }

        /// <summary>1 tick = 0.01s. Thứ tự giống Emitter::Update() trong C++: update particle cũ -> spawn mới.</summary>
        public void Step()
        {
            _age++;
            if (_age >= _def.systemDuration)
            {
                if (_def.systemLoops) _age = 0;
                else { _age = _def.systemDuration - 1; _spawnEnded = true; } // hết giờ: ngừng bắn, particle cũ tự sống nốt
            }
            _sysT = _def.systemDuration > 1 ? _age / (float)(_def.systemDuration - 1) : 0f;

            for (int i = _ps.Count - 1; i >= 0; i--)
            {
                if (!UpdateParticle(_ps[i]))
                {
                    _ps[i].sr.enabled = false;
                    _free.Push(_ps[i].sr);
                    _ps.RemoveAt(i);
                }
            }
            if (!_spawnEnded) UpdateSpawning();
        }

        void UpdateSpawning()
        {
            _accum += _def.spawnRate.Eval(_sysT, _sysInterp) * 0.01f;
            int count = (int)_accum;
            _accum -= count;

            if (_def.spawnMaxLaunched >= 0)
            {
                count = Mathf.Min(count, _def.spawnMaxLaunched - _launched);
            }
            for (int i = 0; i < count; i++) SpawnParticle(i, count);

            if (_def.spawnMaxLaunched >= 0 && _launched >= _def.spawnMaxLaunched) _spawnEnded = true;
        }

        void SpawnParticle(int index, int count)
        {
            var p = new Particle();
            for (int i = 0; i < p.r.Length; i++) p.r[i] = UnityEngine.Random.value;
            p.fi = new float[_def.fields.Count * 2];
            for (int i = 0; i < p.fi.Length; i++) p.fi[i] = UnityEngine.Random.value;

            p.dur = Mathf.Max(1, Mathf.RoundToInt(_def.particleDuration.Eval(_sysT, UnityEngine.Random.value)));
            float speed = _def.launchSpeed.Eval(_sysT, UnityEngine.Random.value) * 0.01f;

            float angle = _def.launchAngle == null
                ? UnityEngine.Random.value * 2f * Mathf.PI
                : _def.launchAngle.Eval(_sysT, UnityEngine.Random.value) * Mathf.Deg2Rad;

            // GIỐNG PvZ: góc 0 chỉ xuống dưới (y dương = xuống màn hình)
            float radius = _def.emitterRadius.Eval(_sysT, UnityEngine.Random.value);
            p.pos = new Vector2(Mathf.Sin(angle) * radius, Mathf.Cos(angle) * radius);
            p.vel = new Vector2(Mathf.Sin(angle) * speed, Mathf.Cos(angle) * speed);
            p.pos.x += _def.emitterOffsetX.Eval(_sysT, UnityEngine.Random.value);
            p.pos.y += _def.emitterOffsetY.Eval(_sysT, UnityEngine.Random.value);

            p.frame = _def.animated ? 0 : UnityEngine.Random.Range(0, _sprites.Length);
            p.spin = _def.randomLaunchSpin ? UnityEngine.Random.value * 2f * Mathf.PI : 0f;
            p.lastSpinAngle = _def.spinAngle.Eval(0f, p.r[R_SPINANGLE]);

            p.sr = GetRenderer();
            _ps.Add(p);
            _launched++;
            UpdateParticle(p); // engine gốc cũng update ngay lần đầu
        }

        bool UpdateParticle(Particle p)
        {
            if (p.age >= p.dur) return false;
            p.t = p.dur > 1 ? p.age / (float)(p.dur - 1) : 0f;

            for (int i = 0; i < _def.fields.Count; i++)
            {
                Field f = _def.fields[i];
                float x = f.x.Eval(p.t, p.fi[i * 2]);
                float y = f.y.Eval(p.t, p.fi[i * 2 + 1]);
                switch (f.type)
                {
                    case FieldType.Acceleration:
                        p.vel.x += 0.01f * x; p.vel.y += 0.01f * y; break;
                    case FieldType.Friction:
                        p.vel.x *= 1f - x; p.vel.y *= 1f - y; break;
                    case FieldType.GroundConstraint:
                        if (p.pos.y > y) // chạm đất (đất ở độ cao y so với tâm emitter)
                        {
                            p.pos.y = y;
                            float reflect = _def.collisionReflect.Eval(p.t, p.r[R_REFLECT]);
                            float cspin = _def.collisionSpin.Eval(p.t, p.r[R_COLLSPIN]) / 1000f;
                            p.spinVel = p.vel.y * cspin;
                            p.vel.x *= reflect;
                            p.vel.y *= -reflect;
                        }
                        break;
                }
            }
            p.pos += p.vel;

            float spinSpeed = _def.spinSpeed.Eval(p.t, p.r[R_SPINSPEED]) * 0.01f;
            float spinAngle = _def.spinAngle.Eval(p.t, p.r[R_SPINANGLE]);
            p.spin += (spinSpeed + spinAngle - p.lastSpinAngle) * Mathf.Deg2Rad + p.spinVel;
            p.lastSpinAngle = spinAngle;

            p.age++;
            return true;
        }

        /// <summary>Đẩy trạng thái particle lên SpriteRenderer.</summary>
        public void Render()
        {
            foreach (Particle p in _ps)
            {
                float a = _def.alpha.Eval(p.t, p.r[R_ALPHA]);
                if (a <= 0f) { p.sr.enabled = false; continue; }
                p.sr.enabled = true;

                int frame = _def.animated
                    ? Mathf.Clamp((int)(p.t * _sprites.Length), 0, _sprites.Length - 1)
                    : p.frame;
                p.sr.sprite = _sprites[frame];
                p.sr.color = new Color(
                    _def.red.Eval(p.t, p.r[R_RED]),
                    _def.green.Eval(p.t, p.r[R_GREEN]),
                    _def.blue.Eval(p.t, p.r[R_BLUE]),
                    a);

                float sc = _def.scale.Eval(p.t, p.r[R_SCALE]);
                float st = _def.stretch.Eval(p.t, p.r[R_STRETCH]);
                Transform tr = p.sr.transform;
                tr.localScale = new Vector3(sc, sc * st, 1f);
                // PvZ: y hướng xuống, Unity: y hướng lên -> đảo dấu y và góc xoay
                tr.localPosition = new Vector3(p.pos.x, -p.pos.y, 0f) / _ppu;
                tr.localRotation = Quaternion.Euler(0f, 0f, -p.spin * Mathf.Rad2Deg);
            }
        }
    }

    /// <summary>Một hiệu ứng đang chạy (gồm nhiều emitter). Tự huỷ khi hết particle.</summary>
    public sealed class PvzParticleEffect : MonoBehaviour
    {
        const float TickTime = 0.01f; // PvZ cập nhật 100 lần/giây
        readonly List<Emitter> _emitters = new List<Emitter>();
        float _acc;

        public static PvzParticleEffect Play(IList<EmitterDef> defs, PvzAtlas atlas, Vector3 worldPos,
            float scale, Material additiveMat, int sortingOrder, string sortingLayer)
        {
            var go = new GameObject("PvzParticleEffect");
            go.transform.position = worldPos;
            go.transform.localScale = Vector3.one * scale;
            var fx = go.AddComponent<PvzParticleEffect>();
            foreach (EmitterDef d in defs)
                fx._emitters.Add(new Emitter(d, atlas, go.transform, additiveMat, sortingOrder, sortingLayer));
            fx.StepAll(); // tick đầu tiên chạy ngay
            fx.RenderAll();
            return fx;
        }

        void StepAll() { foreach (var e in _emitters) e.Step(); }
        void RenderAll() { foreach (var e in _emitters) e.Render(); }

        void Update()
        {
            _acc += Mathf.Min(Time.deltaTime, 0.1f);
            while (_acc >= TickTime) { _acc -= TickTime; StepAll(); }
            RenderAll();

            foreach (var e in _emitters) if (!e.Dead) return;
            Destroy(gameObject);
        }
    }

    // =====================================================================
    //  PRESET: hiệu ứng nổ Potato Mine
    // =====================================================================
    public static class PotatoMineFx
    {
        // Toạ độ tính bằng pixel của atlas 960, gốc (0,0) = tâm vụ nổ, Y DƯƠNG = XUỐNG.
        public static List<EmitterDef> Create(bool includeBlastMark = false)
        {
            var list = new List<EmitterDef>();

            // 0) Vết cháy xém trên đất (tuỳ chọn)
            if (includeBlastMark)
            {
                list.Add(new EmitterDef
                {
                    name = "BlastMark",
                    image = "IMAGE_BLASTMARK",
                    sortingOffset = 0,
                    systemDuration = 2,
                    spawnRate = Track.Const(100000),
                    spawnMaxLaunched = 1,
                    particleDuration = Track.Const(600),
                    emitterOffsetY = Track.Const(20),
                    scale = Track.Const(0.55f),
                    stretch = Track.Const(0.55f),
                    alpha = Track.Curve((0f, 0f), (0.02f, 0.75f), (0.7f, 0.6f), (1f, 0f)),
                });
            }

            // 1) Bụi đất tỏa ngang, bay lên chậm, to dần rồi mờ đi
            list.Add(new EmitterDef
            {
                name = "Dust",
                image = "IMAGE_DUST_PUFFS",
                sortingOffset = 1,
                systemDuration = 2,
                spawnRate = Track.Const(100000),
                spawnMaxLaunched = 9,
                particleDuration = Track.Range(60, 95),
                launchSpeed = Track.Range(100, 320),
                launchAngle = Track.Range(75, 285),           // tỏa sang trái/phải/lên trên
                emitterRadius = Track.Range(0, 18),
                emitterOffsetY = Track.Const(5),
                red = Track.Const(0.78f),
                green = Track.Const(0.64f),
                blue = Track.Const(0.46f),
                alpha = Track.Curve((0f, 0f), (0.08f, 0.85f), (0.55f, 0.5f), (1f, 0f)),
                scale = Track.CurveRange((0f, 0.5f, 0.8f), (1f, 1.6f, 2.4f)),
                spinSpeed = Track.Range(-60, 60),
                randomLaunchSpin = true,
                fields =
                {
                    new Field(FieldType.Friction, Track.Const(0.03f), Track.Const(0.03f)),
                    new Field(FieldType.Acceleration, Track.Const(0), Track.Const(-3)), // hơi bốc lên
                },
            });

            // 2) Mảnh vỡ / cục đất văng ra  <<< phần chính bạn cần
            list.Add(new EmitterDef
            {
                name = "DirtChunks",
                image = "IMAGE_POTATOMINE_PARTICLES",
                sortingOffset = 2,
                randomLaunchSpin = true,
                systemDuration = 2,
                spawnRate = Track.Const(100000),
                spawnMaxLaunched = 16,
                particleDuration = Track.Range(70, 115),
                launchSpeed = Track.Range(250, 680),
                launchAngle = Track.Range(135, 225),          // 180° = thẳng lên, lệch ±45°
                emitterOffsetY = Track.Const(-10),
                scale = Track.Range(0.8f, 1.5f),
                alpha = Track.Curve((0f, 1f), (0.75f, 1f), (1f, 0f)),
                spinSpeed = Track.Range(-500, 500),
                collisionReflect = Track.Range(0.2f, 0.4f),
                collisionSpin = Track.Range(-20, 20),
                fields =
                {
                    new Field(FieldType.Acceleration, Track.Const(0), Track.Const(18)),   // trọng lực
                    new Field(FieldType.Friction, Track.Const(0.01f), Track.Const(0)),
                    new Field(FieldType.GroundConstraint, Track.Const(0), Track.Const(28)), // đất ở y = +28
                },
            });

            // 3) Chớp sáng ngay lúc nổ
            list.Add(new EmitterDef
            {
                name = "Flash",
                image = "IMAGE_POTATOMINEFLASH",
                sortingOffset = 3,
                additive = true,
                systemDuration = 2,
                spawnRate = Track.Const(100000),
                spawnMaxLaunched = 1,
                particleDuration = Track.Const(20),
                emitterOffsetY = Track.Const(-15),
                scale = Track.Curve((0f, 1.5f), (1f, 5f)),
                alpha = Track.Curve((0f, 1f), (0.3f, 0.6f), (1f, 0f)),
                red = Track.Const(1f),
                green = Track.Const(0.85f),
                blue = Track.Const(0.45f),
            });

            return list;
        }
    }
}