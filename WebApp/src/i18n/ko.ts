export const ko = {
  appName: "가상 피팅",
  appTagline: "카메라로 확인하는 3D 가상 피팅",
  privacyShort: "영상은 기기 안에서만 처리됩니다",
  camera: {
    eyebrow: "실시간 3D 가상 피팅",
    title: "카메라 앞에서\n옷을 입어보세요.",
    description:
      "카메라 사용을 허용하고 전신이 보이도록 서면 선택한 옷이 움직임에 맞춰 표시됩니다.",
    start: "카메라 켜기",
    requirements: "HTTPS 또는 localhost · 최신 버전의 Chrome, Edge, Safari",
    permissionTitle: "카메라 권한 확인 중",
    permissionBody: "브라우저에서 카메라 사용을 허용해 주세요.",
    modelTitle: "가상 피팅 준비 중",
    modelBody: "포즈 인식 모델을 불러오고 있습니다.",
    retry: "다시 시도",
    stop: "카메라 끄기",
  },
  status: {
    ready: "준비",
    searching: "자세 인식 중",
    fitted: "피팅 완료",
    outOfFrame: "전신 확인 필요",
    modelLoading: "의상 불러오는 중",
  },
  guide: {
    title: "전신이 화면 안에 들어오도록 맞춰 주세요",
    body: "어깨부터 발목까지 보이면 자동으로 의상이 표시됩니다.",
  },
  panel: {
    garments: "의상 선택",
    styling: "색상 및 패턴",
    baseColor: "기본색",
    stripeColor: "줄무늬 색상",
    hex: "HEX",
    recent: "최근 사용한 색상",
    stripe: "줄무늬 적용",
    stripeWidth: "줄무늬 굵기",
    reset: "초기화",
    previousGarment: "이전 의상",
    nextGarment: "다음 의상",
  },
  capture: {
    action: "결과 저장",
    saved: "이미지를 기기에 저장했습니다.",
    unavailable: "카메라와 피팅이 준비된 후 저장할 수 있습니다.",
    failed: "이미지를 만들지 못했습니다. 다시 시도해 주세요.",
  },
  errors: {
    insecure: {
      title: "안전한 연결이 필요해요",
      body: "카메라는 HTTPS 또는 localhost에서만 사용할 수 있습니다.",
    },
    unsupported: {
      title: "이 브라우저에서는 사용할 수 없어요",
      body: "WebGL, WebAssembly, 카메라를 지원하는 최신 브라우저를 사용해 주세요.",
    },
    denied: {
      title: "카메라 권한이 꺼져 있어요",
      body: "주소창의 카메라 설정에서 권한을 허용한 뒤 다시 시도해 주세요.",
    },
    noCamera: {
      title: "사용할 수 있는 카메라가 없어요",
      body: "카메라 연결 상태를 확인하거나 다른 앱의 카메라 사용을 종료해 주세요.",
    },
    camera: {
      title: "카메라를 시작하지 못했어요",
      body: "카메라 연결 상태를 확인한 뒤 다시 시도해 주세요.",
    },
    model: {
      title: "포즈 인식 모델을 불러오지 못했습니다",
      body: "네트워크 연결을 확인하고 페이지를 새로고침해 주세요.",
    },
    garment: "의상 파일을 불러오지 못했습니다.",
  },
} as const;
