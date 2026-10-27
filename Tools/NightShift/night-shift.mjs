import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { spawn, execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
export const profiles = Object.freeze({
  'golden-path': 'Subject42GoldenPathTests',
  onboarding: 'Subject42OnboardingTests;Subject42TutorialTests;StartScreenPresentationTests',
  localization: 'ProductionLocalizationTests;ProductionFontTests',
  combat: 'PistolPrecisionTests;Subject42OrbitalOperationsTests',
  events: 'OrbitalRelayStateTests;CorridorGameplayTests;CorridorProductionIntegrationTests;WorldHazardTests',
  rewards: 'Subject42RewardProgressionTests;OrbitUpgradeSelectionTests',
  boss: 'Subject42FinalBossFlowTests', core: 'Subject42GoldenPathTests;Subject42OnboardingTests;Subject42RewardProgressionTests;Subject42FinalBossFlowTests;ProductionLocalizationTests',
  placement: 'RocketStormProductionPlacementTests',
});

export function parseTestXml(xml, expectedFixtures = []) {
  const root = xml.match(/<test-run\s+([^>]+)>/);
  if (!root) return { passed: false, executed: 0, failed: 0, result: 'InvalidOrMissingXml' };
  const attrs = Object.fromEntries([...root[1].matchAll(/([\w-]+)="([^"]*)"/g)].map(m => [m[1], m[2]]));
  const passed = Number(attrs.passed ?? 0), failed = Number(attrs.failed ?? 0);
  const complete = xml.includes('</test-run>') || root[1].endsWith('/');
  const skipped = Number(attrs.skipped ?? 0), inconclusive = Number(attrs.inconclusive ?? 0), total = Number(attrs.total);
  const consistent = [passed, failed, skipped, inconclusive, total].every(n => Number.isSafeInteger(n) && n >= 0) && total === passed + failed + skipped + inconclusive;
  const fixtures = new Set([...xml.matchAll(/<test-suite\s+([^>]+)>/g)].map(m => Object.fromEntries([...m[1].matchAll(/([\w-]+)="([^"]*)"/g)].map(a => [a[1], a[2]]))).filter(a => a.type === 'TestFixture').map(a => a.fullname ?? a.name));
  return { passed: complete && consistent && expectedFixtures.every(f => fixtures.has(f)) && attrs.result === 'Passed' && passed > 0 && failed === 0 && skipped === 0 && inconclusive === 0,
    executed: passed + failed, failed, result: attrs.result ?? 'InvalidXml' };
}

export function readVerification(exitCode, xml, editorLog, expectedFixtures = [], wrapperOutcome = null) {
  if (wrapperOutcome?.completed !== true || wrapperOutcome?.preferencesRestored !== true || wrapperOutcome.unityExitCode !== exitCode)
    throw new Error('Unity wrapper did not complete normal exit and verified restoration');
  if (xml !== null) return parseTestXml(xml, expectedFixtures);
  const compilerDiagnostics = [...new Set(editorLog.split(/\r?\n/)
    .filter(line => /\berror CS\d{4}\b/.test(line)))].slice(0, 24);
  if (exitCode !== 0 && compilerDiagnostics.length && /Scripts have compiler errors|script compilation.*(?:error|fail)/i.test(editorLog))
    return { passed: false, executed: 0, failed: 0, result: 'CompileFailed', compilerDiagnostics };
  throw new Error(`Unity produced no results XML; exit ${exitCode}`);
}

export function nightVerified(rows) { return rows.length > 0 && rows.every(row => row.verification.passed); }

export function findPendingBackups(base) {
  if (!fs.existsSync(base)) return [];
  return fs.readdirSync(base, { withFileTypes: true }).flatMap(entry => {
    const file = path.join(base, entry.name);
    return entry.isDirectory() ? findPendingBackups(file) : entry.name === 'playerprefs-backup.clixml' ? [file] : [];
  });
}

export async function withSessionLock(file, body) {
  const fd = fs.openSync(file, 'wx');
  try { fs.writeFileSync(fd, JSON.stringify({ pid: process.pid, at: new Date().toISOString() })); }
  finally { fs.closeSync(fd); }
  try { return await body(); } finally { fs.unlinkSync(file); }
}

export function workspaceFingerprint(root) {
  const digest = crypto.createHash('sha256');
  const options = { encoding: 'utf8', windowsHide: true, maxBuffer: 128 * 1024 * 1024 };
  // Reports can evolve after verification; executable/source/config changes must invalidate it.
  const exclusions = [':(exclude)NIGHT_REPORT.md', ':(exclude)PLAYTEST_CHECKLIST.md'];
  let hasHead = '';
  try { hasHead = execFileSync('git', ['--no-optional-locks', '-C', root, 'rev-parse', '--verify', '--quiet', 'HEAD'], { ...options, stdio: ['ignore', 'pipe', 'ignore'] }); }
  catch (error) { if (error.status !== 1) throw error; }
  if (hasHead) digest.update(execFileSync('git', ['--no-optional-locks', '-C', root, 'diff', '--no-ext-diff', '--no-textconv', '--binary', 'HEAD', '--', '.', ...exclusions], options));
  const untracked = execFileSync('git', ['--no-optional-locks', '-C', root, 'ls-files', '--others', '--exclude-standard', '-z'], options).split('\0').filter(Boolean).sort();
  for (const file of untracked) {
    if (['NIGHT_REPORT.md', 'PLAYTEST_CHECKLIST.md'].includes(file)) continue;
    digest.update(file + '\0'); digest.update(fs.readFileSync(path.join(root, file)));
  }
  return digest.digest('hex');
}

export function smokeReady(rows) {
  const pair = rows.slice(-2);
  return pair.length === 2 && pair.every(r => r.verification.passed) && pair.some(r => r.decision.testProfile === 'golden-path');
}

export async function cycleLoop({ maxCycles, deadline, stopped, decide, work, verify, save, history = [], stopWhen = () => false }) {
  for (let index = 0; index < maxCycles && Date.now() < deadline && !stopped(); index++) {
    const decision = await decide(history, index);
    if (decision.action === 'stop' || stopped() || Date.now() >= deadline) break;
    const workResult = await work(decision, index);
    if (stopped() || Date.now() >= deadline) {
      history.push({ cycle: index + 1, at: new Date().toISOString(), decision, work: workResult, verification: { passed: false, executed: 0, result: 'NotRun' } });
      await save?.(history); break;
    }
    const verification = await verify(decision, index);
    history.push({ cycle: index + 1, at: new Date().toISOString(), decision, work: workResult, verification });
    await save?.(history);
    if (stopWhen(history)) break;
  }
  return history;
}

function writeJson(file, value) { fs.writeFileSync(file, JSON.stringify(value, null, 2) + '\n'); }
function hash(file) { return fs.existsSync(file) ? crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex') : null; }
function git(root, ...args) { return execFileSync('git', ['--no-optional-locks', '-C', root, ...args], { encoding: 'utf8', windowsHide: true, maxBuffer: 128 * 1024 * 1024 }).trim(); }
// Git status may refresh stat-cache bytes without staging anything.
function indexFingerprint(root) { return crypto.createHash('sha256').update(git(root, 'diff', '--cached', '--raw', '--no-renames', '-z', 'HEAD')).digest('hex'); }

export async function runProcess(executable, args, { cwd, input, prefix, timeoutMs, stopped = () => false }) {
  fs.mkdirSync(path.dirname(prefix), { recursive: true });
  const output = fs.createWriteStream(prefix + '.stdout.log'), errors = fs.createWriteStream(prefix + '.stderr.log');
  const child = spawn(executable, args, { cwd, windowsHide: true, shell: false, env: { ...process.env, GIT_OPTIONAL_LOCKS: '0' }, stdio: ['pipe', 'pipe', 'pipe'] });
  child.stdout.pipe(output); child.stderr.pipe(errors);
  child.stdin.on('error', () => {});
  child.stdin.end(input ?? '');
  const began = Date.now(); let reason = null, terminating = false;
  const kill = () => {
    if (terminating) return; terminating = true;
    if (process.platform === 'win32') {
      const killer = spawn('taskkill.exe', ['/PID', String(child.pid), '/T', '/F'], { windowsHide: true, stdio: 'ignore' });
      killer.on('error', () => child.kill());
    } else child.kill('SIGTERM');
  };
  const timer = setInterval(() => {
    if (Date.now() - began >= timeoutMs || stopped()) { reason = stopped() ? 'STOP requested' : 'process timeout'; kill(); }
  }, 500);
  return await new Promise((resolve, reject) => {
    child.on('error', error => { clearInterval(timer); output.end(); errors.end(); reject(error); });
    child.on('close', code => {
      clearInterval(timer); output.end(); errors.end();
      if (reason) reject(new Error(`${reason}: ${path.basename(executable)}; see ${prefix}`));
      else resolve({ exitCode: code, wallSeconds: (Date.now() - began) / 1000, stdout: prefix + '.stdout.log', stderr: prefix + '.stderr.log' });
    });
  });
}

async function main() {
  const args = process.argv.slice(2);
  const option = (key, fallback) => { const i = args.indexOf('--' + key); return i < 0 ? fallback : args[i + 1]; };
  const mode = option('mode', 'smoke');
  if (!['smoke', 'night'].includes(mode)) throw new Error('Mode must be smoke or night');
  const root = path.resolve(here, '../..');
  const base = path.join(root, 'Artifacts/GeneratedQA/NightShift');
  const unity = option('unity', 'G:/unity/editor/6000.3.13f1/Editor/Unity.exe');
  const hours = Number(option('hours', '7'));
  if (!Number.isFinite(hours) || hours <= 0 || hours > 7) throw new Error('hours must be >0 and <=7');
  fs.mkdirSync(base, { recursive: true });
  const pending = findPendingBackups(base);
  if (pending.length) throw new Error(`Unrestored PlayerPrefs backup; recover before launch: ${pending.join(', ')}`);
  if (fs.existsSync(path.join(base, 'STOP'))) throw new Error('STOP file exists; remove it deliberately before starting');
  if (mode === 'night') {
    const readyPath = path.join(base, 'smoke-ready.json');
    if (!fs.existsSync(readyPath)) throw new Error('Night requires a successful real two-cycle smoke first');
    const ready = JSON.parse(fs.readFileSync(readyPath));
    if (!ready.ready || ready.head !== git(root, 'rev-parse', 'HEAD') || ready.fingerprint !== workspaceFingerprint(root) || Date.now() - Date.parse(ready.at) > 86400000)
      throw new Error('Smoke gate is stale or failed; repeat smoke');
  }
  const lockPath = path.join(base, 'session.lock');
  return await withSessionLock(lockPath, async () => {
  const latestPath = path.join(base, 'latest-session.json');
  const priorSession = fs.existsSync(latestPath) ? JSON.parse(fs.readFileSync(latestPath, 'utf8')) : null;
  const session = path.join(base, new Date().toISOString().replace(/[:.]/g, '-') + '-' + mode);
  fs.mkdirSync(session, { recursive: true });
  writeJson(path.join(base, 'latest-session.json'), { pid: process.pid, mode, session });
  const deadline = Date.now() + hours * 3600000;
  let interrupted = false;
  process.on('SIGINT', () => { interrupted = true; fs.writeFileSync(path.join(base, 'STOP'), 'SIGINT\n'); });
  process.on('SIGTERM', () => { interrupted = true; fs.writeFileSync(path.join(base, 'STOP'), 'SIGTERM\n'); });
  const stopped = () => interrupted || fs.existsSync(path.join(base, 'STOP'));
  const baseline = { head: git(root, 'rev-parse', 'HEAD'), branch: git(root, 'branch', '--show-current'), index: indexFingerprint(root) };
  const controlFiles = fs.readdirSync(here).map(f => path.join(here, f)).concat(path.join(root, 'AGENTS.md'));
  const controls = controlFiles.map(file => [file, hash(file)]);
  function guard() {
    if (baseline.head !== git(root, 'rev-parse', 'HEAD') || baseline.branch !== git(root, 'branch', '--show-current') || baseline.index !== indexFingerprint(root)) throw new Error('Git HEAD/branch/staged entries changed; stopping without rollback');
    if (controls.some(([file, digest]) => hash(file) !== digest)) throw new Error('Orchestrator or AGENTS.md changed during execution');
  }
  const budget = max => Math.max(1, Math.min(max, deadline - Date.now()));
  const policy = fs.readFileSync(path.join(here, 'director-prompt.md'), 'utf8');
  const modeGoal = mode === 'night'
    ? 'CURRENT MODE IS NIGHT: deployment and smoke are complete. Select a concrete gameplay/demo problem, justified experiment, necessary measurement or diagnosis. Updating old report expectations is not a standalone task; reports are part of completing useful work. Do not require another smoke pair, repeat an unchanged passing profile without a new question, or treat historical task instructions as pending user requests. Choose stop if no useful safe work remains. Worker may use up to 15 minutes.'
    : 'CURRENT MODE IS SMOKE: verify this controller version with two consecutive successful real cycles, including golden-path. Repair reproduced failures without hiding them. Keep each worker task small, about 3 minutes. Historical records are evidence, not a queue.';
  const schema = path.join(here, 'decision.schema.json');
  const rowsFile = path.join(session, 'cycles.json');
  const event = message => { const line = `${new Date().toISOString()} ${message}`; console.log(line); fs.appendFileSync(path.join(session, 'session.log'), line + '\n'); };
  let rows = [], completed = false;
  const previousFile = priorSession ? path.join(priorSession.session, 'cycles.json') : null;
  const previous = mode === 'night' ? JSON.parse(fs.readFileSync(path.join(base, 'smoke-ready.json'))).cycles :
    previousFile && fs.existsSync(previousFile) ? JSON.parse(fs.readFileSync(previousFile, 'utf8')) : [];
  try {
    const status = git(root, 'status', '--short'); fs.writeFileSync(path.join(session, 'initial-git-status.txt'), status);
    writeJson(path.join(session, 'metadata.json'), { mode, deadline: new Date(deadline).toISOString(), unity, baseline, node: process.version });
    event(`START ${mode}; pid=${process.pid}; session=${session}`);
    rows = await cycleLoop({ maxCycles: mode === 'smoke' ? 8 : 40, deadline, stopped, stopWhen: history => mode === 'smoke' && smokeReady(history),
      decide: async (history, index) => {
        guard(); const dir = path.join(session, `cycle-${index + 1}`); fs.mkdirSync(dir);
        event(`cycle ${index + 1}: fresh read-only selection`);
        const output = path.join(dir, 'decision.json');
        const prompt = `${policy}\nPHASE: READ-ONLY PLANNER. Read files with shell tools; do not change files IN THIS PHASE ONLY. The following executor CAN write: do not put a no-write requirement in the task. Return schema JSON. This is ${mode}, cycle ${index + 1}. Evaluate at least 2 candidates using current production evidence; value/risk/cost 1..5. Select a genuinely useful feasible task or stop if none. Remaining wall seconds: ${Math.floor(budget(Infinity) / 1000)}.\n<verified_history>\n${JSON.stringify([...previous, ...history].slice(-4).map(r => ({ ...r, work: { summaryPath: r.work.summaryPath, summary: r.work.summary?.slice(0, 1800) } })))}\n</verified_history>\n${modeGoal}`;
        fs.writeFileSync(path.join(dir, 'planner-input.txt'), prompt);
        const result = await runProcess('codex', ['exec', '--ignore-user-config', '--sandbox', 'read-only', '--ephemeral', '--disable', 'multi_agent', '-c', 'approval_policy="never"', '-c', 'windows.sandbox="unelevated"', '--json', '--output-schema', schema, '-o', output, '-C', root, '-'], { cwd: root, input: prompt, prefix: path.join(dir, 'planner'), timeoutMs: budget(300000), stopped });
        guard(); if (result.exitCode !== 0 || !fs.existsSync(output)) throw new Error(`Planner failed (${result.exitCode}); see ${dir}`);
        const decision = JSON.parse(fs.readFileSync(output, 'utf8'));
        if (!(decision.testProfile in profiles) || !['work', 'stop'].includes(decision.action) || !Array.isArray(decision.candidates)) throw new Error('Invalid decision');
        event(`selected ${decision.testProfile}: ${decision.task}`); return decision;
      },
      work: async (decision, index) => {
        const dir = path.join(session, `cycle-${index + 1}`), output = path.join(dir, 'worker-summary.md');
        const prompt = `${policy}\nPHASE: EXECUTOR. You CAN write within workspace; the planner's own read-only phase is over. User authorized inline autonomous work; do not ask questions. Implement only this chosen task. Use regression tests for behavior fixes, do not change tests merely to pass. Host runs Unity after you finish; you may run Node/static checks. No Unity launch. Update NIGHT_REPORT.md and PLAYTEST_CHECKLIST.md as part of the work. Cite evidence actually read. CURRENT DECISION:\n${JSON.stringify(decision)}\n<verified_history>\n${JSON.stringify([...previous, ...rows].slice(-4).map(r => ({ ...r, work: { summaryPath: r.work.summaryPath, summary: r.work.summary?.slice(0, 1800) } })))}\n</verified_history>\nFresh planner output: ${path.join(dir, 'decision.json')}. If no justified fix, document precise findings and stop; do not manufacture content.\n${modeGoal}`;
        event(`cycle ${index + 1}: workspace-write work`);
        const result = await runProcess('codex', ['exec', '--ignore-user-config', '--sandbox', 'workspace-write', '--ephemeral', '--disable', 'multi_agent', '-c', 'approval_policy="never"', '-c', 'windows.sandbox="unelevated"', '--json', '-o', output, '-C', root, '-'], { cwd: root, input: prompt, prefix: path.join(dir, 'worker'), timeoutMs: budget(mode === 'smoke' ? 480000 : 1200000), stopped });
        guard(); if (result.exitCode !== 0 || !fs.existsSync(output)) throw new Error(`Worker failed (${result.exitCode}); see ${dir}`);
        const diff = execFileSync('git', ['-C', root, 'diff', '--check'], { encoding: 'utf8', windowsHide: true });
        return { ...result, summaryPath: output, summary: fs.readFileSync(output, 'utf8').slice(0, 18000), diffCheck: diff || 'passed' };
      },
      verify: async (decision, index) => {
        guard(); const dir = path.join(session, `cycle-${index + 1}`, 'unity');
        event(`cycle ${index + 1}: Unity ${decision.testProfile}`);
        const actualFilter = profiles[decision.testProfile];
        const seconds = Math.max(1, Math.floor(budget(900000) / 1000));
        const result = await runProcess('pwsh', ['-NoProfile', '-File', path.join(here, 'Run-UnityTests.ps1'), '-Unity', unity, '-Project', root, '-Output', dir, '-Filter', actualFilter, '-TimeoutSeconds', String(seconds)], { cwd: root, prefix: path.join(dir, 'host'), timeoutMs: seconds * 1000 + 45000 });
        const xmlFile = path.join(dir, 'results.xml');
        const editorLog = path.join(dir, 'editor.log');
        const xml = fs.existsSync(xmlFile) ? fs.readFileSync(xmlFile, 'utf8') : null;
        const outcomeFile = path.join(dir, 'wrapper-outcome.json');
        const wrapperOutcome = fs.existsSync(outcomeFile) ? JSON.parse(fs.readFileSync(outcomeFile, 'utf8')) : null;
        const parsed = readVerification(result.exitCode, xml, fs.existsSync(editorLog) ? fs.readFileSync(editorLog, 'utf8') : '', actualFilter.split(';'), wrapperOutcome);
        const verification = { ...result, ...parsed, passed: result.exitCode === 0 && parsed.passed,
          xml: xml === null ? null : xmlFile, editorLog, profile: decision.testProfile };
        const botDir = path.join(root, 'Artifacts/GeneratedQA/BotBatches');
        for (const file of ['latest_batch.json', 'latest_batch.csv']) {
          const source = path.join(botDir, file);
          if (fs.existsSync(source) && fs.statSync(source).mtimeMs >= Date.now() - result.wallSeconds * 1000 - 2000)
            fs.copyFileSync(source, path.join(dir, file));
        }
        writeJson(path.join(dir, 'verification.json'), verification); guard();
        event(`cycle ${index + 1}: ${verification.passed ? 'PASS' : 'FAIL'}; result=${verification.result}; executed=${verification.executed}; failed=${verification.failed}`);
        return verification;
      },
      save: async history => {
        rows = history; writeJson(rowsFile, history);
        const verified = history.at(-1).verification;
        fs.appendFileSync(path.join(root, 'NIGHT_REPORT.md'), `\n\nАвтоматическая проверка хоста: ${new Date().toISOString()}, цикл ${history.length}, ${history.at(-1).decision.testProfile}: ${verified.passed ? 'PASS' : 'FAIL'} (${verified.result}), выполнено ${verified.executed}, падений ${verified.failed}. Свидетельство: ${verified.xml ?? verified.editorLog}.\n`);
      },
    });
    completed = !stopped() && (mode === 'smoke' ? smokeReady(rows) : nightVerified(rows));
    if (mode === 'smoke') writeJson(path.join(base, 'smoke-ready.json'), { ready: completed, at: new Date().toISOString(), head: baseline.head, fingerprint: workspaceFingerprint(root), session, cycles: rows });
    writeJson(path.join(session, 'final.json'), { completed, mode, cycles: rows.length, stopped: stopped(), deadlineReached: Date.now() >= deadline });
    event(`END; completed=${completed}; cycles=${rows.length}`);
    fs.appendFileSync(path.join(root, 'NIGHT_REPORT.md'), `\n\nСессия ${mode} закончена: ${new Date().toISOString()}; проверенный результат=${completed}; циклов=${rows.length}; STOP=${stopped()}; deadline=${Date.now() >= deadline}. Свидетельства: ${session}. Коммиты не создавались.\n`);
    if (!completed) process.exitCode = 2;
  } catch (error) {
    writeJson(path.join(session, 'failure.json'), { at: new Date().toISOString(), error: error.stack, cycles: rows });
    if (mode === 'smoke') writeJson(path.join(base, 'smoke-ready.json'), { ready: false, at: new Date().toISOString(), session });
    fs.appendFileSync(path.join(root, 'NIGHT_REPORT.md'), `\n\nNight Shift остановлен: ${error.message}. См. ${session}. Успех не подтверждён.\n`);
    event(`STOP: ${error.message}`); process.exitCode = 1;
  }
  });
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) main().catch(error => { console.error(error); process.exitCode = 1; });
