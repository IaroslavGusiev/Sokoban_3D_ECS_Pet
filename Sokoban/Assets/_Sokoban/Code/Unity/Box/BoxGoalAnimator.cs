using PrimeTween;
using UnityEngine;

namespace Sokoban.Presentation
{
    [DisallowMultipleComponent]
    public sealed class BoxGoalAnimator : MonoBehaviour
    {
        [SerializeField] private Transform visual;
        [SerializeField] private float durationSeconds = 0.22f;
        [SerializeField] private float height = 0.12f;
        [SerializeField] private float squashAmount = 0.12f;

        private Vector3 _basePosition;
        private Vector3 _baseScale;
        private Sequence _feedback;
        
        private bool _poseCaptured;

        public bool IsPlaying => _feedback.isAlive;
        
        private void Awake() => 
            CapturePose();

        private void OnDisable() => 
            StopAndRestore();

        private void OnDestroy() => 
            StopAndRestore();

        private void Reset() => 
            visual = transform.Find("Visual");

        public void PlayGoalFeedback()
        {
            CapturePose();
            StopAndRestore();

            if (durationSeconds <= 0f)
            {
                return;
            }

            float halfDuration = durationSeconds * 0.5f;
            Vector3 peakPosition = _basePosition + Vector3.up * height;
            Vector3 squash = Vector3.Scale(_baseScale, new Vector3(1f + squashAmount, 1f - squashAmount, 1f + squashAmount));
            Vector3 stretch = Vector3.Scale(_baseScale, new Vector3(1f - squashAmount * 0.5f, 1f + squashAmount * 0.5f, 1f - squashAmount * 0.5f));

            _feedback = Sequence.Create(useUnscaledTime: false, updateType: UpdateType.Update);
            _feedback.isPaused = true;
            _feedback
                .Group(Tween.LocalPosition(visual, _basePosition, peakPosition, halfDuration, Ease.OutQuad))
                .Chain(Tween.LocalPosition(visual, peakPosition, _basePosition, halfDuration, Ease.InQuad))
                .Insert(0f, Tween.Scale(visual, _baseScale, squash, durationSeconds * 0.25f, Ease.OutSine))
                .Insert(durationSeconds * 0.25f, Tween.Scale(visual, squash, stretch, durationSeconds * 0.25f, Ease.InOutSine))
                .Insert(halfDuration, Tween.Scale(visual, stretch, _baseScale, halfDuration, Ease.OutSine))
                .OnComplete(this, animator => animator.RestorePose());
        }

        public void Tick(float deltaTime)
        {
            if (IsPlaying == false || deltaTime == 0f)
            {
                return;
            }

            float nextTime = _feedback.elapsedTime + deltaTime;
            if (nextTime >= _feedback.duration)
            {
                _feedback.Complete();
            }
            else
            {
                _feedback.elapsedTime = nextTime;
            }
        }

        public void StopAndRestore()
        {
            _feedback.Stop();
            _feedback = default;
            RestorePose();
        }

        private void CapturePose()
        {
            if (_poseCaptured)
            {
                return;
            }

            _basePosition = visual.localPosition;
            _baseScale = visual.localScale;
            _poseCaptured = true;
        }

        private void RestorePose()
        {
            if (_poseCaptured && visual != null)
            {
                visual.localPosition = _basePosition;
                visual.localScale = _baseScale;
            }
        }
    }
}
