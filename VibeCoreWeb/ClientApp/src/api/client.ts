export const customFetch = async <T>(
  url: string,
  options: RequestInit,
): Promise<T> => {
  const method = options.method?.toUpperCase() ?? "GET";
  const headers = new Headers(options.headers);
  if (options.body !== undefined && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }
  if (method !== "GET" && method !== "HEAD") {
    headers.set("X-CSRF-TOKEN", getAntiforgeryToken());
  }

  const response = await fetch(url, {
    ...options,
    credentials: "same-origin",
    headers,
  });

  if (!response.ok) {
    const message = await response.text();
    throw new Error(message || `HTTP ${response.status}: ${response.statusText}`);
  }

  const contentType = response.headers.get("Content-Type") ?? "";
  const data = response.status === 204
    ? undefined
    : contentType.includes("json")
      ? await response.json()
      : await response.text();

  return {
    data,
    status: response.status,
    headers: response.headers,
  } as T;
};

function getAntiforgeryToken(): string {
  return (
    document
      .querySelector<HTMLMetaElement>('meta[name="csrf-token"]')
      ?.getAttribute("content") ?? ""
  );
}

export default customFetch;
