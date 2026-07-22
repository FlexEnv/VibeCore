import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useGetApiUserCurrent } from "../api/user/user";
import {
  SaveScheduledTask,
  ScheduledTask,
  ScheduledTaskKind,
  scheduledTaskApi,
} from "../api/scheduledTasks";

const scheduleKey = ["scheduled-tasks"];
const inputClass = "w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-500/20 dark:border-slate-700 dark:bg-slate-900 dark:text-white";
const secondaryButton = "rounded-lg border border-slate-300 px-3 py-2 text-sm font-medium hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50 dark:border-slate-700 dark:hover:bg-slate-800";

function emptyForm(): SaveScheduledTask {
  const later = new Date(Date.now() + 60 * 60 * 1000);
  return {
    name: "",
    handlerKey: "",
    kind: "Cron",
    cronExpression: "0 0 9 ? * MON-FRI *",
    timeZoneId: Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC",
    runAt: new Date(later.getTime() - later.getTimezoneOffset() * 60000).toISOString().slice(0, 16),
  };
}

export default function ScheduledTasks() {
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState<ScheduledTask | "new" | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const handlersQuery = useQuery({ queryKey: [...scheduleKey, "handlers"], queryFn: scheduledTaskApi.handlers });
  const schedulesQuery = useQuery({ queryKey: scheduleKey, queryFn: scheduledTaskApi.schedules, refetchInterval: 10_000 });
  const { data: userResponse } = useGetApiUserCurrent();
  const roles = userResponse?.data.roles ?? [];
  const canManage = roles.includes("Operator") || roles.includes("Administrator");

  const invalidate = () => queryClient.invalidateQueries({ queryKey: scheduleKey });
  const action = useMutation({
    mutationFn: async ({ type, schedule }: { type: "pause" | "resume" | "run" | "delete"; schedule: ScheduledTask }) => {
      if (type === "delete" && !window.confirm(`Delete “${schedule.name}” and its run history?`)) return;
      await scheduledTaskApi[type](schedule.id);
    },
    onSuccess: invalidate,
  });

  if (schedulesQuery.isLoading || handlersQuery.isLoading) {
    return <StateCard>Loading scheduled tasks…</StateCard>;
  }
  if (schedulesQuery.error || handlersQuery.error) {
    return <StateCard error>Could not load scheduled tasks. {String(schedulesQuery.error || handlersQuery.error)}</StateCard>;
  }

  const schedules = schedulesQuery.data ?? [];
  const handlers = handlersQuery.data ?? [];

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h2 className="text-2xl font-semibold">Scheduled tasks</h2>
          <p className="mt-1 max-w-2xl text-sm text-slate-600 dark:text-slate-400">
            Run registered server-side work once or on a recurring Quartz schedule. Times below are also shown in your browser’s local time.
          </p>
        </div>
        {canManage && (
          <button onClick={() => setEditing("new")} className="rounded-lg bg-sky-600 px-4 py-2 text-sm font-semibold text-white hover:bg-sky-700">
            New schedule
          </button>
        )}
      </div>

      {!canManage && (
        <div className="rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900 dark:border-amber-900 dark:bg-amber-950/30 dark:text-amber-200">
          You have read-only access. An Operator or Administrator can change schedules.
        </div>
      )}

      {editing && (
        <ScheduleForm
          key={editing === "new" ? "new" : editing.id}
          schedule={editing === "new" ? undefined : editing}
          handlers={handlers}
          onClose={() => setEditing(null)}
          onSaved={() => { setEditing(null); invalidate(); }}
        />
      )}

      {schedules.length === 0 ? (
        <StateCard>No schedules yet. Register handlers in the backend, then create the first schedule here.</StateCard>
      ) : (
        <div className="grid gap-4">
          {schedules.map((schedule) => {
            const paused = schedule.status.toLowerCase().includes("paused");
            const handler = handlers.find((item) => item.key === schedule.handlerKey);
            return (
              <article key={schedule.id} className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm dark:border-slate-800 dark:bg-slate-900">
                <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <h3 className="font-semibold">{schedule.name}</h3>
                      <Status status={schedule.status} />
                    </div>
                    <p className="mt-1 text-sm text-slate-600 dark:text-slate-400">{handler?.displayName ?? schedule.handlerKey}</p>
                    <p className="mt-3 font-mono text-xs text-slate-600 dark:text-slate-300">
                      {schedule.kind === "Cron" ? `${schedule.cronExpression} · ${schedule.timeZoneId}` : `Once · ${formatDate(schedule.runAt)}`}
                    </p>
                    <div className="mt-3 flex flex-wrap gap-x-6 gap-y-1 text-xs text-slate-500 dark:text-slate-400">
                      <span>Next: {formatDate(schedule.nextFireAt)}</span>
                      <span>Previous: {formatDate(schedule.previousFireAt)}</span>
                    </div>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <button className={secondaryButton} onClick={() => setSelectedId(selectedId === schedule.id ? null : schedule.id)}>History</button>
                    {canManage && <>
                      <button className={secondaryButton} onClick={() => setEditing(schedule)}>Edit</button>
                      <button className={secondaryButton} disabled={action.isPending} onClick={() => action.mutate({ type: "run", schedule })}>Run now</button>
                      <button className={secondaryButton} disabled={action.isPending} onClick={() => action.mutate({ type: paused ? "resume" : "pause", schedule })}>{paused ? "Resume" : "Pause"}</button>
                      <button className="rounded-lg px-3 py-2 text-sm font-medium text-red-600 hover:bg-red-50 disabled:opacity-50 dark:text-red-400 dark:hover:bg-red-950/30" disabled={action.isPending} onClick={() => action.mutate({ type: "delete", schedule })}>Delete</button>
                    </>}
                  </div>
                </div>
                {selectedId === schedule.id && <RunHistory id={schedule.id} />}
              </article>
            );
          })}
        </div>
      )}
      {action.error && <StateCard error>{String(action.error)}</StateCard>}
    </div>
  );
}

