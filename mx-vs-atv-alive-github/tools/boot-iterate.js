#!/usr/bin/env node
// Iteratively register guest functions that the game calls indirectly but the
// analyzer could not discover. Each iteration launches the build, reads the
// latest log for an unregistered-function fatal, and extends the manifest with
// a function spanning the gap up to the next known function.

const fs = require('fs');
const path = require('path');
const { spawn, spawnSync } = require('child_process');

const ROOT = path.resolve(__dirname, '..');
const MANIFEST = path.join(ROOT, 'mx_alive_manifest.toml');
const PARTITION = path.join(ROOT, 'generated', 'default', 'codegen.partition.json');
const REXGLUE = path.join(ROOT, 'third_party', 'rexglue-sdk', 'out', 'install',
  'win-amd64', 'bin', 'rexglue.exe');
const BUILD_DIR = path.join(ROOT, 'out', 'build', 'win-amd64-release');
const EXE = path.join(BUILD_DIR, 'mx_alive.exe');
const GAME_ROOT = path.join(ROOT, 'Game', 'MX vs. ATV Alive (USA Europe)');

const MAX_ITER = Number(process.env.MAX_ITER || 30);
const RUN_SECONDS = Number(process.env.RUN_SECONDS || 14);

function manifestText() {
  return fs.readFileSync(MANIFEST, 'utf8');
}

function knownFunctions(text) {
  return new Set([...text.matchAll(/\[entrypoint\.functions\."0x([0-9A-Fa-f]+)"\]/g)]
    .map(m => parseInt(m[1], 16)));
}

function nextKnownFunction(addr) {
  const data = JSON.parse(fs.readFileSync(PARTITION, 'utf8'));
  const keys = Object.keys(data.assignments || {})
    .map(k => parseInt(k, 16))
    .filter(Number.isFinite)
    .sort((a, b) => a - b);
  return keys.find(k => k > addr) ?? addr + 0x100;
}

function addFunction(addr, end) {
  let text = manifestText();
  if (!text.endsWith('\n')) text += '\n';
  const hex = n => '0x' + n.toString(16).toUpperCase();
  text += `\n[entrypoint.functions."${hex(addr)}"]\nend = ${hex(end)}\n`;
  fs.writeFileSync(MANIFEST, text);
}

function newestLog() {
  const dir = path.join(BUILD_DIR, 'logs');
  const files = fs.readdirSync(dir)
    .filter(f => f.endsWith('.log'))
    .map(f => ({ f, t: fs.statSync(path.join(dir, f)).mtimeMs }))
    .sort((a, b) => b.t - a.t);
  return files.length ? path.join(dir, files[0].f) : null;
}

function runCodegen() {
  const r = spawnSync(REXGLUE, ['codegen', '--ignore-stamp', 'mx_alive_manifest.toml'],
    { cwd: ROOT, stdio: 'inherit' });
  return r.status === 0;
}

function build() {
  const r = spawnSync('cmd.exe', ['/d', '/c', 'tools\\build-game.bat'],
    { cwd: ROOT, stdio: 'inherit' });
  return r.status === 0;
}

function launchAndScan() {
  return new Promise(resolve => {
    const child = spawn(EXE, ['--game_data_root', GAME_ROOT, '--log_level', 'info'],
      { cwd: BUILD_DIR, stdio: 'ignore' });
    const timer = setTimeout(() => {
      spawnSync('taskkill.exe', ['/PID', String(child.pid), '/T', '/F'], { stdio: 'ignore' });
    }, RUN_SECONDS * 1000);
    child.on('exit', () => {
      clearTimeout(timer);
      setTimeout(resolve, 700);
    });
  });
}

(async () => {
  const seen = knownFunctions(manifestText());
  for (let i = 1; i <= MAX_ITER; i++) {
    console.log(`--- iteration ${i}/${MAX_ITER} ---`);
    await launchAndScan();

    const log = newestLog();
    if (!log) { console.log('No log produced; stopping.'); return; }
    const text = fs.readFileSync(log, 'utf8');
    const match = text.match(/Call to invalid or unregistered function at guest address 0x([0-9A-Fa-f]+)/);
    if (!match) { console.log('No unregistered-function fatal during run; stopping.'); return; }

    const addr = parseInt(match[1], 16);
    if (seen.has(addr)) {
      console.log(`Fatal address 0x${addr.toString(16)} already registered; stopping.`);
      return;
    }
    const end = nextKnownFunction(addr);
    console.log(`fatal 0x${addr.toString(16).toUpperCase()} -> gap end 0x${end.toString(16).toUpperCase()}`);
    addFunction(addr, end);
    seen.add(addr);

    if (!runCodegen()) { console.log('Codegen failed; stopping.'); return; }
    if (!build()) { console.log('Build failed; stopping.'); return; }
  }
  console.log('Iteration cap reached.');
})();
