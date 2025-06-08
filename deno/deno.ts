import * as esbuild  from 'esbuild';

const result = await esbuild.build({
  entryPoints: ['../example/devices.header.template.js'],
  outfile: '../example/devices.header.template.dist.js',
  minify: true,
  target: ['es2023'],
});

console.log("Build done:", result);

esbuild.stop(); // Wichtig: esbuild Prozess sauber beenden