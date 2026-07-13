export type CarouselWheelDirection = -1 | 0 | 1;

export interface CarouselWheelResult {
  direction: CarouselWheelDirection;
  horizontal: boolean;
}

export class CarouselWheelGate {
  private accumulatedDelta = 0;
  private gestureHandled = false;
  private lastHorizontalEventAt: number | null = null;

  constructor(
    private readonly threshold = 32,
    private readonly gestureEndMs = 650,
  ) {}

  consume(deltaX: number, deltaY: number, timeStamp: number): CarouselWheelResult {
    const horizontal = Math.abs(deltaX) > Math.abs(deltaY) * 0.8;
    if (!horizontal) return { direction: 0, horizontal: false };

    if (
      this.lastHorizontalEventAt === null ||
      timeStamp - this.lastHorizontalEventAt > this.gestureEndMs
    ) {
      this.accumulatedDelta = 0;
      this.gestureHandled = false;
    }
    this.lastHorizontalEventAt = timeStamp;

    if (this.gestureHandled) return { direction: 0, horizontal: true };

    this.accumulatedDelta += deltaX;
    if (Math.abs(this.accumulatedDelta) < this.threshold) {
      return { direction: 0, horizontal: true };
    }

    const direction: CarouselWheelDirection = this.accumulatedDelta > 0 ? 1 : -1;
    this.accumulatedDelta = 0;
    this.gestureHandled = true;
    return { direction, horizontal: true };
  }
}
