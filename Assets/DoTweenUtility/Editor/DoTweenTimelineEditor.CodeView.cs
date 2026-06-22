using System.Globalization;
using System.Text;
using DG.Tweening;
using UnityEditor;
using UnityEngine;
using Timeline = global::DoTweenUtility.DoTweenTimeline;

namespace DoTweenUtility.Editor
{
    // Code View: 현재 타임라인을 재현하는 DOTween C# 코드 생성 + 클립보드 복사.
    public partial class DoTweenTimelineEditor
    {
        static GUIStyle _codeStyle;

        void DrawCodeView()
        {
            _codeViewFoldout = EditorGUILayout.Foldout(_codeViewFoldout, "Code View", true, _sectionFoldout);
            if (!_codeViewFoldout) return;

            string code = GenerateCode();

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("이 타임라인을 재현하는 DOTween C# 코드", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("📋 Copy to Clipboard", GUILayout.Width(160), GUILayout.Height(18)))
                {
                    EditorGUIUtility.systemCopyBuffer = code;
                    if (EditorWindow.focusedWindow != null)
                        EditorWindow.focusedWindow.ShowNotification(new GUIContent("Code copied to clipboard"));
                }
            }

            if (_codeStyle == null)
            {
                _codeStyle = new GUIStyle(EditorStyles.textArea)
                {
                    wordWrap = false,
                    richText = false,
                    font = EditorStyles.miniLabel.font,
                    fontSize = 11,
                };
            }

            int lineCount = 1;
            for (int i = 0; i < code.Length; i++) if (code[i] == '\n') lineCount++;
            float contentH = lineCount * 13f + 8f;
            float viewH = Mathf.Min(contentH, 360f);

            _codeScroll = EditorGUILayout.BeginScrollView(_codeScroll, GUILayout.Height(viewH));
            EditorGUILayout.SelectableLabel(code, _codeStyle, GUILayout.Height(contentH), GUILayout.ExpandWidth(true));
            EditorGUILayout.EndScrollView();
        }

        // 현재 clips/설정으로부터, Build()와 동일하게 동작하는 DOTween Sequence 생성 코드를 만든다.
        // (이벤트/마커/Kill-Conflicting은 코드로 재현하지 않으며, 타깃은 이름으로 GameObject.Find 한다.)
        string GenerateCode()
        {
            var sb = new StringBuilder();
            string methodName = SanitizeIdentifier(_t.gameObject.name) + "DoTweenTimeline";

            sb.AppendLine("// Auto-generated from DoTweenTimeline — reproduces this timeline as a DOTween Sequence.");
            sb.AppendLine("// 주의: 타깃은 이름으로 GameObject.Find 하므로 이름이 유일해야 한다(필요 시 참조를 직접 교체).");
            sb.AppendLine("// 이벤트(onStart/onUpdate/onFinish)와 Kill-Conflicting은 포함되지 않는다.");
            sb.AppendLine($"public Sequence {methodName}()");
            sb.AppendLine("{");
            sb.AppendLine("    var seq = DOTween.Sequence();");
            sb.AppendLine("    seq.SetAutoKill(false);");
            sb.AppendLine($"    seq.SetUpdate({B(_t.ignoreTimeScale)});");

            int idx = 0;
            foreach (var clip in _t.clips)
            {
                if (clip == null || !clip.enabled) { idx++; continue; }
                bool isVirtual = Timeline.IsVirtual(clip.tweenType);
                if (!isVirtual && clip.target == null) { idx++; continue; }

                string label = string.IsNullOrEmpty(clip.label) ? clip.tweenType.ToString() : clip.label;
                sb.AppendLine();
                sb.AppendLine($"    // Clip {idx}: {label} ({clip.tweenType})");
                sb.AppendLine("    {");
                if (!isVirtual)
                    sb.AppendLine($"        var go = GameObject.Find(\"{Esc(clip.target.name)}\");");
                sb.AppendLine($"        Tweener t = {ClipCall(clip)};");

                if (clip.useCurveEase && clip.easeCurve != null && clip.easeCurve.length > 0)
                    sb.AppendLine($"        t.SetEase({CurveExpr(clip.easeCurve)});");
                else
                    sb.AppendLine($"        t.SetEase(Ease.{clip.ease});");

                if (clip.loops != 1)
                {
                    string loopsExpr = clip.loops < 0 ? "int.MaxValue" : clip.loops.ToString(CultureInfo.InvariantCulture);
                    sb.AppendLine($"        t.SetLoops({loopsExpr}, LoopType.{clip.loopType});");
                }
                if (clip.relative) sb.AppendLine("        t.SetRelative();");
                if (clip.from) sb.AppendLine("        t.From();");
                if (clip.overrideStart) sb.AppendLine($"        t.ChangeStartValue({StartValueExpr(clip)});");

                sb.AppendLine($"        seq.Insert({F(clip.startTime)}, t);");
                sb.AppendLine("    }");
                idx++;
            }

            sb.AppendLine();
            string tlLoops = _t.timelineLoops.ToString(CultureInfo.InvariantCulture);
            sb.AppendLine($"    seq.SetLoops({tlLoops}, LoopType.{_t.timelineLoopType});");
            sb.AppendLine("    return seq;");
            sb.AppendLine("}");
            return sb.ToString();
        }