function ScheduleForm({ schedule, handlers, onClose, onSaved }: {
  schedule?: ScheduledTask;
  handlers: Awaited<ReturnType<typeof scheduledTaskApi.handlers>>;
  onClose: () => void;
  onSaved: () => void;
}) {
  const initial = useMemo<SaveScheduledTask>(() => schedule ? {
    name: schedule.name,
    handlerKey: schedule.handlerKey,
    kind: schedule.kind,
    cronExpression: schedule.cronExpression,
    timeZoneId: schedule.timeZoneId,
    runAt: schedule.runAt ? localInputValue(schedule.runAt) : emptyForm().runAt,
  } : { ...emptyForm(), handlerKey: handlers[0]?.key ?? "" }, [schedule, handlers]);
  const [form, setForm] = useState(initial);
  const save = useMutation({
    mutationFn: () => {
      const payload = {
        ...form,
        runAt: form.kind === "OneTime" && form.runAt ? new Date(form.runAt).toISOString() : null,
        cronExpression: form.kind === "Cron" ? form.cronExpression : null,
        timeZoneId: form.kind === "Cron" ? form.timeZoneId : null,
      };
      return schedule ? scheduledTaskApi.update(schedule.id, payload) : scheduledTaskApi.create(payload);
    },
    onSuccess: onSaved,
  });
  const set = (key: keyof SaveScheduledTask, value: string) => setForm(current => ({ ...current, [key]: value }));

  return (
    <form onSubmit={(event) => { event.preventDefault(); save.mutate(); }} className="rounded-xl border border-sky-200 bg-sky-50/50 p-5 dark:border-sky-900 dark:bg-sky-950/20">
      <div className="grid gap-4 md:grid-cols-2">
        <label className="text-sm font-medium">Name<input required maxLength={120} className={`${inputClass} mt-1`} value={form.name} onChange={event => set("name", event.target.value)} /></label>
        <label className="text-sm font-medium">Handler<select required className={`${inputClass} mt-1`} value={form.handlerKey} onChange={event => set("handlerKey", event.target.value)}>{handlers.map(handler => <option key={handler.key} value={handler.key}>{handler.displayName}</option>)}</select></label>
        <label className="text-sm font-medium">Type<select className={`${inputClass} mt-1`} value={form.kind} onChange={event => set("kind", event.target.value as ScheduledTaskKind)}><option value="Cron">Recurring</option><option value="OneTime">One time</option></select></label>
        {form.kind === "Cron" ? <>
          <label className="text-sm font-medium">Quartz cron expression<input required className={`${inputClass} mt-1 font-mono`} value={form.cronExpression ?? ""} onChange={event => set("cronExpression", event.target.value)} /><span className="mt-1 block text-xs font-normal text-slate-500">Includes seconds, for example 0 0 9 ? * MON-FRI *</span></label>
          <label className="text-sm font-medium md:col-span-2">IANA time zone<input required className={`${inputClass} mt-1`} value={form.timeZoneId ?? "UTC"} onChange={event => set("timeZoneId", event.target.value)} /></label>
        </> : <label className="text-sm font-medium">Run at (your local time)<input required type="datetime-local" className={`${inputClass} mt-1`} value={form.runAt ?? ""} onChange={event => set("runAt", event.target.value)} /></label>}
      </div>
      {save.error && <p className="mt-3 text-sm text-red-700 dark:text-red-300">{String(save.error)}</p>}
      <div className="mt-5 flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={onClose}>Cancel</button><button disabled={save.isPending} className="rounded-lg bg-sky-600 px-4 py-2 text-sm font-semibold text-white hover:bg-sky-700 disabled:opacity-50">{save.isPending ? "Saving…" : "Save schedule"}</button></div>
    </form>
  );
}

