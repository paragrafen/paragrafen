import { useState } from "react";
import { type ApiError, readError } from "@/lib/errors";
import type { AskResponse } from "@/types";

export function useAsk() {
  const [data, setData] = useState<AskResponse | null>(null);
  const [error, setError] = useState<ApiError | null>(null);
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
      if (res.status === 401) {
        window.location.assign("/login");
        return;
      }
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
