using DG.Tweening;
using UnityEditor;
using UnityEngine;
using Timeline = global::DoTweenUtility.DoTweenTimeline;

namespace DoTweenUtility.Editor
{
    // 타임라인 트랙 캔버스: 룰러 + 클립 막대 + 이벤트 마커 플래그 + 플레이헤드 + 마우스 처리.
    public partial class DoTweenTimelineEditor
    {
        void DrawTimeline()
        {
            int rows = Mathf.Max(1, _t.clips.Count);
            float viewWidth = EditorGUIUtility.currentViewWidth - 24f;
            float bodyHeight = RulerHeight + rows * (RowHeight + RowGap);
            float contentWidth = Mathf.Max(viewWidth, (Mathf.Max(_t.TotalDuration, MaxMarkerTime()) + 2f) * _pps);

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
                DrawMarkerFlags(viewWidth, bodyHeight);
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
                // 비활성(Off) 클립은 어둡게 + 반투명으로 표시.
                if (!clip.enabled)
                {
                    fill = Color.Lerp(fill, new Color(0.18f, 0.18f, 0.18f), 0.6f);
                    fill.a = 0.5f;
                }
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

                string name = string.IsNullOrEmpty(clip.label) ? clip.tweenType.ToString() : clip.label;
                string txt = clip.enabled ? $"{name} · {clip.tweenType}" : $"⏻ OFF · {name}";
                GUI.Label(new Rect(bar.x + 4, bar.y, bar.width - 6, bar.height), txt, _clipLabelStyle);

                EditorGUIUtility.AddCursorRect(bar, MouseCursor.Pan);
                EditorGUIUtility.AddCursorRect(new Rect(bar.xMax - EdgeGrip, bar.y, EdgeGrip, bar.height), MouseCursor.ResizeHorizontal);
            }
        }

        void DrawPlayhead(float bodyHeight)
        {
            // 표시 위치(초): Edit 모드 프리뷰는 _previewTime, Play 모드는 런타임 시퀀스의
            // 현재 루프 사이클 내 경과 시간을 사용한다.
            float t;
            if (_previewing)
            {
                t = _previewTime;
            }
            else if (Application.isPlaying)
            {
                var seq = _t.Sequence;
                if (seq == null || !seq.IsActive()) return;
                t = seq.Elapsed(false); // 현재 사이클 내 경과(초) — 타임라인의 1패스 좌표와 매핑
            }
            else return;

            float x = t * _pps - _scrollX;
            EditorGUI.DrawRect(new Rect(x, 0, 1.5f, bodyHeight), new Color(1f, 0.3f, 0.2f, 0.9f));
        }

        // 마커 색(앰버). 선택 시 밝게.
        static readonly Color MarkerColor = new Color(1f, 0.78f, 0.25f, 0.95f);

        void DrawMarkerFlags(float viewWidth, float bodyHeight)
        {
            if (_t.markers == null) return;

            var lbl = new GUIStyle(EditorStyles.miniLabel);
            lbl.normal.textColor = new Color(0.1f, 0.08f, 0f);

            for (int i = 0; i < _t.markers.Count; i++)
            {
                var mk = _t.markers[i];
                if (mk == null) continue;

                float x = mk.time * _pps - _scrollX;
                if (x < -2 || x > viewWidth) continue;

                bool sel = i == _selectedMarker;
                Color col = sel ? Color.Lerp(MarkerColor, Color.white, 0.35f) : MarkerColor;

                // 세로 가이드 라인(본체 전체)
                EditorGUI.DrawRect(new Rect(x, 0, 1f, bodyHeight), new Color(col.r, col.g, col.b, sel ? 0.6f : 0.35f));

                // 상단 플래그(이름 라벨)
                string name = string.IsNullOrEmpty(mk.name) ? "Event Marker" : mk.name;
                float w = Mathf.Clamp(lbl.CalcSize(new GUIContent(name)).x + 8f, 16f, 140f);
                Rect flag = new Rect(x, 0, w, RulerHeight - 2f);
                EditorGUI.DrawRect(flag, col);
                if (sel)
                {
                    EditorGUI.DrawRect(new Rect(flag.x, flag.y, flag.width, 1), Color.white);
                    EditorGUI.DrawRect(new Rect(flag.x, flag.yMax - 1, flag.width, 1), Color.white);
                    EditorGUI.DrawRect(new Rect(flag.xMax - 1, flag.y, 1, flag.height), Color.white);
                }
                GUI.Label(new Rect(flag.x + 4, flag.y, flag.width - 6, flag.height), name, lbl);

                EditorGUIUtility.AddCursorRect(MarkerHitRect(mk, viewWidth), MouseCursor.Pan);
            }
        }

