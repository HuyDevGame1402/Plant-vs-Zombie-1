using System;

namespace PlantReanimation.Core
{
    public enum PlantReanimPlaybackStatus { Stopped, Playing, Paused }

    public struct PlantReanimPlaybackState
    {
        public int currentFrame, nextFrame;
        public float interpolation, elapsedTime, speed;
        public PlantReanimClip clip;
    }

    public class PlantReanimPlayer
    {
        private float fps;
        private PlantReanimClip currentClip;
        private float elapsedTime, speed = 1f;
        private bool isLooping;
        private PlantReanimPlaybackStatus status = PlantReanimPlaybackStatus.Stopped;
        public event Action<PlantReanimPlaybackState> OnFrameUpdated;
        public event Action<PlantReanimClip> OnCompleted;
        public PlantReanimPlaybackStatus Status { get { return status; } }
        public PlantReanimClip CurrentClip { get { return currentClip; } }
        public float Speed { get { return speed; } }
        public float ElapsedTime { get { return elapsedTime; } }
        public bool IsPaused { get { return status == PlantReanimPlaybackStatus.Paused; } }
        public bool IsStopped { get { return status == PlantReanimPlaybackStatus.Stopped; } }
        public bool IsPlaying { get { return status == PlantReanimPlaybackStatus.Playing; } }

        public void Initialize(float animationFPS)
        {
            if (animationFPS <= 0f || float.IsNaN(animationFPS) || float.IsInfinity(animationFPS)) throw new ArgumentOutOfRangeException(nameof(animationFPS));
            fps = animationFPS; Stop();
        }
        public void Play(PlantReanimClip clip) { Play(clip, clip != null && clip.loop); }
        public void Play(PlantReanimClip clip, bool loop)
        {
            ValidateClip(clip); currentClip = clip; isLooping = loop; elapsedTime = 0f; status = PlantReanimPlaybackStatus.Playing; NotifyCurrentFrame();
        }
        public void PlayOnce(PlantReanimClip clip) { Play(clip, false); }
        public void Update(float deltaTime)
        {
            if (status != PlantReanimPlaybackStatus.Playing || currentClip == null || deltaTime <= 0f || speed <= 0f) return;
            float duration = currentClip.Duration(fps);
            if (duration <= 0f) { Finish(); return; }
            elapsedTime += deltaTime * speed;
            if (isLooping) elapsedTime %= duration;
            else if (elapsedTime >= duration) { elapsedTime = duration; NotifyCurrentFrame(); Finish(); return; }
            NotifyCurrentFrame();
        }
        public void Pause() { if (status == PlantReanimPlaybackStatus.Playing) status = PlantReanimPlaybackStatus.Paused; }
        public void Resume() { if (status == PlantReanimPlaybackStatus.Paused) status = PlantReanimPlaybackStatus.Playing; }
        public void Stop() { status = PlantReanimPlaybackStatus.Stopped; elapsedTime = 0f; currentClip = null; }
        public void SetSpeed(float newSpeed) { speed = Math.Max(0f, newSpeed); }
        public void Seek(float time)
        {
            if (currentClip == null) return;
            float duration = currentClip.Duration(fps);
            elapsedTime = isLooping && duration > 0f ? Math.Max(0f, time) % duration : Math.Max(0f, Math.Min(time, duration));
            NotifyCurrentFrame();
        }
        public bool IsPlayingClip(string clipName) { return IsPlaying && currentClip != null && string.Equals(currentClip.name, clipName, StringComparison.OrdinalIgnoreCase); }

        private void NotifyCurrentFrame()
        {
            if (currentClip == null || fps <= 0f || currentClip.FrameCount <= 0) return;
            int intervals = Math.Max(0, currentClip.FrameCount - 1);
            float position = Math.Max(0f, Math.Min(elapsedTime * fps, intervals));
            int localA = (int)Math.Floor(position);
            float t = position - localA;
            int localB = Math.Min(localA + 1, intervals);
            OnFrameUpdated?.Invoke(new PlantReanimPlaybackState { currentFrame = currentClip.startFrame + localA, nextFrame = currentClip.startFrame + localB, interpolation = t, elapsedTime = elapsedTime, speed = speed, clip = currentClip });
        }
        private void Finish()
        {
            PlantReanimClip completed = currentClip;
            status = PlantReanimPlaybackStatus.Stopped;
            OnCompleted?.Invoke(completed);
        }
        private void ValidateClip(PlantReanimClip clip)
        {
            if (clip == null) throw new ArgumentNullException(nameof(clip));
            if (fps <= 0f) throw new InvalidOperationException("PlantReanimPlayer chưa được Initialize().");
            if (clip.startFrame < 0 || clip.endFrame < clip.startFrame) throw new ArgumentException("Clip frame range không hợp lệ.");
        }
    }
}
