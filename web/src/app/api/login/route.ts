import type { NextRequest } from "next/server";
import { startSession } from "@/lib/api";

export function POST(req: NextRequest) {
  return startSession(req, "login");
}
