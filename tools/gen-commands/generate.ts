// Generates C# options classes and Deno method overloads from `deno json_reference` + the Deno JSON schema.
// Run: deno task generate

import { join, dirname } from '@std/path';

const SCRIPT_DIR = dirname(import.meta.filename!);
const REPO_ROOT = join(SCRIPT_DIR, '..', '..');
const OUTPUT_DIR = join(REPO_ROOT, 'DenoHost.Core', 'Commands', 'Generated');
const SNAPSHOT_FILE = join(SCRIPT_DIR, 'deno_reference.snapshot.json');

const OFFLINE = Deno.args.includes('--offline');

// ─── Types ────────────────────────────────────────────────────────────────────

interface DenoArg {
  name: string;
  short: string | null;
  long: string | null;
  required: boolean;
  help: string | null;
  help_heading: string | null;
  usage: string;
}

interface DenoSubcommand {
  name: string;
  about: string | null;
  args: DenoArg[];
}

interface DenoReference {
  name: string;
  about: string | null;
  args: DenoArg[];
  subcommands: DenoSubcommand[];
}

interface PermissionType {
  name: string;
  hasIgnore: boolean;
}

// ─── Configuration ────────────────────────────────────────────────────────────

// Flags always skipped (handled by DenoHost or not useful for embedding)
const SKIP_FLAGS = new Set([
  'config', 'no-config',  // managed by DenoExecuteOptions / DenoExecuteBaseOptions
  'help',                  // meta
  'inspect', 'inspect-brk', 'inspect-wait', 'inspect-publish-uid',
  'tunnel',                // Deno Deploy specific
]);

// Commands to generate, with positional arg handling
interface CommandConfig {
  name: string;
  // Each positional: csParam = C# parameter declaration, append = statement added to args list
  positional: Array<{ csParam: string; append: string }>;
  hasPermissions: boolean;
}

const COMMANDS: CommandConfig[] = [
  { name: 'run', positional: [{ csParam: 'string script', append: 'args.Add(script);' }], hasPermissions: true },
  { name: 'eval', positional: [{ csParam: 'string code', append: 'args.Add(code);' }], hasPermissions: true },
  { name: 'test', positional: [{ csParam: 'string[]? files = null', append: 'if (files != null) args.AddRange(files);' }], hasPermissions: true },
  { name: 'bench', positional: [{ csParam: 'string[]? files = null', append: 'if (files != null) args.AddRange(files);' }], hasPermissions: true },
  { name: 'fmt', positional: [{ csParam: 'string[]? files = null', append: 'if (files != null) args.AddRange(files);' }], hasPermissions: false },
  { name: 'lint', positional: [{ csParam: 'string[]? files = null', append: 'if (files != null) args.AddRange(files);' }], hasPermissions: false },
  { name: 'check', positional: [{ csParam: 'string[]? files = null', append: 'if (files != null) args.AddRange(files);' }], hasPermissions: false },
  { name: 'compile', positional: [{ csParam: 'string script', append: 'args.Add(script);' }], hasPermissions: true },
  { name: 'task', positional: [{ csParam: 'string taskName', append: 'args.Add(taskName);' }], hasPermissions: false },
  { name: 'serve', positional: [{ csParam: 'string script', append: 'args.Add(script);' }], hasPermissions: true },
  { name: 'cache', positional: [{ csParam: 'string[] files', append: 'args.AddRange(files);' }], hasPermissions: false },
  { name: 'add', positional: [{ csParam: 'string[] packages', append: 'args.AddRange(packages);' }], hasPermissions: false },
  { name: 'remove', positional: [{ csParam: 'string[] packages', append: 'args.AddRange(packages);' }], hasPermissions: false },
];

// ─── Permission derivation from JSON schema ───────────────────────────────────

