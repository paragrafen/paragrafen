import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const API_URL = "http://api.internal";

async function loadRoute() {
  vi.resetModules();
  return import("./route");
}

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
    const { POST } = await loadRoute();

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

describe("without API_URL in development", () => {
  beforeEach(() => {
    vi.stubEnv("API_URL", "");
    vi.stubEnv("NODE_ENV", "development");
  });

  it("answers POST /ask with a mock response", async () => {
    const fetchMock = stubFetch(new Response());
    const { POST } = await loadRoute();

    const res = await POST(
      new Request("http://web/api/ask", { method: "POST", body: "{}" }),
      context("ask"),
    );

    expect(fetchMock).not.toHaveBeenCalled();
    expect(res.status).toBe(200);
    expect((await res.json()).request_id).toBe("mock");
  });
});