function RunHistory({ id }: { id: string }) {
  const query = useQuery({ queryKey: [...scheduleKey, id, "runs"], queryFn: () => scheduledTaskApi.runs(id), refetchInterval: 5000 });
  if (query.isLoading) return <p className="mt-4 text-sm text-slate-500">Loading history…</p>;
  if (query.error) return <p className="mt-4 text-sm text-red-600">Could not load history.</p>;
  if (!query.data?.length) return <p className="mt-4 text-sm text-slate-500">This schedule has not run yet.</p>;
  return <div className="mt-5 overflow-x-auto border-t border-slate-200 pt-4 dark:border-slate-800"><table className="w-full text-left text-sm"><thead className="text-xs uppercase text-slate-500"><tr><th className="pb-2">Started</th><th className="pb-2">Status</th><th className="pb-2">Attempt</th><th className="pb-2">Duration</th><th className="pb-2">Error</th></tr></thead><tbody>{query.data.map(run => <tr key={run.id} className="border-t border-slate-100 dark:border-slate-800"><td className="py-2 pr-4 whitespace-nowrap">{formatDate(run.startedAt)}</td><td className="py-2 pr-4"><Status status={run.status} /></td><td className="py-2 pr-4">{run.attempt + 1}</td><td className="py-2 pr-4">{run.durationMilliseconds == null ? "—" : `${run.durationMilliseconds} ms`}</td><td className="max-w-sm truncate py-2 text-red-600 dark:text-red-400" title={run.errorSummary ?? ""}>{run.errorSummary ?? "—"}</td></tr>)}</tbody></table></div>;
}

function Status({ status }: { status: string }) {
  const healthy = ["normal", "succeeded", "waiting"].includes(status.toLowerCase());
  return <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${healthy ? "bg-emerald-100 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-300" : "bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300"}`}>{status}</span>;
}
function StateCard({ children, error = false }: { children: React.ReactNode; error?: boolean }) { return <div className={`rounded-xl border p-8 text-center text-sm ${error ? "border-red-200 bg-red-50 text-red-800 dark:border-red-900 dark:bg-red-950/20 dark:text-red-300" : "border-slate-200 bg-white text-slate-500 dark:border-slate-800 dark:bg-slate-900 dark:text-slate-400"}`}>{children}</div>; }
function formatDate(value?: string | null) { return value ? new Date(value).toLocaleString(undefined, { timeZoneName: "short" }) : "—"; }
function localInputValue(value: string) { const date = new Date(value); return new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16); }
