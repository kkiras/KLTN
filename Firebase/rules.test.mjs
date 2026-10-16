import { initializeTestEnvironment, assertSucceeds, assertFails } from '@firebase/rules-unit-testing';
import { doc, setDoc, getDoc, writeBatch, serverTimestamp, Timestamp, collection, query, orderBy, limit, getDocs, updateDoc } from 'firebase/firestore';
import fs from 'fs';

const env = await initializeTestEnvironment({ projectId: 'demo-kltn', firestore: { rules: fs.readFileSync('firestore.rules', 'utf8'), host: '127.0.0.1', port: 8080 } });
let pass = 0, fail = 0;
async function t(name, fn) { try { await fn(); pass++; console.log('ok  ', name); } catch (e) { fail++; console.log('FAIL', name, e.message); } }
const now = Date.now(), H = 3600e3, M = 60e3;
const db = uid => env.authenticatedContext(uid).firestore();
const zero = { coins: 0, silver: 0, schemaVersion: 1 };
const wr = (wins, matches) => matches > 0 ? Math.floor(wins * 10000 / matches) : 0;

function calendar(anchorStartMs, pending) {
  return { anchorSeasonId: 1, anchorStartMs, durationMs: 3 * H, lockMs: 30 * M,
    pendingSeasonId: pending ? pending.id : 0, pendingStartMs: pending ? pending.start : 0,
    pendingDurationMs: pending ? pending.dur : 0, pendingLockMs: pending ? pending.lock : 0 };
}
async function seed(fn) { await env.withSecurityRulesDisabled(async c => fn(c.firestore())); }
await seed(async a => {
  // 3 h seasons, 30 min lock; season 3 = [now - 1h, now + 2h), lock at now + 1.5h.
  await setDoc(doc(a, 'config/season'), calendar(now - H - 2 * 3 * H));
  await setDoc(doc(a, 'users/old'), { ...zero, createdAt: Timestamp.now(), updatedAt: Timestamp.now() });
  await setDoc(doc(a, 'users/vet'), { ...zero, username: 'user222222', createdAt: Timestamp.now(), updatedAt: Timestamp.now() });
  await setDoc(doc(a, 'usernames/user222222'), { uid: 'vet', createdAt: Timestamp.now() });
  await setDoc(doc(a, 'seasons/3/entries/vet'), { username: 'user222222', points: 10, wins: 0, losses: 1, draws: 0, matches: 1, winRate: 0, lastMatchId: 'm0', updatedAt: Timestamp.fromMillis(now - 5 * M) });
  await setDoc(doc(a, 'seasons/3/entries/vet/results/m0'), { result: 'loss', delta: -20, matchStartedAt: Timestamp.fromMillis(now - 20 * M), createdAt: Timestamp.now() });
});

function newUser(d, uid, name) {
  const b = writeBatch(d);
  b.set(doc(d, 'users/' + uid), { ...zero, username: name, createdAt: serverTimestamp(), updatedAt: serverTimestamp() });
  b.set(doc(d, 'usernames/' + name), { uid, createdAt: serverTimestamp() });
  b.set(doc(d, `users/${uid}/ownedCards/MaDa`), { source: 'starter', acquiredAt: serverTimestamp() });
  return b.commit();
}
await t('new user with username', () => assertSucceeds(newUser(db('alice'), 'alice', 'user111111')));
await t('new user without username (old client) still allowed', async () => {
  const d = db('nouser'); const b = writeBatch(d);
  b.set(doc(d, 'users/nouser'), { ...zero, createdAt: serverTimestamp(), updatedAt: serverTimestamp() });
  await assertSucceeds(b.commit());
});
await t('username taken by another uid denied', () => assertFails(newUser(db('bob'), 'bob', 'user111111')));
await t('bad username format denied', () => assertFails(newUser(db('carl'), 'carl', 'admin00001')));
await t('username doc pointing to other uid denied', async () => {
  const d = db('dave'); const b = writeBatch(d);
  b.set(doc(d, 'users/dave'), { ...zero, username: 'user333333', createdAt: serverTimestamp(), updatedAt: serverTimestamp() });
  b.set(doc(d, 'usernames/user333333'), { uid: 'alice', createdAt: serverTimestamp() });
  await assertFails(b.commit());
});
function claim(d, uid, name) {
  const b = writeBatch(d);
  b.update(doc(d, 'users/' + uid), { username: name, updatedAt: serverTimestamp() });
  b.set(doc(d, 'usernames/' + name), { uid, createdAt: serverTimestamp() });
  return b.commit();
}
await t('old user claims username', () => assertSucceeds(claim(db('old'), 'old', 'user444444')));
await t('second claim denied', () => assertFails(claim(db('old'), 'old', 'user555555')));
await t('claim username alone without user update denied', () => assertFails(setDoc(doc(db('zed'), 'usernames/user666666'), { uid: 'zed', createdAt: serverTimestamp() })));

