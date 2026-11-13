# Microsoft Store 제출용 글

Partner Center의 제출 화면에 붙여 넣을 내용입니다. 화면 캡처는 `store/screenshots/ko`, `store/screenshots/en`에 있고(1920×1080), 패키지는 `store/build-msix.ps1`로 만듭니다.

## 제품 설정

| 항목 | 값 |
| --- | --- |
| 범주 | 생산성 (Productivity) |
| 가격 | 직접 정함 (참고: 비슷한 아이폰용 유료 앱이 $3.99) |
| 개인정보 처리방침 URL | `https://github.com/yeojin5/catodoro/blob/main/PRIVACY.md` (저장소를 비공개로 돌리면 다른 곳에 올려야 함) |
| 지원 연락처 | `https://github.com/yeojin5/catodoro/issues` |
| 패키지 | `store/out/Catodoro_<버전>_x64.msix` |

## 제한된 기능(runFullTrust) 사용 이유

제출할 때 "이 기능이 왜 필요한가"를 묻습니다. 아래를 붙여 넣으세요.

> CaToDoRo is a classic Win32 (WPF, .NET Framework 4.8) desktop app packaged as MSIX. It needs runFullTrust to do its core job as a focus timer: during a focus session it closes the programs the user put on their own block list, and it reads the address of the visible browser tab through UI Automation so it can close tabs for sites on the user's block list. Nothing is sent off the device.

## 인증 참고 사항 (Notes for certification)

> No account or sign-in is needed. To test: turn the dial to set a time (or use the mouse wheel), then click the knob in the center to start. To test blocking, open the gear icon, add a program (for example notepad.exe) or a site, start a session, then try to open it; it is closed and the indicator light turns red. The app only acts on items the user added to the block list, and only while a session is running.

## 한국어 (ko-KR)

**제품 이름**: CaToDoRo (Partner Center에서 이름을 예약할 때 이 표기 그대로 입력)

**짧은 설명**

> 고양이가 돌아다니는 뽀모도로 타이머. 집중하는 동안 딴짓할 프로그램과 사이트를 막아 주고, 끝까지 해내면 픽셀 고양이가 놀러 옵니다.

**설명**

> 다이얼을 돌려 집중 시간을 맞추고 가운데 버튼을 누르면 시작합니다.
>
> 집중하는 동안에는 내가 고른 프로그램과 사이트가 열리지 않습니다. 열려고 하면 바로 닫히고, 그 회차에는 고양이가 오지 않습니다. 끝까지 참으면 픽셀 고양이 한 마리가 타이머 안의 나무 바닥 놀이터로 놀러 옵니다.
>
> 고양이는 네 종류이고, 10% 확률로 행운의 고양이 복냥이가 옵니다. 모인 고양이들은 놀이터를 돌아다니다가 방석에 들어가 앉거나 캣타워 위로 뛰어올라 쉽니다.
>
> 관리자 권한이 필요 없고, 인터넷에 접속하지 않으며, 계정도 광고도 없습니다. 모든 기록은 내 PC에만 저장됩니다.

**주요 기능** (한 줄씩 입력)

- 다이얼을 돌려 0~60분 집중 시간 설정
- 집중하는 동안 내가 고른 프로그램과 사이트 차단
- 끝까지 집중하면 픽셀 고양이가 한 마리씩 추가
- 10% 확률로 행운의 고양이 등장
- 오늘·최근 7일·전체 집중 기록과 그래프
- 한국어·영어 지원, 항상 위에 표시

**검색어**: 뽀모도로, 집중 타이머, 고양이, 사이트 차단, 공부 타이머, 픽셀

**화면 캡처 설명** (`store/screenshots/ko`)

1. 다이얼을 돌려 집중 시간을 맞춥니다
2. 시간이 가면 초록 러그가 줄어듭니다
3. 끝까지 집중하면 고양이가 놀러 옵니다
4. 집중 기록을 한눈에 봅니다
5. 집중하는 동안 막을 프로그램과 사이트를 고릅니다

## English (en-US)

**Product name**: CaToDoRo

**Short description**

> A Pomodoro timer where pixel cats wander around. It blocks the programs and sites that distract you while you focus, and a cat moves in each time you make it to the end.

**Description**

> Turn the dial to set your focus time and press the knob in the center to start.
>
> While you focus, the programs and sites you chose will not open. If you try, they are closed right away and no cat comes for that session. Hold out to the end and a pixel cat comes to live in the wood-floor playground inside the timer.
>
> There are four kinds of cats, plus a 10% chance of the lucky cat Boknyang. Your cats wander around the playground, curl up on the cushion and hop onto the cat tower.
>
> No administrator rights, no internet connection, no account and no ads. Everything stays on your PC.

**Features** (one per line)

- Turn the dial to set a focus time from 0 to 60 minutes
- Blocks the programs and sites you choose while you focus
- A new pixel cat joins each time you finish a session
- 10% chance of a lucky cat
- Focus history for today, the last 7 days and all time, with a chart
- English and Korean, always-on-top option

**Search terms**: pomodoro, focus timer, cat, website blocker, study timer, pixel art

**Screenshot captions** (`store/screenshots/en`)

1. Turn the dial to set your focus time
2. The green rug shrinks as time passes
3. Stay focused to the end and a cat moves in
4. See your focus history at a glance
5. Choose the programs and sites to block while you focus
