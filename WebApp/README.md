# 가상 피팅 — 독립 웹 3D 의상 미리보기

Unity 빌드와 별개로 동작하는 React + TypeScript + Vite 기반 정적 웹 앱입니다.
카메라 프레임, 포즈 랜드마크, 캡처 결과는 브라우저 밖으로 전송하지 않습니다.

## 실행

요구 사항: Node.js 22 이상, 카메라를 사용할 수 있는 최신 브라우저.

```powershell
npm install
npm run dev
```

카메라는 HTTPS 또는 `localhost`/`127.0.0.1`에서만 사용할 수 있습니다.

```powershell
npm test
npm run build
```

`dist/`가 정적 배포 결과입니다.

## 구현 범위

- 브라우저 로컬 MediaPipe Pose Landmarker(33개 정규화/월드 랜드마크)
- 전면 카메라 미러링, object-cover 크롭, 화면 비율 좌표 보정
- Unity 피팅 필터 규칙을 포팅한 신뢰도·스무딩·속도·이상치·누락 유지 처리
- 어깨/팔/골반/무릎/발목을 이용한 의상 위치·크기·회전과 제한된 메시 변형
- MakeHuman 의상 6종(5종 CC0, 1종 CC-BY)의 지연 로딩 Meshopt GLB
- 상의/원피스를 한 그룹에서 선택하고, 상의일 때만 하의를 선택적으로 레이어링
- 의상별 HSV/HEX 색상, 최근 색상, 기본값 복원, 기본색과 최대 3개 선 색상을 반복하는 가로/세로/사선 줄무늬 및 도트 편집
- 모바일 하단 시트/데스크톱 측면 패널, 권한·미지원·포즈 이탈 상태
- 카메라와 WebGL 레이어를 합성한 로컬 PNG 캡처

계정, 서버 저장, 영상 녹화, 실제 의류 사이즈 추천, 천 물리 시뮬레이션은 포함하지 않습니다.

## 자산 갱신

Unity 원본 OBJ와 PNG는 읽기만 하며 변경하지 않습니다.

```powershell
npm run prepare:assets
npm run prepare:mediapipe
```

`prepare:assets`는 OBJ의 UV를 유지하고 스무스 노멀을 생성한 뒤 Meshopt 압축 GLB와
1024px WebP 텍스처를 `public/`에 만듭니다. `prepare:mediapipe`는 설치된 패키지의
WASM 런타임을 로컬 정적 자산으로 복사합니다.

## 배포

- Netlify: 프로젝트 기준 디렉터리를 `WebApp`으로 지정하면 `netlify.toml`을 사용합니다.
- Vercel: Root Directory를 `WebApp`으로 지정하면 `vercel.json`을 사용합니다.

양쪽 모두 `npm run build`와 `dist`를 사용하며, SPA fallback, 카메라 권한 보안 헤더,
WASM/모델/GLB/텍스처 재검증 캐시가 설정되어 있습니다. 파일명이 해시된 Vite
assets 디렉터리만 장기 불변 캐시를 사용하므로 모델을 교체해도 이전 의상이 남지
않습니다.

GitHub Actions는 웹 소스가 바뀐 pull request와 main push에서 단위 테스트와
프로덕션 빌드를 실행합니다. 카탈로그 테스트는 Unity와 웹의 의상 ID, 슬롯, 출처,
위치 및 피팅 배율을 대조해 두 버전의 설정 드리프트를 막습니다.

## 라이선스

의상 원본은 `Assets/ARCloset/MakeHuman/LICENSES.md`에 기록된 MakeHuman Community
에셋입니다. 하이넥 반팔 원피스는 Elvaerwyn의 CC-BY 에셋이며 나머지 4종은
CC0입니다. MediaPipe Tasks는 Apache-2.0, 앱 소스는 저장소 정책을 따릅니다.
