import { assertEquals, assertMatch, assertStrictEquals } from '@std/assert';
import { argStyleToCsType, inferProperty, renderToArgsLine, toPascalCase } from './generate.ts';

// ─── toPascalCase ─────────────────────────────────────────────────────────────

Deno.test('toPascalCase: single word', () => {
  assertEquals(toPascalCase('watch'), 'Watch');
});

Deno.test('toPascalCase: hyphenated', () => {
  assertEquals(toPascalCase('allow-read'), 'AllowRead');
  assertEquals(toPascalCase('no-check'), 'NoCheck');
  assertEquals(toPascalCase('env-file'), 'EnvFile');
  assertEquals(toPascalCase('v8-flags'), 'V8Flags');
  assertEquals(toPascalCase('node-modules-dir'), 'NodeModulesDir');
});

// ─── inferProperty: type inference from usage strings ─────────────────────────

function arg(long: string, usage: string, heading?: string) {
  return { name: long, short: null, long, required: false, help: null, help_heading: heading ?? null, usage };
}

Deno.test('inferProperty: pure bool flag', () => {
  const prop = inferProperty(arg('no-remote', '--no-remote'));
  assertEquals(prop?.csType, 'bool?');
  assertEquals(prop?.argStyle, 'flag');
  assertEquals(prop?.flagName, '--no-remote');
  assertEquals(prop?.csName, 'NoRemote');
});

Deno.test('inferProperty: bool flag with optional BOOLEAN value', () => {
  const prop = inferProperty(arg('frozen', '--frozen[=<BOOLEAN>]'));
  assertEquals(prop?.csType, 'bool?');
  assertEquals(prop?.argStyle, 'boolopt');
});

Deno.test('inferProperty: required string value', () => {
  const prop = inferProperty(arg('import-map', '--import-map <FILE>'));
  assertEquals(prop?.csType, 'string?');
  assertEquals(prop?.argStyle, 'value');
});

Deno.test('inferProperty: optional string value', () => {
  const prop = inferProperty(arg('no-check', '--no-check[=<NO_CHECK_TYPE>]'));
  assertEquals(prop?.csType, 'string?');
  assertEquals(prop?.argStyle, 'optvalue');
});

Deno.test('inferProperty: optional array', () => {
  const prop = inferProperty(arg('watch', '--watch[=<FILES>...]'));
  assertEquals(prop?.csType, 'string[]?');
  assertEquals(prop?.argStyle, 'optarray');
});

Deno.test('inferProperty: required array', () => {
  const prop = inferProperty(arg('ignore', '--ignore=<ignore>...'));
  assertEquals(prop?.csType, 'string[]?');
  assertEquals(prop?.argStyle, 'array');
});

Deno.test('inferProperty: int value (NUMBER)', () => {
  const prop = inferProperty(arg('seed', '--seed <NUMBER>'));
  assertEquals(prop?.csType, 'int?');
  assertEquals(prop?.argStyle, 'intvalue');
});

Deno.test('inferProperty: long value (MICROSECONDS)', () => {
  const prop = inferProperty(arg('cpu-prof-interval', '--cpu-prof-interval <MICROSECONDS>'));
  assertEquals(prop?.csType, 'long?');
  assertEquals(prop?.argStyle, 'longvalue');
});

Deno.test('inferProperty: int value (INDEX/COUNT)', () => {
  const prop = inferProperty(arg('shard', '--shard=<INDEX/COUNT>'));
  assertEquals(prop?.csType, 'int?');
  assertEquals(prop?.argStyle, 'intvalue');
});

Deno.test('inferProperty: positional arg returns null', () => {
  const result = inferProperty({ name: 'script_arg', short: null, long: null, required: false, help: null, help_heading: null, usage: '[SCRIPT_ARG]...' });
  assertEquals(result, null);
});

Deno.test('inferProperty: heading preserved', () => {
  const prop = inferProperty(arg('filter', '--filter <filter>', 'Testing options'));
  assertEquals(prop?.heading, 'Testing options');
});

Deno.test('inferProperty: null heading defaults to General', () => {
  const prop = inferProperty(arg('cert', '--cert <FILE>'));
  assertEquals(prop?.heading, 'General');
});

// ─── ARG_STYLE_OVERRIDES ──────────────────────────────────────────────────────

Deno.test('inferProperty: override — port is intvalue despite <PORT> hint', () => {
  const prop = inferProperty(arg('port', '--port <PORT>'));
  assertEquals(prop?.csType, 'int?');
  assertEquals(prop?.argStyle, 'intvalue');
});

Deno.test('inferProperty: override — line-width is intvalue', () => {
  const prop = inferProperty(arg('line-width', '--line-width <n>'));
  assertEquals(prop?.csType, 'int?');
  assertEquals(prop?.argStyle, 'intvalue');
});

Deno.test('inferProperty: override — indent-width is intvalue', () => {
  const prop = inferProperty(arg('indent-width', '--indent-width <n>'));
  assertEquals(prop?.csType, 'int?');
  assertEquals(prop?.argStyle, 'intvalue');
});

