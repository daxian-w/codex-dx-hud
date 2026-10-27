// Read-only capability probe. Prints only counts, enum states and error codes.
// Never starts/resumes/interrupts a thread and never prints protocol payloads.
const { spawn } = require('node:child_process');
const { createInterface } = require('node:readline');
const { createHash } = require('node:crypto');

async function main() {
  const proxy = process.argv.includes('--proxy');
  const threadIndex = process.argv.indexOf('--thread');
  const threadId = threadIndex >= 0 ? process.argv[threadIndex + 1] : null;
  const child = spawn('codex.exe', ['app-server', ...(proxy ? ['proxy'] : [])], {
    windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'],
  });
  const pending = new Map();
  let sequence = 0;
  let stderrBytes = 0;
  let notificationCount = 0;
  child.stderr.on('data', data => { stderrBytes += data.length; });
  const lines = createInterface({ input: child.stdout });
  lines.on('line', line => {
    try {
      const message = JSON.parse(line);
      if (message.id != null && pending.has(message.id)) {
        const request = pending.get(message.id);
        pending.delete(message.id);
        clearTimeout(request.timeout);
        request.resolve(message);
      } else if (message.method) { notificationCount++; }
    } catch { /* Ignore non-protocol output; never print it. */ }
  });
  child.on('error', () => { /* Request timeout reports an unavailable source. */ });
  function rpc(method, params) {
    return new Promise(resolve => {
      const id = ++sequence;
      const timeout = setTimeout(() => {
        pending.delete(id);
        resolve({ error: { code: 'timeout' } });
      }, 8000);
      pending.set(id, { resolve, timeout });
      if (!child.stdin.destroyed) child.stdin.write(JSON.stringify({ id, method, params }) + '\n');
    });
  }
  const report = { mode: proxy ? 'existing-daemon-proxy' : 'isolated-stdio',
    startedAt: new Date().toISOString(), readOnlyMethods: [] };
  try {
    const init = await rpc('initialize', {
      clientInfo: { name: 'codex-rate-monitor-capability-probe', title: 'Read-only capability probe', version: '0.3.2' },
      capabilities: { experimentalApi: true },
    });
    report.initialize = init.error ? { errorCode: init.error.code } : { success: true };
    if (init.error) return report;
    child.stdin.write(JSON.stringify({ method: 'initialized', params: {} }) + '\n');
    const loaded = await rpc('thread/loaded/list', { limit: 100 });
    report.readOnlyMethods.push('thread/loaded/list');
    report.loaded = loaded.error ? { errorCode: loaded.error.code } : {
      count: loaded.result?.data?.length ?? null,
      hasNextPage: Boolean(loaded.result?.nextCursor),
      containsTarget: threadId ? loaded.result?.data?.includes(threadId) : null,
    };
    if (threadId) {
      const read = await rpc('thread/read', { threadId, includeTurns: false });
      report.readOnlyMethods.push('thread/read');
      report.target = read.error ? { errorCode: read.error.code } : {
        fingerprint: createHash('sha256').update(threadId).digest('hex').slice(0, 12),
        status: read.result?.thread?.status?.type ?? 'missing',
        activeFlags: read.result?.thread?.status?.activeFlags ?? [],
      };
    }
    report.notificationCount = notificationCount;
    return report;
  } finally {
    child.stdin.end();
    lines.close();
    if (child.exitCode === null) child.kill(); // Only the probe-owned process.
    report.stderrBytes = stderrBytes;
  }
}
main().then(report => process.stdout.write(JSON.stringify(report, null, 2) + '\n'))
  .catch(error => { process.stderr.write('Probe failed: ' + error.name + '\n'); process.exitCode = 1; });
