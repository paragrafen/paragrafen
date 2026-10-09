import type { Metadata } from "next";
import { SiteFooter } from "@/components/SiteFooter";
import "./globals.css";

export const metadata: Metadata = {
  title: "Paragrafen",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="nb">
      <body className="flex min-h-dvh flex-col bg-background text-foreground antialiased">
        {children}
        <SiteFooter />
      </body>
    </html>
  );
}
