# React + Vite Integration with ASP.NET Core

This project now includes a React + Vite frontend integrated with your ASP.NET Core Razor Pages application.

## Project Structure

- **Razor Pages**: Available at root routes (`/`, `/Privacy`, etc.)
- **React App**: Available under `/app` route
- **ClientApp/**: Contains the React + Vite frontend source code

## Getting Started

### Prerequisites

- .NET 10.0 SDK
- Node.js 22.18 or newer

### Development Setup

1. **First Time Setup** - Install npm dependencies:

   ```bash
   cd VibeCore/ClientApp
   npm install
   ```

2. **Start the Vite Dev Server** (for hot reload):

   ```bash
   cd VibeCore/ClientApp
   npm run dev
   ```

   This starts the Vite dev server on http://localhost:5173

3. **Start the ASP.NET Core Application** (in a separate terminal):

   ```bash
   cd VibeCore
   dotnet run
   ```

4. **Access the Application**:
   - Razor Pages: https://localhost:5001 (or as configured)
   - React App: https://localhost:5001/app

### How It Works

#### Development Mode

- The Vite dev server runs on port 5173
- ASP.NET Core proxies requests to `/app` to the Vite dev server
- You get hot module replacement (HMR) - changes appear instantly
- Both servers need to be running simultaneously

#### Production Mode

- When you build/publish the .NET project, it automatically:
  1. Installs npm dependencies
  2. Builds the React app with Vite
  3. Outputs built files to `wwwroot/app`
- ASP.NET Core serves the static files directly
- No need to run Vite dev server

### React App Features

The React app includes:

- **React 18** with modern hooks and features
- **Vite** for fast development and optimized builds
- **React Router** for client-side routing
- **Hot Module Replacement (HMR)** for instant updates during development

### Adding New React Pages

1. Create a new component in `ClientApp/src/pages/`
2. Import and add a route in `ClientApp/src/App.jsx`

Example:

```jsx
// ClientApp/src/pages/NewPage.jsx
function NewPage() {
  return (
    <div>
      <h2>New Page</h2>
    </div>
  );
}
export default NewPage;

// ClientApp/src/App.jsx
import NewPage from "./pages/NewPage";
// Add to Routes:
<Route path="/new-page" element={<NewPage />} />;
```

Access at: https://localhost:5001/app/new-page

### Building for Production

Simply build or publish your .NET project:

```bash
dotnet build
# or
dotnet publish
```

The React app will be built automatically and included in the output.

### Troubleshooting

**Vite dev server not connecting:**

- Ensure the Vite dev server is running (`npm run dev` in ClientApp folder)
- Check that it's running on port 5173
- Verify there are no firewall issues

**Hot reload not working:**

- Make sure both the ASP.NET Core app and Vite dev server are running
- Check browser console for WebSocket connection errors
- Try restarting both servers

**Build errors:**

- Ensure Node.js is installed and accessible in your PATH
- Try deleting `ClientApp/node_modules` and running `npm install` again
- Check that npm version is compatible with the packages
