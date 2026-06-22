using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
// 런타임 컴포넌트(DoTweenUtility 네임스페이스의 DoTweenTimeline 클래스)를
// 코드 전반에서 짧게 참조하기 위한 별칭.
using Timeline = global::DoTweenUtility.DoTweenTimeline;

namespace DoTweenUtility.Editor
{
    /// <summary>
    /// <see cref="Timeline"/>의 커스텀 Inspector. 가로 시간축 위에 클립을 막대로 그려
    /// 드래그로 배치/길이조절하는 타임라인 트랙 뷰 + 선택 클립의 종류별 상세 편집 패널 +
    /// 이벤트 마커 + 미리보기 재생 + Code View를 제공한다.
    ///
    /// 기능별로 partial 파일로 분리되어 있다:
    /// <list type="bullet">
    /// <item>DoTweenTimelineEditor.cs — 코어(필드·라이프사이클·OnInspectorGUI·툴바·전역 설정)</item>
    /// <item>.Track.cs — 타임라인 트랙 캔버스(룰러/클립/마커 플래그/플레이헤드/마우스)</item>
    /// <item>.ClipInspector.cs — 선택 클립 패널·클립 추가/복제/삭제·현재값 캡처</item>
    /// <item>.Markers.cs — 이벤트 마커 섹션·추가/삭제</item>
    /// <item>.Preview.cs — 프리뷰 컨트롤·재생 루프·스냅샷</item>
    /// <item>.CodeView.cs — DOTween C# 코드 생성</item>
    /// <item>.Styles.cs — 스타일/텍스처 캐시·상단 배너</item>
    /// </list>
    /// 안드로이드 토글 스위치는 재사용 가능한 <see cref="SwitchGui"/>로 분리되어 있다.
    /// </summary>
    [CustomEditor(typeof(Timeline))]
    public partial class DoTweenTimelineEditor : UnityEditor.Editor
    {
        // ── 레이아웃 상수 ──
        const float RulerHeight = 18f;
        const float RowHeight = 22f;
        const float RowGap = 2f;
        const float ScrollbarHeight = 14f;
        const float EdgeGrip = 6f;       // 우측 끝 리사이즈 감지 폭(px)
        const float MinPps = 20f;        // 최소 줌(px/초)
        const float MaxPps = 600f;       // 최대 줌

        // ── 에디터 영속 상태 ──
        Timeline _t;
        int _selected = -1;
        float _pps = 80f;                // pixels per second(줌)
        float _scrollX = 0f;

        // 이벤트 섹션 접기/펼치기 상태
        bool _clipEventsFoldout;
        bool _timelineEventsFoldout;
        bool _codeViewFoldout;
        bool _markersFoldout = true;
        Vector2 _codeScroll;

        // 선택된 마커 인덱스(-1 = 없음). 클립 선택(_selected)과 별개.
        int _selectedMarker = -1;

        // 드래그 상태
        enum DragMode { None, Move, Resize, Marker }
        int _dragIndex = -1;          // 클립 드래그 시 클립 인덱스 / 마커 드래그 시 마커 인덱스
        DragMode _dragMode = DragMode.None;
        float _dragMouseStartX;
        float _dragValueStart;           // 이동: startTime / 리사이즈: duration
        int _timelineCtrlId;

        // 미리보기(Edit 모드)
        bool _previewing;
        bool _previewPlaying;
        float _previewTime;     // 현재 루프 사이클 내 위치(Goto 대상 / 플레이헤드 표시)
        float _previewElapsed;  // 루프 계산용 누적 경과 시간(전체)
        double _lastUpdate;
        List<Action> _restore;

        // 토글 애니메이션 시간 델타(SwitchGui로 전달)
        double _lastTime;
        float _dt;
        bool _animating;

        void OnEnable()
        {
            _t = (Timeline)target;
            _lastTime = EditorApplication.timeSinceStartup;
        }

        void OnDisable()
        {
            if (_previewing) EndPreview();
        }

        // 프리뷰 재생 중이거나, Play 모드에서 시퀀스가 도는 동안에는
        // 플레이헤드(빨간 인디케이터)가 매 프레임 갱신되도록 상시 리페인트한다.
        public override bool RequiresConstantRepaint()
        {
            if (_previewing && _previewPlaying) return true;
            if (Application.isPlaying && _t != null && _t.IsPlaying) return true;
            return false;
        }

