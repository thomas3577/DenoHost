import { assertEquals, assertMatch, assertStrictEquals } from '@std/assert';
import {
  argStyleToCsType,
  escapeXml,
  generateOptionsClass,
  inferProperty,
  parseHelpUsageHints,
  renderProperty,
  renderToArgsLine,
  toPascalCase,
} from './generate.ts';

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

Deno.test('inferProperty: override — seed/shard/shuffle/retry/repeats/coverage-threshold/jobs are intvalue', () => {
  for (const long of ['seed', 'shard', 'shuffle', 'retry', 'repeats', 'coverage-threshold', 'jobs']) {
    const prop = inferProperty(arg(long, `--${long} <VALUE>`));
    assertEquals(prop?.csType, 'int?', long);
    assertEquals(prop?.argStyle, 'intvalue', long);
  }
});

// ─── inferProperty: helpUsage overrides the (Deno 2.9.6+) placeholder-less usage field ────

Deno.test('inferProperty: helpUsage recovers a value flag when arg.usage has no placeholder', () => {
  // Deno 2.9.6's `deno json_reference` usage field, for a flag that actually takes a value.
  const prop = inferProperty(arg('output', '--output'), '--output <VALUE>');
  assertEquals(prop?.csType, 'string?');
  assertEquals(prop?.argStyle, 'value');
});

Deno.test('inferProperty: helpUsage recovers an optional-array flag', () => {
  const prop = inferProperty(arg('watch-exclude', '--watch-exclude'), '--watch-exclude[=VALUE...]');
  assertEquals(prop?.csType, 'string[]?');
  assertEquals(prop?.argStyle, 'optarray');
});

Deno.test('inferProperty: helpUsage confirms a pure flag stays bool?', () => {
  const prop = inferProperty(arg('no-terminal', '--no-terminal'), '--no-terminal');
  assertEquals(prop?.csType, 'bool?');
  assertEquals(prop?.argStyle, 'flag');
});

Deno.test('inferProperty: falls back to arg.usage when no helpUsage given', () => {
  const prop = inferProperty(arg('import-map', '--import-map <FILE>'));
  assertEquals(prop?.csType, 'string?');
  assertEquals(prop?.argStyle, 'value');
});

// ─── parseHelpUsageHints ────────────────────────────────────────────────────────

Deno.test('parseHelpUsageHints: extracts value/optvalue/optarray/flag placeholders', () => {
  const help = [
    'Options:',
    '  -o, --output <VALUE>    Output file (defaults to $PWD/<inferred-name>)',
    '      --no-check[=<VALUE>]  Skip type-checking.',
    '      --watch-exclude[=VALUE...]  Exclude provided files/patterns from watch mode',
    '      --no-terminal       Hide terminal on Windows',
    '      --lock [<VALUE>]    Check the specified lock file.',
  ].join('\n');
  const hints = parseHelpUsageHints(help);
  assertEquals(hints.get('output'), '<VALUE>');
  assertEquals(hints.get('no-check'), '[=<VALUE>]');
  assertEquals(hints.get('watch-exclude'), '[=VALUE...]');
  assertEquals(hints.get('no-terminal'), '');
  assertEquals(hints.get('lock'), '[<VALUE>]');
});

Deno.test('parseHelpUsageHints: stops before Permission options section', () => {
  const help = [
    'Options:',
    '      --no-check[=<VALUE>]  Skip type-checking.',
    '',
    'Permission options:',
    '  -R, --allow-read[=<PATH>...]  Allow file system read access.',
    '                                --allow-read  |  --allow-read="/etc,/var/log.txt"',
  ].join('\n');
  const hints = parseHelpUsageHints(help);
  assertEquals(hints.has('allow-read'), false);
});