function result(d, uid, season, matchId, res, delta, entry, exists, startedMs) {
  const b = writeBatch(d);
  const e = { username: entry.username, points: entry.points, wins: entry.wins, losses: entry.losses, draws: entry.draws, matches: entry.matches, winRate: entry.winRate ?? wr(entry.wins, entry.matches), lastMatchId: matchId, updatedAt: serverTimestamp() };
  if (exists) b.update(doc(d, `seasons/${season}/entries/${uid}`), e); else b.set(doc(d, `seasons/${season}/entries/${uid}`), e);
  b.set(doc(d, `seasons/${season}/entries/${uid}/results/${matchId}`), { result: res, delta, matchStartedAt: Timestamp.fromMillis(startedMs), createdAt: serverTimestamp() });
  return b.commit();
}
const A = 'user111111';
await t('first ranked win', () => assertSucceeds(result(db('alice'), 'alice', 3, 'm1', 'win', 30, { username: A, points: 30, wins: 1, losses: 0, draws: 0, matches: 1 }, false, now - 10 * M)));
await t('second result inside 60s denied', () => assertFails(result(db('alice'), 'alice', 3, 'm2', 'win', 30, { username: A, points: 60, wins: 2, losses: 0, draws: 0, matches: 2 }, true, now - 5 * M)));
await t('duplicate match id denied', () => assertFails(result(db('vet'), 'vet', 3, 'm0', 'win', 30, { username: 'user222222', points: 40, wins: 1, losses: 1, draws: 0, matches: 2 }, true, now - 10 * M)));
await t('inflated points denied', () => assertFails(result(db('vet'), 'vet', 3, 'mx', 'win', 30, { username: 'user222222', points: 999, wins: 1, losses: 1, draws: 0, matches: 2 }, true, now - 10 * M)));
await t('wrong delta denied', () => assertFails(result(db('vet'), 'vet', 3, 'my', 'win', 50, { username: 'user222222', points: 60, wins: 1, losses: 1, draws: 0, matches: 2 }, true, now - 10 * M)));
await t('wrong season denied', () => assertFails(result(db('vet'), 'vet', 2, 'mz', 'win', 30, { username: 'user222222', points: 30, wins: 1, losses: 0, draws: 0, matches: 1 }, false, now - 10 * M)));
await t('match started after lock denied', () => assertFails(result(db('vet'), 'vet', 3, 'ml', 'win', 30, { username: 'user222222', points: 40, wins: 1, losses: 1, draws: 0, matches: 2 }, true, now + 1.6 * H)));
await t('match started before season denied', () => assertFails(result(db('vet'), 'vet', 3, 'mb', 'win', 30, { username: 'user222222', points: 40, wins: 1, losses: 1, draws: 0, matches: 2 }, true, now - 2 * H)));
await t('fake username denied', () => assertFails(result(db('vet'), 'vet', 3, 'mu', 'win', 30, { username: 'user999999', points: 40, wins: 1, losses: 1, draws: 0, matches: 2 }, true, now - 10 * M)));
await t('writing other player row denied', () => assertFails(result(db('alice'), 'vet', 3, 'mo', 'win', 30, { username: 'user222222', points: 40, wins: 1, losses: 1, draws: 0, matches: 2 }, true, now - 10 * M)));
await t('loss floors at zero (10 - 20 = 0)', () => assertSucceeds(result(db('vet'), 'vet', 3, 'm3', 'loss', -20, { username: 'user222222', points: 0, wins: 0, losses: 2, draws: 0, matches: 2 }, true, now - 10 * M)));
await t('draw after cooldown counts (alice, seeded old updatedAt)', async () => {
  await seed(a => updateDoc(doc(a, 'seasons/3/entries/alice'), { updatedAt: Timestamp.fromMillis(now - 2 * M) }));
  await assertSucceeds(result(db('alice'), 'alice', 3, 'm4', 'draw', 0, { username: A, points: 30, wins: 1, losses: 0, draws: 1, matches: 2 }, true, now - 10 * M));
});
await t('surrender counts as loss', async () => {
  await seed(a => updateDoc(doc(a, 'seasons/3/entries/alice'), { updatedAt: Timestamp.fromMillis(now - 2 * M) }));
  await assertSucceeds(result(db('alice'), 'alice', 3, 'm5', 'surrender', -20, { username: A, points: 10, wins: 1, losses: 1, draws: 1, matches: 3 }, true, now - 10 * M));
});
await t('season ended: write denied', async () => {
  await seed(a => updateDoc(doc(a, 'seasons/3/entries/alice'), { updatedAt: Timestamp.fromMillis(now - 2 * M) }));
  // Shift the calendar 3 h back: season 3 = [now - 4h, now - 1h) has ended, season 4 runs now.
  await seed(a => setDoc(doc(a, 'config/season'), calendar(now - 4 * H - 2 * 3 * H)));
  await assertFails(result(db('alice'), 'alice', 3, 'm6', 'win', 30, { username: A, points: 40, wins: 2, losses: 1, draws: 1, matches: 4 }, true, now - 4.5 * H));
});
await t('next season is open without any tick', () => assertSucceeds(result(db('vet'), 'vet', 4, 'n1', 'win', 30, { username: 'user222222', points: 30, wins: 1, losses: 0, draws: 0, matches: 1 }, false, now - 10 * M)));
await t('match from previous season cannot score in new season', () => assertFails(result(db('alice'), 'alice', 4, 'n2', 'win', 30, { username: A, points: 30, wins: 1, losses: 0, draws: 0, matches: 1 }, false, now - 1.5 * H)));
await t('non-canonical season id denied', () => assertFails(result(db('alice'), 'alice', '04', 'n3', 'win', 30, { username: A, points: 30, wins: 1, losses: 0, draws: 0, matches: 1 }, false, now - 10 * M)));
await t('pending calendar applies from its season (same window: allowed)', async () => {
  await seed(a => setDoc(doc(a, 'config/season'), calendar(now - 4 * H - 2 * 3 * H, { id: 4, start: now - H, dur: 3 * H, lock: 30 * M })));
  await assertSucceeds(result(db('alice'), 'alice', 4, 'p1', 'win', 30, { username: A, points: 30, wins: 1, losses: 0, draws: 0, matches: 1 }, false, now - 10 * M));
});
await t('pending calendar applies from its season (long lock: denied)', async () => {
  await seed(a => updateDoc(doc(a, 'seasons/4/entries/alice'), { updatedAt: Timestamp.fromMillis(now - 2 * M) }));
  await seed(a => setDoc(doc(a, 'config/season'), calendar(now - 4 * H - 2 * 3 * H, { id: 4, start: now - H, dur: 3 * H, lock: 2.9 * H })));
  await assertFails(result(db('alice'), 'alice', 4, 'p2', 'win', 30, { username: A, points: 60, wins: 2, losses: 0, draws: 0, matches: 2 }, true, now - 10 * M));
});
await t('client cannot write config', () => assertFails(setDoc(doc(db('alice'), 'config/season'), { seasonId: 99 })));
await t('client cannot write season doc', () => assertFails(setDoc(doc(db('alice'), 'seasons/3'), { seasonId: 3 })));
await t('signed-in player reads leaderboard page', async () => {
  const snap = await assertSucceeds(getDocs(query(collection(db('bob'), 'seasons/3/entries'), orderBy('points', 'desc'), orderBy('winRate', 'desc'), orderBy('username'), limit(50))));
  if (snap.size !== 2) throw new Error('expected 2 rows, got ' + snap.size);
});
await t('anonymous cannot read leaderboard', () => assertFails(getDocs(collection(env.unauthenticatedContext().firestore(), 'seasons/3/entries'))));
await t('other player cannot read my results', () => assertFails(getDoc(doc(db('bob'), 'seasons/3/entries/alice/results/m1'))));
await t('reads config/season', () => assertSucceeds(getDoc(doc(db('bob'), 'config/season'))));
// Empty rows and the leaderboard order (season 3 running again).
function empty(d, uid, season, over = {}) {
  return setDoc(doc(d, `seasons/${season}/entries/${uid}`), { username: 'user333333', points: 0, wins: 0, losses: 0, draws: 0, matches: 0, winRate: 0, updatedAt: serverTimestamp(), ...over });
}
await seed(async a => {
  await setDoc(doc(a, 'config/season'), calendar(now - H - 2 * 3 * H));
  await setDoc(doc(a, 'users/cara'), { ...zero, username: 'user333333', createdAt: Timestamp.now(), updatedAt: Timestamp.now() });
  await setDoc(doc(a, 'usernames/user333333'), { uid: 'cara', createdAt: Timestamp.now() });
});
await t('empty row with points denied', () => assertFails(empty(db('cara'), 'cara', 3, { points: 10 })));
await t('empty row with win rate denied', () => assertFails(empty(db('cara'), 'cara', 3, { winRate: 5000 })));
await t('empty row with fake username denied', () => assertFails(empty(db('cara'), 'cara', 3, { username: 'user999999' })));
await t('empty row for ended season denied', () => assertFails(empty(db('cara'), 'cara', 2)));
await t('empty row for future season denied', () => assertFails(empty(db('cara'), 'cara', 4)));
await t('empty row for other player denied', () => assertFails(empty(db('bob'), 'cara', 3)));
await t('empty row in running season (0 points, 0 %)', () => assertSucceeds(empty(db('cara'), 'cara', 3)));
await t('empty row cannot be created twice', () => assertFails(empty(db('cara'), 'cara', 3)));
await t('wrong win rate on result denied', async () => {
  await seed(a => updateDoc(doc(a, 'seasons/3/entries/cara'), { updatedAt: Timestamp.fromMillis(now - 2 * M) }));
  await assertFails(result(db('cara'), 'cara', 3, 'c1', 'win', 30, { username: 'user333333', points: 30, wins: 1, losses: 0, draws: 0, matches: 1, winRate: 5000 }, true, now - 10 * M));
});
await t('first win after empty row (win rate 100 %)', () => assertSucceeds(result(db('cara'), 'cara', 3, 'c1', 'win', 30, { username: 'user333333', points: 30, wins: 1, losses: 0, draws: 0, matches: 1 }, true, now - 10 * M)));
await t('page order: points desc, win rate desc, username asc', async () => {
  // cara 30 pts 1/1, alice 10 pts 1/3, vet 0 pts 0/2; add dave 10 pts 1/3 to tie alice on both.
  await seed(a => setDoc(doc(a, 'seasons/3/entries/dave'), { username: 'user000010', points: 10, wins: 1, losses: 2, draws: 0, matches: 3, winRate: wr(1, 3), updatedAt: Timestamp.now() }));
  const snap = await getDocs(query(collection(db('bob'), 'seasons/3/entries'), orderBy('points', 'desc'), orderBy('winRate', 'desc'), orderBy('username'), limit(50)));
  const names = snap.docs.map(x => x.data().username).join(',');
  if (names !== 'user333333,user000010,user111111,user222222') throw new Error(names);
});
await t('economy: reward claim still works for user with username', async () => {
  const d = db('alice'); const b = writeBatch(d);
  const { increment } = await import('firebase/firestore');
  b.update(doc(d, 'users/alice'), { lastRewardMatchId: 'r1', coins: increment(75), silver: increment(1), updatedAt: serverTimestamp() });
  b.set(doc(d, 'users/alice/matchRewards/r1'), { result: 'win', coins: 75, silver: 1, createdAt: serverTimestamp() });
  await assertSucceeds(b.commit());
});
await t('economy: purchase still works', async () => {
  const d = db('alice'); const b = writeBatch(d);
  await seed(a => updateDoc(doc(a, 'users/alice'), { coins: 500 }));
  const { increment } = await import('firebase/firestore');
  b.update(doc(d, 'users/alice'), { lastPurchase: 'MaMatMam', coins: increment(-150), updatedAt: serverTimestamp() });
  b.set(doc(d, 'users/alice/ownedCards/MaMatMam'), { source: 'coins', acquiredAt: serverTimestamp() });
  await assertSucceeds(b.commit());
});
await env.cleanup();
console.log(`\n${pass} passed, ${fail} failed`);
process.exit(fail ? 1 : 0);
