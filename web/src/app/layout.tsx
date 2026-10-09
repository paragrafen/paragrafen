import "./globals.css";
import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Paragrafen",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="nb">
      <body className="bg-background text-foreground antialiased">
        {children}
      </body>
    </html>
  );
}
