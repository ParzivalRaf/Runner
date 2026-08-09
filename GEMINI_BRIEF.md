# Design review request — endless runner in development

Copy this whole document into Gemini (or any other model) as a single message.

---

## What I need from you

**Task 1 — Research.**
Research the endless runner genre: Subway Surfers, Temple Run, Minion Rush,
Sonic Dash, Jetpack Joyride, Alto's Odyssey, Obama Run and anything else
relevant. I want to know:

- What specifically makes players keep playing these games, and what makes
  them quit. Cite retention mechanics, not vibes.
- What the moment-to-moment "feel" tricks are that top runners use.
- How they structure difficulty, session length and meta-progression.
- What the genre's common design mistakes are.
- I also want the game to be **funny**, in the spirit of Obama Run — meme
  energy, characters people recognise and laugh at. Tell me how humour is
  actually delivered in successful games without becoming a one-note joke
  that stops being funny on run three.
- Add your own original ideas, not just a summary of what exists.

**Task 2 — Critique my game.**
Below is a full description of what I have built so far. Tell me what to
improve, in priority order, with reasoning. Be blunt. If something I built
is a bad idea, say so. Assume I would rather hear a hard truth than
a compliment.

For each recommendation give me: what to change, why it matters, and roughly
how expensive it is to build.

---

## My constraints — read before recommending anything

- **Solo developer, 16 years old, first ever game.** I had never opened Unity
  and do not know C#. An AI assistant writes the code, I direct the project,
  test it and make design calls.
- **No artist, no budget, no team.** Everything visual has to be either
  code-generated, free, or made by me.
- **~15 hours a week.**
- **Hard deadline: around 21 August 2026** I move country and lose access to
  the Mac. After that I work on Windows, which means **I can no longer build
  to iPhone at all.** Anything requiring a physical device or physical people
  must be finished before that date.
- **Distribution is currently impossible.** I build to my own iPhone with
  a free Apple ID. That cannot be shared with anyone. Paid Apple Developer
  Program is $99/year; Android would solve it but I do not own an Android
  device.
- **Audience:** my classmates and teachers. This is a personal/school project
  first, not a commercial launch. But I want it to be genuinely good.

Recommendations that require money, a team, an artist, or a physical device
after 21 August are less useful to me. Say so if you think that constraint
is the real problem.

---

## The concept

A 3D endless runner in the Subway Surfers mould, where the playable
characters are **my real school teachers**, treated affectionately — they are
the heroes of the game, not targets. I have their verbal permission.

Portrait orientation. Fully offline, no accounts, no internet, no ads,
no in-app purchases.

---

## What a run actually looks like

**Visual style: untextured flat-shaded primitives with good lighting.**
Everything is a box or a capsule. There are no character models, no textures
and no animation anywhere in the game. Think "well-lit greybox", not
"finished game". This is the single biggest visual gap.

- **The track** is 12 units wide, three lanes 2.5 units apart. The road
  surface is dark desaturated indigo-purple. Lane dividers and periodic
  cross-stripes are near-white and slightly glowing, so speed reads clearly.
  Waist-high purple side rails run the full length.
- **Decoration** alternates by chunk: bare road, pairs of pillars either side,
  or overhead arches. All plain boxes in a muted purple-grey.
- **The sky** is a procedural sunset — purple and magenta, thick atmosphere,
  a visible sun disc low on the horizon. The sun is at 24° elevation, so
  shadows are long and lie across the track, which reinforces speed.
- **Fog** is linear purple-mauve, starting at 45 metres and fully opaque at
  112 metres. The track is generated 120 metres ahead, so fog hides the seam
  where new geometry appears.
- **Post-processing:** neutral tonemapping, bloom, vignette, warm/cool split
  toning (shadows pushed violet, highlights pushed warm), slight saturation
  and contrast lift, FXAA. Coins, power-ups and obstacles are emissive, so
  bloom has something to catch. Coins are the brightest thing on screen
  by design.
