import { useTheme } from "../contexts/ThemeContext";
import { useGetApiUserCurrent } from "../api/user/user";
import { NavLink } from "react-router-dom";

function Header() {
  const { theme, toggleTheme } = useTheme();
  const { data: userResponse } = useGetApiUserCurrent();
  const user = userResponse?.data;
  const identity = user?.userName || user?.email || "Signed in";

  return (
    <header className="border-b border-slate-200 bg-white dark:border-slate-800 dark:bg-slate-950">
      <div className="mx-auto flex w-full max-w-6xl items-center justify-between px-4 py-4 sm:px-6">
        <div className="flex items-center gap-6">
          <h1 className="text-xl font-semibold text-slate-900 dark:text-white">VibeCore</h1>
          <nav className="flex gap-1 text-sm">
            <NavLink to="/" end className={({ isActive }) => `rounded-lg px-3 py-2 ${isActive ? "bg-sky-50 font-medium text-sky-700 dark:bg-sky-950 dark:text-sky-300" : "text-slate-600 hover:bg-slate-50 dark:text-slate-300 dark:hover:bg-slate-900"}`}>Todos</NavLink>
            <NavLink to="/scheduled-tasks" className={({ isActive }) => `rounded-lg px-3 py-2 ${isActive ? "bg-sky-50 font-medium text-sky-700 dark:bg-sky-950 dark:text-sky-300" : "text-slate-600 hover:bg-slate-50 dark:text-slate-300 dark:hover:bg-slate-900"}`}>Scheduled tasks</NavLink>
          </nav>
        </div>
        <div className="flex items-center gap-3">
          <span className="max-w-48 truncate text-sm text-slate-600 dark:text-slate-300" title={identity}>
            {identity}
          </span>
          <button
            type="button"
            onClick={toggleTheme}
            className="inline-flex h-9 w-9 items-center justify-center rounded-lg border border-slate-200 text-slate-600 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-900"
            aria-label={`Switch to ${theme === "light" ? "dark" : "light"} mode`}
            title={`Switch to ${theme === "light" ? "dark" : "light"} mode`}
          >
            {theme === "light" ? "☾" : "☀"}
          </button>
        </div>
      </div>
    </header>
  );
}

export default Header;
