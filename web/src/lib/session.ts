import type { NextResponse } from "next/server";

export const SESSION_COOKIE = "session";

export function setSession(res: NextResponse, token: string, expires: Date) {
  res.cookies.set(SESSION_COOKIE, token, {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax",
    path: "/",
    expires,
  });
}

const ROLE_CLAIM =
  "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";

export function isAdmin(token: string | undefined) {
  const payload = token?.split(".")[1];
  if (!payload) return false;
  try {
    const claims = JSON.parse(
      Buffer.from(payload, "base64url").toString("utf8"),
    );
    return [claims[ROLE_CLAIM]].flat().includes("Admin");
  } catch {
    return false;
  }
}
