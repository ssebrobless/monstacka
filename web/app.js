const boardEl = document.getElementById('board');
const scoreEl = document.getElementById('score');
const linesEl = document.getElementById('lines');
const holdEl = document.getElementById('hold');
const controlsListEl = document.getElementById('controlsList');
const leaderboardEl = document.getElementById('leaderboard');
const statusCardEl = document.getElementById('statusCard');
const statusTitleEl = document.getElementById('statusTitle');
const statusMessageEl = document.getElementById('statusMessage');
const initialsSectionEl = document.getElementById('initialsSection');
const initialsInputEl = document.getElementById('initialsInput');
const saveScoreButtonEl = document.getElementById('saveScoreButton');
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

function renderBoard(rows) {
    if (!rows?.length) return;
    const rowCount = rows.length;
    const colCount = rows[0].length;
    const cellCount = rowCount * colCount;
    if (boardEl.children.length !== cellCount) {
        boardEl.innerHTML = '';
        boardEl.style.gridTemplateColumns = `repeat(${colCount}, 1fr)`;
        for (let i = 0; i < cellCount; i += 1) {
            const cell = document.createElement('div');
            cell.className = 'cell';
            boardEl.appendChild(cell);
        }
    }
    rows.flat().forEach((value, index) => {
        const cell = boardEl.children[index];
        cell.className = 'cell';
        if (value) cell.classList.add(`piece-${value.toLowerCase()}`);
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

function renderLeaderboard(entries) {
    leaderboardEl.innerHTML = '';
    if (!entries.length) {
        const item = document.createElement('li');
        item.textContent = 'No scores yet. Be the first to set one.';
        leaderboardEl.appendChild(item);
        return;
    }
    entries.forEach((entry, index) => {
        const item = document.createElement('li');
        item.textContent = `${index + 1}. ${entry.initials} - ${entry.score} pts - ${entry.lines} lines`;
        leaderboardEl.appendChild(item);
    });
}

function renderStatus(current) {
    const visible = current.gameOver || current.gameClosed;
    statusCardEl.classList.toggle('hidden', !visible);
    if (!visible) return;
    if (current.gameClosed) {
        statusTitleEl.textContent = 'Session Closed';
        statusMessageEl.textContent = current.finalMessage || 'This session has ended.';
        initialsSectionEl.classList.add('hidden');
        retryButtonEl.disabled = true;
        return;
    }
    statusTitleEl.textContent = 'Game Over';
    statusMessageEl.textContent = `Final score: ${current.score}. Retry, quit, or save initials if you made the top 10.`;
    initialsSectionEl.classList.toggle('hidden', !current.canSubmitHighScore);
    retryButtonEl.disabled = false;
}

function renderState(current) {
    state.latest = current;
    renderBoard(current.rows);
    scoreEl.textContent = String(current.score);
    linesEl.textContent = String(current.lines);
    holdEl.textContent = current.hold || '-';
    renderControls(current.controls);
    renderLeaderboard(current.leaderboard || []);
    renderStatus(current);
}

async function requestJson(url, options = {}) {
    const response = await fetch(url, {
        headers: { 'Content-Type': 'application/json' },
        ...options
    });
    const data = await response.json().catch(() => ({}));
    if (!response.ok) throw new Error(data.error || `Request failed with ${response.status}`);
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
    if (event.target === initialsInputEl) return;
    const action = keyBindings[event.code];
    if (!action || !state.latest || state.latest.gameClosed) return;
    event.preventDefault();
    if (state.latest.gameOver && action !== 'quit') return;
    post(action === 'quit' ? '/api/quit' : '/api/action', action === 'quit' ? {} : { action });
});

saveScoreButtonEl.addEventListener('click', () => post('/api/highscores', { initials: initialsInputEl.value }));
retryButtonEl.addEventListener('click', () => {
    initialsInputEl.value = '';
    post('/api/reset');
    if (!state.pollId) state.pollId = window.setInterval(pollState, 75);
});
quitButtonEl.addEventListener('click', () => post('/api/quit'));
initialsInputEl.addEventListener('input', () => {
    initialsInputEl.value = initialsInputEl.value.toUpperCase().replace(/[^A-Z0-9]/g, '').slice(0, 3);
});

pollState();
state.pollId = window.setInterval(pollState, 75);
