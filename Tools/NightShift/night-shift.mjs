import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { spawn, execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { finished } from 'node:stream/promises';

const here = path.dirname(fileURLToPath(import.meta.url));
export const profiles = Object.freeze({
  static: '',
  'golden-path': 'Subject42GoldenPathTests',
  onboarding: 'Subject42OnboardingTests;Subject42TutorialTests;StartScreenPresentationTests',
  localization: 'ProductionLocalizationTests;ProductionFontTests',
  combat: 'PistolPrecisionTests;Subject42OrbitalOperationsTests',
  events: 'OrbitalRelayStateTests;CorridorGameplayTests;CorridorProductionIntegrationTests;WorldHazardTests',
  rewards: 'Subject42RewardProgressionTests;OrbitUpgradeSelectionTests',
  boss: 'Subject42FinalBossFlowTests', core: 'Subject42GoldenPathTests;Subject42OnboardingTests;Subject42RewardProgressionTests;Subject42FinalBossFlowTests;ProductionLocalizationTests',
  placement: 'RocketStormProductionPlacementTests',
  'orbital-control': 'OrbitalCombatV2LabTests',
  'orbital-tactics': 'OrbitalCombatV2TacticsTests',
  'orbital-placement': 'OrbitalCombatV2PlacementTests',
  'orbital-modules': 'OrbitalCombatV2ModulesTests',
  'orbital-sandbox': 'EnemyOrbitalLabSandboxTests',
  'live-visual': 'LiveVisualProfileTests;LiveVisualProductionTests',
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
  if (/No valid Unity Editor license|Failed to activate.*licen|License.*(?:unavailable|invalid|expired)/i.test(editorLog)) throw new Error('Unity licensing infrastructure unavailable');
  if (wrapperOutcome.timedOut || wrapperOutcome.cancelled) return { passed: false, executed: 0, failed: 0, result: wrapperOutcome.timedOut ? 'TestTimeout' : 'Cancelled' };
  if (xml !== null) return parseTestXml(xml, expectedFixtures);
  const compilerDiagnostics = [...new Set(editorLog.split(/\r?\n/)
    .filter(line => /\berror CS\d{4}\b/.test(line)))].slice(0, 24);
  if (exitCode !== 0 && compilerDiagnostics.length && /Scripts have compiler errors|script compilation.*(?:error|fail)/i.test(editorLog))
    return { passed: false, executed: 0, failed: 0, result: 'CompileFailed', compilerDiagnostics };
  return { passed: false, executed: 0, failed: 0, result: 'MissingXml', diagnostic: `Unity produced no results XML; exit ${exitCode}` };
}

export function nightVerified(rows) { return rows.length > 0 && rows.every(row => row.verification.passed); }

// An intermediate content chunk may defer Unity until the integrated slice is ready.
// This record deliberately cannot be cited as compilation or gameplay evidence.
export function staticVerification() {
  return { passed: true, executed: 0, result: 'StaticOnly', unityRan: false,
    compilationVerified: false, scope: 'git diff --check only; Unity/gameplay remain unverified' };
}

export function sessionDeadline(startedAt, hours, requestedAt, now = Date.now()) {
  const budgetEnd = Date.parse(startedAt) + hours * 3600000;
  const requested = requestedAt ? Date.parse(requestedAt) : budgetEnd;
  const deadline = Math.min(budgetEnd, requested);
  if (!Number.isFinite(deadline) || deadline <= now) throw new Error('deadline must be a valid future instant within the original budget');
  return deadline;
}

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

export async function cycleLoop({ maxCycles = Infinity, deadline, reserveMs = 0, stopped, decide, work, verify, save, history = [], stopWhen = () => false, reviewStop = async () => false }) {
  const failures = new Map();
  for (let index = 0; index < maxCycles && Date.now() < deadline - reserveMs && !stopped(); index++) {
    let decision = { task: 'selection', issueKey: 'planner' }, workResult = {}, verification;
    try {
      decision = await decide(history, index);
      if (stopped() || Date.now() >= deadline - reserveMs) break;
      if (decision.action === 'stop') {
        const review = await reviewStop(decision, history, index);
        if (review === true) break;
        if (review && typeof review === 'object' && review.action === 'work') decision = review;
        else verification = { passed: false, executed: 0, result: 'StopRejected' };
      }
      const key = decision.issueKey || decision.testProfile || decision.task;
      if (!verification && (failures.get(key) ?? 0) >= 2) verification = { passed: false, executed: 0, result: 'BlockedIssue' };
      if (!verification) {
        if (stopped() || Date.now() >= deadline - reserveMs) break;
        workResult = await work(decision, index);
        verification = stopped() || Date.now() >= deadline ? { passed: false, executed: 0, result: 'NotRun' } : await verify(decision, index);
      }
    } catch (error) {
      if (!error.recoverable) throw error;
      verification = { passed: false, executed: 0, failed: 0, result: error.result ?? 'TaskFailed', diagnostic: error.message, details: error.details };
    }
    const key = decision.issueKey || decision.testProfile || decision.task;
    if (verification.passed) failures.delete(key);
    else failures.set(key, (failures.get(key) ?? 0) + 1);
    history.push({ cycle: index + 1, at: new Date().toISOString(), decision, work: workResult, verification, blocked: (failures.get(key) ?? 0) >= 2 });
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

export async function runProcess(executable, args, { cwd, input, prefix, timeoutMs, stopped = () => false, onState = () => {} }) {
  fs.mkdirSync(path.dirname(prefix), { recursive: true });
  const output = fs.createWriteStream(prefix + '.stdout.log'), errors = fs.createWriteStream(prefix + '.stderr.log');
  const child = spawn(executable, args, { cwd, windowsHide: true, shell: false, env: { ...process.env, GIT_OPTIONAL_LOCKS: '0' }, stdio: ['pipe', 'pipe', 'pipe'] });
  child.stdout.pipe(output); child.stderr.pipe(errors);
  child.stdin.on('error', () => {});
  child.stdin.end(input ?? '');
  const began = Date.now(); let reason = null, terminating = false, killDeadline = Infinity;
  onState({ pid: child.pid, executable, prefix, startedAt: new Date(began).toISOString(), timeoutAt: new Date(began + timeoutMs).toISOString() });
  const kill = () => {
    if (terminating) return; terminating = true; killDeadline = Date.now() + 15000;
    if (process.platform === 'win32') {
      const killer = spawn('taskkill.exe', ['/PID', String(child.pid), '/T', '/F'], { windowsHide: true, stdio: 'ignore' });
      killer.on('error', () => child.kill());
    } else child.kill('SIGTERM');
  };
  let lostControl;
  const timer = setInterval(() => {
    if (Date.now() - began >= timeoutMs || stopped()) { reason = stopped() ? 'STOP requested' : 'process timeout'; kill(); }
    if (Date.now() >= killDeadline) lostControl?.();
  }, 500);
  return await new Promise((resolve, reject) => {
    lostControl = () => { clearInterval(timer); reject(new Error(`Lost control of child PID ${child.pid}; recovery not confirmed; ${prefix}`)); };
    child.on('error', error => { clearInterval(timer); output.end(); errors.end(); onState(null); reject(error); });
    child.on('close', async code => {
      clearInterval(timer); output.end(); errors.end(); onState(null);
      const details = { exitCode: code, wallSeconds: (Date.now() - began) / 1000, stdout: prefix + '.stdout.log', stderr: prefix + '.stderr.log' };
      try {
        await Promise.all([finished(output), finished(errors)]);
        if (reason) reject(Object.assign(new Error(`${reason}: ${path.basename(executable)}; see ${prefix}`), { recoverable: true, result: reason === 'process timeout' ? 'ProcessTimeout' : 'Cancelled', details }));
        else resolve(details);
      } catch (error) { reject(error); }
    });
  });
}

export function checkTaskProcess(result, output, phase) {
  const readLog = file => file && fs.existsSync(file) ? fs.readFileSync(file, 'utf8').slice(-300000) : '';
  const stdout = readLog(result.stdout);
  // Successful tool transcripts may quote a quota error as data. Inspect CLI
  // failure events, never arbitrary successful source/test output.
  const failures = stdout.split(/\r?\n/).filter(line => {
    try { return ['error', 'turn.failed'].includes(JSON.parse(line).type); } catch { return false; }
  }).join('\n');
  const logs = readLog(result.stderr) + '\n' + (result.exitCode === 0 ? failures : stdout);
  if (/usage limit|quota exceeded|insufficient_quota|rate_limit_exceeded|hit your.*limit|too many requests|401 Unauthorized|authentication.*(?:failed|required)|not logged in|refresh.token.*(?:failed|expired)|sandbox.*(?:unavailable|initialization failed)/i.test(logs))
    throw new Error(`${phase}: authorization, quota or sandbox infrastructure blocked; see ${result.stderr}`);
  if (result.exitCode !== 0 || !fs.existsSync(output)) throw Object.assign(new Error(`${phase} failed (${result.exitCode}); see ${result.stderr}`), { recoverable: true, result: `${phase}Failed`, details: result });
}

export async function recoverySmoke(directory) {
  // Real child exit/kill paths through the production loop, never game PASS claims.
  const rows = await cycleLoop({ maxCycles: 4, deadline: Date.now() + 30000, stopped: () => false,
    decide: async (_history, index) => ({ action: index === 2 ? 'stop' : 'work', task: `recovery probe ${index}`, issueKey: `probe-${index}` }),
    work: async (_decision, index) => {
      const script = index === 0 ? 'process.exit(3)' : index === 1 ? 'setInterval(()=>{},1000)' : 'process.stdout.write("recovered")';
      const result = await runProcess(process.execPath, ['-e', script], { cwd: directory, prefix: path.join(directory, `probe-${index}`), timeoutMs: index === 1 ? 100 : 3000 });
      if (result.exitCode) throw Object.assign(new Error('deliberate smoke task failure'), { recoverable: true, result: 'WorkerFailed', details: result });
      return result;
    }, verify: async () => ({ passed: true, executed: 0, result: 'ControllerProbePassed' }),
    save: async rows => writeJson(path.join(directory, 'recovery-probe.json'), rows) });
  if (rows.length !== 4 || rows[0].verification.result !== 'WorkerFailed' || rows[1].verification.result !== 'ProcessTimeout' || rows[2].verification.result !== 'StopRejected' || rows[3].verification.passed !== true)
    throw new Error('Recovery smoke failed; no night launch');
  return rows;
}

async function main() {
  const args = process.argv.slice(2);
  const option = (key, fallback) => { const i = args.indexOf('--' + key); return i < 0 ? fallback : args[i + 1]; };
  const mode = option('mode', 'smoke');
  if (!['smoke', 'night'].includes(mode)) throw new Error('Mode must be smoke or night');
  const root = path.resolve(here, '../..');
  const base = path.join(root, 'Artifacts/GeneratedQA/NightShift');
  const unity = option('unity', 'G:/unity/editor/6000.3.13f1/Editor/Unity.exe');
  const hours = Number(option('hours', '6'));
  if (!Number.isFinite(hours) || hours <= 0 || hours > 7) throw new Error('hours must be >0 and <=7');
  const resumeFrom = option('resume-from', null);
  const resumePath = resumeFrom ? path.resolve(resumeFrom) : null;
  if (resumePath && (!resumePath.startsWith(base + path.sep) || !fs.existsSync(path.join(resumePath, 'metadata.json')))) throw new Error('resume-from must name an existing session within this project');
  const resumeMetadata = resumePath ? JSON.parse(fs.readFileSync(path.join(resumePath, 'metadata.json'), 'utf8')) : null;
  fs.mkdirSync(base, { recursive: true });
  const pending = findPendingBackups(base);
  if (pending.length) throw new Error(`Unrestored PlayerPrefs backup; recover before launch: ${pending.join(', ')}`);
  if (fs.existsSync(path.join(base, 'STOP'))) throw new Error('STOP file exists; remove it deliberately before starting');
  if (mode === 'night') {
    const readyPath = path.join(base, 'smoke-ready.json');
    if (!fs.existsSync(readyPath)) throw new Error('Night requires a successful recovery smoke first');
    const ready = JSON.parse(fs.readFileSync(readyPath));
    if (!ready.ready || ready.kind !== 'recovery-v2' || ready.head !== git(root, 'rev-parse', 'HEAD') || ready.fingerprint !== workspaceFingerprint(root) || Date.now() - Date.parse(ready.at) > 86400000)
      throw new Error('Smoke gate is stale or failed; repeat smoke');
  }
  const lockPath = path.join(base, 'session.lock');
  return await withSessionLock(lockPath, async () => {
  const latestPath = path.join(base, 'latest-session.json');
  const priorSession = fs.existsSync(latestPath) ? JSON.parse(fs.readFileSync(latestPath, 'utf8')) : null;
  const session = path.join(base, new Date().toISOString().replace(/[:.]/g, '-') + '-' + mode);
  fs.mkdirSync(session, { recursive: true });
  writeJson(path.join(base, 'latest-session.json'), { pid: process.pid, mode, session });
  const resumedAt = new Date().toISOString();
  const startedAt = resumeMetadata?.startedAt ?? resumedAt;
  const deadline = sessionDeadline(startedAt, hours, option('deadline-at', null));
  const reserveMs = mode === 'night' ? 15 * 60000 : 0;
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
  const budget = max => Math.max(1, Math.min(max, deadline - Date.now() - 60000));
  const policy = fs.readFileSync(path.join(here, 'director-prompt.md'), 'utf8');
  const modeGoal = mode === 'night'
    ? 'CURRENT MODE IS NIGHT SHIFT: CONTENT & CLEANUP, SIX-HOUR FIXED DEADLINE. Build and integrate one complete new playable vertical slice in the current segmented production MVP, then a second independent slice only if useful. Gameplay content has priority over QA, labs, managers and refactoring. Current ORBITAL is 3x12, Q/E energy, FIXED outward fire, production Shift Bullet Time, Tab constructor, O-M-O links. Preserve its latest rules. First selection alone performs a SHORT current-code inventory; later selections continue known integration rather than inventory again. Worker has at most 12 minutes; select a concrete small implementation step, not a whole feature or an analysis-only task. testProfile static defers Unity for intermediate chunks/cleanup/docs; at slice completion select exact narrow Unity mechanics/boundary checks and a real MVP Game View capture using existing helpers. Do not repeatedly test unchanged PASS. Experimental content must be opt-in through existing Development/F1 settings and absent from public defaults. A failed task is not the end of the night. After two justified failed attempts on the SAME issueKey choose independent work. NoSafeWork requires evidence across independent content and safe cleanup directions. Old lab/Live Visual goals are superseded.'
    : 'CURRENT MODE IS SHORT RECOVERY SMOKE.';
  const schema = path.join(here, 'decision.schema.json');
  const rowsFile = path.join(session, 'cycles.json');
  const event = message => { const line = `${new Date().toISOString()} ${message}`; console.log(line); fs.appendFileSync(path.join(session, 'session.log'), line + '\n'); };
  let rows = [], completed = false, phase = 'initializing', activeProcess = null, endReason = null;
  const heartbeatPath = path.join(session, 'heartbeat.json');
  const heartbeat = () => writeJson(heartbeatPath, { pid: process.pid, mode, startedAt, deadlineAt: new Date(deadline).toISOString(), at: new Date().toISOString(), phase, activeProcess, cycles: rows.length, remainingSeconds: Math.max(0, Math.floor((deadline - Date.now()) / 1000)) });
  const onState = state => { activeProcess = state; heartbeat(); };
  const pulse = setInterval(heartbeat, 10000); heartbeat();
  const previousFile = resumePath ? path.join(resumePath, 'cycles.json') : null;
  const previous = mode === 'night' && previousFile && fs.existsSync(previousFile) ? JSON.parse(fs.readFileSync(previousFile, 'utf8')) : [];
  const historyText = history => JSON.stringify([...previous, ...history].slice(-8).map(r => ({ ...r, work: { summaryPath: r.work?.summaryPath, summary: r.work?.summary?.slice(0, 3500) } })));
  function readDecision(output) {
    let decision;
    try { decision = JSON.parse(fs.readFileSync(output, 'utf8')); }
    catch (error) { throw Object.assign(new Error(`Invalid planner JSON: ${error.message}`), { recoverable: true, result: 'PlannerInvalid' }); }
    if (!(decision.testProfile in profiles) || !['work', 'stop'].includes(decision.action) || !Array.isArray(decision.candidates) || !decision.issueKey || (decision.testFilter && !/^[A-Za-z0-9_.;]+$/.test(decision.testFilter)))
      throw Object.assign(new Error('Invalid planner decision/filter'), { recoverable: true, result: 'PlannerInvalid' });
    return decision;
  }
  const codex = async (prompt, dir, label, readOnly, output, structured = false, timeout = 180000) => {
    phase = label; heartbeat(); fs.writeFileSync(path.join(dir, `${label}-input.txt`), prompt);
    const cliArgs = ['exec', '--ignore-user-config', '--sandbox', readOnly ? 'read-only' : 'workspace-write', '--ephemeral', '--disable', 'multi_agent', '-c', 'approval_policy="never"', '-c', 'windows.sandbox="unelevated"', '--json'];
    if (structured) cliArgs.push('--output-schema', schema);
    cliArgs.push('-o', output, '-C', root, '-');
    const result = await runProcess('codex', cliArgs, { cwd: root, input: prompt, prefix: path.join(dir, label), timeoutMs: budget(timeout), stopped, onState });
    guard(); checkTaskProcess(result, output, label); return result;
  };
  try {
    const status = git(root, 'status', '--short'); fs.writeFileSync(path.join(session, 'initial-git-status.txt'), status);
    fs.copyFileSync(path.join(root, 'NIGHT_REPORT.md'), path.join(session, 'previous-night-report.md'));
    writeJson(path.join(session, 'metadata.json'), { mode, startedAt, resumedAt, resumeFrom: resumePath, deadlineAt: new Date(deadline).toISOString(), deadline: new Date(deadline).toISOString(), reserveMinutes: reserveMs / 60000, unity, baseline, node: process.version });
    event(`START ${mode}; pid=${process.pid}; session=${session}`);
    async function verifyUnity(decision, index) {
      if (decision.testProfile === 'static') {
        guard();
        execFileSync('git', ['-C', root, 'diff', '--check'], { encoding: 'utf8', windowsHide: true });
        const verification = staticVerification();
        const dir = path.join(session, `cycle-${index + 1}`, 'static');
        fs.mkdirSync(dir, { recursive: true }); writeJson(path.join(dir, 'verification.json'), verification);
        event(`cycle ${index + 1}: static check only; no Unity or gameplay claim`);
        return verification;
      }
      guard(); const dir = path.join(session, `cycle-${index + 1}`, 'unity');
      phase = 'unity'; heartbeat(); event(`cycle ${index + 1}: Unity ${decision.testProfile}`);
      const actualFilter = decision.testFilter || profiles[decision.testProfile];
      const seconds = Math.max(1, Math.floor(budget(300000) / 1000));
      const result = await runProcess('pwsh', ['-NoProfile', '-File', path.join(here, 'Run-UnityTests.ps1'), '-Unity', unity, '-Project', root, '-Output', dir, '-Filter', actualFilter, '-TimeoutSeconds', String(seconds)], { cwd: root, prefix: path.join(dir, 'host'), timeoutMs: seconds * 1000 + 45000, onState });
      const xmlFile = path.join(dir, 'results.xml'), editorLog = path.join(dir, 'editor.log'), outcomeFile = path.join(dir, 'wrapper-outcome.json');
      const xml = fs.existsSync(xmlFile) ? fs.readFileSync(xmlFile, 'utf8') : null;
      const wrapperOutcome = fs.existsSync(outcomeFile) ? JSON.parse(fs.readFileSync(outcomeFile, 'utf8')) : null;
      const expectedFixtures = decision.testFilter ? [] : actualFilter.split(';');
      const parsed = readVerification(result.exitCode, xml, fs.existsSync(editorLog) ? fs.readFileSync(editorLog, 'utf8') : '', expectedFixtures, wrapperOutcome);
      const verification = { ...result, ...parsed, passed: result.exitCode === 0 && parsed.passed, xml: xml === null ? null : xmlFile, editorLog, profile: decision.testProfile, filter: actualFilter };
      writeJson(path.join(dir, 'verification.json'), verification); guard();
      event(`cycle ${index + 1}: ${verification.passed ? 'PASS' : 'FAIL'}; result=${verification.result}; executed=${verification.executed}; failed=${verification.failed}`);
      return verification;
    }
    if (mode === 'smoke') {
      phase = 'recovery-smoke'; await recoverySmoke(session); guard();
      const probeOutput = path.join(session, 'codex-probe.json');
      await codex('Read-only connectivity/schema probe. Do not explore files or run tools. Return valid schema JSON: action work, task connectivity checked, issueKey smoke-connectivity, classification Verification, reason connectivity only, evidence [], candidates [], testProfile orbital-control, testFilter null, stopCategory null, gdd not applicable, success schema accepted, abort infrastructure failure.', session, 'connectivity', true, probeOutput, true, 120000);
      readDecision(probeOutput);
      const decision = { action: 'work', issueKey: 'smoke-unity', task: 'Compile + one existing outward-fire lab test', testProfile: 'orbital-control', testFilter: 'OrbitalCombatV2LabTests.FixedShotsUseRadialMuzzleWithoutTargetsAndKeepTheProfileCadence' };
      const verification = await verifyUnity(decision, 0);
      rows = [{ cycle: 1, decision, work: {}, verification }]; writeJson(rowsFile, rows);
      if (!verification.passed) throw new Error(`Real smoke Unity check failed: ${verification.result}; see ${verification.editorLog}`);
      completed = true; endReason = 'recovery-smoke-passed';
      writeJson(path.join(base, 'smoke-ready.json'), { ready: true, kind: 'recovery-v2', at: new Date().toISOString(), head: baseline.head, fingerprint: workspaceFingerprint(root), session, cycles: rows });
    } else {
    rows = await cycleLoop({ deadline, reserveMs, stopped,
      stopWhen: history => {
        const last = history.at(-1);
        if (last.blocked && last.decision.issueKey === 'planner') throw new Error('Planner unavailable after two attempts; cannot select safe work');
        return false;
      },
      decide: async (history, index) => {
        guard(); const dir = path.join(session, `cycle-${index + 1}`); fs.mkdirSync(dir);
        event(`cycle ${index + 1}: fresh read-only selection`);
        const output = path.join(dir, 'decision.json');
        const blocked = history.filter(r => r.blocked).map(r => r.decision.issueKey);
        const prompt = `${policy}\nPHASE: READ-ONLY PLANNER. Read files; do not mutate anything IN THIS PHASE ONLY. The executor CAN write. Return schema JSON. Cycle ${index + 1}. Evaluate at least 2 small next steps; prefer finishing current content integration over opening another feature. Only the FIRST cycle inventories current content; use the compact session note and actual history thereafter. testProfile=static with testFilter=null for intermediate content chunks, cleanup and docs; choose exact Unity filter only for ready gameplay slice or a new regression question. Do not select verification-only tasks after sufficient evidence. issueKey is a STABLE identifier for the underlying problem (do not rename it to evade the retry limit). StopCategory null for work, TaskBlocked for individual failure, NoSafeWork only if independent content/cleanup directions are exhausted. BLOCKED issueKeys: ${JSON.stringify(blocked)}. Remaining seconds: ${Math.floor(budget(Infinity) / 1000)}.\n<verified_history>\n${historyText(history)}\n</verified_history>\n${modeGoal}`;
        await codex(prompt, dir, 'planner', true, output, true);
        const decision = readDecision(output);
        event(`selected ${decision.testProfile}: ${decision.task}`); return decision;
      },
      reviewStop: async (proposal, history, index) => {
        const dir = path.join(session, `cycle-${index + 1}`), output = path.join(dir, 'stop-review.json');
        event(`cycle ${index + 1}: controller reviewing stop proposal; category=${proposal.stopCategory}`);
        const prompt = `${policy}\nPHASE READ-ONLY STOP REVIEW. A planner proposed stopping. Individual FAIL, compile error, recoverable timeout or blocked hypothesis does NOT stop the night. Inspect current content integration, another independent small gameplay idea and confirmed dead-code cleanup. Return WORK for feasible useful work. Only return stop with NoSafeWork if no safe useful independent work exists; enumerate at least two independently blocked directions with evidence. Safety/AuthQuota/ProcessControl requires real data/process/access risk.\nProposal: ${JSON.stringify(proposal)}\nHistory: ${historyText(history)}\n${modeGoal}`;
        await codex(prompt, dir, 'stop-review', true, output, true);
        const review = readDecision(output);
        if (review.action === 'work') return review;
        if (review.candidates.length >= 2 && review.evidence.length > 0 && ['NoSafeWork', 'Safety', 'AuthQuota', 'ProcessControl'].includes(review.stopCategory)) {
          endReason = `reviewed-${review.stopCategory}: ${review.reason}`;
          writeJson(path.join(session, 'accepted-stop.json'), { proposal, review }); return true;
        }
        return false;
      },
      work: async (decision, index) => {
        const dir = path.join(session, `cycle-${index + 1}`), output = path.join(dir, 'worker-summary.md');
        const prompt = `${policy}\nPHASE: EXECUTOR. You CAN write within workspace. User authorized autonomous work, no questions. Implement only this concrete small task; keep compileable coherent state. Host runs Unity after your answer ONLY if selected testProfile is not static. You may run static checks; no Unity/background launch. Final answer Russian, max 700 words: actual gameplay behavior/files, actual checks and limits, unresolved issue, next integration step, exact human play steps, real capture paths. Update compact docs/night-shift-content-2026-10-30.md with current slice status and the six design answers BEFORE first implementation, not a long GDD. Preserve adopted GDD; label new ideas experiments pending human playtest. Do not overwrite NIGHT_REPORT.md. CURRENT DECISION:\n${JSON.stringify(decision)}\n<verified_history>\n${historyText(rows)}\n</verified_history>\nDo not create analysis-only or cosmetic busywork. Build useful production content, integrate rewards/settings/visuals/lifecycle, and leave clean temporary state.\n${modeGoal}`;
        event(`cycle ${index + 1}: workspace-write work`);
        const result = await codex(prompt, dir, 'worker', false, output, false, 12 * 60000);
        let diff;
        try { diff = execFileSync('git', ['-C', root, 'diff', '--check'], { encoding: 'utf8', windowsHide: true }); }
        catch (error) { throw Object.assign(new Error('git diff --check failed; repair whitespace next cycle'), { recoverable: true, result: 'DiffCheckFailed', details: { output: String(error.stdout) } }); }
        return { ...result, summaryPath: output, summary: fs.readFileSync(output, 'utf8').slice(0, 18000), diffCheck: diff || 'passed' };
      },
      verify: verifyUnity,
      save: async history => {
        rows = history; writeJson(rowsFile, history);
        const row = history.at(-1); guard();
        if (findPendingBackups(base).length) throw new Error('Unrestored PlayerPrefs: cannot continue safely');
        event(`cycle ${history.length}: saved ${row.verification.result}${row.blocked ? '; BLOCKED issue=' + row.decision.issueKey : ''}`);
        heartbeat();
      },
    });
    phase = 'closeout'; heartbeat(); guard();
    // At most a short read-only report task; no more source changes in the reserve.
    const reportPath = path.join(session, 'morning-summary.md');
    if (!stopped() && budget(Infinity) > 90000) {
      const prompt = `${policy}\nPHASE: READ-ONLY FINAL REPORT. Do not change files or run Unity. Read current session cycles.json, worker-summary.md and verification.json at ${session}; inspect final git diff/status and real capture paths. Return concise Russian report with exactly these six headings: СОЗДАНО, ЗАЧЕМ, ПРОВЕРЕНО, ОЧИЩЕНО, НЕ ЗАВЕРШЕНО, КАК ПОИГРАТЬ. At most 80 lines. Focus on new playable behavior, choices, exact Development/F1/MVP steps and expected result. Embed genuine Game View captures with absolute paths. State absent captures/recording honestly. StaticOnly is NOT compilation or gameplay evidence. Do not use cycle/test/line counts as success metrics. Cite specific GDD sections and experimental status. Do not claim human fun/balance validation. Preserve failures/limitations. Started ${startedAt}; deadline ${new Date(deadline).toISOString()}. No old-report inventory.`;
      try { await codex(prompt, session, 'morning-report', true, reportPath, false, 180000); }
      catch (error) { event(`report summary unavailable: ${error.message}`); if (!error.recoverable) throw error; }
    }
    if (!endReason && !stopped()) {
      phase = 'deadline-reserve'; heartbeat(); event('Normal dispatch ended; settings restored; monitoring until fixed deadline');
      while (Date.now() < deadline && !stopped()) await new Promise(resolve => setTimeout(resolve, Math.min(1000, deadline - Date.now())));
    }
    endReason ??= stopped() ? 'user-stop' : 'deadline';
    completed = !stopped();
    }
    writeJson(path.join(session, 'final.json'), { completed, allChecksPassed: nightVerified(rows), mode, startedAt, endedAt: new Date().toISOString(), deadlineAt: new Date(deadline).toISOString(), endReason, cycles: rows.length, stopped: stopped(), deadlineReached: Date.now() >= deadline });
    event(`END; completed=${completed}; cycles=${rows.length}`);
    if (!completed) process.exitCode = 2;
  } catch (error) {
    writeJson(path.join(session, 'failure.json'), { at: new Date().toISOString(), error: error.stack, cycles: rows });
    if (mode === 'smoke') writeJson(path.join(base, 'smoke-ready.json'), { ready: false, at: new Date().toISOString(), session });
    endReason = `infrastructure-blocked: ${error.message}`;
    writeJson(path.join(session, 'final.json'), { completed: false, startedAt, endedAt: new Date().toISOString(), deadlineAt: new Date(deadline).toISOString(), endReason, cycles: rows.length });
    event(`STOP: ${error.message}`); process.exitCode = 1;
  } finally {
    phase = 'finished'; clearInterval(pulse); heartbeat();
    if (mode === 'night') {
      const summaryFile = path.join(session, 'morning-summary.md');
      const body = fs.existsSync(summaryFile) ? fs.readFileSync(summaryFile, 'utf8') : rows.map(r => `- Цикл ${r.cycle}: ${r.decision.task}; ${r.verification.passed ? 'PASS' : 'FAIL'} (${r.verification.result}). Отчёт: ${r.work?.summaryPath ?? 'см. cycles.json'}`).join('\n');
      fs.writeFileSync(path.join(root, 'NIGHT_REPORT.md'), `# NIGHT REPORT — Subject#42\n\nНачало: ${startedAt}; окончание: ${new Date().toISOString()} (UTC).\nДедлайн: ${new Date(deadline).toISOString()}. Причина: ${endReason}.\n\n${body}\n\nПодробные проверки, включая FAIL и StaticOnly (без Unity): [артефакты](${session.replaceAll('\\', '/')}/cycles.json).\nПрежний отчёт сохранён в ${path.join(session, 'previous-night-report.md')}.\nКоммиты, staging и переключения веток не выполнялись.\n`);
    }
  }
  });
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) main().catch(error => { console.error(error); process.exitCode = 1; });
