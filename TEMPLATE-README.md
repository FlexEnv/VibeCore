# ASP.NET Core with React and Vite Template

This repository is a .NET template for an authenticated full-stack application
using ASP.NET Core 10, React 18, Vite, Entity Framework Core, Tailwind CSS, and
Flex SSO.

## Install and create an application

```bash
dotnet new install .
dotnet new vibe-core -n MyApplication
```

Install the frontend dependencies in the generated project, then run it through
a Flex environment so the required SSO authority is supplied:

```bash
cd MyApplication/MyApplication/ClientApp
npm install
```

For direct local execution, configure `FlexSso__Authority` (and, when needed,
`FlexSso__BackchannelAuthority`) for the local Flex instance. VibeCore does not
include local login, registration, password, or account-management flows.

Project names, namespaces, solution files, API titles, and container names are
renamed from `VibeCore` when the template is instantiated.
