import { NextResponse } from "next/server";
import { SESSION_COOKIE } from "@/lib/session";

export function POST() {
  const res = new NextResponse(null, {
    status: 303,
    headers: { location: "/login" },
  });
  res.cookies.delete(SESSION_COOKIE);
  return res;
}
