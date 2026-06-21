using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
// 런타임 컴포넌트(DoTweenUtility 네임스페이스의 DoTweenTimeline 클래스)를
// 코드 전반에서 짧게 참조하기 위한 별칭.
using Timeline = global::DoTweenUtility.DoTweenTimeline;

namespace DoTweenUtility.Editor
{
    /// <summary>
    /// <see cref="Timeline"/>의 커스텀 Inspector.
    /// 가로 시간축 위에 클립을 막대로 그려 드래그로 배치/길이조절하는 타임라인 트랙 뷰 +
    /// 선택 클립의 종류별 상세 편집 패널 + 미리보기 재생을 제공한다.
    /// </summary>
    [CustomEditor(typeof(Timeline))]
    public class DoTweenTimelineEditor : UnityEditor.Editor
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

        // 드래그 상태
        enum DragMode { None, Move, Resize }
        int _dragIndex = -1;
        DragMode _dragMode = DragMode.None;
        float _dragMouseStartX;
        float _dragValueStart;           // 이동: startTime / 리사이즈: duration
        int _timelineCtrlId;

        // 미리보기(Edit 모드)
        bool _previewing;
        bool _previewPlaying;
        float _previewTime;
        double _lastUpdate;
        List<Action> _restore;

        // ── 정적 스타일 ──
        static GUIStyle _barStyle;
        static GUIStyle _clipLabelStyle;
        static GUIStyle _sectionFoldout;
        static GUIStyle _bannerTitle;
        static GUIStyle _bannerTitleShadow;
        static GUIStyle _bannerSub;
        static Texture2D _bannerTex;

        // 토글 스위치 애니메이션 상태(라벨 → 노브 위치 0..1)
        const float ToggleAnimSpeed = 9f; // 약 0.11초에 끝까지 이동
        readonly Dictionary<string, float> _toggleAnim = new Dictionary<string, float>();
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
            DrawToolbar();
            EditorGUILayout.Space(4);
            DrawTimeline();
            EditorGUILayout.Space(6);
            DrawSelectedClip();
            EditorGUILayout.Space(6);
            DrawGlobalSettings();
            EditorGUILayout.Space(6);
            DrawPreviewControls();

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
        // 타임라인 트랙 뷰
        // ──────────────────────────────────────────────────────────────────

        void DrawTimeline()
        {
            int rows = Mathf.Max(1, _t.clips.Count);
            float viewWidth = EditorGUIUtility.currentViewWidth - 24f;
            float bodyHeight = RulerHeight + rows * (RowHeight + RowGap);
            float contentWidth = Mathf.Max(viewWidth, (_t.TotalDuration + 2f) * _pps);

            Rect area = GUILayoutUtility.GetRect(viewWidth, bodyHeight + ScrollbarHeight);
            Rect viewRect = new Rect(area.x, area.y, viewWidth, bodyHeight);
            Rect scrollRect = new Rect(area.x, area.yMax - ScrollbarHeight, viewWidth, ScrollbarHeight);

            EditorGUI.DrawRect(viewRect, new Color(0.16f, 0.16f, 0.16f));

            // 가로 스크롤바
            float maxScroll = Mathf.Max(0f, contentWidth - viewWidth);
            if (maxScroll > 0f)
                _scrollX = GUI.HorizontalScrollbar(scrollRect, _scrollX, viewWidth, 0f, contentWidth);
            else
                _scrollX = 0f;

            _timelineCtrlId = GUIUtility.GetControlID(FocusType.Passive);

            GUI.BeginGroup(viewRect);
            {
                DrawRuler(viewWidth, bodyHeight);
                DrawClips(viewWidth);
                DrawPlayhead(bodyHeight);
                HandleTimelineMouse(viewWidth, bodyHeight);
            }
            GUI.EndGroup();
        }

        void DrawRuler(float viewWidth, float bodyHeight)
        {
            Rect ruler = new Rect(0, 0, viewWidth, RulerHeight);
            EditorGUI.DrawRect(ruler, new Color(0.22f, 0.22f, 0.22f));

            float interval = NiceInterval(_pps);
            float startT = _scrollX / _pps;
            float endT = (_scrollX + viewWidth) / _pps;
            float firstTick = Mathf.Floor(startT / interval) * interval;

            var tickCol = new Color(1, 1, 1, 0.12f);
            var labelCol = new Color(1, 1, 1, 0.55f);
            var lblStyle = new GUIStyle(EditorStyles.miniLabel);
            lblStyle.normal.textColor = labelCol;

            for (float tk = firstTick; tk <= endT; tk += interval)
            {
                float x = tk * _pps - _scrollX;
                if (x < 0 || x > viewWidth) continue;
                EditorGUI.DrawRect(new Rect(x, 0, 1, bodyHeight), tickCol);
                GUI.Label(new Rect(x + 2, 0, 50, RulerHeight), $"{tk:0.##}", lblStyle);
            }
        }

