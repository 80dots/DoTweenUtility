# DoTweenUtility

> **English** | [한국어](#한국어)

A Unity utility built on top of [DOTween (Demigiant)](http://dotween.demigiant.com/) that lets you **build, edit, and play tween sequences from the Inspector** — no code required. Add a `DoTweenTimeline` component to any GameObject and sequence tweens on a visual timeline track.

When you are ready to move to code, the built-in **Code View** generates the equivalent DOTween C# and copies it to your clipboard.

---

## Features

- **Inspector timeline editor** — Arrange tweens as clips on a horizontal time track. Drag a clip to move it, drag its right edge to resize, and zoom the track. Snapping is `0.05s` (hold `Ctrl`/`Cmd` to disable). Full Undo support.
- **Per-clip On/Off** — Toggle any clip on or off; disabled clips are excluded from the sequence and from preview/playback.
- **Component-aware tween types** — Pick a target GameObject and component; only the tween types that component supports are shown.
- **Supported tweens**
  - **Transform** — Move / LocalMove / Rotate / LocalRotate / Scale (and per-axis X/Y/Z)
  - **RectTransform (UGUI)** — AnchorPos (X/Y) / SizeDelta
  - **CanvasGroup** — Fade
  - **Graphic** (Image / Text / TMP, etc.) — Color / Fade
  - **SpriteRenderer** — Color / Fade
  - **Virtual** (no target) — `DOVirtual.Float` / `Int` / `Vector3` / `Color`: tween an eased value From → To and receive it each frame via a callback
- **Rich per-clip options** — Start time & duration, target value (with a `Current` capture button), Ease (enum **or** AnimationCurve), `From`, `Relative`, `Snapping`, `RotateMode`, **Override Start Value**, per-clip loops & loop type, and per-clip UnityEvents (`onStart` / `onUpdate` / `onFinish`).
- **Event markers** — Place named markers at specific times on the track. During playback, `onMarker(string name)` fires when the playhead reaches each marker, passing the marker's name. Drag marker flags on the ruler to move them; edit name/time in the **Event Markers** section.
- **Timeline settings** — Auto-play (`Manual` / `OnAwake` / `OnStart` / `OnEnable`), `Ignore Time Scale`, `Kill Conflicting Tweens On Play`, timeline-wide loops & loop type, and timeline-wide UnityEvents.
- **Live preview & playhead** — Scrub or play the timeline in Edit mode (loops are reflected), with a red playhead indicator. The same red playhead is shown during Play mode.
- **Code View** — Generate a DOTween C# method that reproduces the timeline and copy it to the clipboard.
- **Runtime API** — `Play()`, `Restart()`, `Pause()`, `Resume()`, `TogglePause()`, `Complete()`, `Kill()`, `Build()`, plus `IsPlaying`, `Sequence`, and `TotalDuration`.

## Requirements

- **Unity 6** (developed on `6000.5.0f1`).
- **[DOTween (Demigiant)](http://dotween.demigiant.com/)** must be installed in the project (`using DG.Tweening;`).
  - Enable the **UI** and **Sprite** modules via **Tools ▸ Demigiant ▸ DOTween Utility Panel ▸ Setup DOTween…** so that UGUI / SpriteRenderer tweens are available.
- The new **Input System** and **URP** are used by the host project template but are **not** required by this utility itself.

## Installation

1. **Install DOTween** first (Asset Store or the official package), then run **Setup DOTween…** and enable the **UI** + **Sprite** modules.
2. **Add DoTweenUtility** using either option:
   - Download the latest `DoTweenUtility_vX.Y.Z.unitypackage` from the [Releases](https://github.com/80dots/DoTweenUtility/releases) page and import it via **Assets ▸ Import Package ▸ Custom Package…**, **or**
   - Copy the `Assets/DoTweenUtility/` folder into your project.

## Usage

1. Select a GameObject and choose **Add Component ▸ DOTween ▸ DoTween Timeline**.
2. Press **＋ Add Clip**. In the **Selected Clip** panel set the **Target**, **Component**, and **Tween Type**, then the **target value**, **timing**, and **ease**.
3. (Optional) In the **Event Markers** section press **＋ Add Event Marker** to place a named marker, then wire the **On Event Marker** event to a method taking a `string` — it receives the marker name when the playhead reaches it during playback. You can also drag marker flags on the ruler to reposition them.
4. Turn on **Preview Mode** to preview in Edit mode (transport: Prev Frame · From Start · Play · Stop · Next Frame · Progress bar; editing is locked while previewing), or set **Auto Play** to run automatically in Play mode.
5. To drive it from code:

   ```csharp
   var timeline = GetComponent<DoTweenUtility.DoTweenTimeline>();
   timeline.Play();      // rebuild and play from the start
   timeline.Pause();
   timeline.Restart();
   ```

6. Expand **Code View** at the bottom of the Inspector and press **📋 Copy to Clipboard** to get the equivalent DOTween C# (a method named `<GameObjectName>DoTweenTimeline`).

## Notes

- DOTween is included in this repository as a **DLL** under `Assets/Plugins/Demigiant/` and must not be edited directly; manage modules through the DOTween Utility Panel.
- Generated Code View targets are resolved with `GameObject.Find(name)`, so make sure target names are unique or replace the references manually. Events, markers, and "Kill Conflicting" are not included in generated code.
- Marker callbacks fire only in **Play mode**. Edit-mode preview scrubs via `Sequence.Goto`, which does not fire callbacks, so game logic wired to `onMarker` won't run while previewing in the editor.

---
---

# 한국어

> [English](#dotweenutility) | **한국어**

[DOTween (Demigiant)](http://dotween.demigiant.com/) 위에 얹어 쓰는 Unity 유틸리티입니다. **인스펙터에서 트윈 시퀀스를 만들고, 편집하고, 재생**할 수 있어 코드 없이도 동작합니다. 임의의 GameObject에 `DoTweenTimeline` 컴포넌트를 붙이면 시각적인 타임라인 트랙 위에서 트윈을 배치할 수 있습니다.

코드로 옮길 준비가 되면, 내장된 **Code View**가 동등한 DOTween C# 코드를 생성해 클립보드로 복사해 줍니다.

---

## 주요 기능

- **인스펙터 타임라인 에디터** — 트윈을 가로 시간축 위 클립으로 배치합니다. 막대를 드래그해 이동, 우측 끝을 드래그해 길이 조절, 트랙 줌이 가능합니다. 스냅은 `0.05초`(`Ctrl`/`Cmd`로 해제). Undo를 완전 지원합니다.
- **클립별 On/Off** — 각 클립을 켜고 끌 수 있으며, 비활성 클립은 시퀀스와 프리뷰/재생에서 제외됩니다.
- **컴포넌트 기반 트윈 타입** — 대상 GameObject와 컴포넌트를 고르면, 그 컴포넌트가 지원하는 트윈 타입만 표시됩니다.
- **지원 트윈**
  - **Transform** — Move / LocalMove / Rotate / LocalRotate / Scale (및 축별 X/Y/Z)
  - **RectTransform (UGUI)** — AnchorPos (X/Y) / SizeDelta
  - **CanvasGroup** — Fade
  - **Graphic** (Image / Text / TMP 등) — Color / Fade
  - **SpriteRenderer** — Color / Fade
  - **Virtual** (타깃 없음) — `DOVirtual.Float` / `Int` / `Vector3` / `Color`: From → To 의 이징된 값을 트윈하여 매 프레임 콜백으로 전달
- **풍부한 클립 옵션** — 시작 시간·길이, 목표값(`Current` 캡처 버튼 포함), 이징(enum **또는** AnimationCurve), `From`, `Relative`, `Snapping`, `RotateMode`, **Override Start Value**, 클립별 루프·루프 타입, 클립별 UnityEvent(`onStart` / `onUpdate` / `onFinish`).
- **이벤트 마커** — 트랙의 특정 시각에 이름 있는 마커를 놓습니다. 재생 중 플레이헤드가 마커에 도달하면 `onMarker(string name)`이 마커 이름을 인자로 호출됩니다. 룰러의 마커 플래그를 드래그해 위치를 옮기고, **Markers** 섹션에서 이름/시각을 편집합니다.
- **타임라인 설정** — 자동 재생(`Manual` / `OnAwake` / `OnStart` / `OnEnable`), `Ignore Time Scale`, `Kill Conflicting Tweens On Play`, 타임라인 전체 루프·루프 타입, 타임라인 전체 UnityEvent.
- **실시간 프리뷰 & 플레이헤드** — Edit 모드에서 스크럽하거나 재생할 수 있고(루프 반영), 빨간 플레이헤드가 표시됩니다. Play 모드에서도 동일한 빨간 플레이헤드가 나타납니다.
- **Code View** — 타임라인을 재현하는 DOTween C# 메서드를 생성해 클립보드로 복사합니다.
- **런타임 API** — `Play()`, `Restart()`, `Pause()`, `Resume()`, `TogglePause()`, `Complete()`, `Kill()`, `Build()` 및 `IsPlaying`, `Sequence`, `TotalDuration`.

## 필요 사항

- **Unity 6** (`6000.5.0f1`에서 개발).
- 프로젝트에 **[DOTween (Demigiant)](http://dotween.demigiant.com/)** 설치 필요 (`using DG.Tweening;`).
  - UGUI / SpriteRenderer 트윈을 쓰려면 **Tools ▸ Demigiant ▸ DOTween Utility Panel ▸ Setup DOTween…** 에서 **UI**·**Sprite** 모듈을 켜세요.
- 새 **Input System**·**URP**는 호스트 프로젝트 템플릿이 사용할 뿐, 이 유틸리티 자체의 필수 요건은 **아닙니다**.

## 설치

1. 먼저 **DOTween을 설치**(에셋 스토어 또는 공식 패키지)하고 **Setup DOTween…** 을 실행해 **UI**·**Sprite** 모듈을 켭니다.
2. 다음 중 하나로 **DoTweenUtility를 추가**합니다:
   - [Releases](https://github.com/80dots/DoTweenUtility/releases)에서 최신 `DoTweenUtility_vX.Y.Z.unitypackage`를 내려받아 **Assets ▸ Import Package ▸ Custom Package…** 로 임포트, 또는
   - `Assets/DoTweenUtility/` 폴더를 프로젝트로 복사.

## 사용법

1. GameObject를 선택하고 **Add Component ▸ DOTween ▸ DoTween Timeline** 을 추가합니다.
2. **＋ Add Clip** 을 누릅니다. **Selected Clip** 패널에서 **Target**, **Component**, **Tween Type** 을 지정한 뒤 **목표값**, **타이밍**, **이징** 을 설정합니다.
3. (선택) **Event Markers** 섹션에서 **＋ Add Event Marker** 로 이름 있는 마커를 놓고, **On Event Marker** 이벤트를 `string` 인자를 받는 메서드에 연결합니다 — 재생 중 플레이헤드가 마커에 도달하면 마커 이름이 전달됩니다. 룰러의 마커 플래그를 드래그해 위치를 옮길 수도 있습니다.
4. **Preview Mode** 를 켜서 Edit 모드에서 미리봅니다(트랜스포트: Prev Frame · From Start · Play · Stop · Next Frame · Progress 바. 프리뷰 중에는 편집이 잠깁니다). 또는 **Auto Play** 를 설정해 Play 모드에서 자동 재생합니다.
5. 코드로 제어하려면:

   ```csharp
   var timeline = GetComponent<DoTweenUtility.DoTweenTimeline>();
   timeline.Play();      // 재빌드 후 처음부터 재생
   timeline.Pause();
   timeline.Restart();
   ```

6. 인스펙터 맨 아래 **Code View** 를 펼치고 **📋 Copy to Clipboard** 를 누르면 동등한 DOTween C# 코드(`<GameObject이름>DoTweenTimeline` 메서드)를 얻을 수 있습니다.

## 참고

- DOTween은 이 저장소에 `Assets/Plugins/Demigiant/` 아래 **DLL**로 포함되어 있으며 직접 수정하면 안 됩니다. 모듈은 DOTween Utility Panel을 통해 관리하세요.
- Code View가 생성하는 코드의 타깃은 `GameObject.Find(name)` 으로 참조되므로, 타깃 이름이 유일하도록 하거나 참조를 직접 교체하세요. 이벤트·마커·"Kill Conflicting"은 생성 코드에 포함되지 않습니다.
- 마커 콜백은 **Play 모드에서만** 발화합니다. Edit 모드 프리뷰는 `Sequence.Goto`로 스크럽하는데 이때는 콜백이 호출되지 않으므로, `onMarker`에 연결한 게임 로직은 에디터 프리뷰 중에는 실행되지 않습니다.

---
---

# Changelog / 변경 이력

Each entry lists changes in **English** and **한국어**. Unreleased items roll into a version on the next release.
각 항목은 **영어**와 **한국어**로 변경 사항을 적습니다. Unreleased 항목은 다음 릴리스에서 버전으로 묶입니다.

## Unreleased

## v0.5.0

- **EN:** Add a **Virtual** clip type backed by `DOVirtual` — a tween with **no target** that interpolates an eased value from **From → To** and reports it every frame through an **On Virtual Update** callback you wire in the Inspector. Supports four value types via a **Value Type** dropdown: **Float / Int / Vector3 / Color** (`UnityEvent<float/int/Vector3/Color>`). Each clip has an **Object Mode / Virtual Mode** toggle at the top of its panel; in Virtual Mode the target/component pickers are hidden and you just pick the value type, set From/To, and pick an Ease (DOTween eases the value). Note: like markers, the callback runs during editor preview too (the value is applied via the tween setter).
- **KO:** `DOVirtual` 기반 **Virtual** 클립 타입 추가 — **타깃이 없는** 트윈으로, **From → To** 의 이징된 값을 보간해 매 프레임 **On Virtual Update** 콜백으로 전달합니다(Inspector에서 연결). **Value Type** 드롭다운으로 네 가지 값 타입 지원: **Float / Int / Vector3 / Color**(`UnityEvent<float/int/Vector3/Color>`). 각 클립 패널 상단에 **Object Mode / Virtual Mode** 토글이 있고, Virtual Mode에서는 타깃/컴포넌트 선택이 숨겨지고 값 타입·From/To·Ease만 지정하면 DOTween이 값에 이징을 적용합니다. 참고: 콜백은 에디터 프리뷰에서도 실행됩니다(값이 트윈 setter로 적용되기 때문).

## v0.4.0

- **EN:** Internal refactor (no behavior change): split the long runtime and editor scripts into feature-focused `partial class` files (runtime → `DoTweenTimeline.cs` / `.Data.cs` / `.Build.cs`; editor → `DoTweenTimelineEditor.cs` / `.Track.cs` / `.ClipInspector.cs` / `.Markers.cs` / `.Preview.cs` / `.CodeView.cs` / `.Styles.cs`), and extracted the reusable Android-style toggle switch into a standalone `SwitchGui` class.
- **KO:** 내부 리팩토링(동작 변화 없음): 길어진 런타임/에디터 스크립트를 기능별 `partial class` 파일로 분리(런타임 → `DoTweenTimeline.cs` / `.Data.cs` / `.Build.cs`, 에디터 → `DoTweenTimelineEditor.cs` / `.Track.cs` / `.ClipInspector.cs` / `.Markers.cs` / `.Preview.cs` / `.CodeView.cs` / `.Styles.cs`)하고, 재사용 가능한 안드로이드 스타일 토글 스위치를 별도 `SwitchGui` 클래스로 추출.
- **EN:** Add **event markers**. Place named markers at specific times on the timeline; during playback `onMarker(string name)` (a `UnityEvent<string>`) fires when the playhead reaches each marker, passing the marker's name. Markers appear as amber flags on the ruler (drag to move) and are edited in a new **Event Markers** section (name, time, "Now" = current playhead, delete) with the **On Event Marker** callback field. Built via `Sequence.InsertCallback`; markers fire only in Play mode (editor preview scrubbing does not trigger callbacks).
- **KO:** **이벤트 마커** 추가. 타임라인 특정 시각에 이름 있는 마커를 놓으면, 재생 중 플레이헤드가 도달할 때 `onMarker(string name)`(`UnityEvent<string>`)이 마커 이름을 인자로 호출됩니다. 마커는 룰러에 앰버색 플래그로 표시(드래그로 이동)되고, 새 **Event Markers** 섹션(이름·시각·"Now"=현재 플레이헤드·삭제)과 **On Event Marker** 콜백 필드에서 편집합니다. `Sequence.InsertCallback`으로 빌드되며, 마커는 Play 모드에서만 발화합니다(에디터 프리뷰 스크럽은 콜백을 호출하지 않음).
- **EN:** Preview Mode transport buttons are shown as **text labels** (Prev Frame, From Start, Play, Stop, Next Frame) instead of glyph icons. **From Start** rewinds to 0 and plays forward; the Play button is highlighted while playing.
- **KO:** Preview Mode 트랜스포트 버튼을 아이콘 글리프 대신 **텍스트 라벨**(Prev Frame, From Start, Play, Stop, Next Frame)로 표시. **From Start** 는 0으로 되감아 정방향 재생하며, 재생 중에는 Play 버튼을 강조.
- **EN:** Make the custom Android-style On/Off toggles **render as disabled** (dimmed + click-blocked) while Preview Mode locks the rest of the inspector. Previously these custom-drawn switches ignored `GUI.enabled`, so they looked fully active even when disabled.
- **KO:** Preview Mode가 나머지 인스펙터를 잠그는 동안, 커스텀 안드로이드 On/Off 토글도 **비활성 상태로 표시**(흐리게 + 클릭 차단)되도록 수정. 기존에는 이 커스텀 스위치가 `GUI.enabled`를 무시해 비활성화돼도 활성처럼 보였음.
- **EN:** Replace the "Start Preview" button with a **Preview Mode** on/off toggle that clearly separates editing from previewing. While Preview Mode is on it shows a transport and **locks all other editing** (toolbar, selected-clip panel, timeline settings, code view, and clip drag/select) so the live scene preview can't be corrupted by edits; turning it off restores the original values.
- **KO:** 기존 "Start Preview" 버튼을 **Preview Mode** On/Off 토글로 교체해 편집과 프리뷰를 명확히 분리. Preview Mode가 켜진 동안 트랜스포트를 제공하고 **나머지 모든 편집(툴바·선택 클립 패널·타임라인 설정·코드 뷰·클립 드래그/선택)을 비활성화**해 프리뷰 중 실제 씬 값이 오염되지 않도록 함. 끄면 원본 값을 복구.
- **EN:** Add a bilingual README and a Changelog section; document the rule that every commit updates the README.
- **KO:** 영문/한국어 README와 Changelog 섹션을 추가하고, 매 커밋마다 README를 갱신하는 규칙을 문서화.

## v0.3.0

- **EN:** Per-clip On/Off (`enabled`); a Code View that generates a DOTween C# method reproducing the timeline and copies it to the clipboard; On/Off toggle switches ~10% smaller.
- **KO:** 클립별 On/Off(`enabled`), 타임라인을 재현하는 DOTween C# 메서드를 생성·복사하는 Code View, 약 10% 작아진 On/Off 토글 스위치.

## v0.2.0

- **EN:** Loop-aware Edit-mode preview (reflects timeline/clip loops); a red playhead indicator shown in Play mode as well; fixed the DOTween "infinite loops inside a Sequence" warning.
- **KO:** 루프를 반영하는 Edit 모드 프리뷰(타임라인/클립 루프 반영), Play 모드에도 표시되는 빨간 플레이헤드, DOTween "시퀀스 내 무한 루프" 경고 수정.

## v0.1.0

- **EN:** First preview release — the `DoTweenTimeline` component with an Inspector timeline editor, supported tween types, edit-mode preview, and runtime control API.
- **KO:** 첫 프리릴리스 — 인스펙터 타임라인 에디터, 지원 트윈 타입, Edit 모드 프리뷰, 런타임 제어 API를 갖춘 `DoTweenTimeline` 컴포넌트.
