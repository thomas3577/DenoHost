import * as esbuild from 'esbuild';
import { parseArgs } from '@std/cli';

const args = parseArgs(Deno.args, {
  string: ['src'],
  alias: {
    s: 'src',
  },
});

if (!args.src) {
  console.error('Error: Source file is required. Use --src or -s to specify the source file.');
  Deno.exit(1);
}

const entryPoints = [args.src];
const outfile = args?.src?.replace(/\.js$/, '.dist.js');

const result = await esbuild.build({
  minify: true,
  target: ['es2023'],
  entryPoints,
  outfile,
});

console.log('Build done:', result);

esbuild.stop();
