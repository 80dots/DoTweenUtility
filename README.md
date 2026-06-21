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
- **Rich per-clip options** — Start time & duration, target value (with a `Current` capture button), Ease (enum **or** AnimationCurve), `From`, `Relative`, `Snapping`, `RotateMode`, **Override Start Value**, per-clip loops & loop type, and per-clip UnityEvents (`onStart` / `onUpdate` / `onFinish`).
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
3. Use **Start Preview ▸ ▶ Play** to preview in Edit mode, or set **Auto Play** to run automatically in Play mode.
4. To drive it from code:

   ```csharp
   var timeline = GetComponent<DoTweenUtility.DoTweenTimeline>();
   timeline.Play();      // rebuild and play from the start
   timeline.Pause();
   timeline.Restart();
   ```

5. Expand **Code View** at the bottom of the Inspector and press **📋 Copy to Clipboard** to get the equivalent DOTween C# (a method named `<GameObjectName>DoTweenTimeline`).

## Notes

- DOTween is included in this repository as a **DLL** under `Assets/Plugins/Demigiant/` and must not be edited directly; manage modules through the DOTween Utility Panel.
- Generated Code View targets are resolved with `GameObject.Find(name)`, so make sure target names are unique or replace the references manually. Events and "Kill Conflicting" are not included in generated code.

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
- **풍부한 클립 옵션** — 시작 시간·길이, 목표값(`Current` 캡처 버튼 포함), 이징(enum **또는** AnimationCurve), `From`, `Relative`, `Snapping`, `RotateMode`, **Override Start Value**, 클립별 루프·루프 타입, 클립별 UnityEvent(`onStart` / `onUpdate` / `onFinish`).
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
3. **Start Preview ▸ ▶ Play** 로 Edit 모드에서 미리보거나, **Auto Play** 를 설정해 Play 모드에서 자동 재생합니다.
4. 코드로 제어하려면:

   ```csharp
   var timeline = GetComponent<DoTweenUtility.DoTweenTimeline>();
   timeline.Play();      // 재빌드 후 처음부터 재생
   timeline.Pause();
   timeline.Restart();
   ```

5. 인스펙터 맨 아래 **Code View** 를 펼치고 **📋 Copy to Clipboard** 를 누르면 동등한 DOTween C# 코드(`<GameObject이름>DoTweenTimeline` 메서드)를 얻을 수 있습니다.

## 참고

- DOTween은 이 저장소에 `Assets/Plugins/Demigiant/` 아래 **DLL**로 포함되어 있으며 직접 수정하면 안 됩니다. 모듈은 DOTween Utility Panel을 통해 관리하세요.
- Code View가 생성하는 코드의 타깃은 `GameObject.Find(name)` 으로 참조되므로, 타깃 이름이 유일하도록 하거나 참조를 직접 교체하세요. 이벤트와 "Kill Conflicting"은 생성 코드에 포함되지 않습니다.
