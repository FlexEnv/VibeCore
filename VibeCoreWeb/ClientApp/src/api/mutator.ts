export const customFetch = async <T>(
  {
    url,
    method,
    params,
    data,
    headers,
    signal,
  }: {
    url: string;
    method: string;
    params?: Record<string, unknown>;
    data?: unknown;
    headers?: Record<string, string>;
    signal?: AbortSignal;
  },
  _options?: unknown,
): Promise<T> => {
  const queryString =
    params && Object.keys(params).length > 0
      ? "?" +
        new URLSearchParams(
          Object.fromEntries(
            Object.entries(params)
              .filter(([, v]) => v !== undefined && v !== null)
              .map(([k, v]) => [k, String(v)]),
          ),
        ).toString()
      : "";

  const response = await fetch(url + queryString, {
    method: method.toUpperCase(),
    credentials: "include",
    headers:
      data !== undefined
        ? { "Content-Type": "application/json", ...headers }
        : headers,
    body: data !== undefined ? JSON.stringify(data) : undefined,
    signal,
  });

  if (response.status === 401) {
    const returnUrl = encodeURIComponent(
      window.location.pathname + window.location.search,
    );
    window.location.href = `/Identity/Account/Login?ReturnUrl=${returnUrl}`;
    throw new Error("Unauthorized - redirecting to login");
  }

  if (!response.ok) {
    const body = await response.text();
    const error = new Error(
      body || `${response.status} ${response.statusText}`,
    );
    Object.assign(error, {
      status: response.status,
      statusText: response.statusText,
      body,
    });
    throw error;
  }

  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
};