Deno.test('parseHelpUsageHints: keeps first occurrence of a flag over a later example line', () => {
  // Regression guard: usage-example continuation lines inside a description (e.g.
  // `--allow-read="/etc"`) must not overwrite the real placeholder captured from the
  // flag's own definition line.
  const help = [
    'Options:',
    '  -R, --allow-read[=<PATH>...]  Allow file system read access.',
    '                                --allow-read  |  --allow-read="/etc,/var/log.txt"',
  ].join('\n');
  const hints = parseHelpUsageHints(help);
  assertEquals(hints.get('allow-read'), '[=<PATH>...]');
});

// ─── escapeXml ────────────────────────────────────────────────────────────────

Deno.test('escapeXml: escapes XML metacharacters', () => {
  assertEquals(escapeXml('a & b <c> d'), 'a &amp; b &lt;c&gt; d');
});

Deno.test('escapeXml: strips ANSI colour sequences from `deno json_reference` help', () => {
  const help =
    'Default value: \u001B[38;5;245mdeno.land:443\u001B[39m';
  assertEquals(escapeXml(help), 'Default value: deno.land:443');
});

Deno.test('escapeXml: strips bare control characters', () => {
  assertEquals(escapeXml('a\u0000b\u0007c\u007F'), 'abc');
});

Deno.test('escapeXml: strips CR so a summary stays on one line', () => {
  assertEquals(escapeXml('Define maximum line width\u000D'), 'Define maximum line width');
});
Deno.test('escapeXml: keeps ordinary printable text untouched', () => {
  assertEquals(escapeXml('--allow-net=example.com:443'), '--allow-net=example.com:443');
});

Deno.test('escapeXml: strips C1 control characters (U+0080-U+009F)', () => {
  assertEquals(escapeXml('a\u0080b\u009Fc'), 'abc');
});

Deno.test('escapeXml: strips 8-bit CSI (U+009B) SGR sequences', () => {
  const help = 'Default value: \u009B38;5;245mdeno.land:443\u009B39m';
  assertEquals(escapeXml(help), 'Default value: deno.land:443');
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

// ─── renderProperty ────────────────────────────────────────────────────────────

Deno.test('renderProperty: uses xmlDoc when present', () => {
  const rendered = renderProperty({ ...prop('Seed', 'int?', 'intvalue', '--seed'), xmlDoc: 'Sets the random seed.' });
  assertMatch(rendered, /\/\/\/ <summary>Sets the random seed\.<\/summary>/);
});

Deno.test('renderProperty: falls back to a generated summary when xmlDoc is empty', () => {
  const rendered = renderProperty({ ...prop('NoRemote', 'bool?', 'flag', '--no-remote'), xmlDoc: '' });
  assertMatch(rendered, /\/\/\/ <summary>The <c>--no-remote<\/c> option\.<\/summary>/);
});

// ─── generateOptionsClass ───────────────────────────────────────────────────────

function commandConfig(name: string): Parameters<typeof generateOptionsClass>[0] {
  return { name, positional: [], hasPermissions: false };
}

function subcommand(about: string | null): Parameters<typeof generateOptionsClass>[1] {
  return { name: 'eval', about, args: [] };
}

Deno.test('generateOptionsClass: sanitizes ANSI/control characters in subcmd.about', () => {
  const about = 'Evaluate [1mJavaScript[0m0m from the command line.';
  const generated = generateOptionsClass(commandConfig('eval'), subcommand(about), [], '2.0.0');
  assertMatch(generated, /\/\/\/ <summary>Options for <c>deno eval<\/c>\. Evaluate JavaScript from the command line\.<\/summary>/);
  assertStrictEquals(generated.includes(''), false);
  assertStrictEquals(generated.includes(''), false);
});

Deno.test('generateOptionsClass: falls back to plain summary when about is null', () => {
  const generated = generateOptionsClass(commandConfig('eval'), subcommand(null), [], '2.0.0');
  assertMatch(generated, /\/\/\/ <summary>Options for <c>deno eval<\/c>\.<\/summary>/);
});
