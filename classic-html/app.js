(function () {
    const STORAGE_KEY = 'classic_html_tetris_v1';
    const TARGET_LINES = 40;
    const HIDDEN_ROWS = 4;
    const VISIBLE_ROWS = 20;
    const COLS = 10;
    const GRAVITY_MS = 700;
    const COUNTDOWN_MS = 1200;

    const boardEl = document.getElementById('board');
    const overlayEl = document.getElementById('overlay');
    const timerEl = document.getElementById('timer');
    const remainingEl = document.getElementById('remaining');
    const linesEl = document.getElementById('lines');
    const holdEl = document.getElementById('hold');
    const nextQueueEl = document.getElementById('nextQueue');
    const leaderboardEl = document.getElementById('leaderboard');
    const statusTitleEl = document.getElementById('statusTitle');
    const statusMessageEl = document.getElementById('statusMessage');
    const nicknamePanelEl = document.getElementById('nicknamePanel');
    const nicknameInputEl = document.getElementById('nicknameInput');
    const saveRecordButtonEl = document.getElementById('saveRecordButton');
    const retryButtonEl = document.getElementById('retryButton');

    const defs = {
        I: [
            [{x:0,y:1},{x:1,y:1},{x:2,y:1},{x:3,y:1}],
            [{x:2,y:0},{x:2,y:1},{x:2,y:2},{x:2,y:3}],
            [{x:0,y:2},{x:1,y:2},{x:2,y:2},{x:3,y:2}],
            [{x:1,y:0},{x:1,y:1},{x:1,y:2},{x:1,y:3}]
        ],
        O: [
            [{x:1,y:0},{x:2,y:0},{x:1,y:1},{x:2,y:1}],
            [{x:1,y:0},{x:2,y:0},{x:1,y:1},{x:2,y:1}],
            [{x:1,y:0},{x:2,y:0},{x:1,y:1},{x:2,y:1}],
            [{x:1,y:0},{x:2,y:0},{x:1,y:1},{x:2,y:1}]
        ],
        T: [
            [{x:1,y:0},{x:0,y:1},{x:1,y:1},{x:2,y:1}],
            [{x:1,y:0},{x:1,y:1},{x:2,y:1},{x:1,y:2}],
            [{x:0,y:1},{x:1,y:1},{x:2,y:1},{x:1,y:2}],
            [{x:1,y:0},{x:0,y:1},{x:1,y:1},{x:1,y:2}]
        ],
        S: [
            [{x:1,y:0},{x:2,y:0},{x:0,y:1},{x:1,y:1}],
            [{x:1,y:0},{x:1,y:1},{x:2,y:1},{x:2,y:2}],
            [{x:1,y:1},{x:2,y:1},{x:0,y:2},{x:1,y:2}],
            [{x:0,y:0},{x:0,y:1},{x:1,y:1},{x:1,y:2}]
        ],
        Z: [
            [{x:0,y:0},{x:1,y:0},{x:1,y:1},{x:2,y:1}],
            [{x:2,y:0},{x:1,y:1},{x:2,y:1},{x:1,y:2}],
            [{x:0,y:1},{x:1,y:1},{x:1,y:2},{x:2,y:2}],
            [{x:1,y:0},{x:0,y:1},{x:1,y:1},{x:0,y:2}]
        ],
        J: [
            [{x:0,y:0},{x:0,y:1},{x:1,y:1},{x:2,y:1}],
            [{x:1,y:0},{x:2,y:0},{x:1,y:1},{x:1,y:2}],
            [{x:0,y:1},{x:1,y:1},{x:2,y:1},{x:2,y:2}],
            [{x:1,y:0},{x:1,y:1},{x:0,y:2},{x:1,y:2}]
        ],
        L: [
            [{x:2,y:0},{x:0,y:1},{x:1,y:1},{x:2,y:1}],
            [{x:1,y:0},{x:1,y:1},{x:1,y:2},{x:2,y:2}],
            [{x:0,y:1},{x:1,y:1},{x:2,y:1},{x:0,y:2}],
            [{x:0,y:0},{x:1,y:0},{x:1,y:1},{x:1,y:2}]
        ]
    };

    const kicks = {
        JLSTZ: {
            '0>1': [[0,0],[-1,0],[-1,1],[0,-2],[-1,-2]],
            '1>0': [[0,0],[1,0],[1,-1],[0,2],[1,2]],
            '1>2': [[0,0],[1,0],[1,-1],[0,2],[1,2]],
            '2>1': [[0,0],[-1,0],[-1,1],[0,-2],[-1,-2]],
            '2>3': [[0,0],[1,0],[1,1],[0,-2],[1,-2]],
            '3>2': [[0,0],[-1,0],[-1,-1],[0,2],[-1,2]],
            '3>0': [[0,0],[-1,0],[-1,-1],[0,2],[-1,2]],
            '0>3': [[0,0],[1,0],[1,1],[0,-2],[1,-2]]
        },
        I: {
            '0>1': [[0,0],[-2,0],[1,0],[-2,-1],[1,2]],
            '1>0': [[0,0],[2,0],[-1,0],[2,1],[-1,-2]],
            '1>2': [[0,0],[-1,0],[2,0],[-1,2],[2,-1]],
            '2>1': [[0,0],[1,0],[-2,0],[1,-2],[-2,1]],
            '2>3': [[0,0],[2,0],[-1,0],[2,1],[-1,-2]],
            '3>2': [[0,0],[-2,0],[1,0],[-2,-1],[1,2]],
            '3>0': [[0,0],[1,0],[-2,0],[1,-2],[-2,1]],
            '0>3': [[0,0],[-1,0],[2,0],[-1,2],[2,-1]]
        }
    };

    const scoreTable = {1:100, 2:300, 3:500, 4:800};
    const keymap = {
        ArrowLeft: 'left',
        ArrowRight: 'right',
        ArrowDown: 'soft',
        Space: 'hard',
        KeyZ: 'ccw',
        KeyX: 'cw',
        KeyC: 'flip',
        ShiftLeft: 'hold',
        ShiftRight: 'hold',
        KeyR: 'retry'
    };

    function loadStorage() {
        try {
            const raw = localStorage.getItem(STORAGE_KEY);
            if (!raw) return { sprint: [], arcade: [], nickname: '' };
            const parsed = JSON.parse(raw);
            return {
                sprint: Array.isArray(parsed.sprint) ? parsed.sprint : [],
                arcade: Array.isArray(parsed.arcade) ? parsed.arcade : [],
                nickname: typeof parsed.nickname === 'string' ? parsed.nickname : ''
            };
        } catch {
            return { sprint: [], arcade: [], nickname: '' };
        }
    }

    const storage = loadStorage();

    function saveStorage() {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(storage));
    }

    function createBoard() {
        return Array.from({ length: VISIBLE_ROWS + HIDDEN_ROWS }, () => Array(COLS).fill(''));
    }

    function newPiece(type) {
        return { type, rotation: 0, x: 3, y: 0, id: 0 };
    }

    function shuffleBag(opening) {
        const pieces = opening ? ['I','J','L','T','O','S','Z'] : ['I','O','T','S','Z','J','L'];
        if (opening) {
            const firstPool = ['I','J','L','T'];
            const first = firstPool[Math.floor(Math.random() * firstPool.length)];
            const rest = pieces.filter(piece => piece !== first).sort(() => Math.random() - 0.5);
            return [first, ...rest];
        }
        return pieces.sort(() => Math.random() - 0.5);
    }

    const state = {
        board: createBoard(),
        active: null,
        hold: '',
        holdUsed: false,
        queue: [],
        hasSpawnedAny: false,
        pieceIdSeed: 0,
        lines: 0,
        score: 0,
        pieces: 0,
        startTime: 0,
        completedTime: 0,
        countdownUntil: 0,
        lastGravity: 0,
        sprintComplete: false,
        gameOver: false,
        pendingNickname: null
    };

    function ensureQueue() {
        while (state.queue.length < 7) {
            state.queue.push(...shuffleBag(!state.hasSpawnedAny && state.queue.length === 0));
        }
    }

    function spawnPiece(forceType) {
        ensureQueue();
        const type = forceType || state.queue.shift();
        const piece = newPiece(type);
        piece.id = ++state.pieceIdSeed;
        if (!isValid(piece)) {
            state.active = null;
            state.gameOver = true;
            state.pendingNickname = maybeQualifyRecord();
            return false;
        }
        state.active = piece;
        state.holdUsed = false;
        state.hasSpawnedAny = true;
        ensureQueue();
        return true;
    }

    function resetGame() {
        state.board = createBoard();
        state.active = null;
        state.hold = '';
        state.holdUsed = false;
        state.queue = [];
        state.hasSpawnedAny = false;
        state.lines = 0;
        state.score = 0;
        state.pieces = 0;
        state.startTime = 0;
        state.completedTime = 0;
        state.countdownUntil = performance.now() + COUNTDOWN_MS;
        state.lastGravity = 0;
        state.sprintComplete = false;
        state.gameOver = false;
        state.pendingNickname = null;
        spawnPiece();
    }

    function getCells(piece) {
        return defs[piece.type][piece.rotation].map(cell => ({ x: piece.x + cell.x, y: piece.y + cell.y }));
    }

    function isValid(piece) {
        return getCells(piece).every(cell =>
            cell.x >= 0 &&
            cell.x < COLS &&
            cell.y < VISIBLE_ROWS + HIDDEN_ROWS &&
            (cell.y < 0 || !state.board[cell.y][cell.x])
        );
    }

    function move(dx, dy) {
        if (!state.active || state.gameOver) return false;
        const next = { ...state.active, x: state.active.x + dx, y: state.active.y + dy };
        if (isValid(next)) {
            state.active = next;
            return true;
        }
        return false;
    }

    function rotate(step, useKicks) {
        if (!state.active || state.gameOver) return false;
        const from = state.active.rotation;
        const to = (from + step + 4) % 4;
        const next = { ...state.active, rotation: to };
        if (!useKicks) {
            if (isValid(next)) {
                state.active = next;
                return true;
            }
            return false;
        }
        const table = state.active.type === 'I' ? kicks.I : kicks.JLSTZ;
        const offsets = table[`${from}>${to}`] || [[0,0]];
        for (const [ox, oy] of offsets) {
            const kicked = { ...next, x: next.x + ox, y: next.y - oy };
            if (isValid(kicked)) {
                state.active = kicked;
                return true;
            }
        }
        return false;
    }

    function clearLines() {
        let cleared = 0;
        const newRows = [];
        for (let y = 0; y < state.board.length; y += 1) {
            if (state.board[y].every(Boolean)) {
                cleared += 1;
            } else {
                newRows.push([...state.board[y]]);
            }
        }
        while (newRows.length < state.board.length) {
            newRows.unshift(Array(COLS).fill(''));
        }
        state.board = newRows;
        if (cleared) {
            state.lines += cleared;
            state.score += scoreTable[Math.min(cleared, 4)] || 0;
            if (state.lines >= TARGET_LINES) {
                state.sprintComplete = true;
                state.gameOver = true;
                state.completedTime = performance.now();
                state.pendingNickname = maybeQualifyRecord();
            }
        }
    }

    function lockPiece() {
        getCells(state.active).forEach(cell => {
            if (cell.y >= 0) {
                state.board[cell.y][cell.x] = state.active.type;
            }
        });
        state.active = null;
        state.pieces += 1;
        clearLines();
        if (!state.sprintComplete) {
            spawnPiece();
        }
    }

    function hardDrop() {
        while (move(0, 1)) {}
        lockPiece();
    }

    function dropOnce(soft) {
        if (move(0, 1)) {
            if (soft) state.score += 1;
            return;
        }
        lockPiece();
    }

    function hold() {
        if (!state.active || state.holdUsed) return;
        const current = state.active.type;
        if (state.hold) {
            const swap = state.hold;
            state.hold = current;
            const piece = newPiece(swap);
            piece.id = ++state.pieceIdSeed;
            if (!isValid(piece)) {
                state.gameOver = true;
                state.pendingNickname = maybeQualifyRecord();
                return;
            }
            state.active = piece;
        } else {
            state.hold = current;
            state.active = null;
            spawnPiece();
        }
        state.holdUsed = true;
    }

    function getGhostCells() {
        if (!state.active) return [];
        let ghost = { ...state.active };
        while (isValid({ ...ghost, y: ghost.y + 1 })) {
            ghost = { ...ghost, y: ghost.y + 1 };
        }
        return getCells(ghost);
    }

    function getElapsed() {
        if (!state.startTime) return 0;
        return Math.floor((state.completedTime || performance.now()) - state.startTime);
    }

    function maybeQualifyRecord() {
        const timeMs = getElapsed();
        const entries = storage.sprint.slice().sort((a, b) => a.timeMs - b.timeMs);
        if (entries.length < 10 || timeMs < entries[entries.length - 1].timeMs) {
            return { timeMs, lines: state.lines, pieces: state.pieces };
        }
        return null;
    }

    function saveNicknameRecord() {
        if (!state.pendingNickname) return;
        const nickname = (nicknameInputEl.value.trim() || storage.nickname || 'PLAYER').slice(0, 12);
        storage.nickname = nickname;
        storage.sprint.push({
            nickname,
            timeMs: state.pendingNickname.timeMs,
            lines: state.pendingNickname.lines,
            pieces: state.pendingNickname.pieces,
            timestamp: new Date().toISOString()
        });
        storage.sprint.sort((a, b) => a.timeMs - b.timeMs);
        storage.sprint = storage.sprint.slice(0, 10);
        saveStorage();
        state.pendingNickname = null;
        render();
    }

    function formatTime(ms) {
        const minutes = Math.floor(ms / 60000);
        const seconds = Math.floor((ms % 60000) / 1000);
        const millis = Math.floor(ms % 1000);
        return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}.${String(millis).padStart(3, '0')}`;
    }

    function renderBoard() {
        const rows = state.board.slice(HIDDEN_ROWS).map(row => [...row]);
        getGhostCells().forEach(cell => {
            if (cell.y >= HIDDEN_ROWS && !rows[cell.y - HIDDEN_ROWS][cell.x]) {
                rows[cell.y - HIDDEN_ROWS][cell.x] = `ghost-${state.active.type}`;
            }
        });
        if (state.active) {
            getCells(state.active).forEach(cell => {
                if (cell.y >= HIDDEN_ROWS) {
                    rows[cell.y - HIDDEN_ROWS][cell.x] = state.active.type;
                }
            });
        }

        const cellCount = rows.length * rows[0].length;
        if (boardEl.children.length !== cellCount) {
            boardEl.innerHTML = '';
            boardEl.style.gridTemplateColumns = `repeat(${COLS}, 1fr)`;
            for (let i = 0; i < cellCount; i += 1) {
                const cell = document.createElement('div');
                cell.className = 'cell';
                boardEl.appendChild(cell);
            }
        }

        rows.flat().forEach((value, index) => {
            const cell = boardEl.children[index];
            cell.className = 'cell';
            if (!value) return;
            if (value.startsWith('ghost-')) {
                cell.classList.add('ghost', `piece-${value.replace('ghost-', '').toLowerCase()}`);
                return;
            }
            cell.classList.add(`piece-${value.toLowerCase()}`);
        });
    }

    function render() {
        renderBoard();
        timerEl.textContent = formatTime(getElapsed());
        remainingEl.textContent = String(Math.max(0, TARGET_LINES - state.lines));
        linesEl.textContent = String(state.lines);
        holdEl.textContent = state.hold || '-';
        holdEl.className = `piece-chip${state.hold ? ` piece-${state.hold.toLowerCase()}` : ''}`;
        nextQueueEl.innerHTML = '';
        state.queue.slice(0, 5).forEach(piece => {
            const item = document.createElement('li');
            item.className = `piece-chip piece-${piece.toLowerCase()}`;
            item.textContent = piece;
            nextQueueEl.appendChild(item);
        });

        leaderboardEl.innerHTML = '';
        if (!storage.sprint.length) {
            const item = document.createElement('li');
            item.textContent = 'No sprint records yet.';
            leaderboardEl.appendChild(item);
        } else {
            storage.sprint.forEach((entry, index) => {
                const item = document.createElement('li');
                item.textContent = `${index + 1}. ${entry.nickname} - ${formatTime(entry.timeMs)}`;
                leaderboardEl.appendChild(item);
            });
        }

        if (!state.startTime && !state.gameOver) {
            const remaining = Math.ceil(Math.max(0, state.countdownUntil - performance.now()) / 1000);
            overlayEl.textContent = remaining > 0 ? String(remaining) : 'GO';
            overlayEl.classList.remove('hidden');
            statusTitleEl.textContent = 'Get Ready';
            statusMessageEl.textContent = 'The sprint starts after the countdown.';
        } else if (state.sprintComplete) {
            overlayEl.textContent = 'CLEAR';
            overlayEl.classList.remove('hidden');
            statusTitleEl.textContent = 'Sprint Complete';
            statusMessageEl.textContent = `Finished in ${formatTime(getElapsed())}.`;
        } else if (state.gameOver) {
            overlayEl.textContent = 'TOP OUT';
            overlayEl.classList.remove('hidden');
            statusTitleEl.textContent = 'Run Over';
            statusMessageEl.textContent = 'You topped out before 40 lines.';
        } else {
            overlayEl.classList.add('hidden');
            statusTitleEl.textContent = 'Classic Sprint';
            statusMessageEl.textContent = 'Clear 40 lines as fast as possible.';
        }

        nicknamePanelEl.classList.toggle('hidden', !state.pendingNickname);
        if (state.pendingNickname) {
            nicknameInputEl.value = storage.nickname || '';
        }
    }

    function tick(now) {
        if (!state.startTime && now >= state.countdownUntil) {
            state.startTime = state.countdownUntil;
            state.lastGravity = state.startTime;
        }
        if (state.startTime && !state.gameOver && now - state.lastGravity >= GRAVITY_MS) {
            dropOnce(false);
            state.lastGravity += GRAVITY_MS;
        }
        render();
        requestAnimationFrame(tick);
    }

    document.addEventListener('keydown', event => {
        const action = keymap[event.code];
        if (!action) return;
        event.preventDefault();
        if (action === 'retry') {
            resetGame();
            return;
        }
        if (!state.startTime || state.gameOver) return;
        switch (action) {
            case 'left': move(-1, 0); break;
            case 'right': move(1, 0); break;
            case 'soft': dropOnce(true); break;
            case 'hard': hardDrop(); break;
            case 'ccw': rotate(-1, true); break;
            case 'cw': rotate(1, true); break;
            case 'flip': rotate(2, false); break;
            case 'hold': hold(); break;
        }
        render();
    });

    retryButtonEl.addEventListener('click', resetGame);
    saveRecordButtonEl.addEventListener('click', saveNicknameRecord);

    resetGame();
    render();
    requestAnimationFrame(tick);
})();
