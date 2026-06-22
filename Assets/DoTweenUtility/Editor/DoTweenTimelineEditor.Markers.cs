using System.Collections.Generic;
using DG.Tweening;
using UnityEditor;
using UnityEngine;
using Timeline = global::DoTweenUtility.DoTweenTimeline;

namespace DoTweenUtility.Editor
{
    // 이벤트 마커 섹션(목록 편집 + 추가/삭제 + onMarker 콜백 필드).
    public partial class DoTweenTimelineEditor
    {
        void DrawMarkersSection()
        {
            if (_t.markers == null) _t.markers = new List<Timeline.Marker>();

            using (new EditorGUILayout.HorizontalScope())
            {
                _markersFoldout = EditorGUILayout.Foldout(
                    _markersFoldout, $"Event Markers ({_t.markers.Count})", true, _sectionFoldout);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("＋ Add Event Marker", GUILayout.Width(140)))
                    AddMarker();
            }
            if (!_markersFoldout) return;

            EditorGUI.indentLevel++;

            int deleteIndex = -1; // 순회 중 리스트를 바꾸지 않도록 삭제는 루프 후 지연 처리
            for (int i = 0; i < _t.markers.Count; i++)
            {
                var mk = _t.markers[i];
                if (mk == null) continue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    // 선택(타임라인 플래그와 양방향 동기화)
                    bool sel = i == _selectedMarker;
                    Color bg = GUI.backgroundColor;
                    if (sel) GUI.backgroundColor = new Color(1f, 0.78f, 0.25f);
                    if (GUILayout.Button("Sel", GUILayout.Width(34)))
                    {
                        _selectedMarker = sel ? -1 : i;
                        Repaint();
                    }
                    GUI.backgroundColor = bg;

                    EditorGUI.BeginChangeCheck();
                    string nm = EditorGUILayout.TextField(mk.name);
                    float tm = EditorGUILayout.FloatField(mk.time, GUILayout.Width(60));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_t, "Edit Marker");
                        mk.name = nm;
                        mk.time = Mathf.Max(0f, tm);
                        EditorUtility.SetDirty(_t);
                    }

                    if (GUILayout.Button(new GUIContent("Now", "Set time to current playhead"), GUILayout.Width(40)))
                    {
                        Undo.RecordObject(_t, "Set Marker Time");
                        mk.time = Mathf.Max(0f, CurrentTimelineTime());
                        EditorUtility.SetDirty(_t);
                    }

                    if (GUILayout.Button(new GUIContent("Del", "Delete marker"), GUILayout.Width(40)))
                        deleteIndex = i;
                }
            }

            if (_t.markers.Count == 0)
                EditorGUILayout.LabelField("No event markers. Add one to fire On Marker (name) at a time.", EditorStyles.miniLabel);

            EditorGUI.indentLevel--;

            if (deleteIndex >= 0) DeleteMarker(deleteIndex);

            // onMarker 콜백(인자: 이벤트 마커 이름 string)
            EditorGUILayout.Space(2);
            serializedObject.Update();
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("onMarker"),
                new GUIContent("On Event Marker", "Invoked when the playhead reaches an event marker; receives the marker's name (string)"));
            serializedObject.ApplyModifiedProperties();
        }

        // 현재 플레이헤드 시각(초): 프리뷰 중이면 _previewTime, Play 모드면 시퀀스 경과, 아니면 0.
        float CurrentTimelineTime()
        {
            if (_previewing) return _previewTime;
            if (Application.isPlaying)
            {
                var seq = _t.Sequence;
                if (seq != null && seq.IsActive()) return seq.Elapsed(false);
            }
            return 0f;
        }

        void AddMarker()
        {
            Undo.RecordObject(_t, "Add Marker");
            if (_t.markers == null) _t.markers = new List<Timeline.Marker>();
            var mk = new Timeline.Marker
            {
                name = "Event Marker " + _t.markers.Count,
                time = Mathf.Max(0f, CurrentTimelineTime()), // 현재 플레이헤드 위치에 추가
            };
            _t.markers.Add(mk);
            _selectedMarker = _t.markers.Count - 1;
            EditorUtility.SetDirty(_t);
        }

        void DeleteMarker(int i)
        {
            if (_t.markers == null || i < 0 || i >= _t.markers.Count) return;
            Undo.RecordObject(_t, "Delete Marker");
            _t.markers.RemoveAt(i);
            if (_selectedMarker == i) _selectedMarker = -1;
            else if (_selectedMarker > i) _selectedMarker--;
            EditorUtility.SetDirty(_t);
        }
    }
}
