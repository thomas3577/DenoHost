// Long-running Deno script that accepts input and produces output

console.log('Long-running Deno process started');

let counter = 0;

// Set up interval to produce periodic output
const interval = setInterval(() => {
  counter++;
  console.log(`Heartbeat ${counter} at ${new Date().toISOString()}`);

  if (counter >= 10) {
    console.log('Stopping after 10 heartbeats');
    clearInterval(interval);
    Deno.exit(0);
  }
}, 1000);

// Listen for input from stdin
const decoder = new TextDecoder();

for await (const chunk of Deno.stdin.readable) {
  const input = decoder.decode(chunk).trim();
  console.log(`Received input: ${input}`);

  if (input === 'stop') {
    console.log('Stop command received, exiting...');
    clearInterval(interval);
    Deno.exit(0);
  }
}
