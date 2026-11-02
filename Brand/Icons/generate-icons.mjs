import { Resvg } from '@resvg/resvg-js';
import { readFileSync, writeFileSync, readdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = dirname(fileURLToPath(import.meta.url));
const srcDir = join(root, 'src');
const sizes = [32, 16];

for (const file of readdirSync(srcDir).filter(f => f.endsWith('.svg')).sort()) {
  const name = file.slice(0, -4);
  const svg = readFileSync(join(srcDir, file));

  for (const size of sizes) {
    const resvg = new Resvg(svg, {
      fitTo: { mode: 'width', value: size }
    });
    const png = resvg.render().asPng();
    const output = join(root, `${name}_${size}.png`);
    writeFileSync(output, png);
    console.log(`  ${name}_${size}.png`);
  }
}

console.log('Command icons generated.');