- **The player** is an orange capsule roughly 2 units tall. Each character is
  the same capsule in a different colour.

**Camera:** third person, 4.5 units up and 6 units behind the player, tilted
15° down. It tracks the player rigidly forward, and smoothly horizontally and
vertically, so lane changes look soft. Field of view widens from 58° to 74°
as speed increases, and gets a short punch outward on power-up pickup and
inward on crash. The camera shakes on impacts.

**HUD:** distance in metres, large, top centre. Coin count top right.
Pause button top left. Up to four power-up timer bars stack below the pause
button. A combo counter ("x7") appears under the distance when you chain
pickups and near misses, and fades out when the chain breaks.

---

## The moment-to-moment loop

1. Menu screen. Tap Play.
2. You start running forward at 14 units/second. The first 20 metres are
   deliberately clear.
3. Obstacles begin. Speed climbs to a hard ceiling of 24 units/second over
   about 33 seconds.
4. Difficulty tiers step up at 60 m, 300 m and 800 m — more obstacle types,
   denser rows, more simultaneous lanes blocked.
5. You run until you hit something. One hit ends the run, unless you are
   playing the character with a shield (one free hit per run) or are under
   the Coffee power-up (temporary invincibility).
6. Game over screen: distance, coins collected, best distance. Restart is
   instant — the scene is never reloaded, every system just resets.

**Controls:** swipe left/right to change lane, swipe up to jump, swipe down
to slide. Swiping down mid-air makes you drop fast and slide on landing.
Lane changes work mid-air. Swipe threshold has been tuned and verified on
a real touchscreen.

**Jump:** 2.2 units high, 0.75 seconds of air time, with heavier gravity on
the way down so it feels snappy rather than floaty.

---

## Obstacle vocabulary

Colour is the language — the colour tells you which action to take.

| Obstacle | Size (w × h × d) | Colour | Required action |
|---|---|---|---|
| Block | 1.7 × 2.8 × 0.7 | red, emissive | Change lane. Cannot be jumped or slid. |
| Barrier | 1.7 × 0.9 × 0.7 | yellow, emissive | Jump over it. |
| Overhead beam | 1.7 × 0.7 × 0.7, hangs at 1.1–1.8 | blue, emissive | Slide under it. |
| Train carriage | 1.7 × 2.6 × 10 | teal, glowing white roof edges | Cannot be jumped or passed. Dodge it — or ride on its roof. |
| Ramp | 1.7 wide, 10 long, rises to 2.6 over the first 7 | teal with glowing edges | Run up it onto the train roof. No input needed. |

---

## The vertical layer (newest feature, built but not yet playtested)

Until recently the track was completely flat: every decision was "which lane".
I added a second level.

- Train carriages chain end to end into **trains that run 1–3 chunks long —
  on average 60 metres of continuous roof**, and can occupy two of the three
  lanes at once.
- The roof sits at 2.6 units. A normal jump reaches 2.2. **You therefore
  cannot jump onto a train.** Jumping into one kills you. This is deliberate
  and matches Subway Surfers.
- **The only way up is a ramp**, present on about 70% of trains. You simply
  run up it, no input required.
- **The Super Sneakers power-up is the second way up** — it nearly doubles
  jump height, letting you board any train anywhere. This emerged from the
  numbers rather than being designed, and I kept it.
- **Coins climb the ramp** as a wordless tutorial that the route exists, then
  continue along the entire roof.
- The roof route is optional. A ground-level path is mathematically guaranteed
  to always exist.

---

## Track generation rules

The generator is built so an impossible arrangement cannot occur:

- Every row of three lanes has at least one passable lane.
- Adjacent rows always share a passable lane, so the track can be completed
  without ever changing lane.
- Rows requiring a jump or slide are never closer than 22 units, because
  a jump takes 18 units at top speed.
- Trains count as impassable for these guarantees, even though you can run
  over them. The roof is a bonus route, never a requirement.
