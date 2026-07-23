import { memo, useEffect, useMemo, useState } from "react";

import { GARMENTS } from "../data/garments";
import { ko } from "../i18n/ko";
import { normalizeHex } from "../lib/color";
import type { GarmentAppearance, GarmentDefinition } from "../types/pose";
import { GarmentCarousel } from "./GarmentCarousel";
import { HsvColorPicker } from "./HsvColorPicker";

type EditorSlot = "upper" | "lower";
interface Outfit { upperId: string; lowerId: string | null; }
interface Props {
  outfit: Outfit;
  editorSlot: EditorSlot;
  selected: GarmentDefinition;
  appearance: GarmentAppearance;
  recentColors: string[];
  onSelect: (garment: GarmentDefinition) => void;
  onEditorSlotChange: (slot: EditorSlot) => void;
  onAppearanceChange: (appearance: GarmentAppearance) => void;
  onColorCommit: (color: string) => void;
  onReset: () => void;
}

const upperGarments = GARMENTS.filter((garment) => garment.slot !== "lower");
const lowerGarments = GARMENTS.filter((garment) => garment.slot === "lower");
type ColorTarget = "base" | 0 | 1 | 2;

export const ControlPanel = memo(function ControlPanel({
  outfit, editorSlot, selected, appearance, recentColors, onSelect, onEditorSlotChange, onAppearanceChange, onColorCommit, onReset,
}: Props) {
  const [colorTarget, setColorTarget] = useState<ColorTarget>("base");
  const visibleStripeColors = appearance.pattern === "stripes" ? appearance.stripeColorCount : appearance.pattern === "dots" ? 1 : 0;
  const activeColorTarget = typeof colorTarget === "number" && colorTarget >= visibleStripeColors ? "base" : colorTarget;
  const selectedColor = activeColorTarget === "base" ? appearance.baseColor : appearance.stripeColors[activeColorTarget];
  const selectedColorLabel = activeColorTarget === "base"
    ? ko.panel.baseColor
    : appearance.pattern === "dots"
      ? "도트 색상"
      : `선 색상 ${activeColorTarget + 1}`;
  const [hexInput, setHexInput] = useState(selectedColor);
  const upperSelected = useMemo(() => GARMENTS.find((item) => item.id === outfit.upperId)!, [outfit.upperId]);
  const dressActive = upperSelected.slot === "onePiece";
  const lowerSelected = useMemo(() => outfit.lowerId ? GARMENTS.find((item) => item.id === outfit.lowerId) ?? null : null, [outfit.lowerId]);

  useEffect(() => setHexInput(selectedColor), [selectedColor]);
  const updateColor = (color: string) => {
    if (activeColorTarget === "base") {
      onAppearanceChange({ ...appearance, baseColor: color });
      return;
    }
    const stripeColors: GarmentAppearance["stripeColors"] = [...appearance.stripeColors];
    stripeColors[activeColorTarget] = color;
    onAppearanceChange({ ...appearance, stripeColors });
  };
  const commitHex = () => {
    const normalized = normalizeHex(hexInput);
    if (!normalized) return setHexInput(selectedColor);
    updateColor(normalized);
    onColorCommit(normalized);
  };
  const switchEditor = (slot: EditorSlot) => onEditorSlotChange(slot);

  return <aside className="control-panel" aria-label="가상 피팅 편집 패널">
    <div className="sheet-handle" aria-hidden="true" />
    <div className="panel-scroll">
      <section className="panel-section garment-section" aria-labelledby="garment-title">
        <div className="section-heading"><div><span className="section-kicker">01</span><h2 id="garment-title">{ko.panel.garments}</h2></div></div>
        <div className="outfit-slot"><button type="button" className="slot-heading" onClick={() => switchEditor("upper")}>상의 · 원피스</button><GarmentCarousel garments={upperGarments} selected={upperSelected} onSelect={onSelect} label="상의와 원피스 선택" /></div>
        <div className={`outfit-slot${dressActive ? " disabled" : ""}`}><button type="button" className="slot-heading" disabled={dressActive || !lowerSelected} onClick={() => switchEditor("lower")}>하의 {dressActive ? "· 원피스 선택 중" : lowerSelected ? "· 다시 누르면 해제" : "· 선택 안 함"}</button><GarmentCarousel garments={lowerGarments} selected={lowerSelected} onSelect={onSelect} disabled={dressActive} label="하의 선택 · 선택된 바지를 다시 누르면 해제" /></div>
      </section>

      <section className="panel-section style-section" aria-labelledby="style-title">
        <div className="section-heading"><div><span className="section-kicker">02</span><h2 id="style-title">{selected.shortName} {ko.panel.styling}</h2></div><button type="button" className="text-button reset-button" onClick={onReset}>↺ {ko.panel.reset}</button></div>
        <div className="pattern-controls pattern-controls-first">
          <label><span>{ko.panel.pattern}</span><select value={appearance.pattern} onChange={(event) => onAppearanceChange({ ...appearance, pattern: event.target.value as GarmentAppearance["pattern"] })}><option value="none">없음</option><option value="stripes">줄무늬 선</option><option value="dots">도트</option></select></label>
          {appearance.pattern === "stripes" && <><div className="stripe-color-count"><span>추가 선 색상</span>{([1, 2, 3] as const).map((count) => <button key={count} type="button" className={appearance.stripeColorCount === count ? "active" : ""} aria-pressed={appearance.stripeColorCount === count} onClick={() => onAppearanceChange({ ...appearance, stripeColorCount: count })}>{count}개</button>)}</div><div className="stripe-direction" role="group" aria-label={ko.panel.stripeDirection}>{(["horizontal", "vertical", "diagonal"] as const).map((direction) => <button key={direction} type="button" className={appearance.stripeDirection === direction ? "active" : ""} aria-pressed={appearance.stripeDirection === direction} onClick={() => onAppearanceChange({ ...appearance, stripeDirection: direction })}>{direction === "horizontal" ? "세로" : direction === "vertical" ? "가로" : "사선"}</button>)}</div><label className="stripe-slider"><span>{ko.panel.stripeWidth}</span><output>{appearance.stripeWidth}px</output><input type="range" min="6" max="60" value={appearance.stripeWidth} onChange={(event) => onAppearanceChange({ ...appearance, stripeWidth: Number(event.target.value) })} /></label></>}
          {appearance.pattern === "dots" && <label className="stripe-slider"><span>{ko.panel.dotSize}</span><output>{appearance.dotSize}px</output><input type="range" min="6" max="42" value={appearance.dotSize} onChange={(event) => onAppearanceChange({ ...appearance, dotSize: Number(event.target.value) })} /></label>}
        </div>
        <div className="color-tabs" role="tablist" aria-label="편집할 색상">
          <button type="button" role="tab" aria-selected={activeColorTarget === "base"} className={activeColorTarget === "base" ? "active" : ""} onClick={() => setColorTarget("base")}><span style={{ background: appearance.baseColor }} />{ko.panel.baseColor}</button>
          {appearance.stripeColors.slice(0, visibleStripeColors).map((color, index) => <button key={`stripe-color-${index + 1}`} type="button" role="tab" aria-selected={activeColorTarget === index} className={activeColorTarget === index ? "active" : ""} onClick={() => setColorTarget(index as 0 | 1 | 2)}><span style={{ background: color }} />{appearance.pattern === "dots" ? "도트 색상" : `선 색상 ${index + 1}`}</button>)}
        </div>
        <HsvColorPicker color={selectedColor} label={selectedColorLabel} onChange={updateColor} onCommit={onColorCommit} />
        <div className="color-meta-row"><label className="hex-field"><span>{ko.panel.hex}</span><input value={hexInput} maxLength={7} spellCheck={false} aria-label="HEX 값" onChange={(event) => setHexInput(event.target.value.toUpperCase())} onBlur={commitHex} onKeyDown={(event) => { if (event.key === "Enter") event.currentTarget.blur(); }} /></label><div className="recent-colors"><span>{ko.panel.recent}</span><div>{recentColors.slice(0, 4).map((color) => <button key={color} type="button" className="color-swatch" style={{ background: color }} aria-label={`최근 색상 ${color}`} onClick={() => updateColor(color)} />)}</div></div></div>
      </section>
    </div>
  </aside>;
});
