---
name: arduino-project
description: Plan, wire, assemble, program, test and tune an Arduino project with a beginner -- parts from RAM, Makers or Flux Electronics in Egypt, uploads with arduino-cli.
---
# Arduino project, start to finish

Work in this order. Finish and confirm each stage with the student before the next one.
Use the `question` tool for every choice (two to four options, recommended first).

## 1. Requirements
Agree on: what it should do (inputs -> behaviour -> outputs), where it runs (desk, battery,
outdoors), the budget in EGP, and which board they already have, if any. Default board for
beginners: Arduino Uno (clone is fine); Arduino Nano when space matters.

## 2. Parts (RAM, Makers, Flux Electronics)
- Call the `dayan` tool `find_parts` for EVERY part (board, sensors, actuators, modules,
  resistors, breadboard, jumper wires, power). Never quote a price from memory.
- Present one table: part, what it does, store, price (EGP), in stock, link. Prefer in-stock
  items; show the cheapest reasonable option first and a second store when one is out of
  stock. Add the total.
- Always include what beginners forget: breadboard, male-male and male-female jumper wires,
  the USB cable for the board, resistors for LEDs (220 ohm), and a separate supply for motors.
- If `find_parts` finds nothing, give the store search links it returns and say so.

## 3. Circuit
- A wiring table: component and pin -> Arduino pin (and breadboard row), with a suggested
  wire colour (red 5V, black GND, others for signals).
- A small text diagram of the connections.
- Safety rules -- check every circuit against them:
  - Every LED needs a series resistor (220-330 ohm on 5V).
  - An Arduino pin gives at most ~20 mA; never drive motors, pumps, relays' coils or many
    LEDs straight from a pin -- use a driver (transistor, L298N/L9110S, relay module).
  - Motors and servos under load get their own supply; connect its GND to the Arduino GND.
  - Never connect 5V straight to GND. Unplug USB before changing wires.
  - 3.3V modules (e.g. some sensors, ESP modules) must not get 5V signals without a level
    shifter or divider -- check the module's page.

## 4. Assembly, one step at a time
Give one small step (e.g. "Put the sensor on the breadboard, pins in rows 10-13"), then ask
with the `question` tool: Done / I'm stuck / Show me again. Continue only after "Done".
Finish with a check: "Is anything warm? Any loose wire?" before plugging in USB.

## 5. Software setup (once)
- Check `arduino-cli version`. If it is not found, the Dayan setup may have put it in
  `%LOCALAPPDATA%\Dayan\bin\arduino-cli.exe` (Windows) or `~/.local/bin/arduino-cli`
  (Linux): run it by that full path for now (a new terminal on Windows, or signing out
  and in on Linux, puts it on the PATH). Otherwise download it from
  https://arduino.github.io/arduino-cli/ after the student agrees.
- On Linux, uploading needs the serial-port group (`dialout`; `uucp` on Arch). If an
  upload says "permission denied", ask the student first, then have them run
  `sudo usermod -aG dialout $USER` (their password) and sign out and in once.
- `arduino-cli core update-index` then `arduino-cli core install arduino:avr` (Uno, Nano,
  Mega). Libraries: `arduino-cli lib install "<name>"` (ask first).
- Find the board: `arduino-cli board list`. No port? Try another USB cable (many are
  charge-only), another port, and for clone boards the CH340 USB driver (Windows usually
  installs it; otherwise the student installs it from the chip maker's site -- ask first).
  Old Nano clones need the FQBN `arduino:avr:nano:cpu=atmega328old`.

## 6. Program
- Sketch in `<project>/<SketchName>/<SketchName>.ino`: comments for a beginner, pins and
  tunable values (thresholds, delays, speeds) as named constants at the top, and `Serial`
  prints (`Serial.begin(9600)`) that show what the board sees and decides.
- Compile: `arduino-cli compile --fqbn arduino:avr:uno <SketchName>`
- Upload: `arduino-cli upload -p <PORT> --fqbn arduino:avr:uno <SketchName>`

## 7. Check it works
- Read the serial output for a few seconds (the monitor keeps running, so stop it with a
  timeout): `arduino-cli monitor -p <PORT> -c baudrate=9600` in the background, or a short
  Python script with pyserial if it is installed.
- Compare with what was expected; ask the student what they see (LED on? servo moving?)
  with the `question` tool.
- Troubleshooting order: power and GND -> wiring against the table -> the right pin numbers
  in the code -> sensor values in the serial output -> one change at a time.

## 8. Tune
Adjust the named constants (thresholds, timings, speeds) one at a time with the student,
upload, and check the serial output again. Explain what each value changes.

## 9. Wrap up
Summarise: the final parts list and cost, the wiring table, where the sketch is, and one
idea to extend the project.
