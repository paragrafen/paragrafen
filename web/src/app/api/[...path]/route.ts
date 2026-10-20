import type { NextRequest } from "next/server";
import { callApi } from "@/lib/api";
import type { AskResponse } from "@/types";

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

async function proxy(
  req: NextRequest,
  { params }: RouteContext<"/api/[...path]">,
) {
  const { path } = await params;

  const isMockAsk =
    !process.env.API_URL &&
    process.env.NODE_ENV !== "production" &&
    req.method === "POST" &&
    path.join("/") === "ask";

  return isMockAsk ? mockAnswer() : callApi(req, path);
}

export { proxy as GET, proxy as POST };
