// Interactive Deno script that responds to commands

console.log('Interactive Deno script started. Type commands:');
console.log('Available commands: hello, time, exit');

const decoder = new TextDecoder();

for await (const chunk of Deno.stdin.readable) {
  const input = decoder.decode(chunk).trim();

  if (!input) continue;

  console.log(`Received command: ${input}`);

  switch (input.toLowerCase()) {
    case 'hello':
      console.log('Hello from Deno!');
      break;
    case 'time':
      console.log(`Current time: ${new Date().toISOString()}`);
      break;
    case 'command1':
      console.log('Executed command1 successfully');
      break;
    case 'command2':
      console.log('Executed command2 successfully');
      break;
    case 'exit':
      console.log('Goodbye!');
      Deno.exit(0);
      break;
    default:
      console.log(`Unknown command: ${input}`);
      break;
  }
}
