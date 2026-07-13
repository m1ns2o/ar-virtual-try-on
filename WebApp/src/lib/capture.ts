export async function captureTryOn(
  video: HTMLVideoElement,
  webglCanvas: HTMLCanvasElement,
  stage: HTMLElement,
): Promise<Blob> {
  if (!video.videoWidth || !video.videoHeight) throw new Error("Camera frame is unavailable.");
  const rect = stage.getBoundingClientRect();
  const pixelRatio = Math.min(window.devicePixelRatio || 1, 2);
  const width = Math.max(1, Math.round(rect.width * pixelRatio));
  const height = Math.max(1, Math.round(rect.height * pixelRatio));
  const output = document.createElement("canvas");
  output.width = width;
  output.height = height;
  const context = output.getContext("2d");
  if (!context) throw new Error("Capture canvas is unavailable.");

  const sourceAspect = video.videoWidth / video.videoHeight;
  const targetAspect = width / height;
  let sourceX = 0;
  let sourceY = 0;
  let sourceWidth = video.videoWidth;
  let sourceHeight = video.videoHeight;
  if (sourceAspect > targetAspect) {
    sourceWidth = video.videoHeight * targetAspect;
    sourceX = (video.videoWidth - sourceWidth) * 0.5;
  } else {
    sourceHeight = video.videoWidth / targetAspect;
    sourceY = (video.videoHeight - sourceHeight) * 0.5;
  }

  context.save();
  context.translate(width, 0);
  context.scale(-1, 1);
  context.drawImage(video, sourceX, sourceY, sourceWidth, sourceHeight, 0, 0, width, height);
  context.restore();
  context.drawImage(webglCanvas, 0, 0, width, height);

  return new Promise<Blob>((resolve, reject) => {
    output.toBlob((blob) => (blob ? resolve(blob) : reject(new Error("Capture encoding failed."))), "image/png");
  });
}

export function saveCapture(blob: Blob): void {
  const now = new Date();
  const stamp = [
    now.getFullYear(),
    String(now.getMonth() + 1).padStart(2, "0"),
    String(now.getDate()).padStart(2, "0"),
    "-",
    String(now.getHours()).padStart(2, "0"),
    String(now.getMinutes()).padStart(2, "0"),
    String(now.getSeconds()).padStart(2, "0"),
  ].join("");
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = `ibobom-try-on-${stamp}.png`;
  link.click();
  window.setTimeout(() => URL.revokeObjectURL(url), 1000);
}