Deno.test('inferProperty: override — use-tabs is boolopt', () => {
  const prop = inferProperty(arg('use-tabs', '--use-tabs[=<true|false>]'));
  assertEquals(prop?.csType, 'bool?');
  assertEquals(prop?.argStyle, 'boolopt');
});

Deno.test('inferProperty: override — single-quote is boolopt', () => {
  const prop = inferProperty(arg('single-quote', '--single-quote[=<true|false>]'));
  assertEquals(prop?.csType, 'bool?');
  assertEquals(prop?.argStyle, 'boolopt');
});

Deno.test('inferProperty: override — no-semicolons is boolopt', () => {
  const prop = inferProperty(arg('no-semicolons', '--no-semicolons[=<true|false>]'));
  assertEquals(prop?.csType, 'bool?');
  assertEquals(prop?.argStyle, 'boolopt');
});

// ─── argStyleToCsType ─────────────────────────────────────────────────────────

Deno.test('argStyleToCsType: flag and boolopt → bool?', () => {
  assertEquals(argStyleToCsType('flag'), 'bool?');
  assertEquals(argStyleToCsType('boolopt'), 'bool?');
});

Deno.test('argStyleToCsType: intvalue → int?, longvalue → long?', () => {
  assertEquals(argStyleToCsType('intvalue'), 'int?');
  assertEquals(argStyleToCsType('longvalue'), 'long?');
});

Deno.test('argStyleToCsType: array and optarray → string[]?', () => {
  assertEquals(argStyleToCsType('array'), 'string[]?');
  assertEquals(argStyleToCsType('optarray'), 'string[]?');
});

Deno.test('argStyleToCsType: value and optvalue → string?', () => {
  assertEquals(argStyleToCsType('value'), 'string?');
  assertEquals(argStyleToCsType('optvalue'), 'string?');
});

// ─── renderToArgsLine ─────────────────────────────────────────────────────────

function prop(csName: string, csType: string, argStyle: Parameters<typeof renderToArgsLine>[0]['argStyle'], flagName: string) {
  return { csName, csType, argStyle, flagName, xmlDoc: '', heading: '' };
}

Deno.test('renderToArgsLine: flag', () => {
  const line = renderToArgsLine(prop('NoRemote', 'bool?', 'flag', '--no-remote'));
  assertEquals(line.trim(), 'if (NoRemote == true) args.Add("--no-remote");');
});

Deno.test('renderToArgsLine: boolopt emits =false for false', () => {
  const line = renderToArgsLine(prop('Frozen', 'bool?', 'boolopt', '--frozen'));
  assertMatch(line, /HasValue/);
  assertMatch(line, /--frozen=false/);
});

Deno.test('renderToArgsLine: value', () => {
  const line = renderToArgsLine(prop('ImportMap', 'string?', 'value', '--import-map'));
  assertMatch(line, /args\.Add\("--import-map"\)/);
  assertMatch(line, /args\.Add\(ImportMap\)/);
});

Deno.test('renderToArgsLine: intvalue', () => {
  const line = renderToArgsLine(prop('Seed', 'int?', 'intvalue', '--seed'));
  assertMatch(line, /HasValue/);
  assertMatch(line, /\.ToString\(CultureInfo\.InvariantCulture\)/);
});

Deno.test('renderToArgsLine: longvalue', () => {
  const line = renderToArgsLine(prop('CpuProfInterval', 'long?', 'longvalue', '--cpu-prof-interval'));
  assertMatch(line, /HasValue/);
  assertMatch(line, /\.ToString\(CultureInfo\.InvariantCulture\)/);
});

Deno.test('renderToArgsLine: optvalue uses string.Concat for =value form', () => {
  const line = renderToArgsLine(prop('NoCheck', 'string?', 'optvalue', '--no-check'));
  assertMatch(line, /string\.Concat/);
  assertMatch(line, /"--no-check="/);
});

Deno.test('renderToArgsLine: optvalue emits bare flag when value is empty string', () => {
  const line = renderToArgsLine(prop('NoCheck', 'string?', 'optvalue', '--no-check'));
  assertMatch(line, /Length == 0/);
  assertMatch(line, /args\.Add\("--no-check"\)/);
});

Deno.test('renderToArgsLine: array uses string.Join', () => {
  const line = renderToArgsLine(prop('Ignore', 'string[]?', 'array', '--ignore'));
  assertMatch(line, /string\.Join\(","/);
  assertMatch(line, /Length: > 0/);
});

Deno.test('renderToArgsLine: optarray emits bare flag for empty array', () => {
  const line = renderToArgsLine(prop('Watch', 'string[]?', 'optarray', '--watch'));
  assertMatch(line, /Length == 0/);
  assertMatch(line, /string\.Join\(","/);
  // bare flag branch
  assertStrictEquals(line.includes('args.Add("--watch")'), true);
});
