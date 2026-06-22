using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Timeline = global::DoTweenUtility.DoTweenTimeline;

namespace DoTweenUtility.Editor
{
    // 프리뷰 컨트롤(Play 모드/Preview Mode 트랜스포트) + 프리뷰 구현(재생 루프·스냅샷/복구).
    public partial class DoTweenTimelineEditor
    {
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

            // ── Edit 모드: Preview Mode On/Off ──
            // 토글이 켜진 동안에만 트랜스포트(Play/Pause·Stop·프레임 스텝·프로그레스)를 노출하고,
            // 나머지 편집 UI는 OnInspectorGUI에서 DisabledScope로 잠근다. 끄면 원본 값을 복구한다.
            bool wasPreviewing = _previewing;
            bool nowPreviewing = AndroidToggle("Preview Mode", wasPreviewing);
            if (nowPreviewing != wasPreviewing)
            {
                if (nowPreviewing) BeginPreview();
                else EndPreview();
            }

            if (!_previewing)
            {
                EditorGUILayout.HelpBox(
                    "Turn on Preview Mode to play/scrub the timeline. Editing is locked while previewing; original values are restored when you turn it off.",
                    MessageType.None);
                return;
            }

            float cycle = Mathf.Max(0.01f, PreviewCycle());

            // 트랜스포트: Prev Frame · From Start · Play · Stop · Next Frame
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Prev Frame", "Previous frame (1/60s)")))
                    StepPreview(-PreviewFrameStep, cycle);

                // 처음부터 재생(0으로 되감고 정방향 재생)
                if (GUILayout.Button(new GUIContent("From Start", "Play from start")))
                {
                    _previewTime = 0f;
                    _previewElapsed = 0f;
                    _previewPlaying = true;
                    _lastUpdate = EditorApplication.timeSinceStartup;
                    ApplyPreview();
                }

                // 정방향 재생(재생 중이면 강조)
                Color prev = GUI.backgroundColor;
                if (_previewPlaying) GUI.backgroundColor = new Color(0.55f, 0.8f, 1f);
                if (GUILayout.Button(new GUIContent("Play", "Play")))
                    StartPreviewPlay(cycle);
                GUI.backgroundColor = prev;

                // 멈춤(현재 위치 유지)
                if (GUILayout.Button(new GUIContent("Stop", "Stop (hold at current frame)")))
                    _previewPlaying = false;

                if (GUILayout.Button(new GUIContent("Next Frame", "Next frame (1/60s)")))
                    StepPreview(PreviewFrameStep, cycle);
            }

            // 프로그레스 바(스크럽)
            EditorGUI.BeginChangeCheck();
            float t = EditorGUILayout.Slider("Progress", _previewTime, 0f, cycle);
            if (EditorGUI.EndChangeCheck())
            {
                _previewPlaying = false;
                _previewTime = t;
                _previewElapsed = t;
                ApplyPreview();
            }
            EditorGUILayout.LabelField($"{_previewTime:0.00}s / {cycle:0.00}s", EditorStyles.miniLabel);

            EditorGUILayout.HelpBox(
                "Editing is locked in Preview Mode. Turn it off to restore original values and edit.",
                MessageType.None);
        }

        // 프레임 스텝 단위(초). 60fps 기준 한 프레임.
        const float PreviewFrameStep = 1f / 60f;

        // 프리뷰 재생을 멈추고 지정 델타만큼 시간을 이동(클램프)한 뒤 적용한다.
        void StepPreview(float delta, float cycle)
        {
            _previewPlaying = false;
            _previewTime = Mathf.Clamp(_previewTime + delta, 0f, cycle);
            _previewElapsed = _previewTime;
            ApplyPreview();
        }

        // 프리뷰 재생을 시작한다. 끝까지 간 상태에서 다시 누르면 처음부터, 아니면 현재 위치에서 이어재생.
        void StartPreviewPlay(float cycle)
        {
            int loops = _t.timelineLoops;
            bool infinite = loops < 0;
            float end = infinite ? cycle : cycle * Mathf.Max(1, loops);

            bool atEnd = !infinite && _previewElapsed >= end - 0.0001f;
            _previewElapsed = atEnd ? 0f : _previewTime;

            _previewPlaying = true;
            _lastUpdate = EditorApplication.timeSinceStartup;
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
            _previewElapsed = 0f;
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

            if (!_previewPlaying) return;

            // 한 루프 사이클 길이(클립별 루프는 시퀀스 사이클에 이미 포함됨).
            float cycle = PreviewCycle();

            // 타임라인 전체 루프(timelineLoops/timelineLoopType)를 프리뷰에 반영.
            int loops = _t.timelineLoops;
            bool infinite = loops < 0;
            float totalFinite = infinite ? Mathf.Infinity : cycle * Mathf.Max(1, loops);

            _previewElapsed += dt;

            if (!infinite && _previewElapsed >= totalFinite)
            {
                // 마지막 사이클의 끝 위치로 고정하고 정지(Yoyo면 짝수 루프 후 시작점으로 끝남).
                _previewElapsed = totalFinite;
                int lastIdx = Mathf.Max(0, loops - 1);
                bool lastReversed = _t.timelineLoopType == LoopType.Yoyo && (lastIdx % 2 == 1);
                _previewTime = lastReversed ? 0f : cycle;
                _previewPlaying = false;
            }
            else
            {
                int completed = (int)(_previewElapsed / cycle);
                float frac = _previewElapsed - completed * cycle;
                bool reversed = _t.timelineLoopType == LoopType.Yoyo && (completed % 2 == 1);
                _previewTime = reversed ? (cycle - frac) : frac;
            }

            ApplyPreview();
            Repaint();
            SceneView.RepaintAll();
        }

        // 프리뷰용 한 루프 사이클 길이(초). 시퀀스의 단일 사이클 길이(클립 루프 포함)를 쓰되,
        // 무한 클립 루프 등으로 비정상(0/무한)이면 에디터 표시용 TotalDuration으로 폴백한다.
        float PreviewCycle()
        {
            var seq = _t.Sequence;
            if (seq != null && seq.IsActive())
            {
                float d = seq.Duration(false);
                if (d > 0.0001f && !float.IsInfinity(d)) return d;
            }
            return Mathf.Max(0.01f, _t.TotalDuration);
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
    }
}
