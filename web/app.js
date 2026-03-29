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
const settingsFormEl = document.getElementById('settingsForm');
const dasInputEl = document.getElementById('dasInput');
const arrInputEl = document.getElementById('arrInput');
const dcdInputEl = document.getElementById('dcdInput');
const sdfInputEl = document.getElementById('sdfInput');
const gravityInputEl = document.getElementById('gravityInput');
const countdownInputEl = document.getElementById('countdownInput');
const resetSettingsButtonEl = document.getElementById('resetSettingsButton');
const sprintLeaderboardEl = document.getElementById('sprintLeaderboard');
const statusCardEl = document.getElementById('statusCard');
const statusTitleEl = document.getElementById('statusTitle');
const statusMessageEl = document.getElementById('statusMessage');
const retryButtonEl = document.getElementById('retryButton');
const quitButtonEl = document.getElementById('quitButton');

const state = { latest: null, pollId: null, actionChain: Promise.resolve() };

const SETTINGS_KEY = 'pwrsh_tetris_handling_v1';
const DEFAULT_SETTINGS = {
    dasMs: 100,
    arrMs: 0,
    dcdMs: 50,
    sdf: 20,
    gravityMs: 700,
    countdownMs: 1200
};

const inputState = {
    pressed: new Map(),
    horizontalDirection: null,
    horizontalTimerId: null,
    horizontalPauseUntil: 0,
    softDropTimerId: null,
    lastPieceId: 0
};

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

function loadSettings() {
    try {
        const raw = window.localStorage.getItem(SETTINGS_KEY);
        if (!raw) return { ...DEFAULT_SETTINGS };
        return { ...DEFAULT_SETTINGS, ...JSON.parse(raw) };
    } catch (error) {
        console.error(error);
        return { ...DEFAULT_SETTINGS };
    }
}

function saveSettings(settings) {
    window.localStorage.setItem(SETTINGS_KEY, JSON.stringify(settings));
}

const settings = loadSettings();

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