        void DrawClips(float viewWidth)
        {
            for (int i = 0; i < _t.clips.Count; i++)
            {
                var clip = _t.clips[i];
                if (clip == null) continue;

                float y = RulerHeight + i * (RowHeight + RowGap) + RowGap;
                float x = clip.startTime * _pps - _scrollX;
                float w = Mathf.Max(6f, clip.duration * _pps);
                Rect bar = new Rect(x, y, w, RowHeight);

                // 가시 영역 밖이면 스킵
                if (bar.xMax < 0 || bar.x > viewWidth) continue;

                Color baseCol = CategoryColor(clip.tweenType);
                bool sel = i == _selected;
                Color fill = sel ? Color.Lerp(baseCol, Color.white, 0.25f) : baseCol;
                EditorGUI.DrawRect(bar, fill);
                if (sel)
                {
                    // 선택 테두리
                    EditorGUI.DrawRect(new Rect(bar.x, bar.y, bar.width, 1), Color.white);
                    EditorGUI.DrawRect(new Rect(bar.x, bar.yMax - 1, bar.width, 1), Color.white);
                    EditorGUI.DrawRect(new Rect(bar.x, bar.y, 1, bar.height), Color.white);
                    EditorGUI.DrawRect(new Rect(bar.xMax - 1, bar.y, 1, bar.height), Color.white);
                }

                // 우측 그립 표시
                EditorGUI.DrawRect(new Rect(bar.xMax - 2, bar.y, 2, bar.height), new Color(0, 0, 0, 0.35f));

                string txt = $"{(string.IsNullOrEmpty(clip.label) ? clip.tweenType.ToString() : clip.label)} · {clip.tweenType}";
                GUI.Label(new Rect(bar.x + 4, bar.y, bar.width - 6, bar.height), txt, _clipLabelStyle);

                EditorGUIUtility.AddCursorRect(bar, MouseCursor.Pan);
                EditorGUIUtility.AddCursorRect(new Rect(bar.xMax - EdgeGrip, bar.y, EdgeGrip, bar.height), MouseCursor.ResizeHorizontal);
            }
        }

        void DrawPlayhead(float bodyHeight)
        {
            if (!_previewing) return;
            float x = _previewTime * _pps - _scrollX;
            EditorGUI.DrawRect(new Rect(x, 0, 1.5f, bodyHeight), new Color(1f, 0.3f, 0.2f, 0.9f));
        }

        void HandleTimelineMouse(float viewWidth, float bodyHeight)
        {
            Event e = Event.current;
            Vector2 m = e.mousePosition; // 그룹 로컬 좌표

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button != 0) break;

                    // 룰러 클릭 → 스크럽(미리보기 중일 때)
                    if (m.y < RulerHeight && _previewing)
                    {
                        _previewTime = Mathf.Max(0f, (m.x + _scrollX) / _pps);
                        ApplyPreview();
                        e.Use();
                        break;
                    }