        // 마커 히트/드래그 영역(상단 플래그 + 라인 그립).
        Rect MarkerHitRect(Timeline.Marker mk, float viewWidth)
        {
            var lbl = EditorStyles.miniLabel;
            string name = string.IsNullOrEmpty(mk.name) ? "Event Marker" : mk.name;
            float w = Mathf.Clamp(lbl.CalcSize(new GUIContent(name)).x + 8f, 16f, 140f);
            float x = mk.time * _pps - _scrollX;
            return new Rect(x - 3f, 0, w + 3f, RulerHeight);
        }

        int HitTestMarker(Vector2 m, float viewWidth)
        {
            if (_t.markers == null) return -1;
            // 위에 그려진(인덱스 큰) 것이 우선 잡히도록 역순 탐색.
            for (int i = _t.markers.Count - 1; i >= 0; i--)
            {
                var mk = _t.markers[i];
                if (mk == null) continue;
                if (MarkerHitRect(mk, viewWidth).Contains(m)) return i;
            }
            return -1;
        }

        // 모든 마커 중 가장 늦은 시각(콘텐츠 폭 계산용). 없으면 0.
        float MaxMarkerTime()
        {
            float max = 0f;
            if (_t.markers != null)
                foreach (var mk in _t.markers)
                    if (mk != null && mk.time > max) max = mk.time;
            return max;
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
                        _previewPlaying = false;
                        _previewTime = Mathf.Max(0f, (m.x + _scrollX) / _pps);
                        _previewElapsed = _previewTime;
                        ApplyPreview();
                        e.Use();
                        break;
                    }

                    // 프리뷰 중에는 클립 선택/이동/리사이즈 등 편집을 막는다(룰러 스크럽만 허용).
                    if (_previewing) break;

                    // 룰러 영역(편집 모드): 마커 선택/드래그. 클립은 룰러 아래에만 있으므로 안전.
                    if (m.y < RulerHeight)
                    {
                        int mk = HitTestMarker(m, viewWidth);
                        if (mk >= 0)
                        {
                            _selectedMarker = mk;
                            _selected = -1;
                            _dragIndex = mk;
                            _dragMode = DragMode.Marker;
                            _dragMouseStartX = m.x;
                            _dragValueStart = _t.markers[mk].time;
                            GUIUtility.hotControl = _timelineCtrlId;
                            GUI.changed = true;
                            e.Use();
                        }
                        else
                        {
                            _selectedMarker = -1;
                            Repaint();
                        }
                        break;
                    }

                    int hit = HitTestClip(m, viewWidth);
                    if (hit >= 0)
                    {
                        _selected = hit;
                        _selectedMarker = -1;
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
                        _selectedMarker = -1;
                        Repaint();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != _timelineCtrlId || _dragMode == DragMode.None) break;
                    {
                        float deltaT = (m.x - _dragMouseStartX) / _pps;
                        if (_dragMode == DragMode.Marker)
                        {
                            Undo.RecordObject(_t, "Move Marker");
                            _t.markers[_dragIndex].time = SnapTime(Mathf.Max(0f, _dragValueStart + deltaT), e);
                        }
                        else
                        {
                            var clip = _t.clips[_dragIndex];
                            Undo.RecordObject(_t, "Edit Clip Timing");
                            if (_dragMode == DragMode.Move)
                                clip.startTime = SnapTime(Mathf.Max(0f, _dragValueStart + deltaT), e);
                            else
                                clip.duration = SnapTime(Mathf.Max(0.01f, _dragValueStart + deltaT), e);
                        }
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

        static Color CategoryColor(Timeline.TweenType t)
        {
            int v = (int)t;
            if (v < 20) return new Color(0.25f, 0.45f, 0.85f);   // Move (파랑)
            if (v < 30) return new Color(0.85f, 0.55f, 0.20f);   // Rotate (주황)
            if (v < 40) return new Color(0.30f, 0.70f, 0.35f);   // Scale (초록)
            if (v < 50) return new Color(0.25f, 0.70f, 0.75f);   // Rect (청록)
            return new Color(0.70f, 0.35f, 0.75f);               // Color/Fade (자주)
        }
    }
}
