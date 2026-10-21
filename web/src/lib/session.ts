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
