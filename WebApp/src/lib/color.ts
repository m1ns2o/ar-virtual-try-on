export interface HsvColor {
  h: number;
  s: number;
  v: number;
}

const clamp01 = (value: number) => Math.min(1, Math.max(0, value));

export function normalizeHex(value: string): string | null {
  const clean = value.trim().replace(/^#/, "");
  if (/^[0-9a-f]{3}$/i.test(clean)) {
    return `#${clean
      .split("")
      .map((character) => character + character)
      .join("")}`.toUpperCase();
  }
  if (!/^[0-9a-f]{6}$/i.test(clean)) return null;
  return `#${clean.toUpperCase()}`;
}

export function hexToHsv(hex: string): HsvColor {
  const normalized = normalizeHex(hex) ?? "#000000";
  const red = Number.parseInt(normalized.slice(1, 3), 16) / 255;
  const green = Number.parseInt(normalized.slice(3, 5), 16) / 255;
  const blue = Number.parseInt(normalized.slice(5, 7), 16) / 255;
  const maximum = Math.max(red, green, blue);
  const minimum = Math.min(red, green, blue);
  const delta = maximum - minimum;

  let hue = 0;
  if (delta > 0) {
    if (maximum === red) hue = ((green - blue) / delta) % 6;
    else if (maximum === green) hue = (blue - red) / delta + 2;
    else hue = (red - green) / delta + 4;
  }

  return {
    h: ((hue * 60 + 360) % 360) / 360,
    s: maximum === 0 ? 0 : delta / maximum,
    v: maximum,
  };
}

export function hsvToHex({ h, s, v }: HsvColor): string {
  const hue = ((h % 1) + 1) % 1;
  const saturation = clamp01(s);
  const value = clamp01(v);
  const sector = hue * 6;
  const chroma = value * saturation;
  const secondary = chroma * (1 - Math.abs((sector % 2) - 1));
  const match = value - chroma;
  let rgb: [number, number, number];

  if (sector < 1) rgb = [chroma, secondary, 0];
  else if (sector < 2) rgb = [secondary, chroma, 0];
  else if (sector < 3) rgb = [0, chroma, secondary];
  else if (sector < 4) rgb = [0, secondary, chroma];
  else if (sector < 5) rgb = [secondary, 0, chroma];
  else rgb = [chroma, 0, secondary];

  return `#${rgb
    .map((channel) => Math.round((channel + match) * 255).toString(16).padStart(2, "0"))
    .join("")}`.toUpperCase();
}
