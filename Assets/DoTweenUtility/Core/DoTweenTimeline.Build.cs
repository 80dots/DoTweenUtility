using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace DoTweenUtility
{
    // 시퀀스 빌드 로직: clips/markers → 하나의 DOTween Sequence 합성 + 충돌 트윈 레지스트리.
    public partial class DoTweenTimeline
    {
        // 이 타임라인이 충돌 레지스트리에 등록한 (키, 트윈) 목록 — Kill 시 정리한다.
        readonly List<KeyValuePair<string, Tween>> _registered = new List<KeyValuePair<string, Tween>>();

        // 모든 활성 클립 트윈을 충돌 키로 모아두는 전역 레지스트리.
        // Sequence에 중첩된 트윈은 DOTween.Kill(id)로 못 찾으므로, 레퍼런스를 직접 들고 Kill한다.
        static readonly Dictionary<string, List<Tween>> s_activeByKey = new Dictionary<string, List<Tween>>();

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
                    if (clip == null || !clip.enabled || clip.target == null) continue;
                    KillConflicting(ConflictKey(clip));
                }
            }

            foreach (var clip in clips)
            {
                if (clip == null || !clip.enabled || clip.target == null) continue;

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
                {
                    // 시퀀스에 중첩된 트윈은 무한 루프(-1)가 허용되지 않는다(DOTween 경고:
                    // "Infinite loops aren't allowed inside a Sequence ... changed to int.MaxValue").
                    // 무한이면 직접 int.MaxValue로 클램프해 사실상 무한처럼 돌리되 경고를 막는다.
                    int clipLoops = clip.loops < 0 ? int.MaxValue : clip.loops;
                    tween.SetLoops(clipLoops, clip.loopType);
                }
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

            // 이벤트 마커: 각 마커 시각에 콜백을 삽입한다. 재생 중 플레이헤드가 그 시각에
            // 도달하면 onMarker(마커이름)이 호출된다(루프 시 매 사이클마다 발화).
            if (markers != null)
            {
                foreach (var marker in markers)
                {
                    if (marker == null) continue;
                    Marker captured = marker; // 클로저가 올바른 마커를 참조하도록 캡처
                    _sequence.InsertCallback(Mathf.Max(0f, marker.time),
                        () => onMarker?.Invoke(captured.name));
                }
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
    }
}
