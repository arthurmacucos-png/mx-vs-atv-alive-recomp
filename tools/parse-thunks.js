#!/usr/bin/env node
// Parse the guest-code dump emitted by mx_alive_dump_thunk_region() and emit
// manifest [entrypoint.functions] blocks for every recognizable thunk.

const fs = require('fs');

const logPath = process.argv[2];
if (!logPath) {
  console.error('usage: node tools/parse-thunks.js <log-file>');
  process.exit(1);
}

const words = new Map();
const lineRe = /([0-9A-F]{8}): ([0-9A-F]{8})\s*$/;
for (const line of fs.readFileSync(logPath, 'utf8').split(/\r?\n/)) {
  const m = lineRe.exec(line);
  if (!m) continue;
  const addr = parseInt(m[1], 16);
  const value = parseInt(m[2], 16);
  if (addr >= 0x82000000 && addr < 0x83200000) words.set(addr, value);
}
if (words.size === 0) {
  console.error('no dump words found');
  process.exit(2);
}

const addrs = [...words.keys()].sort((a, b) => a - b);
const thunks = [];
for (const a of addrs) {
  const w0 = words.get(a);
  const w1 = words.get(a + 4);
  const w2 = words.get(a + 8);
  const w3 = words.get(a + 12);
  if (
    w0 === 0x81830000 &&
    w1 !== undefined && (w1 >>> 16) === 0x816c &&
    w2 === 0x7d6903a6 &&
    w3 === 0x4e800420
  ) {
    thunks.push({ start: a, end: a + 16, kind: 'virtual' });
  } else if (
    (w0 >>> 16) === 0x3863 &&
    w1 !== undefined && (w1 >>> 26) === 0x12  // 0x48 branch opcode
  ) {
    thunks.push({ start: a, end: a + 8, kind: 'adjustor' });
  }
}

const hex = n => '0x' + n.toString(16).toUpperCase();
console.log(`# dump: ${hex(addrs[0])}..${hex(addrs[addrs.length - 1])}, ${words.size} words`);
console.log(`# detected ${thunks.length} thunks`);

let overlap = false;
for (let i = 1; i < thunks.length; i++) {
  if (thunks[i].start < thunks[i - 1].end) {
    console.error(`overlap: ${hex(thunks[i - 1].start)}..${hex(thunks[i - 1].end)} and ${hex(thunks[i].start)}`);
    overlap = true;
  }
}
if (overlap) process.exit(3);

for (const t of thunks) {
  console.log(`\n[entrypoint.functions."${hex(t.start)}"]\nend = ${hex(t.end)} # ${t.kind}`);
}
