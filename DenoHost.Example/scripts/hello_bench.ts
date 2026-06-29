Deno.bench('string join', () => {
  const parts: string[] = [];
  for (let i = 0; i < 100; i++) {
    parts.push(i.toString());
  }
  parts.join('');
});

Deno.bench('array map', () => {
  [1, 2, 3, 4, 5].map((x) => x * x);
});