        public override void OnInspectorGUI()
        {
            _t = (Timeline)target;
            EnsureStyles();

            // 토글 애니메이션용 시간 델타(유휴 후 큰 점프 방지로 clamp)
            double now = EditorApplication.timeSinceStartup;
            _dt = Mathf.Min((float)(now - _lastTime), 0.05f);
            _lastTime = now;
            _animating = false;

            DrawBanner();

            // 프리뷰(Preview Mode) 중에는 편집을 잠근다. 타임라인 트랙은 플레이헤드 표시와
            // 룰러 스크럽을 위해 활성 상태로 두되(클립 선택/드래그는 HandleTimelineMouse에서 차단),
            // 나머지 편집 UI(툴바·선택 클립·전역 설정·코드 뷰)는 DisabledScope로 비활성화한다.
            using (new EditorGUI.DisabledScope(_previewing))
                DrawToolbar();

            EditorGUILayout.Space(4);
            DrawTimeline();
            EditorGUILayout.Space(6);

            using (new EditorGUI.DisabledScope(_previewing))
            {
                DrawSelectedClip();
                EditorGUILayout.Space(6);
                DrawMarkersSection();
                EditorGUILayout.Space(6);
                DrawGlobalSettings();
            }

            EditorGUILayout.Space(6);
            DrawPreviewControls();
            EditorGUILayout.Space(6);

            using (new EditorGUI.DisabledScope(_previewing))
                DrawCodeView();

            // 애니메이션 진행 중이면 다음 프레임 리페인트 요청(스무스 이동)
            if (_animating) Repaint();
        }

        // ──────────────────────────────────────────────────────────────────
        // 툴바
        // ──────────────────────────────────────────────────────────────────

        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("＋ Add Clip", EditorStyles.toolbarButton, GUILayout.Width(90)))
                    AddClip();

                using (new EditorGUI.DisabledScope(_selected < 0))
                {
                    if (GUILayout.Button("Duplicate", EditorStyles.toolbarButton, GUILayout.Width(70)))
                        DuplicateClip(_selected);
                    if (GUILayout.Button("Delete", EditorStyles.toolbarButton, GUILayout.Width(55)))
                        DeleteClip(_selected);
                }

                GUILayout.FlexibleSpace();

                GUILayout.Label("Zoom", GUILayout.Width(36));
                _pps = GUILayout.HorizontalSlider(_pps, MinPps, MaxPps, GUILayout.Width(90));
                GUILayout.Label($"{_t.TotalDuration:0.00}s", EditorStyles.miniLabel, GUILayout.Width(48));
            }
        }

        // ──────────────────────────────────────────────────────────────────
        // 전역 설정
        // ──────────────────────────────────────────────────────────────────

        void DrawGlobalSettings()
        {
            serializedObject.Update();
            EditorGUILayout.LabelField("Timeline Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("autoPlay"));

            var ignoreProp = serializedObject.FindProperty("ignoreTimeScale");
            EditorGUI.BeginChangeCheck();
            bool ignoreVal = AndroidToggle("Ignore Time Scale", ignoreProp.boolValue);
            if (EditorGUI.EndChangeCheck()) ignoreProp.boolValue = ignoreVal;

            var killProp = serializedObject.FindProperty("killConflictingOnPlay");
            EditorGUI.BeginChangeCheck();
            bool killVal = AndroidToggle("Kill Conflicting Tweens On Play", killProp.boolValue);
            if (EditorGUI.EndChangeCheck()) killProp.boolValue = killVal;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("timelineLoops"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("timelineLoopType"));

            EditorGUILayout.Space(2);
            _timelineEventsFoldout = EditorGUILayout.Foldout(_timelineEventsFoldout, "Timeline Events", true, _sectionFoldout);
            if (_timelineEventsFoldout)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("onStart"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("onUpdate"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("onFinish"));
            }
            serializedObject.ApplyModifiedProperties();
        }

        // 안드로이드 스타일 On/Off 토글 — 재사용 클래스 SwitchGui로 위임하는 얇은 래퍼.
        bool AndroidToggle(string label, bool value) => SwitchGui.Toggle(label, value, _dt, ref _animating);
    }
}
