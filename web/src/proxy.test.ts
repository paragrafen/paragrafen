import { describe, expect, it } from "vitest";
import { NextRequest } from "next/server";
import {
  getRedirectUrl,
  unstable_doesMiddlewareMatch,
} from "next/experimental/testing/server";
import { config, proxy } from "./proxy";

function visit(path: string, { loggedIn }: { loggedIn: boolean }) {
  return proxy(
    new NextRequest(`http://web${path}`, {
      headers: loggedIn ? { cookie: "session=abc.def.ghi" } : {},
    }),
  );
}

describe("without a session", () => {
  it("sends the front page to /login", () => {
    const res = visit("/", { loggedIn: false });

    expect(getRedirectUrl(res)).toBe("http://web/login");
  });

  it("remembers a deeper page in neste", () => {
    const res = visit("/resultater?uke=2", { loggedIn: false });

    expect(getRedirectUrl(res)).toBe(
      "http://web/login?neste=%2Fresultater%3Fuke%3D2",
    );
  });

  it.each(["/login", "/registrer"])("lets %s through", (path) => {
    const res = visit(path, { loggedIn: false });

    expect(getRedirectUrl(res)).toBeNull();
  });
});

describe("with a session", () => {
  it("lets the front page through", () => {
    const res = visit("/", { loggedIn: true });

    expect(getRedirectUrl(res)).toBeNull();
  });

  it.each(["/login", "/registrer"])("sends %s to the front page", (path) => {
    const res = visit(path, { loggedIn: true });

    expect(getRedirectUrl(res)).toBe("http://web/");
  });
});

describe("matcher", () => {
  it.each(["/", "/login", "/resultater"])("runs on page %s", (url) => {
    expect(unstable_doesMiddlewareMatch({ config, url })).toBe(true);
  });

  it.each([
    "/api/ask",
    "/api/login",
    "/_next/static/chunks/app.js",
    "/_next/image",
    "/favicon.ico",
  ])("skips %s", (url) => {
    expect(unstable_doesMiddlewareMatch({ config, url })).toBe(false);
  });
});
