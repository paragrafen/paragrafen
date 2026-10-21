import { afterEach, expect, it, vi } from "vitest";
import { NextRequest } from "next/server";
import type { AuthResponse } from "@/types";
import { POST } from "./route";

afterEach(() => {
  vi.unstubAllEnvs();
  vi.unstubAllGlobals();
});

function register() {
  return new NextRequest("http://web/api/register", {
    method: "POST",
    body: '{"email":"kari@example.no","user_name":"kari","password":"hemmelig1"}',
  });
}

it("creates the user in the API and starts a session", async () => {
  vi.stubEnv("API_URL", "http://api.internal");
  const fetchMock = vi.fn().mockResolvedValue(
    Response.json(
      {
        token: "abc.def.ghi",
        expires_at: "2026-10-20T13:00:00Z",
        user: { id: "1", email: "kari@example.no", user_name: "kari", roles: ["User"] },
      } satisfies AuthResponse,
      { status: 201 },
    ),
  );
  vi.stubGlobal("fetch", fetchMock);

  const res = await POST(register());

  expect(fetchMock.mock.calls[0][0]).toBe("http://api.internal/register");
  expect(res.status).toBe(204);
  expect(res.headers.get("set-cookie")).toMatch(/^session=abc\.def\.ghi;/);
});

it("passes a conflict through", async () => {
  vi.stubEnv("API_URL", "http://api.internal");
  const error = {
    request_id: "1",
    error: { code: "username_taken", message: "The username is already registered." },
  };
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue(Response.json(error, { status: 409 })));

  const res = await POST(register());

  expect(res.status).toBe(409);
  expect(await res.json()).toEqual(error);
});
