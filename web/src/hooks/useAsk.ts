import { useState } from "react";
import type { AskResponse, ErrorResponse } from "@/types";

export interface AskError {
  code: string;
  requestId?: string;
}

async function readError(res: Response): Promise<AskError> {
  const body: Partial<ErrorResponse> | null = await res
    .json()
    .catch(() => null);
  return {
    code: body?.error?.code ?? "internal_error",
    requestId: body?.request_id,
  };
}

export function useAsk() {
  const [data, setData] = useState<AskResponse | null>(null);
  const [error, setError] = useState<AskError | null>(null);
  const [loading, setLoading] = useState(false);

  async function ask(question: string) {
    setLoading(true);
    setError(null);
    setData(null);
    try {
      const res = await fetch("/api/ask", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ question }),
      });
      if (!res.ok) {
        setError(await readError(res));
        return;
      }
      setData(await res.json());
    } catch {
      setError({ code: "internal_error" });
    } finally {
      setLoading(false);
    }
  }

  return { ask, data, error, loading };
}
