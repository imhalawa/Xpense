# Xpense.Web

Browser client for [the API](../api/). React 19 and MUI 9 on Vite,
written in TypeScript.

## Running it

```bash
npm install
npm run dev
```

The dev server listens on `http://localhost:5173`. It expects the API at `http://localhost:4000`,
set in `src/App.tsx`.

| Script | Does |
| --- | --- |
| `npm run dev` | Dev server with hot reload |
| `npm run build` | Type-check with `tsc -b`, then bundle |
| `npm run preview` | Serve the built bundle |

## State of the contract

Every endpoint this client calls was removed when the API reset to its v1 contract, so nothing it
fetches currently resolves. Migrating the six call sites is the next piece of work.

## Notes

There is no linter. `typescript-eslint` does not support TypeScript 7
([typescript-eslint#10940](https://github.com/typescript-eslint/typescript-eslint/issues/10940)), so
`tsc` under `strict` is the only static check until that lands.

`package.json` pins `@mui/x-charts-vendor` to 9.4.0, because 9.11.0 is published to npm with only a
package.json and a README and therefore resolves to nothing. Drop the override once upstream
republishes.
