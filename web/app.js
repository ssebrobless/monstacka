const boardEl = document.getElementById('board');
const overlayEl = document.getElementById('overlay');
const timerEl = document.getElementById('timer');
const linesRemainingEl = document.getElementById('linesRemaining');
const linesEl = document.getElementById('lines');
const holdEl = document.getElementById('hold');
const nextQueueEl = document.getElementById('nextQueue');
const piecesPlacedEl = document.getElementById('piecesPlaced');
const keyInputsEl = document.getElementById('keyInputs');
const inputsPerPieceEl = document.getElementById('inputsPerPiece');
const currentPieceInputsEl = document.getElementById('currentPieceInputs');
const controlsListEl = document.getElementById('controlsList');
const sprintLeaderboardEl = document.getElementById('sprintLeaderboard');
const statusCardEl = document.getElementById('statusCard');
const statusTitleEl = document.getElementById('statusTitle');
const statusMessageEl = document.getElementById('statusMessage');
const retryButtonEl = document.getElementById('retryButton');
const quitButtonEl = document.getElementById('quitButton');

const state = { latest: null, pollId: null, busy: false };

const keyBindings = {
    ArrowLeft: 'moveLeft',
    ArrowRight: 'moveRight',
    ArrowDown: 'softDrop',
    Space: 'hardDrop',
    KeyZ: 'rotateCcw',
    KeyX: 'rotateCw',
    KeyC: 'rotate180',
    ShiftLeft: 'hold',
    ShiftRight: 'hold',
    KeyQ: 'quit'
};

