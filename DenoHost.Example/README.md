# DenoHost.Example

This example project demonstrates how to use DenoHost.Core to execute and manage Deno processes from an ASP.NET Core web application.

## Available Endpoints

### GET /

**Description:** Executes a basic Deno script using the `Deno.Execute<T>` method.

**Details:** Runs the `scripts/app.ts` file and returns the script's output as plain text.

**Example:**

```bash
curl http://localhost:5000/
```

---

### GET /demo

**Description:** Demonstrates basic DenoProcess functionality.

**Details:** Runs the `BasicUsageExample` which shows how to:

- Create and start a DenoProcess
- Subscribe to output, error, and exit events
- Send input to a running process
- Stop a process gracefully

**Example:**

```bash
curl http://localhost:5000/demo
```

---

### GET /demo/wait

**Description:** Demonstrates waiting for a Deno process to complete.

**Details:** Runs the `WaitForExitExample` which shows how to:

- Start a process that runs a finite task
- Wait for the process to complete naturally
- Retrieve the exit code

**Example:**

```bash
curl http://localhost:5000/demo/wait
```

---

### GET /demo/interactive

**Description:** Demonstrates interactive communication with a Deno process.

**Details:** Runs the `InteractiveExample` which shows how to:

- Send commands to a running Deno process
- Capture and process output responses
- Handle interactive sessions

**Example:**

```bash
curl http://localhost:5000/demo/interactive
```

---

### GET /version

**Description:** Retrieves the Deno runtime version information.

**Details:** Executes `deno --version` and returns the version details including Deno, V8, and TypeScript versions.

**Example:**

```bash
curl http://localhost:5000/version
```

## Running the Application

1. Run the application:

   ```bash
   dotnet run
   ```

2. Access the endpoints at `http://localhost:5000` (or the configured port)

## Required Scripts

The application expects the following Deno scripts in the `scripts/` directory:

- `app.ts` - Basic script for the root endpoint
- `long-running.ts` - Long-running process for the demo endpoint
- `finite-task.ts` - Script that completes on its own for the wait example
- `interactive.ts` - Interactive script that accepts input commands
