import { defineConfig } from '@hey-api/openapi-ts';

/**
 * Generates the typed API client from the backend's OpenAPI document.
 *
 * The point is not convenience, it is that a change to a C# DTO becomes a TypeScript
 * compile error rather than a field that silently reads as undefined at runtime. Nothing
 * under generated/ is written by hand.
 *
 * @hey-api/openapi-ts rather than openapi-typescript-codegen: the latter is unmaintained
 * and only understands OpenAPI 3.0, while .NET 10 emits 3.1.
 */
export default defineConfig({
  input: './openapi.json',
  output: './generated/api',
  plugins: [
    '@hey-api/client-fetch',
    {
      name: '@hey-api/sdk',
      operations: { strategy: 'single' },
    },
  ],
});