        // 클립 → DOTween 숏컷 호출식(타깃 컴포넌트 접근 포함). BuildTween()의 매핑과 일치시킨다.
        static string ClipCall(Timeline.Clip c)
        {
            string tr = "go.transform";
            string rt = "((RectTransform)go.transform)";
            float d = Mathf.Max(0f, c.duration);
            string dS = F(d), snap = B(c.snapping);

            switch (c.tweenType)
            {
                // Virtual: 타깃 없음. 콜백(On Virtual Update)은 코드로 재현하지 않는다(빈 람다).
                case Timeline.TweenType.VirtualFloat:   return $"DOVirtual.Float({F(c.fromFloatValue)}, {F(c.floatValue)}, {dS}, v => {{ }})";
                case Timeline.TweenType.VirtualInt:     return $"DOVirtual.Int({Mathf.RoundToInt(c.fromFloatValue)}, {Mathf.RoundToInt(c.floatValue)}, {dS}, v => {{ }})";
                case Timeline.TweenType.VirtualVector3: return $"DOVirtual.Vector3({V3(c.fromVectorValue)}, {V3(c.vectorValue)}, {dS}, v => {{ }})";
                case Timeline.TweenType.VirtualColor:   return $"DOVirtual.Color({Col(c.fromColorValue)}, {Col(c.colorValue)}, {dS}, v => {{ }})";
                case Timeline.TweenType.Move:       return $"{tr}.DOMove({V3(c.vectorValue)}, {dS}, {snap})";
                case Timeline.TweenType.MoveX:      return $"{tr}.DOMoveX({F(c.floatValue)}, {dS}, {snap})";
                case Timeline.TweenType.MoveY:      return $"{tr}.DOMoveY({F(c.floatValue)}, {dS}, {snap})";
                case Timeline.TweenType.MoveZ:      return $"{tr}.DOMoveZ({F(c.floatValue)}, {dS}, {snap})";
                case Timeline.TweenType.LocalMove:  return $"{tr}.DOLocalMove({V3(c.vectorValue)}, {dS}, {snap})";
                case Timeline.TweenType.LocalMoveX: return $"{tr}.DOLocalMoveX({F(c.floatValue)}, {dS}, {snap})";
                case Timeline.TweenType.LocalMoveY: return $"{tr}.DOLocalMoveY({F(c.floatValue)}, {dS}, {snap})";
                case Timeline.TweenType.LocalMoveZ: return $"{tr}.DOLocalMoveZ({F(c.floatValue)}, {dS}, {snap})";
                case Timeline.TweenType.Rotate:      return $"{tr}.DORotate({V3(c.vectorValue)}, {dS}, RotateMode.{c.rotateMode})";
                case Timeline.TweenType.LocalRotate: return $"{tr}.DOLocalRotate({V3(c.vectorValue)}, {dS}, RotateMode.{c.rotateMode})";
                case Timeline.TweenType.Scale:  return $"{tr}.DOScale({V3(c.vectorValue)}, {dS})";
                case Timeline.TweenType.ScaleX: return $"{tr}.DOScaleX({F(c.floatValue)}, {dS})";
                case Timeline.TweenType.ScaleY: return $"{tr}.DOScaleY({F(c.floatValue)}, {dS})";
                case Timeline.TweenType.ScaleZ: return $"{tr}.DOScaleZ({F(c.floatValue)}, {dS})";
                case Timeline.TweenType.AnchorPos:  return $"{rt}.DOAnchorPos({V2(c.vectorValue)}, {dS}, {snap})";
                case Timeline.TweenType.AnchorPosX: return $"{rt}.DOAnchorPosX({F(c.floatValue)}, {dS}, {snap})";
                case Timeline.TweenType.AnchorPosY: return $"{rt}.DOAnchorPosY({F(c.floatValue)}, {dS}, {snap})";
                case Timeline.TweenType.SizeDelta:  return $"{rt}.DOSizeDelta({V2(c.vectorValue)}, {dS}, {snap})";
                case Timeline.TweenType.CanvasGroupFade: return $"go.GetComponent<CanvasGroup>().DOFade({F(c.floatValue)}, {dS})";
                case Timeline.TweenType.GraphicColor:    return $"go.GetComponent<Graphic>().DOColor({Col(c.colorValue)}, {dS})";
                case Timeline.TweenType.GraphicFade:     return $"go.GetComponent<Graphic>().DOFade({F(c.floatValue)}, {dS})";
                case Timeline.TweenType.SpriteColor:     return $"go.GetComponent<SpriteRenderer>().DOColor({Col(c.colorValue)}, {dS})";
                case Timeline.TweenType.SpriteFade:      return $"go.GetComponent<SpriteRenderer>().DOFade({F(c.floatValue)}, {dS})";
                default: return $"/* 미지원 TweenType: {c.tweenType} */ null";
            }
        }

