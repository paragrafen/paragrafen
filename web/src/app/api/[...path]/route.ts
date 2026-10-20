import type { AskResponse, ErrorResponse } from "@/types";

function mockAnswer() {
  return Response.json({
    request_id: "mock",
    conversation_id: "mock",
    status: "answered",
    answer: "Dette er et mock-svar. [1]",
    sources: [
      {
        chunk_id: "LOV-2005-06-17-62:§15-6",
        law: "Arbeidsmiljøloven",
        description: "Prøvetid",
        section: "§ 15-6",
        url: "https://lovdata.no/",
      },
    ],
    asked_at: new Date().toISOString(),
  } satisfies AskResponse);
}

function internalError(status: number, message: string, cause?: unknown) {
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

async function proxy(
  req: Request,
  { params }: RouteContext<"/api/[...path]">,
) {
  const { path } = await params;
  const apiUrl = process.env.API_URL;

  if (!apiUrl) {
    const isMockAsk =
      process.env.NODE_ENV !== "production" &&
      req.method === "POST" &&
      path.join("/") === "ask";
    return isMockAsk ? mockAnswer() : internalError(500, "API_URL is not set");
  }

  const target = `${apiUrl}/${path.map(encodeURIComponent).join("/")}`;
  const { search } = new URL(req.url);
  const hasBody = req.method !== "GET";

  let res: Response;
  try {
    res = await fetch(target + search, {
      method: req.method,
      headers: hasBody ? { "content-type": "application/json" } : undefined,
      body: hasBody ? await req.text() : undefined,
      signal: req.signal,
    });
  } catch (error) {
    if (req.signal.aborted) {
      return new Response(null, { status: 499 });
    }
    return internalError(502, "The API could not be reached", error);
  }

  const contentType = res.headers.get("content-type");

  return new Response(res.body, {
    status: res.status,
    headers: contentType ? { "content-type": contentType } : undefined,
  });
}

export { proxy as GET, proxy as POST };
