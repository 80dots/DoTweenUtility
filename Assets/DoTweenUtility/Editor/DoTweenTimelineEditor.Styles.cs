using UnityEditor;
using UnityEngine;

namespace DoTweenUtility.Editor
{
    // 공용 GUIStyle/텍스처 캐시 + 상단 배너 렌더링.
    public partial class DoTweenTimelineEditor
    {
        // ── 정적 스타일 ──
        static GUIStyle _barStyle;
        static GUIStyle _clipLabelStyle;
        static GUIStyle _sectionFoldout;
        static GUIStyle _bannerTitle;
        static GUIStyle _bannerTitleShadow;
        static GUIStyle _bannerSub;
        static Texture2D _bannerTex;

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
    }
}