        // overrideStart 시작값을 트윈 내부 타입에 맞는 리터럴로. GetStartValueBoxed()와 일치.
        static string StartValueExpr(Timeline.Clip c)
        {
            switch (c.tweenType)
            {
                case Timeline.TweenType.Move:
                case Timeline.TweenType.LocalMove:
                case Timeline.TweenType.Rotate:
                case Timeline.TweenType.LocalRotate:
                case Timeline.TweenType.Scale:
                    return V3(c.fromVectorValue);
                case Timeline.TweenType.AnchorPos:
                case Timeline.TweenType.SizeDelta:
                    return V2(c.fromVectorValue);
                case Timeline.TweenType.GraphicColor:
                case Timeline.TweenType.SpriteColor:
                    return Col(c.fromColorValue);
                default:
                    return F(c.fromFloatValue);
            }
        }

        static string CurveExpr(AnimationCurve curve)
        {
            var sb = new StringBuilder("new AnimationCurve(");
            for (int i = 0; i < curve.length; i++)
            {
                var k = curve[i];
                if (i > 0) sb.Append(", ");
                sb.Append($"new Keyframe({F(k.time)}, {F(k.value)})");
            }
            sb.Append(")");
            return sb.ToString();
        }

        // ── 리터럴 포매터 ──
        static string F(float v) => v.ToString("0.#####", CultureInfo.InvariantCulture) + "f";
        static string B(bool v) => v ? "true" : "false";
        static string V3(Vector3 v) => $"new Vector3({F(v.x)}, {F(v.y)}, {F(v.z)})";
        static string V2(Vector3 v) => $"new Vector2({F(v.x)}, {F(v.y)})";
        static string Col(Color c) => $"new Color({F(c.r)}, {F(c.g)}, {F(c.b)}, {F(c.a)})";
        static string Esc(string s) => s == null ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        // GameObject 이름을 유효한 C# 식별자로 정리(영문/숫자/유니코드 문자 외는 '_', 숫자로 시작하면 '_' 접두).
        static string SanitizeIdentifier(string name)
        {
            if (string.IsNullOrEmpty(name)) return "_";
            var sb = new StringBuilder();
            foreach (char ch in name)
                sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
            string s = sb.ToString();
            if (char.IsDigit(s[0])) s = "_" + s;
            return s;
        }
    }
}
