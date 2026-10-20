import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { GET, POST } from "./route";

const API_URL = "http://api.internal";

function context(...path: string[]) {
  return { params: Promise.resolve({ path }) };
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

describe("POST", () => {
  it("forwards the path and body to the API and returns its response", async () => {
    const fetchMock = stubFetch(
      Response.json({ status: "answered" }, { status: 200 }),
    );

    const res = await POST(
      new Request("http://web/api/ask", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: '{"question":"Hva er prøvetid?"}',
      }),
      context("ask"),
    );

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe(`${API_URL}/ask`);
    expect(init.method).toBe("POST");
    expect(init.body).toBe('{"question":"Hva er prøvetid?"}');
    expect(res.status).toBe(200);
    expect(await res.json()).toEqual({ status: "answered" });
  });
});

describe("GET", () => {
  it("forwards the path and query string to the API", async () => {
    const fetchMock = stubFetch(Response.json([{ chunk_id: "1" }]));

    const res = await GET(
      new Request("http://web/api/search?q=prøvetid&limit=5"),
      context("search"),
    );

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe(`${API_URL}/search?q=pr%C3%B8vetid&limit=5`);
    expect(init.method).toBe("GET");
    expect(init.body).toBeUndefined();
    expect(await res.json()).toEqual([{ chunk_id: "1" }]);
  });

  it("encodes each path segment", async () => {
    const fetchMock = stubFetch(new Response());

    await GET(new Request("http://web/api/a/b%3Fc"), context("a", "b?c"));

    expect(fetchMock.mock.calls[0][0]).toBe(`${API_URL}/a/b%3Fc`);
  });
});

describe("responses from the API", () => {
  it("pass error status and body through unchanged", async () => {
    const error = {
      request_id: "0199f0c4-0000-7000-8000-000000000000",
      error: { code: "model_busy", message: "Try again shortly." },
    };
    stubFetch(Response.json(error, { status: 503 }));

    const res = await GET(new Request("http://web/api/ask"), context("ask"));

    expect(res.status).toBe(503);
    expect(await res.json()).toEqual(error);
  });

  it("keep the API's content type", async () => {
    stubFetch(
      new Response("ok", { headers: { "content-type": "text/plain" } }),
    );

    const res = await GET(
      new Request("http://web/api/health"),
      context("health"),
    );

    expect(res.headers.get("content-type")).toBe("text/plain");
    expect(await res.text()).toBe("ok");
  });
});

it("cancels the API call when the browser disconnects", async () => {
  const fetchMock = stubFetch(new Response());
  const req = new Request("http://web/api/ask", { method: "POST", body: "{}" });

  await POST(req, context("ask"));

  expect(fetchMock.mock.calls[0][1].signal).toBe(req.signal);
});

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;

describe("when the API is unreachable", () => {
  it("returns an internal_error ErrorResponse and logs its request id", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("fetch failed")));
    const log = vi.spyOn(console, "error").mockImplementation(() => {});

    const res = await POST(
      new Request("http://web/api/ask", { method: "POST", body: "{}" }),
      context("ask"),
    );

    expect(res.status).toBe(502);
    const body = await res.json();
    expect(body.error.code).toBe("internal_error");
    expect(body.request_id).toMatch(UUID);
    expect(body.error.message).not.toContain("fetch failed");
    expect(log.mock.calls[0].join(" ")).toContain(body.request_id);
  });

  it("does not report a request the browser cancelled", async () => {
    const controller = new AbortController();
    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation(() => {
        controller.abort();
        return Promise.reject(new DOMException("aborted", "AbortError"));
      }),
    );
    const log = vi.spyOn(console, "error").mockImplementation(() => {});

    await POST(
      new Request("http://web/api/ask", {
        method: "POST",
        body: "{}",
        signal: controller.signal,
      }),
      context("ask"),
    );

    expect(log).not.toHaveBeenCalled();
  });
});

describe("without API_URL in production", () => {
  it("returns an internal_error ErrorResponse", async () => {
    vi.stubEnv("API_URL", "");
    vi.stubEnv("NODE_ENV", "production");

    const res = await POST(
      new Request("http://web/api/ask", { method: "POST", body: "{}" }),
      context("ask"),
    );

    expect(res.status).toBe(500);
    const body = await res.json();
    expect(body.error.code).toBe("internal_error");
    expect(body.request_id).toMatch(UUID);
  });
});

describe("without API_URL in development", () => {
  beforeEach(() => {
    vi.stubEnv("API_URL", "");
    vi.stubEnv("NODE_ENV", "development");
  });

  it("answers POST /ask with a mock response", async () => {
    const fetchMock = stubFetch(new Response());

    const res = await POST(
      new Request("http://web/api/ask", { method: "POST", body: "{}" }),
      context("ask"),
    );

    expect(fetchMock).not.toHaveBeenCalled();
    expect(res.status).toBe(200);
    expect((await res.json()).request_id).toBe("mock");
  });

  it("does not mock other paths", async () => {
    const fetchMock = stubFetch(new Response());

    const res = await GET(
      new Request("http://web/api/results"),
      context("results"),
    );

    expect(fetchMock).not.toHaveBeenCalled();
    expect(res.status).toBe(500);
    expect((await res.json()).error.code).toBe("internal_error");
  });
});
