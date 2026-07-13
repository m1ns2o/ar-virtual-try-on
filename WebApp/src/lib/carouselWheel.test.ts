import { describe, expect, it } from "vitest";

import { CarouselWheelGate } from "./carouselWheel";

describe("carousel wheel gesture gate", () => {
  it("advances only once throughout a continuous inertial gesture", () => {
    const gate = new CarouselWheelGate();

    expect(gate.consume(12, 0, 0).direction).toBe(0);
    expect(gate.consume(12, 0, 20).direction).toBe(0);
    expect(gate.consume(12, 0, 40).direction).toBe(1);

    for (let timeStamp = 60; timeStamp <= 600; timeStamp += 30) {
      expect(gate.consume(120, 0, timeStamp).direction).toBe(0);
    }
  });

  it("allows another snap after the gesture has been idle", () => {
    const gate = new CarouselWheelGate();

    expect(gate.consume(40, 0, 0).direction).toBe(1);
    expect(gate.consume(80, 0, 500).direction).toBe(0);
    expect(gate.consume(-40, 0, 1_151).direction).toBe(-1);
  });

  it("leaves vertical-dominant wheel input to the panel scroller", () => {
    const gate = new CarouselWheelGate();
    expect(gate.consume(20, 80, 0)).toEqual({ direction: 0, horizontal: false });
  });
});
