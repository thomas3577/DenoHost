Deno.test('basic math', () => {
  if (1 + 1 !== 2) {
    throw new Error('Math is broken');
  }
});

Deno.test('string operations', () => {
  const s = 'hello' + ' ' + 'world';
  if (s !== 'hello world') {
    throw new Error(`Expected 'hello world', got '${s}'`);
  }
});
