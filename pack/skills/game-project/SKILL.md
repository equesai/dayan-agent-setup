---
name: game-project
description: Build a simple game with a beginner -- a browser game (HTML canvas + JavaScript) by default, or Python with pygame -- then run, test and tune it.
---
# Game project

## Pick the kind (ask with the `question` tool)
- Browser game (recommended): HTML canvas + JavaScript, runs by opening a file, easy to share.
- Python + pygame: when they are learning Python (`pip install pygame`, ask first).
Good first games: catch the falling objects, pong, snake, a simple platformer, a quiz game.

## Browser game skeleton
```
<project>/index.html   <canvas id="game" width="800" height="450"></canvas> + <script src="game.js">
<project>/game.js
```
game.js:
1. Constants at the top (speeds, sizes, colours, gravity) -- the student tunes these.
2. State: player, objects, score, lives, `gameOver`.
3. Input: keydown/keyup flags (arrows/WASD, Space), and touch/click when it makes sense.
4. `update(dt)`: movement, collisions (axis-aligned rectangles: overlap on x AND y), score.
5. `draw()`: clear, draw everything, draw the score; a "Game over -- press R" screen.
6. The loop: `requestAnimationFrame(loop)` with `dt` from the timestamp so speed does not
   depend on the computer.

## pygame skeleton
`pygame.init()`, a window, `clock = pygame.time.Clock()`, a `while running:` loop with the
event queue, `update`, `draw`, `pygame.display.flip()`, `clock.tick(60)`.

## Run, test, tune
- Run it and play for a minute: controls respond, collisions are fair, the score counts,
  game over and restart work.
- Tune one constant at a time with the student (speed too fast? more lives?) and explain
  what each one changes.
- Next ideas: levels, sound (`new Audio('pop.wav').play()`), a high score in localStorage.
