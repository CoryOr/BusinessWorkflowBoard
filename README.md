# Business Workflow Board

A C# web application for tracking business requests, department ownership, approvals, and handoffs. Connected browsers receive live board updates through SignalR.

## Features

- Create purchase, access, and service requests.
- Track requests across Queued, In Progress, Awaiting Approval, and Completed columns.
- Require Finance approval for purchase requests and IT approval for access requests.
- Complete service requests without approval.
- Transfer ownership between departments with a required note and timestamped handoff history.
- Filter the board by owning department.
- Preserve unfinished handoff drafts during live updates.
- Store requests and history in SQLite.
- Reject outdated database saves using optimistic concurrency.

## Technology

- C# and ASP.NET Core Minimal APIs, targeting .NET 10
- Entity Framework Core with SQLite and migrations
- SignalR
- HTML, CSS, and JavaScript
- xUnit with WebApplicationFactory

## Run locally

Install the .NET 10 SDK. From the project root, run these commands separately:

```bash
dotnet restore
```

```bash
dotnet tool restore
```

```bash
dotnet ef database update
```

```bash
dotnet run --urls http://localhost:5100
```

Open [the workflow board](http://localhost:5100/board.html).

The database is created locally as `workflow.db`. The browser loads the SignalR client library from a CDN, which requires internet access.

## Run tests

From the project root:

```bash
dotnet test tests/BusinessWorkflowBoard.Tests/BusinessWorkflowBoard.Tests.csproj
```

The test suite covers purchase approval, service completion, handoff persistence, invalid request rejection, and outdated-save protection. Tests use separate in-memory SQLite databases.

## Try the workflow

1. Open the board in two browsers.
2. Create a purchase request owned by Operations.
3. Start it, then hand it off to IT with a note.
4. Submit it for approval.
5. Select Finance as the demo reviewer department and approve it.
6. Watch both boards update and inspect the handoff history.

For an access request, select IT as the reviewer. Service requests can be completed directly after starting.

## Project structure

- `Models/` — task models, enums, request models, and response models
- `Data/` — EF Core database configuration and version handling
- `Endpoints/` — task API routes and workflow rules
- `Hubs/` — SignalR hub
- `Migrations/` — database schema migrations
- `wwwroot/board.html` — interactive board
- `tests/BusinessWorkflowBoard.Tests/` — automated tests

## Demo scope

The reviewer department selector demonstrates approval rules. It is not authentication: users can select a department, and the API checks the submitted value. Authenticated user identities and permissions would be needed for a production deployment.