                    int hit = HitTestClip(m, viewWidth);
                    if (hit >= 0)
                    {
                        _selected = hit;
                        var clip = _t.clips[hit];
                        float barX = clip.startTime * _pps - _scrollX;
                        float barW = Mathf.Max(6f, clip.duration * _pps);
                        bool onEdge = m.x >= barX + barW - EdgeGrip;

                        _dragIndex = hit;
                        _dragMode = onEdge ? DragMode.Resize : DragMode.Move;
                        _dragMouseStartX = m.x;
                        _dragValueStart = onEdge ? clip.duration : clip.startTime;
                        GUIUtility.hotControl = _timelineCtrlId;
                        GUI.changed = true;
                        e.Use();
                    }
                    else
                    {
                        _selected = -1;
                        Repaint();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != _timelineCtrlId || _dragMode == DragMode.None) break;
                    {
                        var clip = _t.clips[_dragIndex];
                        float deltaT = (m.x - _dragMouseStartX) / _pps;
                        Undo.RecordObject(_t, "Edit Clip Timing");
                        if (_dragMode == DragMode.Move)
                            clip.startTime = SnapTime(Mathf.Max(0f, _dragValueStart + deltaT), e);
                        else
                            clip.duration = SnapTime(Mathf.Max(0.01f, _dragValueStart + deltaT), e);
                        EditorUtility.SetDirty(_t);
                        Repaint();
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == _timelineCtrlId)
                    {
                        GUIUtility.hotControl = 0;
                        _dragIndex = -1;
                        _dragMode = DragMode.None;
                        e.Use();
                    }
                    break;
            }
        }

        int HitTestClip(Vector2 m, float viewWidth)
        {
            for (int i = 0; i < _t.clips.Count; i++)
            {
                var clip = _t.clips[i];
                if (clip == null) continue;
                float y = RulerHeight + i * (RowHeight + RowGap) + RowGap;
                float x = clip.startTime * _pps - _scrollX;
                float w = Mathf.Max(6f, clip.duration * _pps);
                if (new Rect(x, y, w, RowHeight).Contains(m)) return i;
            }
            return -1;
        }

        // 줌에 따라 눈금 라벨이 겹치지 않을 '보기 좋은' 시간 간격을 고른다.
        static float NiceInterval(float pps)
        {
            // 라벨 1개가 차지하는 최소 픽셀 폭 ≈ 50px 보장
            float[] candidates = { 0.05f, 0.1f, 0.25f, 0.5f, 1f, 2f, 5f, 10f, 30f, 60f };
            foreach (float c in candidates)
                if (c * pps >= 50f) return c;
            return candidates[candidates.Length - 1];
        }

        // Ctrl 누르면 스냅 해제, 기본은 0.05초 스냅
        float SnapTime(float t, Event e)
        {
            if (e.control || e.command) return t;
            return Mathf.Round(t / 0.05f) * 0.05f;
        }

        // ──────────────────────────────────────────────────────────────────
        // 선택 클립 상세 패널 (종류별 필드)
        // ──────────────────────────────────────────────────────────────────

        void DrawSelectedClip()
        {
            if (_selected < 0 || _selected >= _t.clips.Count)
            {
                EditorGUILayout.HelpBox("Select a clip in the timeline, or press 'Add Clip'.", MessageType.Info);
                return;
            }

            var clip = _t.clips[_selected];

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"Clip #{_selected}", EditorStyles.boldLabel);

                // Target → Component → (필터된) Tween Type. 자체 변경 처리/정규화를 한다.
                DrawTargetAndType(clip);
                var tweenType = clip.tweenType;

                EditorGUILayout.Space(2);
                EditorGUI.BeginChangeCheck();

                string label = EditorGUILayout.TextField("Label", clip.label);

                EditorGUILayout.Space(2);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Timing", GUILayout.Width(60));
                }
                float startTime = EditorGUILayout.FloatField("Start (s)", clip.startTime);
                float duration = EditorGUILayout.FloatField("Duration (s)", clip.duration);

                EditorGUILayout.Space(2);
                // ── 종류별 목표값 ──
                Vector3 vec = clip.vectorValue;
                float fl = clip.floatValue;
                Color col = clip.colorValue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UsesVector3(tweenType))
                        vec = EditorGUILayout.Vector3Field("To", vec);
                    else if (UsesVector2(tweenType))
                    {
                        Vector2 v2 = EditorGUILayout.Vector2Field("To", new Vector2(vec.x, vec.y));
                        vec = new Vector3(v2.x, v2.y, 0f);
                    }
                    else if (UsesColor(tweenType))
                        col = EditorGUILayout.ColorField("To Color", col);
                    else // float
                        fl = EditorGUILayout.FloatField("To", fl);

                    if (GUILayout.Button("Current", GUILayout.Width(60)))
                    {
                        CaptureInto(clip, true);
                        // 캡처 결과가 아래 ChangeCheck 저장 시 stale 로컬로 덮이지 않도록 동기화
                        vec = clip.vectorValue; fl = clip.floatValue; col = clip.colorValue;
                    }
                }

                // ── 시작값 강제(Override Start Value) ──
                bool overrideStart = AndroidToggle("Override Start Value", clip.overrideStart);
                Vector3 fromVec = clip.fromVectorValue;
                float fromFl = clip.fromFloatValue;
                Color fromCol = clip.fromColorValue;
                if (overrideStart)
                {
                    EditorGUI.indentLevel++;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (UsesVector3(tweenType))
                            fromVec = EditorGUILayout.Vector3Field("Start", fromVec);
                        else if (UsesVector2(tweenType))
                        {
                            Vector2 v2 = EditorGUILayout.Vector2Field("Start", new Vector2(fromVec.x, fromVec.y));
                            fromVec = new Vector3(v2.x, v2.y, 0f);
                        }
                        else if (UsesColor(tweenType))
                            fromCol = EditorGUILayout.ColorField("Start Color", fromCol);
                        else // float
                            fromFl = EditorGUILayout.FloatField("Start", fromFl);

                        if (GUILayout.Button("Current", GUILayout.Width(60)))
                        {
                            CaptureInto(clip, false);
                            fromVec = clip.fromVectorValue; fromFl = clip.fromFloatValue; fromCol = clip.fromColorValue;
                        }
                    }
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space(2);
                bool useCurveEase = AndroidToggle("Use Curve Ease", clip.useCurveEase);
                Ease ease = clip.ease;
                AnimationCurve easeCurve = clip.easeCurve ?? AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
                if (useCurveEase)
                {
                    EditorGUI.indentLevel++;
                    easeCurve = EditorGUILayout.CurveField("Ease Curve", easeCurve);
                    EditorGUI.indentLevel--;
                }
                else
                {
                    ease = (Ease)EditorGUILayout.EnumPopup("Ease", clip.ease);
                }
                bool from = AndroidToggle("From (reverse)", clip.from);
                bool relative = AndroidToggle("Relative", clip.relative);

                bool snapping = clip.snapping;
                if (SupportsSnapping(tweenType))
                    snapping = AndroidToggle("Snapping", clip.snapping);

                var rotateMode = clip.rotateMode;
                if (IsRotate(tweenType))
                    rotateMode = (RotateMode)EditorGUILayout.EnumPopup("Rotate Mode", clip.rotateMode);

                int loops = EditorGUILayout.IntField("Loops (-1=inf)", clip.loops);
                var loopType = clip.loopType;
                if (clip.loops != 1)
                    loopType = (LoopType)EditorGUILayout.EnumPopup("Loop Type", clip.loopType);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_t, "Edit Clip");
                    clip.label = label;
                    clip.startTime = Mathf.Max(0f, startTime);
                    clip.duration = Mathf.Max(0.01f, duration);
                    clip.vectorValue = vec;
                    clip.floatValue = fl;
                    clip.colorValue = col;
                    clip.ease = ease;
                    clip.useCurveEase = useCurveEase;
                    clip.easeCurve = easeCurve;
                    clip.from = from;
                    clip.relative = relative;
                    clip.overrideStart = overrideStart;
                    clip.fromVectorValue = fromVec;
                    clip.fromFloatValue = fromFl;
                    clip.fromColorValue = fromCol;
                    clip.snapping = snapping;
                    clip.rotateMode = rotateMode;
                    clip.loops = loops;
                    clip.loopType = loopType;
                    EditorUtility.SetDirty(_t);
                }

                // ── 클립별 이벤트 (UnityEvent는 SerializedProperty로 그려야 정상 UI) ──
                EditorGUILayout.Space(2);
                EditorGUI.indentLevel++;
                _clipEventsFoldout = EditorGUILayout.Foldout(_clipEventsFoldout, "Clip Events", true, _sectionFoldout);
                EditorGUI.indentLevel--;
                if (_clipEventsFoldout)
                {
                    serializedObject.Update();
                    SerializedProperty clipsProp = serializedObject.FindProperty("clips");
                    if (clipsProp != null && _selected < clipsProp.arraySize)
                    {
                        SerializedProperty cp = clipsProp.GetArrayElementAtIndex(_selected);
                        EditorGUILayout.PropertyField(cp.FindPropertyRelative("onStart"));
                        EditorGUILayout.PropertyField(cp.FindPropertyRelative("onUpdate"));
                        EditorGUILayout.PropertyField(cp.FindPropertyRelative("onFinish"));
                    }
                    serializedObject.ApplyModifiedProperties();
                }
            }
        }

        // Target → Component 팝업 → 컴포넌트가 지원하는 Tween Type 만 필터링해 노출.
        void DrawTargetAndType(Timeline.Clip clip)
        {
            // ── Target ──
            EditorGUI.BeginChangeCheck();
            var targetObj = (GameObject)EditorGUILayout.ObjectField("Target", clip.target, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_t, "Edit Clip Target");
                clip.target = targetObj;
                // 타깃이 바뀌면 컴포넌트 선택을 초기화(기본값 = Transform)
                clip.targetComponent = targetObj != null ? targetObj.transform : null;
                EditorUtility.SetDirty(_t);
            }

            if (clip.target == null)
            {
                EditorGUILayout.HelpBox("Assign a Target to choose a component and tween type.", MessageType.Info);
                return;
            }

            // ── Component 팝업 ──
            var comps = GetTweenableComponents(clip.target);
            if (comps.Count == 0)
            {
                EditorGUILayout.HelpBox("This GameObject has no tween-able component.", MessageType.Warning);
                return;
            }

            int compIdx = comps.IndexOf(clip.targetComponent);
            if (compIdx < 0) compIdx = 0;

            EditorGUI.BeginChangeCheck();
            int newCompIdx = EditorGUILayout.Popup("Component", compIdx, BuildComponentNames(comps));
            bool compChanged = EditorGUI.EndChangeCheck();
            Component chosenComp = comps[newCompIdx];

            if (compChanged)
            {
                Undo.RecordObject(_t, "Edit Clip Component");
                clip.targetComponent = chosenComp;
                EditorUtility.SetDirty(_t);
            }
            else if (clip.targetComponent != chosenComp)
            {
                // 정규화: 이전 데이터/타깃 변경으로 참조가 어긋난 경우 (undo 항목 없이 보정)
                clip.targetComponent = chosenComp;
                EditorUtility.SetDirty(_t);
            }

            // ── 선택 컴포넌트가 지원하는 Tween Type 만 ──
            Timeline.TweenType[] allowed = AllowedTypes(chosenComp);
            if (allowed.Length == 0)
            {
                EditorGUILayout.HelpBox("Selected component supports no tween type.", MessageType.Warning);
                return;
            }

            int tIdx = Array.IndexOf(allowed, clip.tweenType);
            if (tIdx < 0) tIdx = 0; // 현재 타입이 허용되지 않으면 첫 항목으로 보정

            var typeNames = new string[allowed.Length];
            for (int i = 0; i < allowed.Length; i++) typeNames[i] = allowed[i].ToString();

            EditorGUI.BeginChangeCheck();
            int newTIdx = EditorGUILayout.Popup("Tween Type", tIdx, typeNames);
            bool tChanged = EditorGUI.EndChangeCheck();
            Timeline.TweenType chosenType = allowed[newTIdx];

            if (tChanged)
            {
                Undo.RecordObject(_t, "Edit Clip Tween Type");
                clip.tweenType = chosenType;
                EditorUtility.SetDirty(_t);
            }
            else if (clip.tweenType != chosenType)
            {
                clip.tweenType = chosenType; // 정규화
                EditorUtility.SetDirty(_t);
            }
        }

        // 트윈 가능한 컴포넌트 후보: Transform/RectTransform, Graphic 파생들, CanvasGroup, SpriteRenderer
        static List<Component> GetTweenableComponents(GameObject go)
        {
            var list = new List<Component>();
            if (go == null) return list;

            list.Add(go.transform); // Transform 또는 RectTransform (항상 1개)

            foreach (var g in go.GetComponents<Graphic>())
                list.Add(g);

            var cg = go.GetComponent<CanvasGroup>();
            if (cg != null) list.Add(cg);

            var sp = go.GetComponent<SpriteRenderer>();
            if (sp != null) list.Add(sp);

            return list;
        }

        // 같은 타입이 여러 개면 인덱스로 구분해 표시
        static string[] BuildComponentNames(List<Component> comps)
        {
            var names = new string[comps.Count];
            for (int i = 0; i < comps.Count; i++)
            {
                Type ti = comps[i].GetType();
                int count = 0, pos = 0;
                for (int j = 0; j < comps.Count; j++)
                {
                    if (comps[j].GetType() == ti) { count++; if (j < i) pos++; }
                }
                names[i] = count > 1 ? $"{ti.Name} ({pos})" : ti.Name;
            }
            return names;
        }

        // 컴포넌트 카테고리 → 허용 Tween Type 목록
        static Timeline.TweenType[] AllowedTypes(Component c)
        {
            if (c is RectTransform) return RectTransformTypes; // Transform 검사보다 먼저!
            if (c is Transform) return TransformTypes;
            if (c is CanvasGroup) return CanvasGroupTypes;
            if (c is Graphic) return GraphicTypes;
            if (c is SpriteRenderer) return SpriteTypes;
            return Array.Empty<Timeline.TweenType>();
        }

        static readonly Timeline.TweenType[] TransformTypes =
        {
            Timeline.TweenType.Move, Timeline.TweenType.MoveX, Timeline.TweenType.MoveY, Timeline.TweenType.MoveZ,
            Timeline.TweenType.LocalMove, Timeline.TweenType.LocalMoveX, Timeline.TweenType.LocalMoveY, Timeline.TweenType.LocalMoveZ,
            Timeline.TweenType.Rotate, Timeline.TweenType.LocalRotate,
            Timeline.TweenType.Scale, Timeline.TweenType.ScaleX, Timeline.TweenType.ScaleY, Timeline.TweenType.ScaleZ,
        };

        static readonly Timeline.TweenType[] RectTransformTypes = Concat(TransformTypes, new[]
        {
            Timeline.TweenType.AnchorPos, Timeline.TweenType.AnchorPosX, Timeline.TweenType.AnchorPosY, Timeline.TweenType.SizeDelta,
        });

        static readonly Timeline.TweenType[] GraphicTypes =
        {
            Timeline.TweenType.GraphicColor, Timeline.TweenType.GraphicFade,
        };

        static readonly Timeline.TweenType[] CanvasGroupTypes =
        {
            Timeline.TweenType.CanvasGroupFade,
        };

        static readonly Timeline.TweenType[] SpriteTypes =
        {
            Timeline.TweenType.SpriteColor, Timeline.TweenType.SpriteFade,
        };

        static Timeline.TweenType[] Concat(Timeline.TweenType[] a, Timeline.TweenType[] b)
        {
            var r = new Timeline.TweenType[a.Length + b.Length];
            a.CopyTo(r, 0);
            b.CopyTo(r, a.Length);
            return r;
        }

        // ──────────────────────────────────────────────────────────────────
        // 전역 설정 / 미리보기
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

        void DrawPreviewControls()
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            if (Application.isPlaying)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("▶ Play")) _t.Play();
                    if (GUILayout.Button("⏸ Pause")) _t.Pause();
                    if (GUILayout.Button("⏵ Resume")) _t.Resume();
                    if (GUILayout.Button("⟲ Restart")) _t.Restart();
                    if (GUILayout.Button("⏹ Kill")) _t.Kill();
                }
                EditorGUILayout.HelpBox("Play mode: controls the runtime sequence directly.", MessageType.None);
                return;
            }

            // Edit 모드: Goto 스크럽 프리뷰
            using (new EditorGUILayout.HorizontalScope())
            {
                if (!_previewing)
                {
                    if (GUILayout.Button("Start Preview")) BeginPreview();
                }
                else
                {
                    if (GUILayout.Button(_previewPlaying ? "⏸ Pause" : "▶ Play"))
                    {
                        _previewPlaying = !_previewPlaying;
                        _lastUpdate = EditorApplication.timeSinceStartup;
                    }
                    if (GUILayout.Button("⟲ To Start")) { _previewTime = 0f; ApplyPreview(); }
                    if (GUILayout.Button("Stop Preview")) EndPreview();
                }
            }

            if (_previewing)
            {
                EditorGUI.BeginChangeCheck();
                float t = EditorGUILayout.Slider("Time", _previewTime, 0f, Mathf.Max(0.01f, _t.TotalDuration));
                if (EditorGUI.EndChangeCheck())
                {
                    _previewTime = t;
                    _previewPlaying = false;
                    ApplyPreview();
                }
                EditorGUILayout.HelpBox("Edit-mode preview restores original values on stop. (Stop before saving the scene.)", MessageType.None);
            }
        }

        // ──────────────────────────────────────────────────────────────────
        // 미리보기 구현
        // ──────────────────────────────────────────────────────────────────

        void BeginPreview()
        {
            CaptureSnapshot();
            _t.Build();
            _previewing = true;
            _previewPlaying = false;
            _previewTime = 0f;
            _lastUpdate = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnEditorUpdate;
            ApplyPreview();
        }

        void EndPreview()
        {
            EditorApplication.update -= OnEditorUpdate;
            _previewing = false;
            _previewPlaying = false;
            _t.Kill();
            RestoreSnapshot();
            SceneView.RepaintAll();
        }

        void OnEditorUpdate()
        {
            if (!_previewing) return;
            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - _lastUpdate);
            _lastUpdate = now;

            if (_previewPlaying)
            {
                float total = Mathf.Max(0.01f, _t.TotalDuration);
                _previewTime += dt;
                if (_previewTime >= total)
                {
                    _previewTime = total;
                    _previewPlaying = false;
                }
                ApplyPreview();
                Repaint();
                SceneView.RepaintAll();
            }
        }

        void ApplyPreview()
        {
            var seq = _t.Sequence;
            if (seq != null && seq.IsActive())
            {
                try { seq.Goto(_previewTime); }
                catch (Exception ex) { Debug.LogWarning($"[DoTweenTimeline] 미리보기 Goto 실패: {ex.Message}"); }
            }
            SceneView.RepaintAll();
        }

        void CaptureSnapshot()
        {
            _restore = new List<Action>();
            var seen = new HashSet<GameObject>();
            foreach (var clip in _t.clips)
            {
                if (clip?.target == null || !seen.Add(clip.target)) continue;
                SnapshotGO(clip.target);
            }
        }

        void SnapshotGO(GameObject go)
        {
            Transform tr = go.transform;
            Vector3 lpos = tr.localPosition;
            Quaternion lrot = tr.localRotation;
            Vector3 lscale = tr.localScale;
            _restore.Add(() => { if (tr) { tr.localPosition = lpos; tr.localRotation = lrot; tr.localScale = lscale; } });

            if (tr is RectTransform rt)
            {
                Vector2 ap = rt.anchoredPosition;
                Vector2 sd = rt.sizeDelta;
                _restore.Add(() => { if (rt) { rt.anchoredPosition = ap; rt.sizeDelta = sd; } });
            }

            var g = go.GetComponent<Graphic>();
            if (g) { Color c = g.color; _restore.Add(() => { if (g) g.color = c; }); }

            var cg = go.GetComponent<CanvasGroup>();
            if (cg) { float a = cg.alpha; _restore.Add(() => { if (cg) cg.alpha = a; }); }

            var sp = go.GetComponent<SpriteRenderer>();
            if (sp) { Color c = sp.color; _restore.Add(() => { if (sp) sp.color = c; }); }
        }

        void RestoreSnapshot()
        {
            if (_restore == null) return;
            foreach (var r in _restore) r?.Invoke();
            _restore = null;
        }

        // ──────────────────────────────────────────────────────────────────
        // 클립 조작
        // ──────────────────────────────────────────────────────────────────

        void AddClip()
        {
            Undo.RecordObject(_t, "Add Clip");
            var clip = new Timeline.Clip
            {
                label = "Clip " + _t.clips.Count,
                startTime = _t.TotalDuration, // 맨 끝에 이어붙이기
                target = _t.gameObject,
                targetComponent = _t.transform, // 기본값: Transform
            };
            _t.clips.Add(clip);
            _selected = _t.clips.Count - 1;
            EditorUtility.SetDirty(_t);
        }

        void DuplicateClip(int i)
        {
            if (i < 0 || i >= _t.clips.Count) return;
            Undo.RecordObject(_t, "Duplicate Clip");
            var src = _t.clips[i];
            var copy = new Timeline.Clip
            {
                label = src.label + " (복제)",
                target = src.target,
                targetComponent = src.targetComponent,
                tweenType = src.tweenType,
                startTime = src.startTime + src.duration,
                duration = src.duration,
                vectorValue = src.vectorValue,
                floatValue = src.floatValue,
                colorValue = src.colorValue,
                ease = src.ease,
                useCurveEase = src.useCurveEase,
                easeCurve = new AnimationCurve(src.easeCurve != null ? src.easeCurve.keys : new Keyframe[0]),
                from = src.from,
                relative = src.relative,
                snapping = src.snapping,
                rotateMode = src.rotateMode,
                overrideStart = src.overrideStart,
                fromVectorValue = src.fromVectorValue,
                fromFloatValue = src.fromFloatValue,
                fromColorValue = src.fromColorValue,
                loops = src.loops,
                loopType = src.loopType,
            };
            _t.clips.Insert(i + 1, copy);
            _selected = i + 1;
            EditorUtility.SetDirty(_t);
        }

        void DeleteClip(int i)
        {
            if (i < 0 || i >= _t.clips.Count) return;
            Undo.RecordObject(_t, "Delete Clip");
            _t.clips.RemoveAt(i);
            _selected = Mathf.Clamp(_selected, -1, _t.clips.Count - 1);
            EditorUtility.SetDirty(_t);
        }

        // 타깃의 현재 상태를 목표값으로 캡처
        // 현재 타깃 상태를 읽어 v/f/c 로 반환 (tweenType에 따라 의미 있는 슬롯만 채워짐)
        void ReadCurrent(Timeline.Clip clip, out Vector3 v, out float f, out Color c)
        {
            v = clip.vectorValue; f = clip.floatValue; c = clip.colorValue;
            if (clip.target == null) return;
            Transform tr = clip.target.transform;
            var rt = tr as RectTransform;

            switch (clip.tweenType)
            {
                case Timeline.TweenType.Move: v = tr.position; break;
                case Timeline.TweenType.MoveX: f = tr.position.x; break;
                case Timeline.TweenType.MoveY: f = tr.position.y; break;
                case Timeline.TweenType.MoveZ: f = tr.position.z; break;
                case Timeline.TweenType.LocalMove: v = tr.localPosition; break;
                case Timeline.TweenType.LocalMoveX: f = tr.localPosition.x; break;
                case Timeline.TweenType.LocalMoveY: f = tr.localPosition.y; break;
                case Timeline.TweenType.LocalMoveZ: f = tr.localPosition.z; break;
                case Timeline.TweenType.Rotate: v = tr.eulerAngles; break;
                case Timeline.TweenType.LocalRotate: v = tr.localEulerAngles; break;
                case Timeline.TweenType.Scale: v = tr.localScale; break;
                case Timeline.TweenType.ScaleX: f = tr.localScale.x; break;
                case Timeline.TweenType.ScaleY: f = tr.localScale.y; break;
                case Timeline.TweenType.ScaleZ: f = tr.localScale.z; break;
                case Timeline.TweenType.AnchorPos: if (rt) v = rt.anchoredPosition; break;
                case Timeline.TweenType.AnchorPosX: if (rt) f = rt.anchoredPosition.x; break;
                case Timeline.TweenType.AnchorPosY: if (rt) f = rt.anchoredPosition.y; break;
                case Timeline.TweenType.SizeDelta: if (rt) v = rt.sizeDelta; break;
                case Timeline.TweenType.CanvasGroupFade:
                    { var cg = clip.target.GetComponent<CanvasGroup>(); if (cg) f = cg.alpha; break; }
                case Timeline.TweenType.GraphicColor:
                    { var g = clip.target.GetComponent<Graphic>(); if (g) c = g.color; break; }
                case Timeline.TweenType.GraphicFade:
                    { var g = clip.target.GetComponent<Graphic>(); if (g) f = g.color.a; break; }
                case Timeline.TweenType.SpriteColor:
                    { var s = clip.target.GetComponent<SpriteRenderer>(); if (s) c = s.color; break; }
                case Timeline.TweenType.SpriteFade:
                    { var s = clip.target.GetComponent<SpriteRenderer>(); if (s) f = s.color.a; break; }
            }
        }

        // 현재값을 To(목표, toSlot=true) 또는 Start(시작값, toSlot=false) 슬롯에 캡처
        void CaptureInto(Timeline.Clip clip, bool toSlot)
        {
            if (clip.target == null) return;
            Undo.RecordObject(_t, "Capture Current Value");
            ReadCurrent(clip, out var v, out var f, out var c);
            var type = clip.tweenType;
            if (UsesColor(type))
            {
                if (toSlot) clip.colorValue = c; else clip.fromColorValue = c;
            }
            else if (UsesVector3(type) || UsesVector2(type))
            {
                if (toSlot) clip.vectorValue = v; else clip.fromVectorValue = v;
            }
            else // float
            {
                if (toSlot) clip.floatValue = f; else clip.fromFloatValue = f;
            }
            EditorUtility.SetDirty(_t);
        }

        // ──────────────────────────────────────────────────────────────────
        // 종류 분류 헬퍼
        // ──────────────────────────────────────────────────────────────────

        static bool UsesVector3(Timeline.TweenType t) => t == Timeline.TweenType.Move
            || t == Timeline.TweenType.LocalMove || t == Timeline.TweenType.Rotate
            || t == Timeline.TweenType.LocalRotate || t == Timeline.TweenType.Scale;

        static bool UsesVector2(Timeline.TweenType t) => t == Timeline.TweenType.AnchorPos
            || t == Timeline.TweenType.SizeDelta;

        static bool UsesColor(Timeline.TweenType t) => t == Timeline.TweenType.GraphicColor
            || t == Timeline.TweenType.SpriteColor;

        static bool IsRotate(Timeline.TweenType t) => t == Timeline.TweenType.Rotate
            || t == Timeline.TweenType.LocalRotate;

        static bool SupportsSnapping(Timeline.TweenType t)
        {
            switch (t)
            {
                case Timeline.TweenType.Move:
                case Timeline.TweenType.MoveX:
                case Timeline.TweenType.MoveY:
                case Timeline.TweenType.MoveZ:
                case Timeline.TweenType.LocalMove:
                case Timeline.TweenType.LocalMoveX:
                case Timeline.TweenType.LocalMoveY:
                case Timeline.TweenType.LocalMoveZ:
                case Timeline.TweenType.AnchorPos:
                case Timeline.TweenType.AnchorPosX:
                case Timeline.TweenType.AnchorPosY:
                case Timeline.TweenType.SizeDelta:
                    return true;
                default:
                    return false;
            }
        }

        static Color CategoryColor(Timeline.TweenType t)
        {
            int v = (int)t;
            if (v < 20) return new Color(0.25f, 0.45f, 0.85f);   // Move (파랑)
            if (v < 30) return new Color(0.85f, 0.55f, 0.20f);   // Rotate (주황)
            if (v < 40) return new Color(0.30f, 0.70f, 0.35f);   // Scale (초록)
            if (v < 50) return new Color(0.25f, 0.70f, 0.75f);   // Rect (청록)
            return new Color(0.70f, 0.35f, 0.75f);               // Color/Fade (자주)
        }

        static void EnsureStyles()
        {
            if (_clipLabelStyle == null)
            {
                _clipLabelStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Clip,
                };
                _clipLabelStyle.normal.textColor = Color.white;
            }
            if (_barStyle == null)
                _barStyle = new GUIStyle(GUI.skin.box);
            if (_sectionFoldout == null)
                _sectionFoldout = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
            if (_bannerTitle == null)
            {
                _bannerTitle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 20, alignment = TextAnchor.MiddleLeft };
                _bannerTitle.normal.textColor = Color.white;
                _bannerTitleShadow = new GUIStyle(_bannerTitle);
                _bannerTitleShadow.normal.textColor = new Color(0f, 0f, 0f, 0.5f);
                _bannerSub = new GUIStyle(EditorStyles.label) { fontSize = 10, alignment = TextAnchor.MiddleLeft };
                _bannerSub.normal.textColor = new Color(1f, 1f, 1f, 0.78f);
            }
        }

        // ──────────────────────────────────────────────────────────────────
        // 상단 배너
        // ──────────────────────────────────────────────────────────────────

        void DrawBanner()
        {
            const float h = 54f;
            Rect r = GUILayoutUtility.GetRect(0, h, GUILayout.ExpandWidth(true));

            // 그라데이션 배경
            GUI.DrawTexture(r, GetBannerTexture(), ScaleMode.StretchToFill);

            // 우측 이징 곡선 모티프(smoothstep)
            Handles.BeginGUI();
            Handles.color = new Color(1f, 1f, 1f, 0.22f);
            const int n = 48;
            float cw = 120f, cx = r.xMax - cw - 14f, top = r.y + 10f, bottom = r.yMax - 10f;
            var pts = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                float e = t * t * (3f - 2f * t); // smoothstep ease
                pts[i] = new Vector3(cx + t * cw, bottom - e * (bottom - top), 0f);
            }
            Handles.DrawAAPolyLine(2.5f, pts);
            Handles.EndGUI();

            // 하단 액센트 라인
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 2f, r.width, 2f), new Color(0.36f, 0.86f, 0.96f, 0.95f));

            // 타이틀(드롭 섀도 + 본문)
            Rect titleRect = new Rect(r.x + 16f, r.y + 5f, r.width - 32f, 28f);
            GUI.Label(new Rect(titleRect.x + 1f, titleRect.y + 1f, titleRect.width, titleRect.height), "DoTween Timeline", _bannerTitleShadow);
            GUI.Label(titleRect, "DoTween Timeline", _bannerTitle);

            // 서브타이틀
            GUI.Label(new Rect(r.x + 17f, r.y + 31f, r.width - 32f, 16f), "GUI Tween Sequencer", _bannerSub);

            EditorGUILayout.Space(4);
        }

        static Texture2D GetBannerTexture()
        {
            if (_bannerTex != null) return _bannerTex;

            const int w = 256;
            var tex = new Texture2D(w, 1, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            // indigo → purple → teal 3-stop 가로 그라데이션
            Color a = new Color(0.15f, 0.11f, 0.30f);
            Color b = new Color(0.41f, 0.19f, 0.55f);
            Color c = new Color(0.13f, 0.52f, 0.74f);
            for (int x = 0; x < w; x++)
            {
                float t = x / (float)(w - 1);
                Color col = t < 0.5f ? Color.Lerp(a, b, t / 0.5f) : Color.Lerp(b, c, (t - 0.5f) / 0.5f);
                tex.SetPixel(x, 0, col);
            }
            tex.Apply();
            _bannerTex = tex;
            return tex;
        }

        // ──────────────────────────────────────────────────────────────────
        // 안드로이드 스타일 On/Off 토글 스위치
        // ──────────────────────────────────────────────────────────────────

        static Texture2D _circleTex;

        // 라벨 + 슬라이딩 스위치 한 줄. 클릭으로 토글되고 변경된 값을 반환한다.
        bool AndroidToggle(string label, bool value)
        {
            Rect row = EditorGUILayout.GetControlRect();
            Rect labelRect = new Rect(row.x, row.y, EditorGUIUtility.labelWidth, row.height);
            EditorGUI.LabelField(labelRect, label);

            const float swW = 34f, swH = 18f;
            Rect sw = new Rect(row.x + EditorGUIUtility.labelWidth, row.y + (row.height - swH) * 0.5f, swW, swH);

            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && sw.Contains(e.mousePosition))
            {
                value = !value;
                GUI.changed = true;
                e.Use();
            }
            EditorGUIUtility.AddCursorRect(sw, MouseCursor.Link);

            // 노브 위치(0..1)를 target으로 부드럽게 이동
            float target = value ? 1f : 0f;
            if (!_toggleAnim.TryGetValue(label, out float t)) t = target;
            t = Mathf.MoveTowards(t, target, ToggleAnimSpeed * _dt);
            _toggleAnim[label] = t;
            if (!Mathf.Approximately(t, target)) _animating = true;

            DrawSwitch(sw, t);
            return value;
        }

        // t: 노브 위치/색 보간값 (0 = OFF, 1 = ON)
        static void DrawSwitch(Rect r, float t)
        {
            // 트랙(알약): OFF=회색 → ON=초록 보간
            Color off = new Color(0.42f, 0.42f, 0.45f);
            Color on = new Color(0.298f, 0.776f, 0.435f);
            DrawPill(r, Color.Lerp(off, on, t));

            // 노브(흰 원) + 옅은 그림자
            const float pad = 2f;
            float knobD = r.height - pad * 2f;
            float left = r.x + pad;
            float right = r.xMax - knobD - pad;
            float kx = Mathf.Lerp(left, right, t);
            Rect knob = new Rect(kx, r.y + pad, knobD, knobD);
            DrawCircle(new Rect(knob.x, knob.y + 1f, knob.width, knob.height), new Color(0f, 0f, 0f, 0.25f));
            DrawCircle(knob, Color.white);
        }

        // 양 끝 원형 캡 + 가운데 사각형 = 알약 모양
        static void DrawPill(Rect r, Color color)
        {
            float h = r.height;
            DrawCircle(new Rect(r.x, r.y, h, h), color);
            DrawCircle(new Rect(r.xMax - h, r.y, h, h), color);
            EditorGUI.DrawRect(new Rect(r.x + h * 0.5f, r.y, r.width - h, h), color);
        }

        static void DrawCircle(Rect r, Color color)
        {
            GUI.DrawTexture(r, GetCircleTexture(), ScaleMode.StretchToFill, true, 0f, color, 0f, 0f);
        }

        static Texture2D GetCircleTexture()
        {
            if (_circleTex != null) return _circleTex;

            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            float c = (s - 1) * 0.5f;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a = Mathf.Clamp01(c - d); // 1px 페더 안티앨리어싱
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            _circleTex = tex;
            return tex;
        }
    }
}
