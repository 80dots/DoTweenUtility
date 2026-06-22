using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

namespace DoTweenUtility
{
    /// <summary>
    /// Scene 내 임의의 GameObject/Component에 대한 DOTween 트윈을 Inspector GUI로
    /// 생성·편집·재생하기 위한 런타임 컨테이너 컴포넌트.
    ///
    /// 동작: <see cref="Clip"/> 목록을 들고 있다가 재생 시 하나의 DOTween
    /// <see cref="Sequence"/>로 합성한다. 각 클립은 자신의 <see cref="Clip.startTime"/>
    /// 위치에 <c>Insert</c> 되므로 진짜 "타임라인"처럼 동작한다.
    ///
    /// 기능별로 partial 파일로 분리되어 있다:
    /// <list type="bullet">
    /// <item>DoTweenTimeline.cs — 코어(직렬화 필드·런타임 상태·라이프사이클·제어 API)</item>
    /// <item>DoTweenTimeline.Data.cs — 데이터 모델(enum/Clip/Marker)</item>
    /// <item>DoTweenTimeline.Build.cs — 시퀀스 빌드 + 충돌 트윈 레지스트리</item>
    /// </list>
    /// GUI(타임라인 편집기)는 별도의 Editor 스크립트에서 이 데이터를 조작한다.
    /// </summary>
    [AddComponentMenu("DOTween/DoTween Timeline")]
    [DisallowMultipleComponent]
    public partial class DoTweenTimeline : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────────
        // 직렬화 필드
        // ──────────────────────────────────────────────────────────────────

        [Header("Playback")]
        public AutoPlayMode autoPlay = AutoPlayMode.Manual;

        [Tooltip("Ignore Time.timeScale (keeps running while paused). Enable to use unscaled time like the project glow")]
        public bool ignoreTimeScale = false;

        [Tooltip("On Play, kill other timelines' tweens that target the SAME component with the SAME TweenType (last play wins)")]
        public bool killConflictingOnPlay = false;

        [Tooltip("Loop count for the whole timeline (-1 = infinite, 1 = no loop)")]
        public int timelineLoops = 1;

        public LoopType timelineLoopType = LoopType.Restart;

        [Header("Clips")]
        public List<Clip> clips = new List<Clip>();

        [Header("Markers")]
        [Tooltip("Event markers placed on the timeline. onMarker fires (with the marker name) when the playhead reaches each one")]
        public List<Marker> markers = new List<Marker>();

        [Header("Events")]
        [Tooltip("Invoked when the timeline starts")]
        public UnityEvent onStart;
        [Tooltip("Invoked every frame while the timeline updates")]
        public UnityEvent onUpdate;
        [Tooltip("Invoked when the timeline completes")]
        public UnityEvent onFinish;
        [Tooltip("Invoked when the playhead reaches a marker; receives the marker's name as a string argument")]
        public MarkerEvent onMarker;

        // ──────────────────────────────────────────────────────────────────
        // 런타임 상태
        // ──────────────────────────────────────────────────────────────────

        Sequence _sequence;

        /// <summary>현재 빌드된 시퀀스. 재생 중이 아니면 null일 수 있다.</summary>
        public Sequence Sequence => _sequence;

        public bool IsPlaying => _sequence != null && _sequence.IsActive() && _sequence.IsPlaying();

        // ──────────────────────────────────────────────────────────────────
        // 라이프사이클
        // ──────────────────────────────────────────────────────────────────

        void Awake()
        {
            if (autoPlay == AutoPlayMode.OnAwake) Play();
        }

        void Start()
        {
            if (autoPlay == AutoPlayMode.OnStart) Play();
        }

        void OnEnable()
        {
            if (autoPlay == AutoPlayMode.OnEnable) Play();
        }

        void OnDisable()
        {
            // 비활성화 시 트윈 정리(타깃이 사라진 뒤 진행되어 에러나는 것 방지)
            Kill();
        }

        void OnDestroy()
        {
            Kill();
        }

        // ──────────────────────────────────────────────────────────────────
        // 퍼블릭 제어 API
        // ──────────────────────────────────────────────────────────────────

        /// <summary>시퀀스를 (재)빌드하고 처음부터 재생한다.</summary>
        public void Play()
        {
            Build();
            _sequence?.Restart();
        }

        /// <summary>이미 빌드된 시퀀스를 처음부터 다시 재생한다.</summary>
        public void Restart()
        {
            if (_sequence == null || !_sequence.IsActive()) Build();
            _sequence?.Restart();
        }

        public void Pause() => _sequence?.Pause();

        public void Resume() => _sequence?.Play();

        public void TogglePause() => _sequence?.TogglePause();

        /// <summary>시퀀스를 정지·해제한다.</summary>
        public void Kill()
        {
            UnregisterAll();
            if (_sequence != null && _sequence.IsActive())
                _sequence.Kill();
            _sequence = null;
        }

        /// <summary>모든 트윈을 즉시 끝 상태로 보낸다.</summary>
        public void Complete()
        {
            if (_sequence == null || !_sequence.IsActive()) Build();
            _sequence?.Complete();
        }

        // ──────────────────────────────────────────────────────────────────
        // 유틸 (Editor GUI에서 사용)
        // ──────────────────────────────────────────────────────────────────

        /// <summary>모든 클립이 끝나는 가장 늦은 시각(타임라인 전체 길이).</summary>
        public float TotalDuration
        {
            get
            {
                float max = 0f;
                foreach (var c in clips)
                {
                    if (c == null) continue;
                    float end = c.startTime + Mathf.Max(0f, c.duration);
                    if (end > max) max = end;
                }
                return max;
            }
        }
    }
}
