#!/usr/bin/env node
// Map a crash RVA (module-relative) from out/mx_alive.map to its nearest
// generated symbol and print the symbol immediately after it.

const fs = require('fs');
const path = require('path');

const MAP = path.join(__dirname, '..', 'out', 'mx_alive.map');
const IMAGE_BASE = 0x140000000n;
const input = process.argv[2];
if (!input) {
  console.error('usage: node tools/symbolize.js <hex-rva>');
  process.exit(1);
}
const target = IMAGE_BASE + BigInt(input.startsWith('0x') ? input : '0x' + input);
const re = /^\s+[0-9A-Fa-f]{4}:[0-9A-Fa-f]{8}\s+(\S+)\s+([0-9A-Fa-f]{16})\s+(.*)$/;

const byAddr = new Map();
for (const line of fs.readFileSync(MAP, 'utf8').split(/\r?\n/)) {
  const m = re.exec(line);
  if (!m) continue;
  const addr = BigInt('0x' + m[2]);
  const name = m[1];
  const obj = m[3];
  const prev = byAddr.get(addr.toString());
  if (!prev || (prev.name.startsWith('__imp__') && !name.startsWith('__imp__'))) {
    byAddr.set(addr.toString(), { name, addr, obj });
  }
}

const entries = [...byAddr.values()].sort((a, b) => (a.addr < b.addr ? -1 : a.addr > b.addr ? 1 : 0));
let best = null;
let next = null;
for (const e of entries) {
  if (e.addr <= target) best = e;
  else { next = e; break; }
}

if (!best) {
  console.log('no symbol at or below 0x' + target.toString(16));
  process.exit(2);
}
const delta = target - best.addr;
console.log(`target  RVA 0x${(target - IMAGE_BASE).toString(16).toUpperCase()} (VA 0x${target.toString(16).toUpperCase()})`);
console.log(`symbol  ${best.name} + 0x${delta.toString(16).toUpperCase()}`);
console.log(`object  ${best.obj}`);
if (next) {
  console.log(`next    ${next.name} at +0x${(next.addr - best.addr).toString(16).toUpperCase()} (size bound)`);
}
