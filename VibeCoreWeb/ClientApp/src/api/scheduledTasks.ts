import {
  deleteApiScheduledTasksSchedulesId,
  getApiScheduledTasksHandlers,
  getApiScheduledTasksSchedules,
  getApiScheduledTasksSchedulesIdRuns,
  postApiScheduledTasksSchedules,
  postApiScheduledTasksSchedulesIdPause,
  postApiScheduledTasksSchedulesIdResume,
  postApiScheduledTasksSchedulesIdRun,
  putApiScheduledTasksSchedulesId,
} from "./scheduled-tasks/scheduled-tasks";

export type ScheduledTaskKind = "Cron" | "OneTime";

export interface ScheduledTaskHandler {
  key: string;
  displayName: string;
  description: string;
  retryCount: number;
  allowsConcurrentExecution: boolean;
}

export interface ScheduledTask {
  id: string;
  name: string;
  handlerKey: string;
  kind: ScheduledTaskKind;
  cronExpression?: string | null;
  timeZoneId?: string | null;
  runAt?: string | null;
  status: string;
  nextFireAt?: string | null;
  previousFireAt?: string | null;
}

export interface ScheduledTaskRun {
  id: string;
  scheduleId: string;
  handlerKey: string;
  status: string;
  attempt: number;
  startedAt: string;
  completedAt?: string | null;
  durationMilliseconds?: number | null;
  errorSummary?: string | null;
}

export interface SaveScheduledTask {
  name: string;
  handlerKey: string;
  kind: ScheduledTaskKind;
  cronExpression?: string | null;
  timeZoneId?: string | null;
  runAt?: string | null;
}

export const scheduledTaskApi = {
  handlers: async () => (await getApiScheduledTasksHandlers()).data as ScheduledTaskHandler[],
  schedules: async () => (await getApiScheduledTasksSchedules()).data as ScheduledTask[],
  runs: async (id: string) => (await getApiScheduledTasksSchedulesIdRuns(id)).data as ScheduledTaskRun[],
  create: async (data: SaveScheduledTask) => (await postApiScheduledTasksSchedules(data)).data as ScheduledTask,
  update: async (id: string, data: SaveScheduledTask) => (await putApiScheduledTasksSchedulesId(id, data)).data as ScheduledTask,
  pause: async (id: string) => { await postApiScheduledTasksSchedulesIdPause(id); },
  resume: async (id: string) => { await postApiScheduledTasksSchedulesIdResume(id); },
  run: async (id: string) => { await postApiScheduledTasksSchedulesIdRun(id); },
  delete: async (id: string) => { await deleteApiScheduledTasksSchedulesId(id); },
};
