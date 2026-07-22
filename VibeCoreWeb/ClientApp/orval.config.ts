import { defineConfig } from "orval";

export default defineConfig({
  api: {
    input: "swagger.json",
    output: {
      target: "src/api/endpoints.gen.ts",
      schemas: "src/api/models",
      client: "react-query",
      mode: "tags-split",
      override: {
        mutator: {
          path: "src/api/client.ts",
          name: "customFetch",
        },
      },
    },
  },
});
