import { useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import {
  useGetApiTodos,
  usePostApiTodos,
  usePutApiTodosId,
  useDeleteApiTodosId,
  usePatchApiTodosIdComplete,
} from "../api/todos/todos";

export default function TodoList() {
  const [newTodoTitle, setNewTodoTitle] = useState("");
  const queryClient = useQueryClient();

  const invalidateTodos = () =>
    queryClient.invalidateQueries({ queryKey: ["/api/Todos"] });

  const { data: todosResponse, isLoading, error } = useGetApiTodos({
    query: { refetchInterval: 5000 },
  });
  const todos = todosResponse?.data ?? [];
  const createMutation = usePostApiTodos({
    mutation: { onSuccess: invalidateTodos },
  });
  const updateMutation = usePutApiTodosId({
    mutation: { onSuccess: invalidateTodos },
  });
  const deleteMutation = useDeleteApiTodosId({
    mutation: { onSuccess: invalidateTodos },
  });
  const completeMutation = usePatchApiTodosIdComplete({
    mutation: { onSuccess: invalidateTodos },
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!newTodoTitle.trim()) return;
    createMutation.mutate({ data: { title: newTodoTitle, isCompleted: false } });
    setNewTodoTitle("");
  };

  if (error) {
    return (
      <div className="rounded-xl bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 p-4">
        <p className="text-red-800 dark:text-red-300">
          Error loading todos: {String(error)}
        </p>
      </div>
    );
  }

  return (
    <div className="w-full space-y-6">
      <form onSubmit={handleSubmit} className="flex gap-2">
        <input
          type="text"
          value={newTodoTitle}
          onChange={(e) => setNewTodoTitle(e.target.value)}
          placeholder="What needs to be done?"
          className="flex-1 px-4 py-3 rounded-xl bg-white dark:bg-slate-800 border border-slate-300 dark:border-slate-700 focus:outline-none focus:ring-2 focus:ring-sky-500 dark:focus:ring-sky-400 transition-all"
          disabled={createMutation.isPending}
        />
        <button
          type="submit"
          disabled={createMutation.isPending || !newTodoTitle.trim()}
          className="px-6 py-3 rounded-xl bg-gradient-to-r from-sky-600 to-violet-600 text-white font-medium hover:from-sky-700 hover:to-violet-700 disabled:opacity-50 disabled:cursor-not-allowed transition-all hover:shadow-lg hover:-translate-y-0.5"
        >
          {createMutation.isPending ? "Adding..." : "Add"}
        </button>
      </form>

      {/* Todo List */}
      <div className="space-y-2">
        {isLoading ? (
          <div className="text-center py-8 text-slate-500 dark:text-slate-400">
            Loading todos...
          </div>
        ) : todos.length === 0 ? (
          <div className="text-center py-12 text-slate-500 dark:text-slate-400">
            <p className="text-lg">No todos yet!</p>
            <p className="text-sm mt-2">
              Add your first todo above to get started.
            </p>
          </div>
        ) : (
          todos.map((todo) => (
            <div
              key={todo.id}
              className="group flex items-center gap-3 p-4 rounded-xl bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 hover:border-sky-300 dark:hover:border-sky-600 transition-all hover:shadow-md"
            >
              <input
                type="checkbox"
                checked={todo.isCompleted}
                onChange={() => {
                  if (!todo.isCompleted) {
                    completeMutation.mutate({ id: todo.id! });
                  } else {
                    updateMutation.mutate({
                      id: todo.id!,
                      data: {
                        ...todo,
                        isCompleted: false,
                        completedAt: null,
                      },
                    });
                  }
                }}
                className="w-5 h-5 rounded border-slate-300 dark:border-slate-600 text-sky-600 focus:ring-sky-500 cursor-pointer"
              />
              <div className="flex-1">
                <p
                  className={`${todo.isCompleted ? "line-through text-slate-400 dark:text-slate-500" : "text-slate-900 dark:text-slate-100"}`}
                >
                  {todo.title}
                </p>
                <p className="text-xs text-slate-500 dark:text-slate-400 mt-1">
                  {todo.createdAt && new Date(todo.createdAt).toLocaleString()}
                  {todo.completedAt &&
                    ` • Completed ${new Date(todo.completedAt).toLocaleString()}`}
                </p>
              </div>
              <button
                onClick={() => deleteMutation.mutate({ id: todo.id! })}
                disabled={deleteMutation.isPending}
                className="opacity-0 group-hover:opacity-100 px-3 py-1 text-sm text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-900/20 rounded-lg transition-all"
              >
                Delete
              </button>
            </div>
          ))
        )}
      </div>

      {/* Stats */}
      {todos.length > 0 && (
        <div className="flex justify-center gap-6 text-sm text-slate-600 dark:text-slate-400 pt-4 border-t border-slate-200 dark:border-slate-800">
          <span>Total: {todos.length}</span>
          <span>Active: {todos.filter((t) => !t.isCompleted).length}</span>
          <span>Completed: {todos.filter((t) => t.isCompleted).length}</span>
        </div>
      )}
    </div>
  );
}
