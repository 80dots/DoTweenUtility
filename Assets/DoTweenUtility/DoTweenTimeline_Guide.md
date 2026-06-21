# DoTweenTimeline 에이전트 가이드

DOTween을 **Inspector GUI로 제어**하는 Unity 에디터 에셋. GameObject에 `DoTweenTimeline` 컴포넌트를 붙이면, 씬 내 임의 GameObject의 Component에 대한 트윈을 타임라인 트랙 GUI로 만들고 재생할 수 있다.

> **이 에셋을 수정하기 전에 이 문서를 먼저 읽고, 변경 후 이 문서도 갱신할 것.**

---

## 파일 구성

| 파일 | 네임스페이스 | 역할 |
|---|---|---|
| `Core/DoTweenTimeline.cs` | `DoTweenUtility` | 런타임 컴포넌트. 데이터 모델(`Clip`) + `Sequence` 빌드/재생 + 충돌 트윈 레지스트리 |
| `Editor/DoTweenTimelineEditor.cs` | `DoTweenUtility.Editor` | 커스텀 Inspector. 배너 + 타임라인 트랙 뷰 + 클립 상세 + 미리보기 + 안드로이드 스위치 |

- 에디터 코드는 반드시 **`Editor/` 폴더** 아래에 둬야 `Assembly-CSharp-Editor`로 컴파일된다(빌드 제외).
- 폴더명은 원래 `DoTweenTimeline`이었다가 **`DoTweenUtility`로 변경**됨(앞으로 이 폴더 하위에 기능별 하위 폴더를 만들어 파일 생성).

### ⚠️ 네임스페이스 = 클래스명 주의
런타임 클래스가 `DoTweenUtility.DoTweenTimeline`이라 네임스페이스(`DoTweenUtility`)와 별개지만, 에디터에서 모호함을 피하려고 별칭을 쓴다:
```csharp
using Timeline = global::DoTweenUtility.DoTweenTimeline;
```
에디터 코드 전반에서 `Timeline.Clip`, `Timeline.TweenType` 형태로 참조한다.

---

## 설계 불변식 (깨지 말 것)

1. **`TweenType` enum의 정수값은 씬/프리팹에 직렬화된다.** 중간 삽입·재정렬 금지, **항상 끝에만 추가**. (현재 값: Move=0..MoveZ=3, LocalMove=10.., Rotate=20/LocalRotate=21, Scale=30.., AnchorPos=40.., CanvasGroupFade=50.. SpriteFade=54)
2. **직렬화 키 = public 필드 이름.** 이름/타입 변경은 기존 씬 데이터를 깬다.
3. **`BuildTween`은 `Tweener`를 반환한다** (`Tween` 아님). `.From()`이 `Tweener` 확장이라 그렇다.
4. **거리/색 값**: Linear 색공간 프로젝트지만, 트윈 값은 셰이더가 아니라 컴포넌트 프로퍼티에 직접 들어가므로 일반 `Color`/`Vector`로 충분.
5. **충돌 Kill은 레지스트리 레퍼런스로만 가능.** `DOTween.Kill(id)`는 Sequence에 중첩된 트윈을 못 찾는다(아래 참조).

---

## 런타임 (`DoTweenTimeline.cs`)

### 흐름
`clips` 리스트 → `Build()`가 하나의 `Sequence`로 합성(각 클립을 `startTime`에 `Insert`) → `Play()`로 재생.

### 제어 API
| 메서드 | 동작 |
|---|---|
| `Play()` | **재빌드 후** 처음부터 재생 (클립 변경 후엔 이걸 사용) |
| `Restart()` | 빌드된 시퀀스를 처음부터 (없으면 빌드) |
| `Pause()` / `Resume()` / `TogglePause()` | 일시정지 / 이어재생 / 토글 |
| `Complete()` | 즉시 끝 상태로 |
| `Kill()` | 정지·해제 + 레지스트리 정리 |
| `Build()` | 재생 없이 시퀀스만 빌드(paused) |
| `IsPlaying`, `Sequence`, `TotalDuration` | 상태/접근 프로퍼티 |

### Timeline 직렬화 필드
- `autoPlay`: `Manual`(기본)/`OnAwake`/`OnStart`/`OnEnable`
- `ignoreTimeScale`: `SetUpdate(true)` (timeScale 무시)
- `killConflictingOnPlay`: Play 시 같은 컴포넌트+같은 TweenType의 다른 트윈을 Kill (아래)
- `timelineLoops`(-1=무한) / `timelineLoopType`
- `clips`: `List<Clip>`
- `onStart` / `onUpdate` / `onFinish`: 타임라인 전체 UnityEvent (시퀀스 `OnStart`/`OnUpdate`/`OnComplete`에 연결)

