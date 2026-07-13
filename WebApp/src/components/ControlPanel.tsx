import { useEffect, useState } from "react";

import { ko } from "../i18n/ko";
import { normalizeHex } from "../lib/color";
import type { GarmentAppearance, GarmentDefinition } from "../types/pose";
import { GarmentCarousel } from "./GarmentCarousel";
import { HsvColorPicker } from "./HsvColorPicker";

interface Props {
  selected: GarmentDefinition;
  appearance: GarmentAppearance;
  recentColors: string[];
  onSelect: (garment: GarmentDefinition) => void;
  onAppearanceChange: (appearance: GarmentAppearance) => void;
  onColorCommit: (color: string) => void;
  onReset: () => void;
}

export function ControlPanel({
  selected,
  appearance,
  recentColors,
  onSelect,
  onAppearanceChange,
  onColorCommit,
  onReset,
}: Props) {
  const [colorTarget, setColorTarget] = useState<"base" | "stripe">("base");
  const selectedColor = colorTarget === "base" ? appearance.baseColor : appearance.stripeColor;
  const [hexInput, setHexInput] = useState(selectedColor);

  useEffect(() => setHexInput(selectedColor), [selectedColor]);

  const updateColor = (color: string) => {
    onAppearanceChange({
      ...appearance,
      [colorTarget === "base" ? "baseColor" : "stripeColor"]: color,
    });
  };

  const commitHex = () => {
    const normalized = normalizeHex(hexInput);
    if (!normalized) {
      setHexInput(selectedColor);
      return;
    }
    updateColor(normalized);
    onColorCommit(normalized);
  };

  return (
    <aside className="control-panel" aria-label="가상 피팅 편집 패널">
      <div className="sheet-handle" aria-hidden="true" />
      <div className="panel-scroll">
        <section className="panel-section garment-section" aria-labelledby="garment-title">
          <div className="section-heading">
            <div>
              <span className="section-kicker">01</span>
              <h2 id="garment-title">{ko.panel.garments}</h2>
            </div>
            <span className="asset-credit">
              {selected.author} · {selected.license}
            </span>
          </div>
          <GarmentCarousel selected={selected} onSelect={onSelect} />
        </section>

        <section className="panel-section style-section" aria-labelledby="style-title">
          <div className="section-heading">
            <div>
              <span className="section-kicker">02</span>
              <h2 id="style-title">{ko.panel.styling}</h2>
            </div>
            <button type="button" className="text-button reset-button" onClick={onReset} title={ko.panel.reset}>
              ↺ {ko.panel.reset}
            </button>
          </div>

          <div className="color-tabs" role="tablist" aria-label="편집할 색상">
            <button
              type="button"
              role="tab"
              aria-selected={colorTarget === "base"}
              className={colorTarget === "base" ? "active" : ""}
              onClick={() => setColorTarget("base")}
            >
              <span style={{ background: appearance.baseColor }} />
              {ko.panel.baseColor}
            </button>
            <button
              type="button"
              role="tab"
              aria-selected={colorTarget === "stripe"}
              className={colorTarget === "stripe" ? "active" : ""}
              onClick={() => setColorTarget("stripe")}
            >
              <span style={{ background: appearance.stripeColor }} />
              {ko.panel.stripeColor}
            </button>
          </div>

          <HsvColorPicker
            color={selectedColor}
            label={colorTarget === "base" ? ko.panel.baseColor : ko.panel.stripeColor}
            onChange={updateColor}
            onCommit={onColorCommit}
          />

          <div className="color-meta-row">
            <label className="hex-field">
              <span>{ko.panel.hex}</span>
              <input
                value={hexInput}
                maxLength={7}
                spellCheck={false}
                aria-label={`${colorTarget === "base" ? ko.panel.baseColor : ko.panel.stripeColor} HEX 값`}
                onChange={(event) => setHexInput(event.target.value.toUpperCase())}
                onBlur={commitHex}
                onKeyDown={(event) => {
                  if (event.key === "Enter") event.currentTarget.blur();
                }}
              />
            </label>
            <div className="recent-colors">
              <span>{ko.panel.recent}</span>
              <div>
                {recentColors.slice(0, 4).map((color) => (
                  <button
                    key={color}
                    type="button"
                    className="color-swatch"
                    style={{ background: color }}
                    aria-label={`최근 색상 ${color}`}
                    title={color}
                    onClick={() => updateColor(color)}
                  />
                ))}
              </div>
            </div>
          </div>

          <div className="pattern-controls">
            <div className="control-label-row">
              <span>{ko.panel.stripe}</span>
              <button
                type="button"
                className={`switch${appearance.stripeEnabled ? " on" : ""}`}
                role="switch"
                aria-checked={appearance.stripeEnabled}
                aria-label={ko.panel.stripe}
                onClick={() =>
                  onAppearanceChange({ ...appearance, stripeEnabled: !appearance.stripeEnabled })
                }
              >
                <i />
              </button>
            </div>
            <label className={`stripe-slider${appearance.stripeEnabled ? "" : " disabled"}`}>
              <span>{ko.panel.stripeWidth}</span>
              <output>{appearance.stripeWidth}px</output>
              <input
                type="range"
                min="8"
                max="72"
                value={appearance.stripeWidth}
                disabled={!appearance.stripeEnabled}
                onChange={(event) =>
                  onAppearanceChange({ ...appearance, stripeWidth: Number(event.target.value) })
                }
              />
            </label>
          </div>
        </section>
      </div>
    </aside>
  );
}
