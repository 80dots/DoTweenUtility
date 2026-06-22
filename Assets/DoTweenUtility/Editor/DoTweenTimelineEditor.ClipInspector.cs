using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Timeline = global::DoTweenUtility.DoTweenTimeline;

namespace DoTweenUtility.Editor
{
    // 선택 클립 상세 패널(종류별 필드) + 클립 조작(추가/복제/삭제) + 현재값 캡처 + 종류 분류 헬퍼.
    public partial class DoTweenTimelineEditor
    {
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

                // On/Off 토글(제일 위). 자체 change-check로 즉시 저장한다.
                EditorGUI.BeginChangeCheck();
                bool clipEnabled = AndroidToggle("Enabled", clip.enabled);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_t, "Toggle Clip Enabled");
                    clip.enabled = clipEnabled;
                    EditorUtility.SetDirty(_t);
                }

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
                enabled = src.enabled,
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

        // ──────────────────────────────────────────────────────────────────
        // 현재값 캡처
        // ──────────────────────────────────────────────────────────────────

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
    }
}
