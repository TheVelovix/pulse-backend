# Pulse — Backend

> Privacy-friendly website analytics, without the complexity.

The backend API for [Pulse](https://pulse.velovix.com) — built with ASP.NET Core and PostgreSQL.

**[Live Demo](https://pulse.velovix.com) · [Frontend Repo](https://github.com/TheVelovix/pulse-frontend) · [Docs](https://pulse.velovix.com/docs)**

---

## Tech Stack

- ASP.NET Core (C#)
- PostgreSQL

---

## Environment Variables

| Variable | Required | Description |
|---|---|---|
| `DB_URL` | Yes | PostgreSQL connection string |
| `JWT_SECRET` | Yes | Secret key for JWT signing |
| `JWT_ISSUER` | Yes | JWT issuer |
| `JWT_AUDIENCE` | Yes | JWT audience |
| `TURNSTILE_SECRET` | Yes | Cloudflare Turnstile secret |
| `SMTP_HOST` | Yes | SMTP server host |
| `SMTP_PORT` | Yes | SMTP server port |
| `SMTP_USER` | Yes | SMTP username/email |
| `SMTP_PASSWORD` | Yes | SMTP password |
| `PADDLE_API_KEY` | No | Paddle API key (payments) |
| `PADDLE_BASE_URL` | No | Paddle API base URL |
| `PADDLE_WEBHOOK_SECRET` | No | Paddle webhook secret |
| `PADDLE_SUCCESS_URL` | No | Redirect URL after payment |
| `PRO_PLAN_PRICE_ID` | No | Paddle price ID for Pro plan |

---

## Contributing

Pull requests are welcome. For major changes, open an issue first.
