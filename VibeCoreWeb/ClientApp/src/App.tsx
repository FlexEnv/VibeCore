import Header from "./components/Header";
import Home from "./pages/Home";

function App() {
  return (
    <div className="min-h-screen bg-slate-50 text-slate-900 dark:bg-slate-950 dark:text-slate-100">
      <Header />
      <main className="mx-auto w-full max-w-3xl px-4 py-10 sm:px-6">
        <Home />
      </main>
    </div>
  );
}

export default App;
