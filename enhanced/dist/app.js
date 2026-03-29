(function () {
    const STORAGE_KEY = 'eris_preview_tetris_v1';
    const TARGET_LINES = 40;
    const HIDDEN_ROWS = 4;
    const VISIBLE_ROWS = 20;
    const COLS = 10;
    const GRAVITY_MS = 650;
    const COUNTDOWN_MS = 1000;

    const boardEl = document.getElementById('board');
    const overlayEl = document.getElementById('overlay');
    const timerEl = document.getElementById('timer');
    const remainingEl = document.getElementById('remaining');
    const linesEl = document.getElementById('lines');
    const holdEl = document.getElementById('hold');
    const nextQueueEl = document.getElementById('nextQueue');
    const leaderboardEl = document.getElementById('leaderboard');
    const statusTextEl = document.getElementById('statusText');
    const retryButtonEl = document.getElementById('retryButton');

    const defs = {
        I: [[{x:0,y:1},{x:1,y:1},{x:2,y:1},{x:3,y:1}],[{x:2,y:0},{x:2,y:1},{x:2,y:2},{x:2,y:3}],[{x:0,y:2},{x:1,y:2},{x:2,y:2},{x:3,y:2}],[{x:1,y:0},{x:1,y:1},{x:1,y:2},{x:1,y:3}]],
        O: [[{x:1,y:0},{x:2,y:0},{x:1,y:1},{x:2,y:1}],[{x:1,y:0},{x:2,y:0},{x:1,y:1},{x:2,y:1}],[{x:1,y:0},{x:2,y:0},{x:1,y:1},{x:2,y:1}],[{x:1,y:0},{x:2,y:0},{x:1,y:1},{x:2,y:1}]],
        T: [[{x:1,y:0},{x:0,y:1},{x:1,y:1},{x:2,y:1}],[{x:1,y:0},{x:1,y:1},{x:2,y:1},{x:1,y:2}],[{x:0,y:1},{x:1,y:1},{x:2,y:1},{x:1,y:2}],[{x:1,y:0},{x:0,y:1},{x:1,y:1},{x:1,y:2}]],
        S: [[{x:1,y:0},{x:2,y:0},{x:0,y:1},{x:1,y:1}],[{x:1,y:0},{x:1,y:1},{x:2,y:1},{x:2,y:2}],[{x:1,y:1},{x:2,y:1},{x:0,y:2},{x:1,y:2}],[{x:0,y:0},{x:0,y:1},{x:1,y:1},{x:1,y:2}]],
        Z: [[{x:0,y:0},{x:1,y:0},{x:1,y:1},{x:2,y:1}],[{x:2,y:0},{x:1,y:1},{x:2,y:1},{x:1,y:2}],[{x:0,y:1},{x:1,y:1},{x:1,y:2},{x:2,y:2}],[{x:1,y:0},{x:0,y:1},{x:1,y:1},{x:0,y:2}]],
        J: [[{x:0,y:0},{x:0,y:1},{x:1,y:1},{x:2,y:1}],[{x:1,y:0},{x:2,y:0},{x:1,y:1},{x:1,y:2}],[{x:0,y:1},{x:1,y:1},{x:2,y:1},{x:2,y:2}],[{x:1,y:0},{x:1,y:1},{x:0,y:2},{x:1,y:2}]],
        L: [[{x:2,y:0},{x:0,y:1},{x:1,y:1},{x:2,y:1}],[{x:1,y:0},{x:1,y:1},{x:1,y:2},{x:2,y:2}],[{x:0,y:1},{x:1,y:1},{x:2,y:1},{x:0,y:2}],[{x:0,y:0},{x:1,y:0},{x:1,y:1},{x:1,y:2}]]
    };

    const kicks = {
        JLSTZ: {'0>1':[[0,0],[-1,0],[-1,1],[0,-2],[-1,-2]],'1>0':[[0,0],[1,0],[1,-1],[0,2],[1,2]],'1>2':[[0,0],[1,0],[1,-1],[0,2],[1,2]],'2>1':[[0,0],[-1,0],[-1,1],[0,-2],[-1,-2]],'2>3':[[0,0],[1,0],[1,1],[0,-2],[1,-2]],'3>2':[[0,0],[-1,0],[-1,-1],[0,2],[-1,2]],'3>0':[[0,0],[-1,0],[-1,-1],[0,2],[-1,2]],'0>3':[[0,0],[1,0],[1,1],[0,-2],[1,-2]]},
        I: {'0>1':[[0,0],[-2,0],[1,0],[-2,-1],[1,2]],'1>0':[[0,0],[2,0],[-1,0],[2,1],[-1,-2]],'1>2':[[0,0],[-1,0],[2,0],[-1,2],[2,-1]],'2>1':[[0,0],[1,0],[-2,0],[1,-2],[-2,1]],'2>3':[[0,0],[2,0],[-1,0],[2,1],[-1,-2]],'3>2':[[0,0],[-2,0],[1,0],[-2,-1],[1,2]],'3>0':[[0,0],[1,0],[-2,0],[1,-2],[-2,1]],'0>3':[[0,0],[-1,0],[2,0],[-1,2],[2,-1]]}
    };

    const keymap = { ArrowLeft:'left', ArrowRight:'right', ArrowDown:'soft', Space:'hard', KeyZ:'ccw', KeyX:'cw', KeyC:'flip', ShiftLeft:'hold', ShiftRight:'hold', KeyR:'retry' };

    function loadStorage() {
        try {
            const parsed = JSON.parse(localStorage.getItem(STORAGE_KEY) || '{}');
            return { sprint: Array.isArray(parsed.sprint) ? parsed.sprint : [] };
        } catch {
            return { sprint: [] };
        }
    }

    const storage = loadStorage();
    const state = { board:[], active:null, hold:'', holdUsed:false, queue:[], hasSpawned:false, lines:0, startTime:0, completedTime:0, countdownUntil:0, lastGravity:0, sprintComplete:false, gameOver:false };

    function saveStorage() { localStorage.setItem(STORAGE_KEY, JSON.stringify(storage)); }
    function createBoard() { return Array.from({ length: VISIBLE_ROWS + HIDDEN_ROWS }, () => Array(COLS).fill('')); }
    function newPiece(type) { return { type, rotation:0, x:3, y:0 }; }
    function shuffleBag(opening) { const all = ['I','O','T','S','Z','J','L']; if (!opening) return all.sort(() => Math.random() - 0.5); const firstPool = ['I','J','L','T']; const first = firstPool[Math.floor(Math.random() * firstPool.length)]; return [first, ...all.filter(p => p !== first).sort(() => Math.random() - 0.5)]; }
    function ensureQueue() { while (state.queue.length < 7) state.queue.push(...shuffleBag(!state.hasSpawned && state.queue.length === 0)); }
    function getCells(piece) { return defs[piece.type][piece.rotation].map(cell => ({ x: piece.x + cell.x, y: piece.y + cell.y })); }
    function isValid(piece) { return getCells(piece).every(cell => cell.x >= 0 && cell.x < COLS && cell.y < VISIBLE_ROWS + HIDDEN_ROWS && (cell.y < 0 || !state.board[cell.y][cell.x])); }
    function spawn(forceType) { ensureQueue(); const piece = newPiece(forceType || state.queue.shift()); if (!isValid(piece)) { state.active = null; state.gameOver = true; maybeStoreRecord(); return false; } state.active = piece; state.holdUsed = false; state.hasSpawned = true; ensureQueue(); return true; }
    function reset() { state.board = createBoard(); state.active = null; state.hold = ''; state.holdUsed = false; state.queue = []; state.hasSpawned = false; state.lines = 0; state.startTime = 0; state.completedTime = 0; state.countdownUntil = performance.now() + COUNTDOWN_MS; state.lastGravity = 0; state.sprintComplete = false; state.gameOver = false; spawn(); }
    function move(dx, dy) { if (!state.active || state.gameOver) return false; const next = { ...state.active, x: state.active.x + dx, y: state.active.y + dy }; if (isValid(next)) { state.active = next; return true; } return false; }
    function rotate(step, useKicks) { if (!state.active || state.gameOver) return false; const from = state.active.rotation; const to = (from + step + 4) % 4; const next = { ...state.active, rotation: to }; if (!useKicks) { if (isValid(next)) { state.active = next; return true; } return false; } const table = state.active.type === 'I' ? kicks.I : kicks.JLSTZ; for (const [ox, oy] of (table[`${from}>${to}`] || [[0,0]])) { const kicked = { ...next, x: next.x + ox, y: next.y - oy }; if (isValid(kicked)) { state.active = kicked; return true; } } return false; }
    function clearLines() { let cleared = 0; const nextRows = []; state.board.forEach(row => row.every(Boolean) ? cleared++ : nextRows.push([...row])); while (nextRows.length < state.board.length) nextRows.unshift(Array(COLS).fill('')); state.board = nextRows; state.lines += cleared; if (state.lines >= TARGET_LINES) { state.sprintComplete = true; state.gameOver = true; state.completedTime = performance.now(); maybeStoreRecord(); } }
    function lockPiece() { getCells(state.active).forEach(cell => { if (cell.y >= 0) state.board[cell.y][cell.x] = state.active.type; }); state.active = null; clearLines(); if (!state.sprintComplete) spawn(); }
    function dropOnce() { if (move(0,1)) return; lockPiece(); }
    function hardDrop() { while (move(0,1)) {} lockPiece(); }
    function hold() { if (!state.active || state.holdUsed) return; const current = state.active.type; if (state.hold) { const swap = state.hold; state.hold = current; state.active = newPiece(swap); if (!isValid(state.active)) { state.gameOver = true; maybeStoreRecord(); } } else { state.hold = current; state.active = null; spawn(); } state.holdUsed = true; }
    function getGhostCells() { if (!state.active) return []; let ghost = { ...state.active }; while (isValid({ ...ghost, y: ghost.y + 1 })) ghost = { ...ghost, y: ghost.y + 1 }; return getCells(ghost); }
    function elapsed() { if (!state.startTime) return 0; return Math.floor((state.completedTime || performance.now()) - state.startTime); }
    function formatTime(ms) { const minutes = Math.floor(ms / 60000); const seconds = Math.floor((ms % 60000) / 1000); const millis = Math.floor(ms % 1000); return `${String(minutes).padStart(2,'0')}:${String(seconds).padStart(2,'0')}.${String(millis).padStart(3,'0')}`; }
    function maybeStoreRecord() { if (!state.sprintComplete) return; storage.sprint.push({ nickname:'+E+RIS', timeMs:elapsed(), lines:state.lines, timestamp:new Date().toISOString() }); storage.sprint.sort((a,b) => a.timeMs - b.timeMs); storage.sprint = storage.sprint.slice(0, 10); saveStorage(); }

    function render() {
        const rows = state.board.slice(HIDDEN_ROWS).map(row => [...row]);
        getGhostCells().forEach(cell => { if (cell.y >= HIDDEN_ROWS && !rows[cell.y - HIDDEN_ROWS][cell.x]) rows[cell.y - HIDDEN_ROWS][cell.x] = `ghost-${state.active.type}`; });
        if (state.active) getCells(state.active).forEach(cell => { if (cell.y >= HIDDEN_ROWS) rows[cell.y - HIDDEN_ROWS][cell.x] = state.active.type; });
        const total = rows.length * rows[0].length;
        if (boardEl.children.length !== total) {
            boardEl.innerHTML = '';
            boardEl.style.gridTemplateColumns = `repeat(${COLS}, 1fr)`;
            for (let i = 0; i < total; i++) { const cell = document.createElement('div'); cell.className = 'cell'; boardEl.appendChild(cell); }
        }
        rows.flat().forEach((value, index) => {
            const cell = boardEl.children[index];
            cell.className = 'cell';
            if (!value) return;
            if (value.startsWith('ghost-')) { cell.classList.add('ghost', `piece-${value.replace('ghost-', '').toLowerCase()}`); return; }
            cell.classList.add(`piece-${value.toLowerCase()}`);
        });
        timerEl.textContent = formatTime(elapsed());
        remainingEl.textContent = String(Math.max(0, TARGET_LINES - state.lines));
        linesEl.textContent = String(state.lines);
        holdEl.textContent = state.hold || '-';
        holdEl.className = `piece-chip${state.hold ? ` piece-${state.hold.toLowerCase()}` : ''}`;
        nextQueueEl.innerHTML = '';
        state.queue.slice(0, 5).forEach(piece => { const item = document.createElement('li'); item.className = `piece-chip piece-${piece.toLowerCase()}`; item.textContent = piece; nextQueueEl.appendChild(item); });
        leaderboardEl.innerHTML = '';
        (storage.sprint.length ? storage.sprint : [{ nickname: 'No runs yet', timeMs: 0 }]).forEach((entry, index) => {
            const item = document.createElement('li');
            item.textContent = entry.timeMs ? `${index + 1}. ${entry.nickname} - ${formatTime(entry.timeMs)}` : entry.nickname;
            leaderboardEl.appendChild(item);
        });
        if (!state.startTime && !state.gameOver) {
            const count = Math.ceil(Math.max(0, state.countdownUntil - performance.now()) / 1000);
            overlayEl.textContent = count > 0 ? String(count) : 'GO';
            overlayEl.classList.remove('hidden');
            statusTextEl.textContent = 'Countdown active. +E+RIS sprint preview is ready.';
        } else if (state.sprintComplete) {
            overlayEl.textContent = 'CLEAR';
            overlayEl.classList.remove('hidden');
            statusTextEl.textContent = `Sprint complete in ${formatTime(elapsed())}.`;
        } else if (state.gameOver) {
            overlayEl.textContent = 'TOP OUT';
            overlayEl.classList.remove('hidden');
            statusTextEl.textContent = 'Run ended by top out.';
        } else {
            overlayEl.classList.add('hidden');
            statusTextEl.textContent = 'Sprint preview is live. Clear 40 lines and retry instantly.';
        }
    }

    function tick(now) {
        if (!state.startTime && now >= state.countdownUntil) { state.startTime = state.countdownUntil; state.lastGravity = state.startTime; }
        if (state.startTime && !state.gameOver && now - state.lastGravity >= GRAVITY_MS) { dropOnce(); state.lastGravity += GRAVITY_MS; }
        render();
        requestAnimationFrame(tick);
    }

    document.addEventListener('keydown', (event) => {
        const action = keymap[event.code];
        if (!action) return;
        event.preventDefault();
        if (action === 'retry') { reset(); return; }
        if (!state.startTime || state.gameOver) return;
        if (action === 'left') move(-1, 0);
        else if (action === 'right') move(1, 0);
        else if (action === 'soft') dropOnce();
        else if (action === 'hard') hardDrop();
        else if (action === 'ccw') rotate(-1, true);
        else if (action === 'cw') rotate(1, true);
        else if (action === 'flip') rotate(2, false);
        else if (action === 'hold') hold();
        render();
    });

    retryButtonEl.addEventListener('click', reset);
    reset();
    render();
    requestAnimationFrame(tick);
})();
