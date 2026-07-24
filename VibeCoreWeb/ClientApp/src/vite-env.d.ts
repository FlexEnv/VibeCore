/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_FLEX_APP_PLAN_SUMMARY?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
