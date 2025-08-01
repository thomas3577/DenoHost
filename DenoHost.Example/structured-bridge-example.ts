// deno-lint-ignore-file no-explicit-any
/**
 * Example Deno script demonstrating bidirectional API bridge with DenoProcess
 * This script shows how to use JSON-RPC over stdin/stdout for structured communication
 */

interface JsonRpcRequest {
  jsonrpc: "2.0";
  id: string;
  method: string;
  params?: any;
}

interface JsonRpcResponse {
  jsonrpc: "2.0";
  id: string;
  result?: any;
  error?: {
    code: number;
    message: string;
    data?: any;
  };
}

interface JsonRpcNotification {
  jsonrpc: "2.0";
  method: string;
  params?: any;
}

class DenoApiHandler {
  private methods = new Map<string, (params: any) => Promise<any>>();

  constructor() {
    this.setupStdinHandler();
    this.registerBuiltinMethods();
  }

  private setupStdinHandler() {
    // Listen for JSON-RPC messages from .NET
    const decoder = new TextDecoder();

    (async () => {
      for await (const chunk of Deno.stdin.readable) {
        const text = decoder.decode(chunk);
        const lines = text.split("\n").filter((line) => line.trim());

        for (const line of lines) {
          try {
            const message = JSON.parse(line);
            if (message.jsonrpc === "2.0") {
              await this.handleMessage(message);
            }
          } catch (error) {
            console.error(
              "Failed to parse JSON-RPC message:",
              error,
            );
          }
        }
      }
    })();
  }

  private async handleMessage(message: JsonRpcRequest | JsonRpcNotification) {
    if ("id" in message) {
      // It's a request - send response
      await this.handleRequest(message as JsonRpcRequest);
    } else {
      // It's a notification - no response expected
      await this.handleNotification(message as JsonRpcNotification);
    }
  }

  private async handleRequest(request: JsonRpcRequest) {
    try {
      const handler = this.methods.get(request.method);
      if (!handler) {
        await this.sendResponse({
          jsonrpc: "2.0",
          id: request.id,
          error: {
            code: -32601,
            message: "Method not found",
            data: `Method '${request.method}' is not available`,
          },
        });
        return;
      }

      const result = await handler(request.params);
      await this.sendResponse({
        jsonrpc: "2.0",
        id: request.id,
        result: result,
      });
    } catch (error: unknown) {
      await this.sendResponse({
        jsonrpc: "2.0",
        id: request.id,
        error: {
          code: -32603,
          message: "Internal error",
          data: (error as Error).message,
        },
      });
    }
  }

  private async handleNotification(notification: JsonRpcNotification) {
    console.log(
      `Received notification: ${notification.method}`,
      notification.params,
    );
  }

  private async sendResponse(response: JsonRpcResponse) {
    const json = JSON.stringify(response);
    await Deno.stdout.write(new TextEncoder().encode(json + "\n"));
  }

  private async sendNotification(method: string, params?: any) {
    const notification: JsonRpcNotification = {
      jsonrpc: "2.0",
      method: method,
      params: params,
    };
    const json = JSON.stringify(notification);
    await Deno.stdout.write(new TextEncoder().encode(json + "\n"));
  }

  private async callMethod(method: string, params?: any): Promise<any> {
    const id = crypto.randomUUID();
    const request: JsonRpcRequest = {
      jsonrpc: "2.0",
      id: id,
      method: method,
      params: params,
    };

    const json = JSON.stringify(request);
    await Deno.stdout.write(new TextEncoder().encode(json + "\n"));

    // In a real implementation, you'd wait for the response with the matching ID
    // For this demo, we'll just return a promise
    return new Promise((resolve) => {
      setTimeout(() => resolve(`Response for ${method}`), 100);
    });
  }

  public registerMethod(
    name: string,
    handler: (params: any) => Promise<any>,
  ) {
    this.methods.set(name, handler);
  }

  private registerBuiltinMethods() {
    // Math operations
    this.registerMethod("math.add", async (params) => {
      const { a, b } = params;
      return a + b;
    });

    this.registerMethod("math.multiply", async (params) => {
      const { a, b } = params;
      return a * b;
    });

    // System information
    this.registerMethod("system.info", async () => {
      return {
        os: Deno.build.os,
        arch: Deno.build.arch,
        version: Deno.version.deno,
        timestamp: new Date().toISOString(),
      };
    });

    // File operations
    this.registerMethod("file.read", async (params) => {
      const { path } = params;
      const content = await Deno.readTextFile(path);
      return { content, size: content.length };
    });

    this.registerMethod("file.write", async (params) => {
      const { path, content } = params;
      await Deno.writeTextFile(path, content);
      return { success: true, path };
    });

    // HTTP requests
    this.registerMethod("http.get", async (params) => {
      const { url } = params;
      const response = await fetch(url);
      const data = await response.text();
      return {
        status: response.status,
        statusText: response.statusText,
        data: data.substring(0, 1000), // Limit response size
      };
    });

    // Process operations
    this.registerMethod("process.run", async (params) => {
      const { command, args = [] } = params;
      const process = new Deno.Command(command, { args });
      const output = await process.output();

      return {
        success: output.success,
        code: output.code,
        stdout: new TextDecoder().decode(output.stdout),
        stderr: new TextDecoder().decode(output.stderr),
      };
    });
  }

  public async start() {
    // Send startup notification
    await this.sendNotification("deno.ready", {
      methods: Array.from(this.methods.keys()),
      pid: Deno.pid,
      cwd: Deno.cwd(),
    });

    // Send periodic heartbeat
    setInterval(async () => {
      await this.sendNotification("deno.heartbeat", {
        timestamp: new Date().toISOString(),
        memoryUsage: (performance as any).memory?.usedJSHeapSize ||
          "unknown",
      });
    }, 5000);

    console.log(
      "Deno API Handler started - ready for JSON-RPC communication",
    );
  }
}

// Start the API handler
const handler = new DenoApiHandler();
await handler.start();

// Keep the process alive
await new Promise(() => {});
