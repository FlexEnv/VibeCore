function App() {
  const planSummary = import.meta.env.VITE_FLEX_APP_PLAN_SUMMARY?.trim();

  return (
    <main className="flex min-h-screen items-center justify-center bg-slate-50 px-6 py-12 text-slate-900 dark:bg-slate-950 dark:text-slate-100">
      <section
        role="status"
        aria-live="polite"
        className="w-full max-w-xl rounded-2xl border border-slate-200 bg-white p-7 shadow-sm dark:border-slate-800 dark:bg-slate-900"
      >
        <div className="flex items-center gap-3">
          <span
            aria-hidden="true"
            className="h-2.5 w-2.5 animate-pulse rounded-full bg-sky-500"
          />
          <p className="text-sm font-semibold text-sky-700 dark:text-sky-300">
            Flex is starting your app
          </p>
        </div>
        {planSummary ? (
          <>
            <p className="mt-6 text-xs font-semibold uppercase tracking-[0.14em] text-slate-400 dark:text-slate-500">
              Planned app
            </p>
            <p className="mt-2 text-base leading-7 text-slate-700 dark:text-slate-200">
              {planSummary}
            </p>
          </>
        ) : (
          <p className="mt-4 text-sm leading-6 text-slate-500 dark:text-slate-400">
            Your first preview will appear here as Flex builds it.
          </p>
        )}
        <p className="mt-6 text-xs text-slate-400 dark:text-slate-500">
          This starting screen will update as the build takes shape.
        </p>
      </section>
    </main>
  );
}

export default App;
