import Header from "./components/Header";
import Home from "./pages/Home";
import ScheduledTasks from "./pages/ScheduledTasks";
import { Navigate, Route, Routes } from "react-router-dom";

function App() {
  return (
    <div className="min-h-screen bg-slate-50 text-slate-900 dark:bg-slate-950 dark:text-slate-100">
      <Header />
      <main className="mx-auto w-full max-w-6xl px-4 py-10 sm:px-6">
        <Routes>
          <Route path="/" element={<Home />} />
          <Route path="/scheduled-tasks" element={<ScheduledTasks />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </main>
    </div>
  );
}

export default App;