async function fetchPermissionTypes(denoVersion: string): Promise<PermissionType[]> {
  const schemaUrl = `https://raw.githubusercontent.com/denoland/deno/v${denoVersion}/cli/schemas/config-file.v1.json`;
  console.log(`Fetching schema: ${schemaUrl}`);
  const response = await fetch(schemaUrl);
  if (!response.ok) throw new Error(`Failed to fetch schema: ${response.status} ${response.statusText}`);
  // deno-lint-ignore no-explicit-any
  const schema: any = await response.json();

  const defs = schema.$defs ?? {};
  const permissionSetProps = defs.permissionSet?.properties ?? {};

  return Object.keys(permissionSetProps)
    .filter((k) => k !== 'all')
    .map((k) => {
      // deno-lint-ignore no-explicit-any
      const findRef = (obj: any): string => {
        if (!obj) return '';
        if (obj.$ref) return obj.$ref;
        if (obj.anyOf) return obj.anyOf.map(findRef).join('|');
        if (obj.oneOf) return obj.oneOf.map(findRef).join('|');
        return '';
      };
      const ref = findRef(permissionSetProps[k]);
      return { name: k, hasIgnore: ref.includes('allowDenyIgnore') };
    });
}

function buildPermissionSupplement(permTypes: PermissionType[]): DenoArg[] {
  const args: DenoArg[] = [];

  // --allow-all / -A
  args.push({
    name: 'allow-all', short: 'A', long: 'allow-all', required: false,
    help: 'Allow all permissions.',
    help_heading: 'Permissions', usage: '--allow-all',
  });

  for (const perm of permTypes) {
    const n = perm.name;
    // Friendly names for usage hints
    const hint = n === 'net' ? 'HOST' : n === 'env' ? 'VAR' : n === 'sys' ? 'API' : n === 'run' ? 'PROGRAM' : 'PATH';
    args.push({
      name: `allow-${n}`, short: null, long: `allow-${n}`, required: false,
      help: `Allow ${n} access. Empty array = allow all.`,
      help_heading: 'Permissions', usage: `--allow-${n}[=<${hint}>...]`,
    });
    args.push({
      name: `deny-${n}`, short: null, long: `deny-${n}`, required: false,
      help: `Deny ${n} access.`,
      help_heading: 'Permissions', usage: `--deny-${n}[=<${hint}>...]`,
    });
    if (perm.hasIgnore) {
      args.push({
        name: `ignore-${n}`, short: null, long: `ignore-${n}`, required: false,
        help: `Ignore ${n} permission check.`,
        help_heading: 'Permissions', usage: `--ignore-${n}[=<${hint}>...]`,
      });
    }
  }

  // --no-prompt
  args.push({
    name: 'no-prompt', short: null, long: 'no-prompt', required: false,
    help: 'Always throw if required permission was not passed.',
    help_heading: 'Permissions', usage: '--no-prompt',
  });

  return args;
}

// Fallback when --offline: hardcoded based on Deno 2.x stable permission set
function builtinPermissionTypes(): PermissionType[] {
  return [
    { name: 'read', hasIgnore: true },
    { name: 'write', hasIgnore: false },
    { name: 'import', hasIgnore: false },
    { name: 'env', hasIgnore: true },
    { name: 'net', hasIgnore: false },
    { name: 'run', hasIgnore: false },
    { name: 'ffi', hasIgnore: false },
    { name: 'sys', hasIgnore: false },
  ];
}

// ─── Type inference ────────────────────────────────────────────────────────────

type ArgStyle = 'flag' | 'boolopt' | 'value' | 'intvalue' | 'longvalue' | 'optvalue' | 'array' | 'optarray';

interface Property {
  csName: string;
  csType: string;
  argStyle: ArgStyle;
  flagName: string;
  xmlDoc: string;
  heading: string;
}

export function toPascalCase(s: string): string {
  return s.split('-').map((w) => w.charAt(0).toUpperCase() + w.slice(1)).join('');
}

