using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

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
    /// 이 파일은 데이터 모델 + 시퀀스 빌드 로직만 담당한다.
    /// GUI(타임라인 편집기)는 별도의 Editor 스크립트에서 이 데이터를 조작한다.
    /// </summary>
    [AddComponentMenu("DOTween/DoTween Timeline")]
    [DisallowMultipleComponent]
    public class DoTweenTimeline : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────────
        // 열거형
        // ──────────────────────────────────────────────────────────────────

        /// <summary>타임라인이 언제 자동 재생될지.</summary>
        public enum AutoPlayMode
        {
            Manual,   // 코드/이벤트로 Play() 직접 호출
            OnAwake,
            OnStart,
            OnEnable,
        }

        /// <summary>
        /// 클립이 어떤 트윈을 만들지 결정한다.
        /// 값(정수)은 씬/프리팹에 직렬화되므로 <b>중간 삽입 금지, 항상 끝에 추가</b>한다.
        /// </summary>
        public enum TweenType
        {
            // Transform (world)
            Move = 0,
            MoveX = 1,
            MoveY = 2,
            MoveZ = 3,
            // Transform (local)
            LocalMove = 10,
            LocalMoveX = 11,
            LocalMoveY = 12,
            LocalMoveZ = 13,
            // Rotation
            Rotate = 20,        // world euler
            LocalRotate = 21,   // local euler
            // Scale
            Scale = 30,
            ScaleX = 31,
            ScaleY = 32,
            ScaleZ = 33,
            // RectTransform (UGUI)
            AnchorPos = 40,
            AnchorPosX = 41,
            AnchorPosY = 42,
            SizeDelta = 43,
            // Color / Fade
            CanvasGroupFade = 50,
            GraphicColor = 51,  // Image, RawImage, Text 등 Graphic 파생
            GraphicFade = 52,
            SpriteColor = 53,
            SpriteFade = 54,
        }

        // ──────────────────────────────────────────────────────────────────
        // 클립 데이터
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// 하나의 트윈 정의. 타깃·종류·목표값·타이밍·이징을 담는다.
        /// 직렬화 키 = 필드 이름이므로 이름 변경은 씬 데이터를 깬다.
        /// </summary>
        [Serializable]
        public class Clip
        {
            [Tooltip("Label for identifying this clip in the timeline/Inspector")]
            public string label = "Clip";

            [Tooltip("Target GameObject the tween is applied to")]
            public GameObject target;

            [Tooltip("Specific component on the target to tween (selected in the Inspector)")]
            public Component targetComponent;

            public TweenType tweenType = TweenType.Move;

            [Header("Timing")]
            [Tooltip("Time (seconds) at which this tween starts on the timeline")]
            public float startTime = 0f;

            [Tooltip("Tween duration (seconds)")]
            public float duration = 1f;

            [Header("Target value (the field used depends on tweenType)")]
            public Vector3 vectorValue = Vector3.zero; // Move/Rotate/Scale/AnchorPos/SizeDelta
            public float floatValue = 0f;              // 단일 축(MoveX 등) / Fade
            public Color colorValue = Color.white;     // GraphicColor / SpriteColor

            [Header("Options")]
            public Ease ease = Ease.OutQuad;

            [Tooltip("Use an AnimationCurve instead of the Ease enum")]
            public bool useCurveEase = false;
            [Tooltip("Custom ease curve (time 0..1 → eased value). Used when Use Curve Ease is on")]
            public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

            [Tooltip("From mode: tween from the set value to the current value, instead of current to set")]
            public bool from = false;

            [Tooltip("Treat the target value as an offset relative to the current value, not an absolute value")]
            public bool relative = false;

            [Tooltip("Snap to integer pixels (Move/AnchorPos types only)")]
            public bool snapping = false;

            [Tooltip("Rotation interpolation mode for Rotate types")]
            public RotateMode rotateMode = RotateMode.Fast;

            [Header("Override Start Value")]
            [Tooltip("If on, force the tween's start value to the values below when it begins (DOTween ChangeStartValue)")]
            public bool overrideStart = false;
            public Vector3 fromVectorValue = Vector3.zero; // Move/Rotate/Scale/AnchorPos/SizeDelta
            public float fromFloatValue = 0f;              // 단일 축 / Fade
            public Color fromColorValue = Color.white;     // GraphicColor / SpriteColor

            [Header("Loop")]
            [Tooltip("Loop count for this clip only (-1 = infinite, 1 = no loop)")]
            public int loops = 1;

            public LoopType loopType = LoopType.Restart;

            [Header("Events")]
            [Tooltip("Invoked when this clip's tween starts")]
            public UnityEvent onStart;
            [Tooltip("Invoked every frame while this clip's tween updates")]
            public UnityEvent onUpdate;
            [Tooltip("Invoked when this clip's tween completes")]
            public UnityEvent onFinish;
        }

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

        [Header("Events")]
        [Tooltip("Invoked when the timeline starts")]
        public UnityEvent onStart;
        [Tooltip("Invoked every frame while the timeline updates")]
        public UnityEvent onUpdate;
        [Tooltip("Invoked when the timeline completes")]
        public UnityEvent onFinish;

        // ──────────────────────────────────────────────────────────────────
        // 런타임 상태
        // ──────────────────────────────────────────────────────────────────

        Sequence _sequence;

        /// <summary>현재 빌드된 시퀀스. 재생 중이 아니면 null일 수 있다.</summary>
        public Sequence Sequence => _sequence;

        public bool IsPlaying => _sequence != null && _sequence.IsActive() && _sequence.IsPlaying();

        // 이 타임라인이 충돌 레지스트리에 등록한 (키, 트윈) 목록 — Kill 시 정리한다.
        readonly List<KeyValuePair<string, Tween>> _registered = new List<KeyValuePair<string, Tween>>();

        // 모든 활성 클립 트윈을 충돌 키로 모아두는 전역 레지스트리.
        // Sequence에 중첩된 트윈은 DOTween.Kill(id)로 못 찾으므로, 레퍼런스를 직접 들고 Kill한다.
        static readonly Dictionary<string, List<Tween>> s_activeByKey = new Dictionary<string, List<Tween>>();

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
        // 시퀀스 빌드
        // ──────────────────────────────────────────────────────────────────

        /// <summary>클립 목록으로부터 새 시퀀스를 만든다. 기존 시퀀스는 폐기.</summary>
        public void Build()
        {
            Kill();

            _sequence = DOTween.Sequence();
            _sequence.SetAutoKill(false);
            _sequence.SetUpdate(ignoreTimeScale);

            // 충돌 트윈 정리: 같은 컴포넌트 + 같은 TweenType을 노리는 다른(이미 도는) 트윈을 먼저 제거.
            // 새 트윈 생성 전에 호출하므로 지금부터 만들 트윈은 영향을 받지 않는다.
            if (killConflictingOnPlay)
            {
                foreach (var clip in clips)
                {
                    if (clip == null || clip.target == null) continue;
                    KillConflicting(ConflictKey(clip));
                }
            }

            foreach (var clip in clips)
            {
                if (clip == null || clip.target == null) continue;

                Tweener tween = BuildTween(clip);
                if (tween == null) continue;

                // 충돌 식별용 키(컴포넌트 인스턴스 + TweenType) 부여 + 레지스트리 등록.
                string key = ConflictKey(clip);
                tween.SetId(key);
                Register(key, tween);
                _registered.Add(new KeyValuePair<string, Tween>(key, tween));

                if (clip.useCurveEase && clip.easeCurve != null && clip.easeCurve.length > 0)
                    tween.SetEase(clip.easeCurve);
                else
                    tween.SetEase(clip.ease);
                if (clip.loops != 1)
                    tween.SetLoops(clip.loops, clip.loopType);
                if (clip.relative)
                    tween.SetRelative(true);
                if (clip.from)
                    tween.From();

                // 시작값 강제: DOTween이 startup 시 캡처하는 시작값을 지정값으로 덮어쓴다.
                if (clip.overrideStart)
                    tween.ChangeStartValue(GetStartValueBoxed(clip));

                // 클립별 이벤트 (지역 변수로 캡처해 클로저가 올바른 clip을 참조)
                Clip captured = clip;
                tween.OnStart(() => captured.onStart?.Invoke());
                tween.OnUpdate(() => captured.onUpdate?.Invoke());
                tween.OnComplete(() => captured.onFinish?.Invoke());

                _sequence.Insert(Mathf.Max(0f, clip.startTime), tween);
            }

            _sequence.SetLoops(timelineLoops, timelineLoopType);
            // 타임라인 전체 이벤트
            _sequence.OnStart(() => onStart?.Invoke());
            _sequence.OnUpdate(() => onUpdate?.Invoke());
            _sequence.OnComplete(() => onFinish?.Invoke());
            _sequence.Pause(); // 빌드만 하고 재생은 Play()에서
        }

        /// <summary>단일 클립을 DOTween 트윈으로 변환. 지원 안 되면 null + 경고.</summary>
        Tweener BuildTween(Clip c)
        {
            Transform tr = c.target.transform;
            float d = Mathf.Max(0f, c.duration);

            switch (c.tweenType)
            {
                // ── Transform (world) ──
                case TweenType.Move:  return tr.DOMove(c.vectorValue, d, c.snapping);
                case TweenType.MoveX: return tr.DOMoveX(c.floatValue, d, c.snapping);
                case TweenType.MoveY: return tr.DOMoveY(c.floatValue, d, c.snapping);
                case TweenType.MoveZ: return tr.DOMoveZ(c.floatValue, d, c.snapping);

                // ── Transform (local) ──
                case TweenType.LocalMove:  return tr.DOLocalMove(c.vectorValue, d, c.snapping);
                case TweenType.LocalMoveX: return tr.DOLocalMoveX(c.floatValue, d, c.snapping);
                case TweenType.LocalMoveY: return tr.DOLocalMoveY(c.floatValue, d, c.snapping);
                case TweenType.LocalMoveZ: return tr.DOLocalMoveZ(c.floatValue, d, c.snapping);

                // ── Rotation ──
                case TweenType.Rotate:      return tr.DORotate(c.vectorValue, d, c.rotateMode);
                case TweenType.LocalRotate: return tr.DOLocalRotate(c.vectorValue, d, c.rotateMode);

                // ── Scale ──
                case TweenType.Scale:  return tr.DOScale(c.vectorValue, d);
                case TweenType.ScaleX: return tr.DOScaleX(c.floatValue, d);
                case TweenType.ScaleY: return tr.DOScaleY(c.floatValue, d);
                case TweenType.ScaleZ: return tr.DOScaleZ(c.floatValue, d);

                // ── RectTransform (UGUI) ──
                case TweenType.AnchorPos:
                    return RequireRect(c)?.DOAnchorPos(c.vectorValue, d, c.snapping);
                case TweenType.AnchorPosX:
                    return RequireRect(c)?.DOAnchorPosX(c.floatValue, d, c.snapping);
                case TweenType.AnchorPosY:
                    return RequireRect(c)?.DOAnchorPosY(c.floatValue, d, c.snapping);
                case TweenType.SizeDelta:
                    return RequireRect(c)?.DOSizeDelta(c.vectorValue, d, c.snapping);

                // ── Color / Fade ──
                case TweenType.CanvasGroupFade:
                    return Require<CanvasGroup>(c)?.DOFade(c.floatValue, d);
                case TweenType.GraphicColor:
                    return Require<Graphic>(c)?.DOColor(c.colorValue, d);
                case TweenType.GraphicFade:
                    return Require<Graphic>(c)?.DOFade(c.floatValue, d);
                case TweenType.SpriteColor:
                    return Require<SpriteRenderer>(c)?.DOColor(c.colorValue, d);
                case TweenType.SpriteFade:
                    return Require<SpriteRenderer>(c)?.DOFade(c.floatValue, d);

                default:
                    Debug.LogWarning($"[DoTweenTimeline] 미지원 TweenType: {c.tweenType}", this);
                    return null;
            }
        }

        // 충돌 판별 키: "같은 컴포넌트 인스턴스 + 같은 TweenType"을 유일하게 식별하는 문자열.
        // 이 키로 레지스트리를 조회해 해당 트윈만 Kill하므로, 같은 transform의 다른 TweenType이나
        // 외부의 무관한 트윈에는 영향을 주지 않는다.
        static string ConflictKey(Clip c)
        {
            // 실제로 구동되는 컴포넌트(targetComponent, 없으면 Transform)의 인스턴스 ID 사용
            Component comp = c.targetComponent != null ? c.targetComponent
                           : (c.target != null ? c.target.transform : null);
            // Unity 6에서 GetInstanceID()가 obsolete → GetEntityId() 사용.
            EntityId id = comp != null ? comp.GetEntityId() : default;
            return "DTT:" + id + ":" + ((int)c.tweenType);
        }

        static void Register(string key, Tween t)
        {
            if (!s_activeByKey.TryGetValue(key, out var list))
            {
                list = new List<Tween>();
                s_activeByKey[key] = list;
            }
            list.Add(t);
        }

        static void Unregister(string key, Tween t)
        {
            if (s_activeByKey.TryGetValue(key, out var list))
            {
                list.Remove(t);
                if (list.Count == 0) s_activeByKey.Remove(key);
            }
        }

        // 같은 키(컴포넌트+TweenType)로 등록된, 살아있는 모든 트윈을 레퍼런스로 직접 Kill한다.
        static void KillConflicting(string key)
        {
            if (!s_activeByKey.TryGetValue(key, out var list) || list.Count == 0) return;

            // 복사본으로 순회 (Kill → Unregister가 원본 list를 수정할 수 있음)
            var snapshot = list.ToArray();
            foreach (var t in snapshot)
                if (t != null && t.IsActive()) t.Kill();

            // 죽었거나 정리 안 된 항목 prune
            if (s_activeByKey.TryGetValue(key, out var l))
            {
                l.RemoveAll(x => x == null || !x.IsActive());
                if (l.Count == 0) s_activeByKey.Remove(key);
            }
        }

        void UnregisterAll()
        {
            for (int i = 0; i < _registered.Count; i++)
                Unregister(_registered[i].Key, _registered[i].Value);
            _registered.Clear();
        }

        // overrideStart 용 시작값을 tweenType이 만드는 트윈의 내부 타입에 맞게 박싱한다.
        // (ChangeStartValue(object)는 트윈의 값 타입과 정확히 일치해야 적용된다)
        static object GetStartValueBoxed(Clip c)
        {
            switch (c.tweenType)
            {
                // Vector3 트윈
                case TweenType.Move:
                case TweenType.LocalMove:
                case TweenType.Rotate:
                case TweenType.LocalRotate:
                case TweenType.Scale:
                    return c.fromVectorValue;
                // Vector2 트윈
                case TweenType.AnchorPos:
                case TweenType.SizeDelta:
                    return (Vector2)c.fromVectorValue;
                // Color 트윈
                case TweenType.GraphicColor:
                case TweenType.SpriteColor:
                    return c.fromColorValue;
                // 나머지는 모두 float 트윈
                default:
                    return c.fromFloatValue;
            }
        }

        RectTransform RequireRect(Clip c)
        {
            var rt = c.target.transform as RectTransform;
            if (rt == null)
                Debug.LogWarning($"[DoTweenTimeline] '{c.label}': {c.target.name}에 RectTransform이 없습니다.", this);
            return rt;
        }

        T Require<T>(Clip c) where T : Component
        {
            // 인스펙터에서 명시적으로 고른 컴포넌트를 우선 사용, 없으면 타입으로 탐색(하위호환)
            var comp = c.targetComponent as T;
            if (comp == null)
                comp = c.target.GetComponent<T>();
            if (comp == null)
                Debug.LogWarning($"[DoTweenTimeline] '{c.label}': {c.target.name}에 {typeof(T).Name} 컴포넌트가 없습니다.", this);
            return comp;
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
