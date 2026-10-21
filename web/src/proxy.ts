import { type NextRequest, NextResponse } from "next/server";
import { SESSION_COOKIE } from "@/lib/session";

const AUTH_PAGES = ["/login", "/registrer"];

export function proxy(req: NextRequest) {
  const { pathname, search } = req.nextUrl;
  const hasSession = req.cookies.has(SESSION_COOKIE);
  const isAuthPage = AUTH_PAGES.includes(pathname);

  if (!hasSession && !isAuthPage) {
    const login = new URL("/login", req.url);
    if (pathname !== "/") {
      login.searchParams.set("neste", pathname + search);
    }
    return NextResponse.redirect(login);
  }

  if (hasSession && isAuthPage) {
    return NextResponse.redirect(new URL("/", req.url));
  }

  return NextResponse.next();
}

export const config = {
  matcher: ["/((?!api/|_next/static|_next/image|.*\\.[a-z0-9]+$).*)"],
};
