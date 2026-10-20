import { expect, it } from "vitest";
import { safeNext } from "./navigation";

it.each([
  ["/resultater?uke=2", "/resultater?uke=2"],
  ["/a/b#c", "/a/b#c"],
])("keeps the local path %s", (value, expected) => {
  expect(safeNext(value)).toBe(expected);
});

it.each([
  null,
  "",
  "resultater",
  "//evil.example",
  "/\\evil.example",
  "https://evil.example/",
  "javascript:alert(1)",
])("falls back to the front page for %j", (value) => {
  expect(safeNext(value)).toBe("/");
});