export function escapeXml(s: string): string {
  return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

export function inferProperty(arg: DenoArg): Property | null {
  if (!arg.long) return null;
  const usage = arg.usage;
  const upper = usage.toUpperCase();

  let csType: string;
  let argStyle: ArgStyle;

  if (upper.includes('BOOLEAN')) {
    csType = 'bool?';
    argStyle = 'boolopt';
  } else if (!usage.includes('<') && !usage.includes('[=')) {
    csType = 'bool?';
    argStyle = 'flag';
  } else if (upper.includes('MICROSECOND')) {
    csType = 'long?';
    argStyle = 'longvalue';
  } else if (upper.includes('NUMBER') || upper.includes('PERCENT') || upper.includes('INDEX/COUNT')) {
    csType = 'int?';
    argStyle = 'intvalue';
  } else if (usage.includes('...')) {
    csType = 'string[]?';
    argStyle = /\[=?</.test(usage) ? 'optarray' : 'array';
  } else if (/\[=?</.test(usage)) {
    csType = 'string?';
    argStyle = 'optvalue';
  } else {
    csType = 'string?';
    argStyle = 'value';
  }

  return {
    csName: toPascalCase(arg.long),
    csType,
    argStyle,
    flagName: `--${arg.long}`,
    xmlDoc: arg.help ? escapeXml(arg.help.split('\n')[0]) : '',
    heading: arg.help_heading ?? 'General',
  };
}

// ─── C# code generation ───────────────────────────────────────────────────────

function renderProperty(prop: Property): string {
  const lines: string[] = [];
  if (prop.xmlDoc) lines.push(`  /// <summary>${prop.xmlDoc}</summary>`);
  lines.push(`  public ${prop.csType} ${prop.csName} { get; set; }`);
  return lines.join('\n');
}

export function renderToArgsLine(prop: Property): string {
  const n = prop.csName;
  const f = prop.flagName;
  switch (prop.argStyle) {
    case 'flag': return `    if (${n} == true) args.Add("${f}");`;
    case 'boolopt': return `    if (${n}.HasValue) args.Add(${n}.Value ? "${f}" : "${f}=false");`;
    case 'value': return `    if (${n} is not null) { args.Add("${f}"); args.Add(${n}); }`;
    case 'intvalue':
    case 'longvalue': return `    if (${n}.HasValue) { args.Add("${f}"); args.Add(${n}.Value.ToString()); }`;
    case 'optvalue': return `    if (${n} is not null) { if (${n}.Length == 0) args.Add("${f}"); else args.Add(string.Concat("${f}=", ${n})); }`;
    case 'array': return `    if (${n} is { Length: > 0 }) { args.Add("${f}"); args.Add(string.Join(",", ${n})); }`;
    case 'optarray': return `    if (${n} is not null) { if (${n}.Length == 0) args.Add("${f}"); else { args.Add("${f}"); args.Add(string.Join(",", ${n})); } }`;
  }
}

function generateOptionsClass(
  cmd: CommandConfig,
  subcmd: DenoSubcommand,
  permSupplement: DenoArg[],
  denoVersion: string,
): string {
  const className = `${toPascalCase(cmd.name)}Options`;

  // Build full arg list: permissions first, then json_reference args
  const sourceArgs: DenoArg[] = [
    ...(cmd.hasPermissions ? permSupplement : []),
    ...subcmd.args,
  ];

  // Filter: skip excluded flags and positionals (usage not starting with -)
  const filtered = sourceArgs.filter((a) => {
    if (!a.long) return false;
    if (SKIP_FLAGS.has(a.long)) return false;
    if (!a.usage.startsWith('-')) return false;
    return true;
  });

  // Map to properties and group by heading
  const groups = new Map<string, Property[]>();
  const allProps: Property[] = [];

  for (const arg of filtered) {
    const prop = inferProperty(arg);
    if (!prop) continue;
    if (!groups.has(prop.heading)) groups.set(prop.heading, []);
    groups.get(prop.heading)!.push(prop);
    allProps.push(prop);
  }

  const lines: string[] = [];
  lines.push('// <auto-generated/>');
  lines.push(`// Generated by tools/gen-commands/generate.ts`);
  lines.push(`// Sources: \`deno json_reference\` + Deno JSON schema (Deno ${denoVersion})`);
  lines.push('// Do not edit manually — run `deno task generate` in tools/gen-commands/ to regenerate.');
  lines.push('#nullable enable');
  lines.push('');
  lines.push('using System.Collections.Generic;');
  lines.push('');
  lines.push('namespace DenoHost.Core.Commands;');
  lines.push('');

  const firstLine = subcmd.about?.split('\n')[0].replace(/\s*\[[^m]*m/g, '').trim() ?? '';
  const summary = firstLine
    ? `Options for <c>deno ${cmd.name}</c>. ${escapeXml(firstLine)}`
    : `Options for <c>deno ${cmd.name}</c>.`;
  lines.push(`/// <summary>${summary}</summary>`);
  lines.push(`public sealed class ${className}`);
  lines.push('{');

  for (const [heading, props] of groups) {
    lines.push(`  #region ${heading}`);
    lines.push('');
    for (const prop of props) {
      lines.push(renderProperty(prop));
      lines.push('');
    }
    lines.push('  #endregion');
    lines.push('');
  }

  lines.push('  internal string[] ToArgs()');
  lines.push('  {');
  lines.push('    var args = new List<string>();');
  for (const prop of allProps) {
    lines.push(renderToArgsLine(prop));
  }
  lines.push('    return [.. args];');
  lines.push('  }');
  lines.push('}');

  return lines.join('\n');
}

function generateDenoCommandsPartial(ref: DenoReference, denoVersion: string): string {
  const lines: string[] = [];
  lines.push('// <auto-generated/>');
  lines.push(`// Generated by tools/gen-commands/generate.ts`);
  lines.push(`// Source: \`deno json_reference\` (Deno ${denoVersion})`);
  lines.push('// Do not edit manually — run `deno task generate` in tools/gen-commands/ to regenerate.');
  lines.push('#nullable enable');
  lines.push('');
  lines.push('using System;');
  lines.push('using System.Collections.Generic;');
  lines.push('using System.Threading;');
  lines.push('using System.Threading.Tasks;');
  lines.push('using DenoHost.Core.Commands;');
  lines.push('');
  lines.push('namespace DenoHost.Core;');
  lines.push('');
  lines.push('public static partial class Deno');
  lines.push('{');

  for (const cmd of COMMANDS) {
    const subcmd = ref.subcommands.find((s) => s.name === cmd.name);
    if (!subcmd) continue;

    const methodName = toPascalCase(cmd.name);
    const optClass = `${methodName}Options`;

    // Build parameter list
    const positionalParams = cmd.positional.map((p) => p.csParam).join(', ');
    const allParamStr = [positionalParams, `${optClass}? options = null`, 'DenoExecuteBaseOptions? baseOptions = null', 'CancellationToken cancellationToken = default']
      .filter(Boolean)
      .join(', ');

    // Non-null checks for required positional params
    const nullChecks = cmd.positional
      .filter((p) => !p.csParam.includes('?') && !p.csParam.includes('[]?'))
      .map((p) => {
        const varName = p.csParam.split(' ').at(-1)!;
        return p.csParam.startsWith('string[]')
          ? `    ArgumentNullException.ThrowIfNull(${varName});`
          : `    ArgumentException.ThrowIfNullOrWhiteSpace(${varName});`;
      });

    const positionalAppends = cmd.positional.map((p) => `    ${p.append}`).join('\n');

    // Arg names for the non-generic → generic delegation call
    // Strip default value (e.g. "string[]? files = null" → "files")
    const positionalArgNames = cmd.positional.map((p) => p.csParam.replace(/\s*=.*$/, '').split(' ').at(-1)!.trim());
    const delegateArgs = [...positionalArgNames, 'options', 'baseOptions', 'cancellationToken'].join(', ');

    lines.push(`  /// <summary>Executes <c>deno ${cmd.name}</c>.</summary>`);
    lines.push(`  public static Task ${methodName}(${allParamStr})`);
    lines.push(`    => ${methodName}<string>(${delegateArgs});`);
    lines.push('');
    lines.push(`  /// <summary>Executes <c>deno ${cmd.name}</c> and deserializes stdout as <typeparamref name="T"/>.</summary>`);
    lines.push(`  public static Task<T> ${methodName}<T>(${allParamStr})`);
    lines.push('  {');
    if (nullChecks.length > 0) lines.push(nullChecks.join('\n'));
    lines.push('    var args = new List<string>();');
    lines.push('    if (options != null) args.AddRange(options.ToArgs());');
    lines.push(positionalAppends);
    lines.push(`    return ExecuteCore<T>("${cmd.name}", [.. args], baseOptions, null, null, cancellationToken);`);
    lines.push('  }');
    lines.push('');
  }

  lines.push('}');
  return lines.join('\n');
}

// Commands for which DenoProcess factory methods are generated (long-running only)
const PROCESS_COMMAND_NAMES = new Set(['run', 'serve', 'task']);

function generateDenoProcessCommandsPartial(denoVersion: string): string {
  const processCommands = COMMANDS.filter((c) => PROCESS_COMMAND_NAMES.has(c.name));

  const lines: string[] = [];
  lines.push('// <auto-generated/>');
  lines.push('// Generated by tools/gen-commands/generate.ts');
  lines.push(`// Source: \`deno json_reference\` (Deno ${denoVersion})`);
  lines.push('// Do not edit manually — run `deno task generate` in tools/gen-commands/ to regenerate.');
  lines.push('#nullable enable');
  lines.push('');
  lines.push('using System;');
  lines.push('using System.Collections.Generic;');
  lines.push('using DenoHost.Core.Commands;');
  lines.push('');
  lines.push('namespace DenoHost.Core;');
  lines.push('');
  lines.push('public partial class DenoProcess');
  lines.push('{');

  for (const cmd of processCommands) {
    const methodName = toPascalCase(cmd.name);
    const optClass = `${methodName}Options`;

    const positionalParams = cmd.positional.map((p) => p.csParam).join(', ');
    const allParamStr = [positionalParams, `${optClass}? options = null`, 'DenoExecuteBaseOptions? baseOptions = null']
      .filter(Boolean)
      .join(', ');

    const nullChecks = cmd.positional
      .filter((p) => !p.csParam.includes('?') && !p.csParam.includes('[]?'))
      .map((p) => {
        const varName = p.csParam.split(' ').at(-1)!;
        return p.csParam.startsWith('string[]')
          ? `    ArgumentNullException.ThrowIfNull(${varName});`
          : `    ArgumentException.ThrowIfNullOrWhiteSpace(${varName});`;
      });

    const positionalAppends = cmd.positional.map((p) => `    ${p.append}`).join('\n');

    lines.push(`  /// <summary>Creates a <see cref="DenoProcess"/> for <c>deno ${cmd.name}</c>.</summary>`);
    lines.push(`  public static DenoProcess ${methodName}(${allParamStr})`);
    lines.push('  {');
    if (nullChecks.length > 0) lines.push(nullChecks.join('\n'));
    lines.push(`    var args = new List<string> { "${cmd.name}" };`);
    lines.push('    if (options != null) args.AddRange(options.ToArgs());');
    lines.push(positionalAppends);
    lines.push('    return baseOptions != null');
    lines.push('      ? new DenoProcess(baseOptions, [.. args])');
    lines.push('      : new DenoProcess([.. args]);');
    lines.push('  }');
    lines.push('');
  }

  lines.push('}');
  return lines.join('\n');
}

function generateSnapshot(ref: DenoReference): string {
  // Reduced snapshot: only the commands we care about, only flag names
  // Used by DenoCommandsSchemaTests to detect when deno json_reference diverges from what was generated.
  const snapshot: Record<string, string[]> = {};
  for (const cmd of COMMANDS) {
    const subcmd = ref.subcommands.find((s) => s.name === cmd.name);
    if (!subcmd) continue;
    snapshot[cmd.name] = subcmd.args
      .filter((a) => a.long && !SKIP_FLAGS.has(a.long) && a.usage.startsWith('-'))
      .map((a) => a.long!);
  }
  return JSON.stringify({ commands: snapshot }, null, 2);
}

// ─── Main ────────────────────────────────────────────────────────────────────

async function main() {
  // 1. Get deno version
  const versionProc = new Deno.Command('deno', { args: ['--version'], stdout: 'piped' });
  const { stdout: versionOut } = await versionProc.output();
  const denoVersion = new TextDecoder().decode(versionOut).match(/deno (\S+)/)?.[1] ?? 'unknown';
  console.log(`Deno version: ${denoVersion}`);

  // 2. Run deno json_reference
  const refProc = new Deno.Command('deno', { args: ['json_reference'], stdout: 'piped' });
  const { stdout: refOut, success } = await refProc.output();
  if (!success) throw new Error('deno json_reference failed');
  const ref: DenoReference = JSON.parse(new TextDecoder().decode(refOut));

  // 3. Get permission types from JSON schema (or fallback)
  let permTypes: PermissionType[];
  if (OFFLINE) {
    console.log('Offline mode: using built-in permission types.');
    permTypes = builtinPermissionTypes();
  } else {
    permTypes = await fetchPermissionTypes(denoVersion);
  }
  console.log(`Permission types: ${permTypes.map((p) => p.name + (p.hasIgnore ? '+ignore' : '')).join(', ')}`);
  const permSupplement = buildPermissionSupplement(permTypes);

  // 4. Ensure output directory
  await Deno.mkdir(OUTPUT_DIR, { recursive: true });

  // 5. Generate options classes
  for (const cmd of COMMANDS) {
    const subcmd = ref.subcommands.find((s) => s.name === cmd.name);
    if (!subcmd) {
      console.warn(`  Warning: subcommand '${cmd.name}' not found in json_reference — skipping.`);
      continue;
    }
    const className = `${toPascalCase(cmd.name)}Options`;
    const content = generateOptionsClass(cmd, subcmd, permSupplement, denoVersion);
    const outPath = join(OUTPUT_DIR, `${className}.g.cs`);
    await Deno.writeTextFile(outPath, content + '\n');
    console.log(`  Generated ${className}.g.cs`);
  }

  // 6. Generate Deno.Commands.g.cs
  const denoCommandsContent = generateDenoCommandsPartial(ref, denoVersion);
  await Deno.writeTextFile(join(OUTPUT_DIR, 'Deno.Commands.g.cs'), denoCommandsContent + '\n');
  console.log('  Generated Deno.Commands.g.cs');

  // 7. Generate DenoProcess.Commands.g.cs
  const denoProcessCommandsContent = generateDenoProcessCommandsPartial(denoVersion);
  await Deno.writeTextFile(join(OUTPUT_DIR, 'DenoProcess.Commands.g.cs'), denoProcessCommandsContent + '\n');
  console.log('  Generated DenoProcess.Commands.g.cs');

  // 8. Save snapshot (for test validation)
  const snapshotContent = generateSnapshot(ref);
  await Deno.writeTextFile(SNAPSHOT_FILE, snapshotContent + '\n');
  console.log(`  Snapshot saved → ${SNAPSHOT_FILE}`);

  console.log('\nDone! Run `dotnet build` to verify the generated code compiles.');
}

if (import.meta.main) {
  await main();
}