### Clip 필드
- 식별/대상: `label`, `target`(GameObject), `targetComponent`(Component, Inspector에서 선택), `tweenType`
- 타이밍: `startTime`, `duration`
- 목표값: `vectorValue`(V3) / `floatValue` / `colorValue` — tweenType에 따라 하나만 사용
- 이징: `ease`(enum) **또는** `useCurveEase`+`easeCurve`(AnimationCurve)
- 옵션: `from`(역방향 `.From()`), `relative`(`SetRelative`), `snapping`, `rotateMode`
- 시작값 강제: `overrideStart` + `fromVectorValue`/`fromFloatValue`/`fromColorValue` → `tween.ChangeStartValue(박싱값)`
- 루프: `loops`(-1=무한) / `loopType`
- 이벤트: `onStart` / `onUpdate` / `onFinish` (각 클립 트윈의 `OnStart`/`OnUpdate`/`OnComplete`)

### 지원 TweenType ↔ 컴포넌트
- **Transform**: Move(World) / LocalMove / Rotate / LocalRotate / Scale (+ 각 축 X/Y/Z)
- **RectTransform**: 위 Transform 전체 + AnchorPos(X/Y) / SizeDelta
- **CanvasGroup**: CanvasGroupFade
- **Graphic**(Image/Text/TMP 등): GraphicColor / GraphicFade
- **SpriteRenderer**: SpriteColor / SpriteFade

`BuildTween()`의 `switch`가 tweenType→DOTween 숏컷 매핑. `Require<T>()`는 `targetComponent` 우선, 없으면 `GetComponent<T>()` 폴백(하위호환).

### 시작값 강제 (`ChangeStartValue`)
`overrideStart`가 켜지면 `GetStartValueBoxed(clip)`이 트윈 내부 타입(Vector3/Vector2/Color/float)에 맞게 박싱한 값을 `tween.ChangeStartValue(...)`로 전달. 타입이 정확히 일치해야 적용됨. `from`과는 별개 기능(동시 사용 비권장).

### 충돌 트윈 Kill (중요)
**문제**: `DOTween.Kill(id)`/`Kill(target)`는 **Sequence에 중첩된 트윈을 찾지 못한다.** 우리 클립은 전부 한 Sequence 안에 들어가므로 id 기반 Kill 무력.

**해결**: 정적 레지스트리 `s_activeByKey: Dictionary<string, List<Tween>>`에 각 클립 트윈의 **레퍼런스**를 등록.
- 키 = `ConflictKey(clip)` = `"DTT:" + 컴포넌트.GetInstanceID() + ":" + (int)tweenType` → 같은 컴포넌트 인스턴스 + 같은 TweenType만 동일 키.
- 등록은 **무조건**(토글 무관) → 토글 켠 다른 타임라인이 내 트윈을 찾을 수 있게.
- `killConflictingOnPlay`가 켜진 타임라인의 `Build()`는, 새 트윈 생성 **전에** 각 키로 `KillConflicting(key)` → 레지스트리의 같은-키 트윈을 **레퍼런스로 직접 `.Kill()`**(중첩 트윈도 레퍼런스면 정확히 죽음).
- `Kill()`에서 `UnregisterAll()`로 자기 등록분 해제. `KillConflicting`은 죽은 항목 prune.
- 정밀도: 같은 transform의 **다른** TweenType이나 외부 무관 트윈은 안 건드림.
- 한계: **같은 타임라인 내부**에서 시간이 겹치는 동일-키 클립끼리는 해결 안 함(작성자 배치 책임).

---

## 에디터 (`DoTweenTimelineEditor.cs`)

### 구성 (`OnInspectorGUI` 순서)
1. **Banner** — 코드 생성 그라데이션(indigo→purple→teal) 텍스처 + 타이틀(드롭섀도) + 서브타이틀 + 이징 곡선 모티프 + 하단 액센트. 외부 PNG 없음.
2. **Toolbar** — `+ Add Clip` / `Duplicate` / `Delete` / Zoom 슬라이더 / 전체 길이.
3. **Timeline Track View** — 가로 시간축 룰러 + 클립 막대. 막대 본체 드래그=이동, 우측 끝 드래그=길이조절, 클릭=선택, 0.05s 스냅(Ctrl 해제), 가로 스크롤, 카테고리별 색상. Undo 지원.
4. **Selected Clip 패널** — Target→Component→(필터된)TweenType, 타이밍, 종류별 목표값(+`Current` 캡처), 이징(enum/Curve), 옵션 토글, Override Start Value, 루프, Clip Events(폴드아웃).
5. **Timeline Settings** — autoPlay, 스위치들, 루프, Timeline Events(폴드아웃).
6. **Preview** — Play 모드: 런타임 직접 제어. Edit 모드: `Sequence.Goto` 스크럽(+재생). 종료 시 원본 복구.