function renderSettings(current) {
    if (settingsFormEl.contains(document.activeElement)) {
        return;
    }
    const gravityMs = current?.timing?.gravityMs ?? settings.gravityMs;
    const countdownMs = current?.timing?.countdownMs ?? settings.countdownMs;
    dasInputEl.value = String(settings.dasMs);
    arrInputEl.value = String(settings.arrMs);
    dcdInputEl.value = String(settings.dcdMs);
    sdfInputEl.value = String(settings.sdf);
    gravityInputEl.value = String(gravityMs);
    countdownInputEl.value = String(countdownMs);
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
    const pieceChanged = current.activePieceId !== inputState.lastPieceId;
    if (pieceChanged) {
        inputState.lastPieceId = current.activePieceId;
        if (inputState.horizontalDirection) {
            pauseHorizontalRepeat(settings.dcdMs);
        }
    }

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
    renderSettings(current);
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

function post(url, payload = {}) {
    state.actionChain = state.actionChain
        .catch(() => {})
        .then(async () => {
            const current = await requestJson(url, { method: 'POST', body: JSON.stringify(payload) });
            renderState(current);
            if (url === '/api/quit' && state.pollId) {
                window.clearInterval(state.pollId);
                state.pollId = null;
            }
        })
        .catch((error) => {
            console.error(error);
        });

    return state.actionChain;
}

function clearHorizontalRepeat() {
    if (inputState.horizontalTimerId) {
        window.clearTimeout(inputState.horizontalTimerId);
        inputState.horizontalTimerId = null;
    }
}

function pauseHorizontalRepeat(ms) {
    inputState.horizontalPauseUntil = performance.now() + Math.max(0, ms);
    restartHorizontalRepeat();
}

function getCurrentHorizontalDirection() {
    const pressed = [...inputState.pressed.values()]
        .filter((entry) => entry.action === 'moveLeft' || entry.action === 'moveRight')
        .sort((a, b) => b.order - a.order);

    if (!pressed.length) return null;
    return pressed[0].action === 'moveLeft' ? -1 : 1;
}

function enqueueAction(action) {
    if (action === 'quit') {
        post('/api/quit');
        return;
    }
    post('/api/action', { action });
}

function scheduleHorizontalRepeatOnce(delayMs, callback) {
    clearHorizontalRepeat();
    inputState.horizontalTimerId = window.setTimeout(callback, Math.max(0, delayMs));
}

function restartHorizontalRepeat() {
    clearHorizontalRepeat();
    const direction = inputState.horizontalDirection;
    if (!direction || !state.latest || state.latest.gameOver || state.latest.gameClosed) return;

    const now = performance.now();
    const pauseDelay = Math.max(0, inputState.horizontalPauseUntil - now);
    const startRepeat = () => {
        if (!inputState.horizontalDirection || !state.latest || state.latest.gameOver || state.latest.gameClosed) return;
        if (settings.arrMs === 0) {
            enqueueAction(direction < 0 ? 'moveLeftMax' : 'moveRightMax');
            return;
        }

        const repeat = () => {
            if (!inputState.horizontalDirection || !state.latest || state.latest.gameOver || state.latest.gameClosed) return;
            enqueueAction(direction < 0 ? 'moveLeft' : 'moveRight');
            scheduleHorizontalRepeatOnce(settings.arrMs, repeat);
        };

        scheduleHorizontalRepeatOnce(settings.arrMs, repeat);
    };

    scheduleHorizontalRepeatOnce(pauseDelay + settings.dasMs, startRepeat);
}

function updateHorizontalDirection() {
    const direction = getCurrentHorizontalDirection();
    const changed = direction !== inputState.horizontalDirection;
    inputState.horizontalDirection = direction;
    if (!direction) {
        clearHorizontalRepeat();
        return;
    }

    if (changed) {
        enqueueAction(direction < 0 ? 'moveLeft' : 'moveRight');
    }

    restartHorizontalRepeat();
}

function restartSoftDropRepeat() {
    if (inputState.softDropTimerId) {
        window.clearTimeout(inputState.softDropTimerId);
        inputState.softDropTimerId = null;
    }

    if (!inputState.pressed.has('softDrop') || !state.latest || state.latest.gameOver || state.latest.gameClosed) return;

    const delay = Math.max(16, Math.floor(1000 / Math.max(1, settings.sdf * 20)));
    const tick = () => {
        if (!inputState.pressed.has('softDrop') || !state.latest || state.latest.gameOver || state.latest.gameClosed) return;
        for (let index = 0; index < settings.sdf; index += 1) {
            enqueueAction('softDrop');
        }
        inputState.softDropTimerId = window.setTimeout(tick, delay);
    };

    inputState.softDropTimerId = window.setTimeout(tick, delay);
}

function setPressed(code, action, pressed) {
    if (pressed) {
        if (!inputState.pressed.has(code)) {
            inputState.pressed.set(code, { action, order: performance.now() });
        }
        return;
    }
    inputState.pressed.delete(code);
}

async function applySettings() {
    const next = {
        dasMs: Math.max(0, Number(dasInputEl.value || DEFAULT_SETTINGS.dasMs)),
        arrMs: Math.max(0, Number(arrInputEl.value || DEFAULT_SETTINGS.arrMs)),
        dcdMs: Math.max(0, Number(dcdInputEl.value || DEFAULT_SETTINGS.dcdMs)),
        sdf: Math.max(1, Number(sdfInputEl.value || DEFAULT_SETTINGS.sdf)),
        gravityMs: Math.max(16, Number(gravityInputEl.value || DEFAULT_SETTINGS.gravityMs)),
        countdownMs: Math.max(0, Number(countdownInputEl.value || DEFAULT_SETTINGS.countdownMs))
    };

    Object.assign(settings, next);
    saveSettings(settings);
    await requestJson('/api/settings', {
        method: 'POST',
        body: JSON.stringify({
            gravityMs: settings.gravityMs,
            countdownMs: settings.countdownMs
        })
    }).then(renderState).catch(console.error);

    restartHorizontalRepeat();
    restartSoftDropRepeat();
}

window.addEventListener('keydown', (event) => {
    const action = keyBindings[event.code];
    if (!action || !state.latest || state.latest.gameClosed) return;

    event.preventDefault();

    if (state.latest.gameOver && action !== 'quit') return;

    if (event.repeat && action !== 'quit') return;

    if (action === 'quit') {
        post('/api/quit');
        return;
    }

    setPressed(event.code, action, true);

    if (action === 'moveLeft' || action === 'moveRight') {
        updateHorizontalDirection();
        return;
    }

    if (action === 'softDrop') {
        enqueueAction('softDrop');
        restartSoftDropRepeat();
        return;
    }

    enqueueAction(action);

    if (action === 'rotateCcw' || action === 'rotateCw' || action === 'rotate180') {
        pauseHorizontalRepeat(settings.dcdMs);
    }
});

window.addEventListener('keyup', (event) => {
    const action = keyBindings[event.code];
    if (!action) return;

    setPressed(event.code, action, false);

    if (action === 'moveLeft' || action === 'moveRight') {
        updateHorizontalDirection();
        return;
    }

    if (action === 'softDrop') {
        restartSoftDropRepeat();
    }
});

retryButtonEl.addEventListener('click', () => {
    post('/api/reset');
    if (!state.pollId) {
        state.pollId = window.setInterval(pollState, 75);
    }
});

quitButtonEl.addEventListener('click', () => post('/api/quit'));
settingsFormEl.addEventListener('submit', (event) => {
    event.preventDefault();
    applySettings();
});
resetSettingsButtonEl.addEventListener('click', () => {
    Object.assign(settings, DEFAULT_SETTINGS);
    saveSettings(settings);
    renderSettings(state.latest);
    applySettings();
});

pollState();
state.pollId = window.setInterval(pollState, 75);
