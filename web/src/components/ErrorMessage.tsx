export function ErrorMessage({ message }: { message: string }) {
  return (
    <p
      role="alert"
      className="rounded-lg border border-danger/30 bg-danger/10 p-4 text-danger"
    >
      {message}
    </p>
  );
}