### 컴포넌트 기반 TweenType 필터링
`DrawTargetAndType(clip)`:
- Target 지정 시 `GetTweenableComponents(go)`로 트윈 가능 컴포넌트(Transform/RectTransform, Graphic 파생들, CanvasGroup, SpriteRenderer)를 팝업으로.
- `AllowedTypes(comp)`로 선택 컴포넌트가 지원하는 TweenType만 팝업에 노출. 현재 타입이 허용 목록에 없으면 첫 항목으로 자동 보정(undo 없이).
- `AllowedTypes`에서 **RectTransform을 Transform보다 먼저 검사**(RectTransform이 Transform 상속).

### Edit 모드 미리보기
`BeginPreview` → 타깃들 스냅샷(transform/RectTransform/Graphic/CanvasGroup/SpriteRenderer) → `_t.Build()` → `EditorApplication.update`로 `_previewTime` 진행 + `Sequence.Goto()`. `EndPreview`에서 `Kill()` + 스냅샷 복원(씬 훼손 방지). 슬라이더로 스크럽 가능.

### 안드로이드 스타일 토글 스위치
`AndroidToggle(label, value)` — 라벨 + 슬라이딩 스위치(알약 트랙 + 흰 노브). **Inspector의 모든 불리언이 이걸로 통일**됨(Use Curve Ease/From/Relative/Snapping/Override Start Value/Ignore Time Scale/Kill Conflicting).
- 원형은 코드 생성 안티앨리어싱 원형 텍스처를 `GUI.DrawTexture` 틴트로 그림(`GetCircleTexture`, 캐시).
- 스위치 크기 34×18px, 노브 14px, ON=초록/OFF=회색.
- **스무스 애니메이션**: 라벨별 노브 위치(0..1)를 `_toggleAnim` dict에 저장, `Mathf.MoveTowards(t, target, ToggleAnimSpeed*_dt)`로 보간(≈0.11s). 색도 같이 Lerp. 애니메이션 중 `OnInspectorGUI` 끝에서 `Repaint()`로 다음 프레임 유도. `_dt`는 0.05s clamp.
- 클릭 시 `GUI.changed=true`로 기존 `BeginChangeCheck` 저장 로직과 호환.

### 알려진 함정 / 패턴
- **`Current` 버튼 버그(수정됨)**: 버튼 클릭은 `GUI.changed`를 켜서 직후 ChangeCheck 저장 블록이 캡처 전 stale 로컬로 덮어씀. → 캡처(`CaptureInto`) 후 로컬 변수를 재동기화해 해결.
- **Foldout 스타일**: `EditorStyles.foldoutHeader`는 좌측 음수 마진+짙은 배경으로 helpBox 밖으로 튀어나옴. → `EditorStyles.foldout` 기반 굵은 스타일(`_sectionFoldout`) 사용. Clip Events는 `EditorGUI.indentLevel++`로 한 단 들여씀.
- 스타일/텍스처는 `EnsureStyles()`와 `Get*Texture()`에서 한 번만 생성·캐시.

---

## 빌드/검증
- 컴파일·렌더링·동작 검증은 **Unity 에디터에서만** 가능(에이전트 환경에선 불가). 코드 변경 후 사용자에게 컴파일·동작 확인을 요청할 것.
- 컴포넌트 메뉴: `Add Component ▸ DOTween ▸ DoTween Timeline`.
- 의존성: DOTween(`Assets/Plugins/Demigiant`) + UI/Sprite 모듈.

---

## 향후 작업 후보 (미구현)
- 클립 우클릭 컨텍스트 메뉴(삭제/복제/현재값 캡처)
- 타임라인 트랙 세로 재정렬(드래그)
- 프리셋 저장/불러오기(ScriptableObject)
- 같은 타임라인 내부 동일-키 클립 충돌 경고 표시
- 실제 PNG 로고로 배너 교체 옵션
- AnimationCurve 이징 미리보기 곡선을 클립 막대에 미니 표시

## 작업 이력 요약
런타임 코어 → 타임라인 트랙 뷰 → 폴더 `DoTweenUtility` 개명 → UI 영문화 → 컴포넌트 선택/TweenType 필터 → 클립·타임라인 이벤트(폴드아웃) → Override Start Value → AnimationCurve 이징 → 배너 → Kill Conflicting(레지스트리) → 안드로이드 토글 스위치(전 토글 적용 + 스무스 애니메이션) 순으로 구현.
