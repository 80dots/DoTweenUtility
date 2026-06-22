using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

namespace DoTweenUtility
{
    // 직렬화 데이터 모델: 열거형 + Clip + Marker(+ 콜백 타입).
    // 직렬화 키 = 필드/enum 값이므로 이름·정수값 변경은 기존 씬 데이터를 깬다.
    public partial class DoTweenTimeline
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
            [Tooltip("Enable/disable this clip. Disabled clips are skipped when the timeline is built/played")]
            public bool enabled = true;

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

        /// <summary>
        /// 타임라인 특정 시각에 놓이는 이벤트 마커. 재생 중 플레이헤드가 이 시각에
        /// 도달하면 <see cref="onMarker"/>가 호출되며, 인자로 <see cref="name"/>이 전달된다.
        /// 직렬화 키 = 필드 이름이므로 이름 변경은 씬 데이터를 깬다.
        /// </summary>
        [Serializable]
        public class Marker
        {
            [Tooltip("Marker name. Passed as the string argument to the onMarker callback")]
            public string name = "Marker";

            [Tooltip("Time (seconds) on the timeline at which onMarker fires")]
            public float time = 0f;
        }

        /// <summary>마커 콜백 형식. 인자는 마커 이름(string).</summary>
        [Serializable]
        public class MarkerEvent : UnityEvent<string> { }
    }
}
