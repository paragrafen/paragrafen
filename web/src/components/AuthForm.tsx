"use client";

import { type SubmitEvent, useId } from "react";
import { useSignIn } from "@/hooks/useSignIn";
import { ErrorMessage } from "./ErrorMessage";

const RATE_LIMITED = "For mange forsøk. Vent litt og prøv igjen.";

const MESSAGES = {
  login: {
    unauthorized: "Feil e-post eller passord.",
    validation_failed: "Fyll inn e-post og passord.",
    rate_limited: RATE_LIMITED,
  },
  register: {
    email_already_registered:
      "Det finnes allerede en konto med denne e-postadressen.",
    username_taken: "Brukernavnet er allerede tatt.",
    validation_failed:
      "Sjekk at e-post, brukernavn og passord oppfyller kravene.",
    rate_limited: RATE_LIMITED,
  },
};

const INPUT_CLASS =
  "w-full rounded-lg border border-border bg-surface p-3 focus:border-primary focus:ring-2 focus:ring-primary/30 focus:outline-none";

export function AuthForm({ mode }: { mode: "login" | "register" }) {
  const id = useId();
  const { signIn, error, loading } = useSignIn(mode);
  const isRegister = mode === "register";

  function handleSubmit(e: SubmitEvent<HTMLFormElement>) {
    e.preventDefault();
    const form = new FormData(e.currentTarget);
    const email = String(form.get("email"));
    const password = String(form.get("password"));
    signIn(
      isRegister
        ? { email, user_name: String(form.get("user_name")), password }
        : { email, password },
    );
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <div className="space-y-1">
        <label htmlFor={`${id}-email`} className="block font-medium">
          E-post
        </label>
        <input
          id={`${id}-email`}
          name="email"
          type="email"
          autoComplete="email"
          required
          className={INPUT_CLASS}
        />
      </div>

      {isRegister && (
        <div className="space-y-1">
          <label htmlFor={`${id}-user_name`} className="block font-medium">
            Brukernavn
          </label>
          <input
            id={`${id}-user_name`}
            name="user_name"
            autoComplete="username"
            required
            minLength={3}
            maxLength={32}
            pattern="[a-zA-Z0-9_\-]+"
            aria-describedby={`${id}-user_name-hint`}
            className={INPUT_CLASS}
          />
          <p id={`${id}-user_name-hint`} className="text-sm text-muted">
            3–32 tegn: bokstaver a–z, tall, - og _.
          </p>
        </div>
      )}

      <div className="space-y-1">
        <label htmlFor={`${id}-password`} className="block font-medium">
          Passord
        </label>
        <input
          id={`${id}-password`}
          name="password"
          type="password"
          autoComplete={isRegister ? "new-password" : "current-password"}
          required
          minLength={isRegister ? 8 : undefined}
          maxLength={isRegister ? 32 : undefined}
          aria-describedby={isRegister ? `${id}-password-hint` : undefined}
          className={INPUT_CLASS}
        />
        {isRegister && (
          <p id={`${id}-password-hint`} className="text-sm text-muted">
            8–32 tegn.
          </p>
        )}
      </div>

      {error && <ErrorMessage error={error} messages={MESSAGES[mode]} />}

      <button
        disabled={loading}
        className="w-full cursor-pointer rounded-lg bg-primary px-5 py-2 font-medium text-primary-foreground hover:bg-primary-hover disabled:cursor-not-allowed disabled:opacity-50"
      >
        {isRegister
          ? loading
            ? "Oppretter konto..."
            : "Opprett konto"
          : loading
            ? "Logger inn..."
            : "Logg inn"}
      </button>
    </form>
  );
}
