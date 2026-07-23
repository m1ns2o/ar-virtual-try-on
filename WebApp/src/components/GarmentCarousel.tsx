import { useCallback, useEffect, useRef, useState, type CSSProperties } from "react";
import useEmblaCarousel from "embla-carousel-react";

import { ko } from "../i18n/ko";
import { CarouselWheelGate } from "../lib/carouselWheel";
import type { GarmentDefinition } from "../types/pose";

interface Props {
  garments: GarmentDefinition[];
  selected: GarmentDefinition | null;
  onSelect: (garment: GarmentDefinition) => void;
  disabled?: boolean;
  label: string;
}

export function GarmentCarousel({ garments, selected, onSelect, disabled = false, label }: Props) {
  const [viewportRef, emblaApi] = useEmblaCarousel({
    align: "start",
    containScroll: "trimSnaps",
    dragFree: false,
    duration: 24,
    loop: false,
    skipSnaps: false,
    slidesToScroll: 1,
    breakpoints: {
      "(prefers-reduced-motion: reduce)": { duration: 0 },
    },
  });
  const viewportNodeRef = useRef<HTMLDivElement | null>(null);
  const [canScrollPrevious, setCanScrollPrevious] = useState(false);
  const [canScrollNext, setCanScrollNext] = useState(false);

  const setViewportNode = useCallback(
    (node: HTMLDivElement | null) => {
      viewportNodeRef.current = node;
      viewportRef(node);
    },
    [viewportRef],
  );

  const updateControls = useCallback(() => {
    if (!emblaApi) return;
    setCanScrollPrevious(emblaApi.canScrollPrev());
    setCanScrollNext(emblaApi.canScrollNext());
  }, [emblaApi]);

  useEffect(() => {
    if (!emblaApi) return;
    updateControls();
    emblaApi.on("select", updateControls);
    emblaApi.on("reInit", updateControls);
    return () => {
      emblaApi.off("select", updateControls);
      emblaApi.off("reInit", updateControls);
    };
  }, [emblaApi, updateControls]);

  useEffect(() => {
    if (!emblaApi) return;
    const selectedIndex = garments.findIndex((garment) => garment.id === selected?.id);
    if (selectedIndex >= 0) emblaApi.scrollTo(selectedIndex);
  }, [emblaApi, garments, selected?.id]);

  useEffect(() => {
    const viewport = viewportNodeRef.current;
    if (!viewport || !emblaApi) return;

    const wheelGate = new CarouselWheelGate();

    const handleWheel = (event: WheelEvent) => {
      const result = wheelGate.consume(event.deltaX, event.deltaY, event.timeStamp);
      if (!result.horizontal) return;
      event.preventDefault();
      if (result.direction > 0) emblaApi.scrollNext();
      else if (result.direction < 0) emblaApi.scrollPrev();
    };

    viewport.addEventListener("wheel", handleWheel, { passive: false });
    return () => {
      viewport.removeEventListener("wheel", handleWheel);
    };
  }, [emblaApi]);

  const selectGarment = (garment: GarmentDefinition, index: number) => {
    onSelect(garment);
    emblaApi?.scrollTo(index);
  };

  return (
    <div
      className="garment-carousel"
      data-testid="garment-carousel"
      role="region"
      aria-roledescription="carousel"
      aria-label={label}
    >
      <div className="garment-carousel-viewport" ref={setViewportNode}>
        <div className="garment-carousel-track" id="garment-carousel-track">
          {garments.map((garment, index) => (
            <div
              className="garment-carousel-slide"
              key={garment.id}
              role="group"
              aria-roledescription="slide"
              aria-label={`${index + 1} / ${garments.length} · ${garment.displayName}`}
            >
              <button
                type="button"
                data-testid={`garment-${garment.id}`}
                aria-pressed={selected?.id === garment.id}
                className={`garment-card${selected?.id === garment.id ? " selected" : ""}`}
                disabled={disabled}
                onClick={() => selectGarment(garment, index)}
                title={`${garment.displayName} · ${garment.author}`}
              >
                <span
                  className={`garment-thumbnail silhouette-${garment.silhouette}`}
                  style={{ "--garment-color": garment.defaultBaseColor } as CSSProperties}
                  aria-hidden="true"
                >
                  <i />
                </span>
                <span className="garment-name">{garment.shortName}</span>
                <span className="garment-slot">
                  {garment.slot === "upper"
                    ? "상의"
                    : garment.slot === "lower"
                      ? "하의"
                      : garment.slot === "onePiece"
                        ? "원피스"
                        : "아우터"}
                </span>
              </button>
            </div>
          ))}
        </div>
      </div>

      <button
        type="button"
        data-testid="garment-carousel-previous"
        className="carousel-button carousel-button-previous"
        aria-label={ko.panel.previousGarment}
        aria-controls="garment-carousel-track"
        disabled={!canScrollPrevious}
        onClick={() => emblaApi?.scrollPrev()}
      >
        <span aria-hidden="true">‹</span>
      </button>
      <button
        type="button"
        data-testid="garment-carousel-next"
        className="carousel-button carousel-button-next"
        aria-label={ko.panel.nextGarment}
        aria-controls="garment-carousel-track"
        disabled={!canScrollNext}
        onClick={() => emblaApi?.scrollNext()}
      >
        <span aria-hidden="true">›</span>
      </button>
    </div>
  );
}
