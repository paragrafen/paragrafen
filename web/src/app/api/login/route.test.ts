import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { NextRequest } from "next/server";
import type { AuthResponse } from "@/types";
import { POST } from "./route";

const API_URL = "http://api.internal";

const auth = {
  token: "abc.def.ghi",
  expires_at: "2026-10-20T13:00:00Z",
  user: {
    id: "0199f0c4-0000-7000-8000-000000000000",
    email: "kari@example.no",
    user_name: "kari",
    roles: ["User"],
  },
} satisfies AuthResponse;

function login() {
  return new NextRequest("http://web/api/login", {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: '{"email":"kari@example.no","password":"hemmelig1"}',
  });
}

function stubFetch(response: Response) {
  const fetchMock = vi.fn().mockResolvedValue(response);
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

beforeEach(() => {
  vi.stubEnv("API_URL", API_URL);
});

afterEach(() => {
  vi.unstubAllEnvs();
  vi.unstubAllGlobals();
});

it("forwards the credentials to the API", async () => {
  const fetchMock = stubFetch(Response.json(auth));

  await POST(login());

  const [url, init] = fetchMock.mock.calls[0];
  expect(url).toBe(`${API_URL}/login`);
  expect(init.body).toBe('{"email":"kari@example.no","password":"hemmelig1"}');
});

describe("on success", () => {
  it("keeps the token in an HttpOnly cookie that expires with it", async () => {
    stubFetch(Response.json(auth));

    const res = await POST(login());

    const setCookie = res.headers.get("set-cookie") ?? "";
    expect(setCookie).toMatch(/^session=abc\.def\.ghi;/);
    expect(setCookie).toContain("HttpOnly");
    expect(setCookie).toContain("SameSite=lax");
    expect(setCookie).toContain("Path=/");
    expect(setCookie).toContain("Expires=Tue, 20 Oct 2026 13:00:00 GMT");
  });

  it("does not return the token to the browser", async () => {
    stubFetch(Response.json(auth));

    const res = await POST(login());

    expect(res.status).toBe(204);
    expect(await res.text()).toBe("");
  });

  it("marks the cookie Secure in production", async () => {
    vi.stubEnv("NODE_ENV", "production");
    stubFetch(Response.json(auth));

    const res = await POST(login());

    expect(res.headers.get("set-cookie")).toContain("Secure");
  });
});

it("passes errors from the API through without a session", async () => {
  const error = {
    request_id: "0199f0c4-0000-7000-8000-000000000001",
    error: { code: "unauthorized", message: "Invalid email or password." },
  };
  stubFetch(Response.json(error, { status: 401 }));

  const res = await POST(login());

  expect(res.status).toBe(401);
  expect(await res.json()).toEqual(error);
  expect(res.headers.get("set-cookie")).not.toMatch(/^session=abc/);
});

it("signs in anyone without API_URL in development", async () => {
  vi.stubEnv("API_URL", "");
  vi.stubEnv("NODE_ENV", "development");
  const fetchMock = stubFetch(new Response());

  const res = await POST(login());

  expect(fetchMock).not.toHaveBeenCalled();
  expect(res.status).toBe(204);
  expect(res.headers.get("set-cookie")).toMatch(/^session=mock;/);
});