function formatTime(ms) {
    const totalMs = Math.max(0, ms || 0);
    const minutes = Math.floor(totalMs / 60000);
    const seconds = Math.floor((totalMs % 60000) / 1000);
    const milliseconds = totalMs % 1000;
    return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}.${String(milliseconds).padStart(3, '0')}`;
}

function renderBoard(rows) {
    if (!rows?.length) return;
    const rowCount = rows.length;
    const colCount = rows[0].length;
    const cellCount = rowCount * colCount;

    if (boardEl.children.length !== cellCount) {
        boardEl.innerHTML = '';
        boardEl.style.gridTemplateColumns = `repeat(${colCount}, 1fr)`;
        for (let index = 0; index < cellCount; index += 1) {
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

function renderControls(controls) {
    const pairs = [
        ['Left', controls.left],
        ['Right', controls.right],
        ['Soft Drop', controls.softDrop],
        ['Hard Drop', controls.hardDrop],
        ['Rotate CCW', controls.rotateCcw],
        ['Rotate CW', controls.rotateCw],
        ['Rotate 180', controls.rotate180],
        ['Hold', controls.hold],
        ['Quit', controls.quit]
    ];
    controlsListEl.innerHTML = '';
    pairs.forEach(([label, value]) => {
        const item = document.createElement('li');
        item.textContent = `${label}: ${value}`;
        controlsListEl.appendChild(item);
    });
}

function renderQueue(queue) {
    nextQueueEl.innerHTML = '';
    queue.forEach((piece) => {
        const item = document.createElement('li');
        item.className = `piece-chip piece-${piece.toLowerCase()}`;
        item.textContent = piece;
        nextQueueEl.appendChild(item);
    });
}

function renderSprintLeaderboard(entries) {
    sprintLeaderboardEl.innerHTML = '';
    if (!entries.length) {
        const item = document.createElement('li');
        item.textContent = 'No sprint records yet. Finish a run to set the first time.';
        sprintLeaderboardEl.appendChild(item);
        return;
    }

    entries.forEach((entry, index) => {
        const item = document.createElement('li');
        item.textContent = `${index + 1}. ${formatTime(entry.timeMs)} · ${entry.pieces} pieces · ${entry.keys} keys`;
        sprintLeaderboardEl.appendChild(item);
    });
}

function renderOverlay(current) {
    let text = '';
    if (!current.runStarted && !current.gameOver) {
        const count = Math.ceil((current.countdownRemainingMs || 0) / 1000);
        text = count > 0 ? String(count) : 'GO';
    } else if (current.sprintComplete) {
        text = '40 LINES CLEAR';
    } else if (current.gameOver && !current.sprintComplete) {
        text = 'TOP OUT';
    }

    overlayEl.textContent = text;
    overlayEl.classList.toggle('hidden', !text);
}

function renderStatus(current) {
    const showSummary = current.gameOver || current.gameClosed || !current.runStarted;
    statusCardEl.classList.toggle('muted', !showSummary);

    if (current.gameClosed) {
        statusTitleEl.textContent = 'Session Closed';
        statusMessageEl.textContent = current.finalMessage || 'This game session has ended.';
        retryButtonEl.disabled = true;
        return;
    }

    retryButtonEl.disabled = false;

    if (!current.runStarted && !current.gameOver) {
        statusTitleEl.textContent = current.mode;
        statusMessageEl.textContent = `Countdown running. The sprint starts in ${Math.ceil((current.countdownRemainingMs || 0) / 1000)} second(s).`;
        return;
    }

    if (current.sprintComplete) {
        statusTitleEl.textContent = 'Sprint Complete';
        statusMessageEl.textContent = `Finished in ${formatTime(current.elapsedMs)} with ${current.piecesPlaced} pieces and ${current.keyInputs} inputs.`;
        return;
    }

    if (current.gameOver) {
        statusTitleEl.textContent = 'Run Over';
        statusMessageEl.textContent = current.finalMessage || 'You topped out before 40 lines.';
        return;
    }

    statusTitleEl.textContent = current.mode;
    statusMessageEl.textContent = 'Clear 40 lines as fast as possible. Retry is tuned for quick restarts.';
}

function renderState(current) {
    state.latest = current;
    renderBoard(current.rows);
    timerEl.textContent = formatTime(current.elapsedMs);
    linesRemainingEl.textContent = String(current.linesRemaining);
    linesEl.textContent = String(current.lines);
    holdEl.textContent = current.hold || '-';
    holdEl.className = `piece-chip${current.hold ? ` piece-${current.hold.toLowerCase()}` : ''}`;
    piecesPlacedEl.textContent = String(current.piecesPlaced);
    keyInputsEl.textContent = String(current.keyInputs);
    inputsPerPieceEl.textContent = Number(current.inputsPerPiece || 0).toFixed(2);
    currentPieceInputsEl.textContent = String(current.currentPieceInputs);
    renderControls(current.controls);
    renderQueue(current.nextQueue || []);
    renderSprintLeaderboard(current.sprintLeaderboard || []);
    renderOverlay(current);
    renderStatus(current);
}

async function requestJson(url, options = {}) {
    const response = await fetch(url, {
        headers: { 'Content-Type': 'application/json' },
        ...options
    });
    const data = await response.json().catch(() => ({}));
    if (!response.ok) {
        throw new Error(data.error || `Request failed with ${response.status}`);
    }
    return data;
}

async function pollState() {
    try {
        renderState(await requestJson('/api/state'));
    } catch (error) {
        console.error(error);
    }
}

async function post(url, payload = {}) {
    if (state.busy) return;
    state.busy = true;
    try {
        const current = await requestJson(url, { method: 'POST', body: JSON.stringify(payload) });
        renderState(current);
        if (url === '/api/quit' && state.pollId) {
            window.clearInterval(state.pollId);
            state.pollId = null;
        }
    } catch (error) {
        console.error(error);
    } finally {
        state.busy = false;
    }
}

window.addEventListener('keydown', (event) => {
    const action = keyBindings[event.code];
    if (!action || !state.latest || state.latest.gameClosed) return;

    event.preventDefault();

    if (state.latest.gameOver && action !== 'quit') return;

    if (action === 'quit') {
        post('/api/quit');
        return;
    }

    post('/api/action', { action });
});

retryButtonEl.addEventListener('click', () => {
    post('/api/reset');
    if (!state.pollId) {
        state.pollId = window.setInterval(pollState, 75);
    }
});

quitButtonEl.addEventListener('click', () => post('/api/quit'));

pollState();
state.pollId = window.setInterval(pollState, 75);
