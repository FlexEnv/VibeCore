import { defineConfig } from "orval";

export default defineConfig({
  altaview: {
    input: "./swagger.json",
    output: {
      mode: "tags-split",
      target: "./src/api/generated",
      client: "react-query",
      prettier: true,
      override: {
        mutator: {
          path: "./src/api/mutator.ts",
          name: "customFetch",
        },
      },
    },
  },
});
