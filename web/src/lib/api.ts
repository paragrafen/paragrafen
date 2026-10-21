import { type NextRequest, NextResponse } from "next/server";
import { SESSION_COOKIE, setSession } from "@/lib/session";
import type { AuthResponse, ErrorResponse } from "@/types";

const MOCK_SESSION_MS = 60 * 60 * 1000;

export function usesMockApi() {
  return !process.env.API_URL && process.env.NODE_ENV !== "production";
}

export function errorResponse(status: number, message: string, cause?: unknown) {
  const requestId = crypto.randomUUID();
  console.error(`Proxy request ${requestId} failed: ${message}`, cause ?? "");
  return Response.json(
    {
      request_id: requestId,
      error: { code: "internal_error", message },
    } satisfies ErrorResponse,
    { status },
  );
}

export async function callApi(req: NextRequest, path: string[]) {
  const apiUrl = process.env.API_URL;
  if (!apiUrl) {
    return errorResponse(500, "API_URL is not set");
  }

  const target = `${apiUrl}/${path.map(encodeURIComponent).join("/")}`;
  const { search } = new URL(req.url);
  const hasBody = req.method !== "GET";
  const token = req.cookies.get(SESSION_COOKIE)?.value;

  const headers = new Headers();
  if (hasBody) headers.set("content-type", "application/json");
  if (token) headers.set("authorization", `Bearer ${token}`);

  let res: Response;
  try {
    res = await fetch(target + search, {
      method: req.method,
      headers,
      body: hasBody ? await req.text() : undefined,
      signal: req.signal,
    });
  } catch (error) {
    if (req.signal.aborted) {
      return new Response(null, { status: 499 });
    }
    return errorResponse(502, "The API could not be reached", error);
  }

  const contentType = res.headers.get("content-type");

  const response = new NextResponse(res.body, {
    status: res.status,
    headers: contentType ? { "content-type": contentType } : undefined,
  });
  if (res.status === 401) {
    response.cookies.delete(SESSION_COOKIE);
  }
  return response;
}

export async function startSession(req: NextRequest, path: string) {
  const response = new NextResponse(null, { status: 204 });

  if (usesMockApi()) {
    setSession(response, "mock", new Date(Date.now() + MOCK_SESSION_MS));
    return response;
  }

  const res = await callApi(req, [path]);
  if (!res.ok) {
    return res;
  }

  const auth: AuthResponse = await res.json();
  setSession(response, auth.token, new Date(auth.expires_at));
  return response;
}
