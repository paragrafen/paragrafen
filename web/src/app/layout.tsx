import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Paragrafen",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="nb">
      <body>{children}</body>
    </html>
  );
}
