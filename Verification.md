# Verification of the initial local build

- 4,691 numerical assertions across 480 engine operating points passed.
- Included unit conversions, power/torque identities, oxygen-limited fuel energy, boost pressure ratios, power-loss accounting, zero speed, ideal gear power, input rejection and case snapshots.
- Valve interpolation compared with the CAD law over 72,001 cycle positions: worst error below 0.001 mm.
- Cantera closed-cylinder tests passed reversible constant-cv motoring and fired cases at 1000, 3500 and 6500 RPM with two charge conditions.
- Fired calculations passed first-law energy balance and half-step work/peak-pressure checks.
- Native desktop smoke check passed every tab and all four boost modes (128 points).
- Native app -> bundled Python/Cantera -> saved pressure trace -> chart integration passed.
- Live gauges use the model output. Start/Stop, frame-independent display smoothing, rolling readings, CSV recording and input snapshots passed their integration check.
- Native SOLIDWORKS preview: 120 exported frames decoded and selected across four exterior packages and one cutaway. Crank/cam/piston pose readback maximum matrix-element error was below 3e-16. Geometry mismatch falls back to the schematic.
- Performance chart displays the highest sampled HP and RPM in the selected sweep, with 200 RPM sampling explicitly labelled.
- Optional SOLIDWORKS helper successfully read the active assembly's mass, volume and area without saving or modifying CAD.

These are numerical and software checks. No real dyno calibration, measured power validation, structural durability, turbo-map validation or GT-POWER equivalence is claimed.
