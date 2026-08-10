# Xpense.Web

Browser client for [the API](../api/). React 19 and Fluent UI v9 on Vite, written in TypeScript.

It is also where the cryptography lives: the client holds the vault keys, encrypts records before
they are sent and decrypts them after they arrive. See
[Security and encryption](../docs/security-and-encryption.md).

## Running it

```bash
npm install
npm run dev
```

The dev server listens on `http://localhost:5173` and proxies `/api` to `http://localhost:4000`,
configured in `vite.config.ts`. From the repository root, `make start dev` runs this alongside the
API with both hot-reloading.

| Script | Does |
| --- | --- |
| `npm run dev` | Dev server with hot reload |
| `npm run build` | Type-check with `tsc -b`, then bundle |
| `npm run preview` | Serve the built bundle |
| `npm test` | Unit and component tests |
| `npm run test:browser` | Tests needing a real browser, including the plaintext-leak checks |
| `npm run check:init` | Builds the entry module under SSR to catch module-init failures |

## Layout

| Directory | Holds |
| --- | --- |
| `src/auth/` | Registration and sign-in flows, and their HTTP dependencies |
| `src/crypto/` | Primitives, key hierarchy, HPKE, Argon2, and the vault Worker |
| `src/vault/` | Projections (plaintext, encrypted, transition), the local database, the unlock gate |
| `src/sync/` | The sync client, outbox and conflict handling |
| `src/claim/` | The legacy claim migration flow |
| `src/domain/` | Client-side domain rules — balances, budget spending, spending by category |
| `src/shell/` | Navigation, sidebar filters, dialogs, the identity menu |
| `src/pages/` | Auth, Overview, Transactions, Budgets, Claim |
| `src/components/` | Reusable presentation pieces |

## Notes

There is no linter. `typescript-eslint` does not support TypeScript 7
([typescript-eslint#10940](https://github.com/typescript-eslint/typescript-eslint/issues/10940)), so
`tsc` under `strict` is the only static check until that lands.

`@fluentui/react-charts` is installed but not yet used — there are no charts in the interface today.
