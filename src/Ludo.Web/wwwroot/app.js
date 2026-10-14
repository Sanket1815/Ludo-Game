const TRACK = [
  [13, 6], [12, 6], [11, 6], [10, 6], [9, 6], [8, 5], [8, 4], [8, 3], [8, 2], [8, 1], [8, 0],
  [7, 0], [6, 0], [6, 1], [6, 2], [6, 3], [6, 4], [6, 5], [5, 6], [4, 6], [3, 6], [2, 6], [1, 6], [0, 6],
  [0, 7], [0, 8], [1, 8], [2, 8], [3, 8], [4, 8], [5, 8], [6, 9], [6, 10], [6, 11], [6, 12], [6, 13], [6, 14],
  [7, 14], [8, 14], [8, 13], [8, 12], [8, 11], [8, 10], [8, 9], [9, 8], [10, 8], [11, 8], [12, 8], [13, 8],
  [14, 8], [14, 7], [14, 6]
];

const HOMES = {
  Red: [[13, 7], [12, 7], [11, 7], [10, 7], [9, 7]],
  Green: [[7, 1], [7, 2], [7, 3], [7, 4], [7, 5]],
  Yellow: [[1, 7], [2, 7], [3, 7], [4, 7], [5, 7]],
  Blue: [[7, 13], [7, 12], [7, 11], [7, 10], [7, 9]]
};

const YARDS = {
  Red: [[10, 1], [10, 3], [12, 1], [12, 3]],
  Green: [[1, 1], [1, 3], [3, 1], [3, 3]],
  Yellow: [[1, 11], [1, 13], [3, 11], [3, 13]],
  Blue: [[10, 11], [10, 13], [12, 11], [12, 13]]
};

const SEATS = {
  2: ["Red", "Yellow"],
  3: ["Red", "Green", "Yellow"],
  4: ["Red", "Green", "Yellow", "Blue"]
};

const STARTS = { 0: "red", 13: "green", 26: "yellow", 39: "blue" };
const STARS = new Set([8, 21, 34, 47]);

const board = document.querySelector("#board");
const seats = document.querySelector("#seats");
const setup = document.querySelector("#setup");
const setupError = document.querySelector("#setup-error");
const statusEl = document.querySelector("#status");
const streakEl = document.querySelector("#streak");
const rollButton = document.querySelector("#roll");
const playersEl = document.querySelector("#players");
const logEl = document.querySelector("#log");
const dice = document.querySelector("#dice");

let game = null;
let busy = false;
let aiGeneration = 0;
let diceTimer = 0;

buildBoard();
renderSeats();

document.querySelectorAll('input[name="count"]').forEach((input) => {
  input.addEventListener("change", renderSeats);
});

document.querySelector("#setup-form").addEventListener("submit", (event) => {
  event.preventDefault();
  const count = Number(new FormData(event.target).get("count"));
  const humans = [...seats.querySelectorAll("input:checked")].map((input) => input.value);
  startGame(count, humans);
});

document.querySelector("#watch").addEventListener("click", () => startGame(4, []));
document.querySelector("#new-game").addEventListener("click", () => {
  aiGeneration++;
  setup.classList.remove("hidden");
});
rollButton.addEventListener("click", roll);

