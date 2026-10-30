import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { cycleLoop, parseTestXml, readVerification, smokeReady, findPendingBackups, withSessionLock, workspaceFingerprint, nightVerified, runProcess, recoverySmoke, checkTaskProcess, sessionDeadline, staticVerification } from './night-shift.mjs';

test('static work never invents Unity compilation, tests or gameplay evidence', () => {
  const result = staticVerification();
  assert.equal(result.passed, true);
  assert.equal(result.result, 'StaticOnly');
  assert.equal(result.executed, 0);
  assert.equal(result.unityRan, false);
  assert.equal(result.compilationVerified, false);
});

test('second decision receives actual first verification, no fixed queue', async () => {
  const seen = [];
  const history = await cycleLoop({ maxCycles: 2, deadline: Date.now() + 10000,
    stopped: () => false,
    decide: async history => { seen.push(structuredClone(history)); return { task: history.length ? 'investigate failure' : 'baseline' }; },
    work: async decision => ({ summary: decision.task }),
    verify: async (_decision, index) => ({ passed: index > 0, executed: 1 }),
    save: async () => {} });
  assert.equal(seen[0].length, 0);
  assert.equal(seen[1][0].verification.passed, false);
  assert.equal(history[1].decision.task, 'investigate failure');
});
test('empty, skipped-only, malformed and failed results never pass', () => {
  for (const xml of ['', '<test-run total="0" passed="0" failed="0" result="Passed"/>', '<test-run total="1" passed="0" failed="0" skipped="1" result="Passed"/>', '<test-run', '<test-run total="1" passed="0" failed="1" result="Failed"/>'])
    assert.equal(parseTestXml(xml).passed, false);
  assert.deepEqual(parseTestXml('<test-run total="2" passed="2" failed="0" result="Passed"></test-run>'), { passed: true, executed: 2, failed: 0, result: 'Passed' });
});
test('stop and deadline prevent dispatch', async () => {
  for (const opts of [{ stopped: () => true, deadline: Date.now() + 10000 }, { stopped: () => false, deadline: 0 }]) {
    const rows = await cycleLoop({ ...opts, maxCycles: 2, decide: () => assert.fail('must not dispatch') });
    assert.equal(rows.length, 0);
  }
});
test('stop after decision prevents mutation and stop action ends loop', async () => {
  let stopped = false;
  const rows = await cycleLoop({ maxCycles: 2, deadline: Date.now() + 10000, stopped: () => stopped,
    decide: async () => { stopped = true; return { task: 'must not run' }; }, work: () => assert.fail('must not mutate') });
  assert.equal(rows.length, 0);
});
test('infrastructure exceptions propagate, cannot create successful cycle', async () => {
  await assert.rejects(cycleLoop({ maxCycles: 2, deadline: Date.now() + 10000, stopped: () => false,
    decide: async () => ({ task: 'baseline' }), work: async () => ({}), verify: async () => { throw new Error('timeout'); } }), /timeout/);
});
test('compile and missing XML are failed task evidence; unrestored prefs and licensing stop', () => {
  const log = "Assets/Example.cs(4,2): error CS1061: inaccessible member\nScripts have compiler errors.";
  const outcome = { completed: true, preferencesRestored: true, unityExitCode: 1 };
  const result = readVerification(1, null, log, ['ExampleTests'], outcome);
  assert.equal(result.passed, false);
  assert.equal(result.result, 'CompileFailed');
  assert.equal(result.executed, 0);
  assert.equal(result.compilerDiagnostics.length, 1);
  for (const incomplete of [null, { ...outcome, completed: false }, { ...outcome, preferencesRestored: false }, { ...outcome, unityExitCode: 0 }])
    assert.throws(() => readVerification(1, null, log, ['ExampleTests'], incomplete), /wrapper did not complete/);
  assert.throws(() => readVerification(1, null, 'License client unavailable', ['ExampleTests'], outcome), /licensing/);
  assert.equal(readVerification(0, null, '', ['ExampleTests'], { ...outcome, unityExitCode: 0 }).result, 'MissingXml');
  assert.equal(readVerification(124, null, '', [], { ...outcome, timedOut: true, unityExitCode: 124 }).result, 'TestTimeout');
});
test('readiness requires two genuine verified cycles and Golden Path', () => {
  const row = { verification: { passed: true }, decision: { testProfile: 'golden-path' } };
  assert.equal(smokeReady([row]), false);
  assert.equal(smokeReady([row, { ...row, verification: { passed: false } }]), false);
  assert.equal(smokeReady([row, row]), true);
  assert.equal(smokeReady([row, row].map(r => ({ ...r, decision: { testProfile: 'localization' } }))), false);
  assert.equal(smokeReady([{ ...row, verification: { passed: false } }, row, row]), true);
});
test('repair loop stops at two consecutive verified cycles and retains earlier failures', async () => {
  const rows = await cycleLoop({ maxCycles: 8, deadline: Date.now() + 10000, stopped: () => false,
    decide: async history => ({ testProfile: 'golden-path', task: history.length ? 'repair/recheck' : 'baseline' }),
    work: async () => ({}), verify: async (_decision, index) => ({ passed: index > 0 }),
    stopWhen: smokeReady });
  assert.equal(rows.length, 3);
  assert.equal(rows[0].verification.passed, false);
});
test('XML counts and every selected fixture must agree', () => {
  assert.equal(parseTestXml('<test-run total="500" passed="1" failed="0" result="Passed"></test-run>').passed, false);
  const xml = '<test-run total="1" passed="1" failed="0" result="Passed"><test-suite type="TestFixture" name="Relay" fullname="Relay"></test-suite></test-run>';
  assert.equal(parseTestXml(xml, ['Relay']).passed, true);
  assert.equal(parseTestXml(xml, ['Relay', 'Corridor']).passed, false);
});
test('unfinished work remains recorded and cannot be called verified', async () => {
  let stopped = false;
  const rows = await cycleLoop({ maxCycles: 2, deadline: Date.now() + 10000, stopped: () => stopped,
    decide: async () => ({ task: 'fix' }), work: async () => { stopped = true; return { summary: 'changed' }; },
    verify: () => assert.fail('no verification after stop'), save: async () => {} });
  assert.equal(rows.length, 1);
  assert.equal(rows[0].verification.result, 'NotRun');
  assert.equal(nightVerified(rows), false);
  assert.equal(nightVerified([{ verification: { passed: false } }]), false);
  assert.equal(nightVerified([{ verification: { passed: true } }]), true);
});
test('old session backup blocks later sessions and restored snapshots do not', () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'night-backup-'));
  try {
    const nested = path.join(dir, 'old-session/cycle-1/unity'); fs.mkdirSync(nested, { recursive: true });
    fs.writeFileSync(path.join(nested, 'playerprefs-backup.clixml'), 'backup');
    assert.equal(findPendingBackups(dir).length, 1);
    fs.renameSync(path.join(nested, 'playerprefs-backup.clixml'), path.join(nested, 'playerprefs-restored.clixml'));
    assert.equal(findPendingBackups(dir).length, 0);
  } finally { fs.rmSync(dir, { recursive: true, force: true }); }
});
test('lock excludes concurrent entry and is released even if initialization fails', async () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'night-lock-')), lock = path.join(dir, 'session.lock');
  try {
    await assert.rejects(withSessionLock(lock, async () => {
      await assert.rejects(withSessionLock(lock, async () => assert.fail('second entry')), /EEXIST/);
      throw new Error('policy initialization failed');
    }), /policy initialization failed/);
    assert.equal(fs.existsSync(lock), false);
  } finally { fs.rmSync(dir, { recursive: true, force: true }); }
});
test('working content changes invalidate fingerprint without a commit', () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'night-fingerprint-'));
  try {
    execFileSync('git', ['init', '--quiet', dir], { windowsHide: true });
    fs.mkdirSync(path.join(dir, 'Assets'));
    const file = path.join(dir, 'Assets/prototype.cs'); fs.writeFileSync(file, 'before');
    const before = workspaceFingerprint(dir);
    fs.writeFileSync(file, 'after');
    assert.notEqual(workspaceFingerprint(dir), before);
  } finally { fs.rmSync(dir, { recursive: true, force: true }); }
});
test('a hanging process is killed on timeout and on STOP', async () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'night-process-'));
  try {
    for (const [name, options] of [['timeout', { timeoutMs: 50 }], ['stop', { timeoutMs: 10000, stopped: () => true }]])
      await assert.rejects(runProcess(process.execPath, ['-e', 'setInterval(()=>{},10000)'], { cwd: dir, prefix: path.join(dir, name), ...options }), /timeout|STOP/);
  } finally { fs.rmSync(dir, { recursive: true, force: true }); }
});

