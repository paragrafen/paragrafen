import { useState } from "react";
import { type ApiError, readError } from "@/lib/errors";
import { safeNext } from "@/lib/navigation";
import type { LoginRequest, RegisterRequest } from "@/types";

export function useSignIn(path: "login" | "register") {
  const [error, setError] = useState<ApiError | null>(null);
  const [loading, setLoading] = useState(false);

  async function signIn(body: LoginRequest | RegisterRequest) {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch(`/api/${path}`, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(body),
      });
      if (!res.ok) {
        setError(await readError(res));
        setLoading(false);
        return;
      }
      const next = new URLSearchParams(window.location.search).get("neste");
      window.location.assign(safeNext(next));
    } catch {
      setError({ code: "internal_error" });
      setLoading(false);
    }
  }

  return { signIn, error, loading };
}