function buildBoard() {
  const used = new Set();
  const place = (row, col, className, text) => {
    const key = `${row},${col}`;
    if (used.has(key)) return;
    used.add(key);
    const cell = document.createElement("div");
    cell.className = `cell ${className}`;
    cell.dataset.row = String(row);
    cell.dataset.col = String(col);
    cell.style.gridRow = String(row + 1);
    cell.style.gridColumn = String(col + 1);
    if (text) cell.innerHTML = text;
    board.append(cell);
  };

  for (const [color, cells] of Object.entries(YARDS)) {
    const yard = document.createElement("div");
    yard.className = `yard ${color.toLowerCase()}`;
    yard.setAttribute("aria-label", `${color} yard`);
    board.append(yard);
    for (const [row, col] of cells) used.add(`${row},${col}`);
  }

  for (const [row, col] of [[8, 6], [6, 6], [6, 8], [8, 8]]) place(row, col, "track");

  TRACK.forEach(([row, col], index) => {
    const start = STARTS[index];
    const safe = start || STARS.has(index);
    const star = STARS.has(index) ? '<span class="star" aria-hidden="true">★</span>' : "";
    place(row, col, `track${safe ? " safe" : ""}${start ? ` start-${start}` : ""}`, star);
  });

  for (const [color, cells] of Object.entries(HOMES)) {
    for (const [row, col] of cells) place(row, col, `home ${color.toLowerCase()}`);
  }

  const nest = document.createElement("div");
  nest.id = "home-nest";
  nest.className = "nest";
  nest.innerHTML = '<div class="diamond"></div>';
  board.append(nest);

  const pieces = document.createElement("div");
  pieces.id = "pieces";
  pieces.className = "pieces";
  board.append(pieces);
}

function renderSeats() {
  const count = Number(document.querySelector('input[name="count"]:checked').value);
  seats.innerHTML = "";
  for (const color of SEATS[count]) {
    const label = document.createElement("label");
    label.className = `seat ${color.toLowerCase()}`;
    label.innerHTML = `<input type="checkbox" value="${color}" checked /> ${color}`;
    seats.append(label);
  }
}

async function startGame(playerCount, humans) {
  setupError.textContent = "";
  try {
    const view = await api("/api/games", {
      method: "POST",
      body: JSON.stringify({ playerCount, humans })
    });
    aiGeneration++;
    apply(view);
    setup.classList.add("hidden");
    runAi(aiGeneration);
  } catch (error) {
    setupError.textContent = error.message;
  }
}

async function roll() {
  if (!game || busy || game.winner || !game.currentIsHuman || game.awaitingMove) return;
  await play(() => api(`/api/games/${game.id}/roll`, { method: "POST" }));
  runAi(aiGeneration);
}

async function move(color, id) {
  if (!game || busy) return;
  await play(() => api(`/api/games/${game.id}/move`, {
    method: "POST",
    body: JSON.stringify({ color, id })
  }));
  runAi(aiGeneration);
}

async function runAi(generation) {
  while (game && !game.winner && !game.currentIsHuman && generation === aiGeneration) {
    await sleep(700);
    if (generation !== aiGeneration || !game || game.winner || game.currentIsHuman) return;
    await play(() => api(`/api/games/${game.id}/ai`, { method: "POST" }));
  }
}

async function play(request) {
  busy = true;
  paint();
  spinDice();
  try {
    const view = await request();
    await sleep(320);
    stopDice(view.roll);
    apply(view);
  } catch (error) {
    stopDice(game?.roll);
    statusEl.textContent = error.message;
  } finally {
    busy = false;
    paint();
  }
}

function apply(view) {
  const previousRoll = game?.roll;
  const previousId = game?.id;
  game = view;
  if (view.roll == null) game.roll = previousRoll;
  if (previousId !== view.id) document.querySelector("#pieces").replaceChildren();
  paint();
  requestAnimationFrame(placePieces);
}

function paint() {
  if (!game) return;
  const last = game.log.at(-1) ?? "";
  if (game.winner) {
    statusEl.textContent = `${game.winner} wins.`;
  } else if (game.awaitingMove && game.currentIsHuman) {
    statusEl.textContent = `${game.currentPlayer} rolled ${game.pendingRoll}. Choose a glowing token.`;
  } else if (game.currentIsHuman && !busy) {
    statusEl.textContent = `${game.currentPlayer}, roll the dice.`;
  } else {
    statusEl.textContent = last;
  }

  streakEl.textContent = game.consecutiveSixes > 0 ? `Sixes this turn: ${game.consecutiveSixes}` : "";
  rollButton.disabled = busy || !game.currentIsHuman || game.awaitingMove || Boolean(game.winner);
  rollButton.textContent = game.winner ? "Game over" : "Roll";

  playersEl.innerHTML = "";
  for (const player of game.players) {
    const home = player.tokens.filter((token) => token.state === "Finished").length;
    const card = document.createElement("div");
    card.className = `player ${player.color.toLowerCase()}${player.color === game.currentPlayer && !game.winner ? " active" : ""}`;
    card.innerHTML = `<span class="who">${player.color}</span><span>${player.isHuman ? "Human" : "Computer"} · ${home}/4 home</span>`;
    playersEl.append(card);
  }

  logEl.innerHTML = "";
  for (const line of game.log) {
    const item = document.createElement("li");
    item.textContent = line;
    logEl.append(item);
  }
}