test('a task stop proposal cannot end the session without controller approval', async () => {
  const worked = [];
  const rows = await cycleLoop({ maxCycles: 3, deadline: Date.now() + 10000, stopped: () => false,
    decide: async (_history, index) => index === 0 ? { action: 'stop', task: 'one test timed out' } : { action: 'work', task: 'independent lab work' },
    work: async decision => { worked.push(decision.task); return {}; }, verify: async () => ({ passed: true }) });
  assert.equal(worked.length, 2);
  assert.equal(rows[0].verification.result, 'StopRejected');
});
test('recoverable child failures retain diagnostics and dispatch another task', async () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'night-recovery-'));
  try {
    const rows = await cycleLoop({ maxCycles: 2, deadline: Date.now() + 10000, stopped: () => false,
      decide: async (_history, index) => ({ task: `work-${index}`, issueKey: `issue-${index}` }),
      work: async (_decision, index) => {
        const result = await runProcess(process.execPath, ['-e', `process.exit(${index === 0 ? 3 : 0})`], { cwd: dir, prefix: path.join(dir, `child-${index}`), timeoutMs: 2000 });
        if (result.exitCode) throw Object.assign(new Error('ordinary child task failed'), { recoverable: true, result: 'WorkerFailed', details: result });
        return result;
      }, verify: async () => ({ passed: true, executed: 1 }) });
    assert.equal(rows.length, 2);
    assert.equal(rows[0].verification.passed, false);
    assert.equal(rows[0].verification.result, 'WorkerFailed');
    assert.equal(rows[1].verification.passed, true);
  } finally { fs.rmSync(dir, { recursive: true, force: true }); }
});
test('two failed attempts block only their issue and independent work continues', async () => {
  const worked = [];
  const rows = await cycleLoop({ maxCycles: 4, deadline: Date.now() + 10000, stopped: () => false,
    decide: async (_history, index) => ({ task: `attempt-${index}`, issueKey: index < 3 ? 'broken' : 'independent' }),
    work: async decision => { worked.push(decision.issueKey); return {}; },
    verify: async decision => ({ passed: decision.issueKey === 'independent', result: decision.issueKey === 'independent' ? 'Passed' : 'Failed' }) });
  assert.deepEqual(worked, ['broken', 'broken', 'independent']);
  assert.equal(rows[1].blocked, true);
  assert.equal(rows[2].verification.result, 'BlockedIssue');
  assert.equal(rows[3].verification.passed, true);
});
test('the final fifteen minutes prohibit normal task dispatch', async () => {
  const rows = await cycleLoop({ maxCycles: 2, deadline: Date.now() + 14 * 60000, reserveMs: 15 * 60000, stopped: () => false,
    decide: () => assert.fail('long task must not start in cleanup reserve') });
  assert.equal(rows.length, 0);
});
test('real recovery smoke survives child exit, timeout and rejected stop with original FAILs intact', async () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'night-smoke-'));
  try {
    const rows = await recoverySmoke(dir);
    assert.deepEqual(rows.map(r => r.verification.result), ['WorkerFailed', 'ProcessTimeout', 'StopRejected', 'ControllerProbePassed']);
    assert.equal(fs.readFileSync(rows[3].work.stdout, 'utf8'), 'recovered');
    assert.equal(JSON.parse(fs.readFileSync(path.join(dir, 'recovery-probe.json'))).length, 4);
  } finally { fs.rmSync(dir, { recursive: true, force: true }); }
});
test('quota failure is fatal but an ordinary child error remains recoverable', () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'night-quota-'));
  try {
    const stderr = path.join(dir, 'stderr.log'), output = path.join(dir, 'missing.json');
    fs.writeFileSync(stderr, 'ordinary task error');
    assert.throws(() => checkTaskProcess({ exitCode: 1, stderr }, output, 'Worker'), error => error.recoverable === true);
    fs.writeFileSync(stderr, 'You have hit your usage limit');
    assert.throws(() => checkTaskProcess({ exitCode: 1, stderr }, output, 'Worker'), error => !error.recoverable && /quota/.test(error.message));
  } finally { fs.rmSync(dir, { recursive: true, force: true }); }
});
test('an explicit morning cutoff shortens the original session and cannot extend its budget', () => {
  const start = '2026-10-27T22:47:35.881Z', now = Date.parse('2026-10-27T23:00:00Z');
  assert.equal(sessionDeadline(start, 5, '2026-10-28T06:00:00+03:00', now), Date.parse('2026-10-28T03:00:00Z'));
  assert.equal(sessionDeadline(start, 5, '2026-10-28T08:00:00+03:00', now), Date.parse('2026-10-28T03:47:35.881Z'));
  assert.throws(() => sessionDeadline(start, 5, 'not a date', now), /deadline/);
  assert.throws(() => sessionDeadline(start, 5, '2026-10-27T22:00:00Z', now), /deadline/);
});
