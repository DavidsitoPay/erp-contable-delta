import { act } from "@testing-library/react";
import { afterEach, beforeEach, vi } from "vitest";

export async function avanzar(ms) {
  await act(async () => {
    await vi.advanceTimersByTimeAsync(ms);
  });
}

export function usarTemporizadoresFalsos() {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());
}