function placePieces() {
  const layer = document.querySelector("#pieces");
  const eligible = new Set((game.eligible ?? []).map((token) => `${token.color}-${token.id}`));
  const tokens = game.players.flatMap((player) => player.tokens);
  const groups = new Map();

  for (const token of tokens) {
    const spot = spotFor(token);
    if (!groups.has(spot.key)) groups.set(spot.key, []);
    groups.get(spot.key).push(token);
  }

  for (const token of tokens) {
    const spot = spotFor(token);
    const group = groups.get(spot.key);
    const offset = stackOffset(group.indexOf(token), group.length);
    const id = `${token.color}-${token.id}`;
    let piece = layer.querySelector(`[data-id="${id}"]`);
    if (!piece) {
      piece = document.createElement("button");
      piece.type = "button";
      piece.className = `piece ${token.color.toLowerCase()}`;
      piece.dataset.id = id;
      piece.textContent = String(token.id);
      piece.addEventListener("click", () => {
        if (piece.classList.contains("eligible")) move(token.color, token.id);
      });
      layer.append(piece);
    }

    piece.style.left = `${spot.x + offset.x}px`;
    piece.style.top = `${spot.y + offset.y}px`;
    piece.classList.toggle("eligible", eligible.has(id));
    piece.classList.toggle("done", token.state === "Finished");
    piece.disabled = !eligible.has(id);
    piece.setAttribute("aria-label", `${token.color} token ${token.id}, ${token.state}`);
  }
}

function spotFor(token) {
  if (token.state === "Finished") return { ...homePoint(token), key: `home-${token.color}-${token.id}` };
  const [row, col] = token.state === "OnBoard"
    ? TRACK[token.index]
    : token.state === "InHomeStretch"
      ? HOMES[token.color][token.index]
      : YARDS[token.color][token.id - 1];
  return { ...centerOf(row, col), key: `${row},${col}` };
}

function homePoint(token) {
  const width = board.clientWidth;
  const height = board.clientHeight;
  const quad = { Red: [0, 0.16], Green: [-0.16, 0], Yellow: [0, -0.16], Blue: [0.16, 0] };
  const [dx, dy] = quad[token.color];
  const spread = (token.id - 2.5) * width * 0.012;
  return {
    x: (7.5 / 15) * width + dx * width * 0.18 + spread,
    y: (7.5 / 15) * height + dy * height * 0.18
  };
}

function centerOf(row, col) {
  return {
    x: ((col + 0.5) / 15) * board.clientWidth,
    y: ((row + 0.5) / 15) * board.clientHeight
  };
}

function stackOffset(index, count) {
  if (count === 1) return { x: 0, y: 0 };
  const angle = (index / count) * Math.PI * 2 - Math.PI / 2;
  const radius = 8;
  return { x: Math.cos(angle) * radius, y: Math.sin(angle) * radius };
}

function spinDice() {
  window.clearInterval(diceTimer);
  dice.classList.add("rolling");
  diceTimer = window.setInterval(() => {
    dice.dataset.face = String(1 + Math.floor(Math.random() * 6));
  }, 70);
}

function stopDice(face) {
  window.clearInterval(diceTimer);
  dice.classList.remove("rolling");
  if (face) dice.dataset.face = String(face);
}

function sleep(ms) {
  return new Promise((resolve) => window.setTimeout(resolve, ms));
}

async function api(url, options) {
  const response = await fetch(url, {
    headers: { "Content-Type": "application/json" },
    ...options
  });
  const body = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error(body.error || "The game could not do that.");
  return body;
}

window.addEventListener("resize", () => {
  if (game) placePieces();
});
