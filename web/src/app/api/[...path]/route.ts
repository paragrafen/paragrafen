import type { AskResponse } from "@/types";

const API_URL = process.env.API_URL;
const USE_MOCK = !API_URL && process.env.NODE_ENV !== "production";

async function proxy(
  req: Request,
  { params }: RouteContext<"/api/[...path]">,
) {
  const { path } = await params;

  if (USE_MOCK) {
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

  if (!API_URL) {
    return Response.json(
      { error: { code: "internal_error", message: "API_URL is not set" } },
      { status: 500 },
    );
  }

  const target = `${API_URL}/${path.map(encodeURIComponent).join("/")}`;
  const { search } = new URL(req.url);
  const hasBody = req.method !== "GET";

  const res = await fetch(target + search, {
    method: req.method,
    headers: hasBody ? { "content-type": "application/json" } : undefined,
    body: hasBody ? await req.text() : undefined,
    signal: req.signal,
  });

  const contentType = res.headers.get("content-type");

  return new Response(res.body, {
    status: res.status,
    headers: contentType ? { "content-type": contentType } : undefined,
  });
}

export { proxy as GET, proxy as POST };
