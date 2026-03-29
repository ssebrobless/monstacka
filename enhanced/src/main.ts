import { createEngineState, getGhostCells } from './engine';

const snapshot = createEngineState();
console.info('+E+RIS scaffold ready', {
  queue: snapshot.queue.slice(0, 5),
  ghostCells: getGhostCells(snapshot)
});