- After a train ends, its lane is forced empty for one row, so you have room
  to land after dropping 2.6 units.

Verified by simulation over 180,000 generated rows: zero impassable rows,
zero rows without a shared lane, zero unsafe landings.

---

## Economy and progression

- **Coins** are the only currency. Roughly 10 per chunk when a coin line
  spawns (75% chance), plus a longer line along train roofs (85% chance when
  a train exists). Near misses grant 1 coin each.
- **Shop upgrades**, max level 5 each: Magnet range 150×(level+1),
  Coffee duration 200×(level+1), Head start distance 300×(level+1).
- **Characters** cost 400 / 900 / 1500 / 2500. Five total.

**Power-ups**, ~16% chance per chunk:

| Power-up | Duration | Effect |
|---|---|---|
| Magnet | 6 s | Pulls nearby coins in |
| Coffee | 6 s | ×1.6 speed and invincibility — you smash through obstacles |
| Super Sneakers | 7 s | ×1.8 jump height — also lets you board any train |
| Double Score | 8 s | Coins count double |

**Characters and their passive abilities:**

| Character | Price | Ability |
|---|---|---|
| Rookie | free | none |
| PE teacher | 400 | +4 starting speed |
| Maths teacher | 900 | +10% coins |
| Chemistry teacher | 1500 | Power-ups last 2 s longer |
| Principal | 2500 | Shield — survives one crash per run |

Names and catchphrases are still placeholders. All five are currently
identical coloured capsules.

---

## Feedback and "juice" (built, not yet playtested)

- Camera shake scaled per event: crash, near miss, power-up pickup, landing.
- **Hit-stop** — time nearly freezes for 0.11 s on impact while the camera
  keeps shaking.
- **Near-miss detection** — passing very close to an obstacle without hitting
  it gives a whoosh, a shake, a coin and a combo increment.
- **Combo counter** — coins and near misses chain; the number punches in scale
  and shifts colour as the chain grows.
- **Screen effects tied to power-ups** — Coffee adds chromatic aberration and
  lens distortion; Double Score tints the whole frame gold.
- **Coin pitch ramp** — each consecutive coin plays a semitone higher, capped,
  resetting after a pause.

---

## Audio

All sound effects are **synthesised in code** — sine waves, sweeps and noise —
because the AI assistant cannot download audio. They sound deliberately
8-bit. The music is a 30-second code-generated chiptune loop at 128 BPM.
Both are placeholders. Real music is still on the list.

---

## What is deliberately NOT in the game

So you do not recommend things I already know are missing:

- **No character models or animation.** Everyone is a capsule. This is the
  single biggest gap.
- **No real music or sound design.**
- **No turns.** The world runs along one axis. I know Minion Rush has turns;
  I decided they were too expensive and risky for now, but not permanently.
- **No moving obstacles.** Trains are static.
- **No chase character.** Nothing pursues you, unlike Subway Surfers' guard.
- **No missions, no daily rewards, no leaderboards, no events, no tutorial.**
- **No ads, no in-app purchases.** Not planned.
- **No meta-progression** beyond the shop and character unlocks.
- **No humour implemented yet at all.** The comedy premise — teachers as
  runners — exists only as placeholder names. This is what I most want ideas
  for.

---

## Where I already suspect the problems are

Tell me if you agree or disagree with my own reading:

1. Nothing has personality yet. The teacher premise is the whole point of the
   game and currently it is five identical capsules with placeholder names.
2. There is no reason to play a second run beyond a slightly higher number.
   No missions, no goals, no unlock drip.
3. Every run is structurally identical. Same obstacle vocabulary from metre 1
   to metre 3000, only denser.
4. The vertical layer might not be rewarding enough to be worth the risk of
   using it.
5. I do not know how long a session actually lasts or should last.

---

## Ask me if you need it

I can supply exact numbers for anything — speeds, spawn rates, timings,
distances, prices. If a recommendation depends on a number I have not given
you, ask instead of assuming.
