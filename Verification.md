# Verification of the initial local build

- 4,691 numerical assertions across 480 engine operating points passed.
- Included unit conversions, power/torque identities, oxygen-limited fuel energy, boost pressure ratios, power-loss accounting, zero speed, ideal gear power, input rejection and case snapshots.
- Valve interpolation compared with the CAD law over 72,001 cycle positions: worst error below 0.001 mm.
- Cantera closed-cylinder tests passed reversible constant-cv motoring and fired cases at 1000, 3500 and 6500 RPM with two charge conditions.
- Fired calculations passed first-law energy balance and half-step work/peak-pressure checks.
- Native desktop smoke check passed every tab and all four boost modes (128 points).
- Native app -> bundled Python/Cantera -> saved pressure trace -> chart integration passed.
- Live gauges use the model output. Start/Stop, frame-independent display smoothing, rolling readings, CSV recording and input snapshots passed their integration check.
- Native SOLIDWORKS preview: 96 Rev B frames decoded across naturally aspirated, separate turbo/blower exteriors and cutaway. Crank/cam/piston native pose readback was checked. Geometry mismatch and combined boost fall back to the schematic.
- Performance chart displays the highest sampled HP and RPM in the selected sweep, with 200 RPM sampling explicitly labelled.
- Power development: separate packages, target solve residual, no-hit status, source/VE/combustion preservation, fuel blending/lambda, energy bounds, UI case loading and study/CSV persistence passed.
- Target cylinder cases passed energy/step checks, but reported about 1546/1632 hp versus target-matched fast-model 1000 hp. Different loss assumptions and no cross-calibration are a material limitation. Neither output is a validated prediction.
- RPM geometry checks passed analytical TDC acceleration, full-cycle travel/mean speed, velocity/acceleration scaling and displacement-preserving stroke alternatives. Component limits remain unknown; no safe redline or power extrapolation is claimed.
- Measured-data parser rejects nonfinite, unordered and HP/torque-inconsistent samples; its numerical fixture is synthetic test-only data. No measured V10 dyno data is supplied or fabricated.
- Optional SOLIDWORKS helper successfully read the active assembly's mass, volume and area without saving or modifying CAD.

These are numerical and software checks. No real dyno calibration, measured power validation, structural durability, turbo-map validation or GT-POWER equivalence is claimed.
