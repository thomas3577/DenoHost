export default {
  fetch(_req: Request): Response {
    return new Response('Hello from Deno serve!', {
      headers: { 'Content-Type': 'text/plain' },
    });
  },
} satisfies Deno.ServeDefaultExport;
