#!/usr/bin/env node
// Symbolizes thread-sampler RIP output against the linker map.
//
// Usage:
//   node tools/symbolize-rip.js <samples.csv> <mx_alive.map>
//        [--module-map <moduleName>=<mapPath>]...
//        [--from <ms>] [--to <ms>] [--tid <id>] [--top <n>]
//
// The positional map is used for mx_alive.exe. Additional maps can be given for
// SDK DLLs (rexgpu-xenos.dll, rexruntime.dll) via --module-map.

const fs = require('fs');

const args = process.argv.slice(2);
if (args.length < 2) {
  console.error('usage: node tools/symbolize-rip.js <samples.csv> <mx_alive.map> [--module-map name=path] [--from ms] [--to ms] [--tid id] [--top n]');
  process.exit(1);
}
const samplesPath = args[0];
let fromMs = -Infinity;
let toMs = Infinity;
let onlyTid = null;
let topN = 25;
let groupBy = 'leaf';
let leafModule = null;
const mapSpecs = [{ module: 'mx_alive.exe', path: args[1] }];
for (let i = 2; i < args.length; i += 2) {
  const flag = args[i];
  const value = args[i + 1];
  if (flag === '--from') fromMs = Number(value);
  else if (flag === '--to') toMs = Number(value);
  else if (flag === '--tid') onlyTid = Number(value);
  else if (flag === '--top') topN = Number(value);
  else if (flag === '--group') groupBy = value;
  else if (flag === '--leaf-module') leafModule = value.toLowerCase();
  else if (flag === '--module-map') {
    const eq = value.indexOf('=');
    mapSpecs.push({ module: value.slice(0, eq), path: value.slice(eq + 1) });
  } else { console.error(`unknown flag ${flag}`); process.exit(1); }
}

// --- Parse maps. ---
const symbolRe = /^\s+[0-9A-Fa-f]{4}:[0-9A-Fa-f]{8}\s+(\S+)\s+([0-9A-Fa-f]{16})\s+(\S+)\s*$/;
const maps = new Map();
for (const spec of mapSpecs) {
  const mapLines = fs.readFileSync(spec.path, 'utf8').split(/\r?\n/);
  const preferredBaseLine = mapLines.find((l) => l.includes('Preferred load address is'));
  const preferredBase = preferredBaseLine
    ? BigInt('0x' + preferredBaseLine.trim().split(/\s+/).pop())
    : 0x140000000n;
  const symbols = [];
  let inPublics = false;
  for (const line of mapLines) {
    if (line.includes('Publics by Value')) { inPublics = true; continue; }
    if (!inPublics) continue;
    const m = symbolRe.exec(line);
    if (!m) continue;
    const addr = BigInt('0x' + m[2]);
    if (addr < preferredBase) continue;
    symbols.push({ rva: addr - preferredBase, name: m[1], object: m[3] });
  }
  symbols.sort((a, b) => (a.rva < b.rva ? -1 : a.rva > b.rva ? 1 : 0));
  if (symbols.length === 0) {
    console.error(`No symbols parsed from map ${spec.path}`);
    process.exit(1);
  }
  maps.set(spec.module.toLowerCase(), { symbols });
}

function findSymbol(entry, rva) {
  const { symbols } = entry;
  let lo = 0, hi = symbols.length - 1, best = -1;
  while (lo <= hi) {
    const mid = (lo + hi) >> 1;
    if (symbols[mid].rva <= rva) { best = mid; lo = mid + 1; } else { hi = mid - 1; }
  }
  if (best < 0) return null;
  return symbols[best];
}

// --- Parse samples. ---
const lines = fs.readFileSync(samplesPath, 'utf8').split(/\r?\n/);
let startLocal = null;
const modules = [];
const samples = [];
for (const line of lines) {
  if (line.startsWith('# start_local=')) { startLocal = line.slice('# start_local='.length); continue; }
  if (line.startsWith('# module=')) {
    const [name, base, size] = line.slice('# module='.length).split(',');
    modules.push({ name, base: BigInt(base), size: BigInt(size) });
    continue;
  }
  if (!line || line.startsWith('time_ms') || line.startsWith('#')) continue;
  const [time, tid, rip, ra] = line.split(',');
  const t = Number(time), d = Number(tid), r = BigInt(rip);
  const caller = ra ? BigInt(ra) : 0n;
  if (t >= fromMs && t <= toMs && (onlyTid === null || d === onlyTid)) {
    if (leafModule) {
      const leaf = moduleFor(r);
      if (!leaf || !leaf.name.toLowerCase().includes(leafModule)) continue;
    }
    samples.push({ time: t, tid: d, rip: r, ra: caller });
  }
}
if (samples.length === 0) {
  console.error('No samples in range');
  process.exit(1);
}

function moduleFor(rip) {
  for (const m of modules) {
    if (rip >= m.base && rip < m.base + m.size) return m;
  }
  return null;
}

function resolve(sample) {
  const module = moduleFor(sample.rip);
  if (!module) return { module: '?', name: `0x${sample.rip.toString(16)}` };
  const entry = maps.get(module.name.toLowerCase());
  if (!entry) return { module: module.name, name: module.name };
  const rva = sample.rip - module.base;
  const symbol = findSymbol(entry, rva);
  return {
    module: module.name,
    name: symbol ? symbol.name : `<gap rva=0x${rva.toString(16)}>`,
  };
}

const byTid = new Map();
for (const sample of samples) {
  if (!byTid.has(sample.tid)) byTid.set(sample.tid, []);
  const target = groupBy === 'caller' ? { rip: sample.ra || sample.rip } : sample;
  byTid.get(sample.tid).push(resolve(target));
}

const tidTotals = [...byTid.entries()].map(([tid, list]) => ({ tid, n: list.length }))
  .sort((a, b) => b.n - a.n);

console.log(`samples=${samples.length} start=${startLocal} from=${fromMs} to=${toMs}`);
for (const { tid, n } of tidTotals) {
  const list = byTid.get(tid);
  const counts = new Map();
  for (const r of list) {
    const key = `${r.module}\t${r.name}`;
    counts.set(key, (counts.get(key) || 0) + 1);
  }
  const ranked = [...counts.entries()].sort((a, b) => b[1] - a[1]).slice(0, topN);
  console.log(`\n== tid ${tid}: ${n} samples`);
  for (const [key, count] of ranked) {
    const [module, name] = key.split('\t');
    const pct = ((count / n) * 100).toFixed(1);
    console.log(`  ${String(count).padStart(5)} ${pct.padStart(5)}%  ${module}  ${name}`);
  }
}
