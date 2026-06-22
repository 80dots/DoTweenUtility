using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DoTweenUtility.Editor
{
    /// <summary>
    /// 안드로이드 스타일 슬라이딩 On/Off 토글 스위치를 그리는 재사용 가능한 에디터 GUI 유틸리티.
    /// 코드로 생성한 원형 텍스처 + 스무스 노브 애니메이션을 사용한다. 라벨별 애니메이션 상태를
    /// 내부 정적 딕셔너리로 보관하므로 호출자는 별도 상태를 들고 있을 필요가 없다.
    /// 다른 커스텀 Inspector/EditorWindow에서도 그대로 재사용할 수 있다.
    /// </summary>
    internal static class SwitchGui
    {
        const float AnimSpeed = 9f; // 약 0.11초에 끝까지 이동
        static readonly Dictionary<string, float> s_anim = new Dictionary<string, float>();
        static Texture2D s_circleTex;

        /// <summary>
        /// 라벨 + 슬라이딩 스위치 한 줄을 그린다. 클릭으로 토글되며 변경된 값을 반환한다.
        /// <see cref="GUI.enabled"/> == false면 클릭을 막고 흐리게 그려 비활성 상태를 반영한다.
        /// 노브가 목표 위치로 이동 중이면 <paramref name="animating"/>을 true로 만들어
        /// 호출자가 다음 프레임 Repaint를 요청하게 한다.
        /// </summary>
        public static bool Toggle(string label, bool value, float dt, ref bool animating)
        {
            bool enabled = GUI.enabled;

            Rect row = EditorGUILayout.GetControlRect();
            Rect labelRect = new Rect(row.x, row.y, EditorGUIUtility.labelWidth, row.height);
            EditorGUI.LabelField(labelRect, label);

            const float swW = 30.6f, swH = 16.2f; // 기존 34×18에서 약 10% 축소
            Rect sw = new Rect(row.x + EditorGUIUtility.labelWidth, row.y + (row.height - swH) * 0.5f, swW, swH);

            Event e = Event.current;
            if (enabled && e.type == EventType.MouseDown && e.button == 0 && sw.Contains(e.mousePosition))
            {
                value = !value;
                GUI.changed = true;
                e.Use();
            }
            if (enabled) EditorGUIUtility.AddCursorRect(sw, MouseCursor.Link);

            // 노브 위치(0..1)를 target으로 부드럽게 이동
            float target = value ? 1f : 0f;
            if (!s_anim.TryGetValue(label, out float t)) t = target;
            t = Mathf.MoveTowards(t, target, AnimSpeed * dt);
            s_anim[label] = t;
            if (enabled && !Mathf.Approximately(t, target)) animating = true;

            DrawSwitch(sw, t, enabled);
            return value;
        }

        // t: 노브 위치/색 보간값 (0 = OFF, 1 = ON). enabled=false면 흐리게(비활성 표시).
        static void DrawSwitch(Rect r, float t, bool enabled)
        {
            // 트랙(알약): OFF=회색 → ON=초록 보간
            Color off = new Color(0.42f, 0.42f, 0.45f);
            Color on = new Color(0.298f, 0.776f, 0.435f);
            Color track = Color.Lerp(off, on, t);

            // 비활성: 채도를 낮추고 전체를 반투명하게 → Unity 기본 disabled 룩과 맞춤
            float dim = enabled ? 1f : 0.45f;
            if (!enabled) track = Color.Lerp(track, new Color(0.5f, 0.5f, 0.5f), 0.5f);
            track.a *= dim;
            DrawPill(r, track);

            // 노브(흰 원) + 옅은 그림자
            const float pad = 2f;
            float knobD = r.height - pad * 2f;
            float left = r.x + pad;
            float right = r.xMax - knobD - pad;
            float kx = Mathf.Lerp(left, right, t);
            Rect knob = new Rect(kx, r.y + pad, knobD, knobD);
            DrawCircle(new Rect(knob.x, knob.y + 1f, knob.width, knob.height), new Color(0f, 0f, 0f, 0.25f * dim));
            DrawCircle(knob, new Color(1f, 1f, 1f, dim));
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
            if (s_circleTex != null) return s_circleTex;

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
            s_circleTex = tex;
            return tex;
        }
    }
}
