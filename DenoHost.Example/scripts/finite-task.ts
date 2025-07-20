// A finite task that runs for a short time and then exits

console.log('Finite task started');

for (let i = 1; i <= 5; i++) {
  console.log(`Processing step ${i}/5`);
  await new Promise((resolve) => setTimeout(resolve, 1000));
}

console.log('Finite task completed successfully');
Deno.exit(0);
