import { describe, expect, it } from "vitest";
import {
  formatCount,
  formatDate,
  formatMs,
  formatNok,
  formatPercent,
} from "./format";

// Intl uses non-breaking spaces in nb-NO.
const plain = (value: string) => value.replace(/\s/g, " ");

describe("formatNok", () => {
  it("keeps small amounts readable", () => {
    expect(formatNok(0.027)).toBe("0,027 kr");
    expect(formatNok(0.0004)).toBe("0,0004 kr");
    expect(formatNok(0.808)).toBe("0,81 kr");
  });

  it("uses two decimals from 1 krone", () => {
    expect(plain(formatNok(1234.5))).toBe("1 234,50 kr");
  });

  it("shows zero and missing values", () => {
    expect(formatNok(0)).toBe("0 kr");
    expect(formatNok(null)).toBe("–");
  });
});

describe("formatPercent", () => {
  it("shows one decimal", () => {
    expect(plain(formatPercent(2 / 6))).toBe("33,3 %");
    expect(plain(formatPercent(0))).toBe("0 %");
  });
});

describe("formatMs", () => {
  it("uses ms below a second and seconds above", () => {
    expect(formatMs(2)).toBe("2 ms");
    expect(formatMs(3800)).toBe("3,8 s");
    expect(formatMs(null)).toBe("–");
  });
});

describe("formatCount", () => {
  it("groups thousands", () => {
    expect(plain(formatCount(6500))).toBe("6 500");
  });
});

describe("formatDate", () => {
  it("turns an ISO date into a Norwegian date", () => {
    expect(formatDate("2026-10-21")).toBe("21.10.2026");
  });
});
