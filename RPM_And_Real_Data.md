# RPM study and real engine data

**Proven maximum RPM for this V10: unknown. No measured data from this engine is loaded.**

The 7,000 RPM power-calculation ceiling is a software study range, not a validated redline. The separate motion study can evaluate 0–20,000 RPM without extending the assumed horsepower curve. Its range is not a recommendation to operate hardware there.

## What the motion study calculates

Current concept dimensions are 95 mm bore, 98.756274 mm stroke and 160 mm rod length, with ten cylinders. These are CAD design dimensions, not measurements from a manufactured engine.

For a rigid slider crank, crank radius r = stroke/2, rod length l and crank angle theta:

`x = r(1-cos(theta)) + l - sqrt(l^2-r^2 sin(theta)^2)`

Velocity and acceleration use the analytical first and second derivatives, multiplied by crank angular speed and its square. Peaks are sampled every 0.1 crank degree. Mean piston speed is `2 * stroke(m) * RPM / 60`; a four-stroke camshaft rotates at half crank RPM. Geometry and arithmetic are checked against full-cycle travel, the exact TDC acceleration and RPM scaling. These checks do not establish stress, fatigue or durability.

| Current CAD geometry | 8,000 RPM | 10,000 RPM |
|---|---:|---:|
| Mean piston speed | 26.34 m/s | 32.92 m/s |
| Peak piston speed, sampled | 43.31 m/s | 54.13 m/s |
| Peak piston acceleration | 4,624 g | 7,226 g |
| Camshaft speed | 4,000 RPM | 5,000 RPM |

These are calculated motions, not safe limits. Acceleration is not a force without the relevant moving masses. No reciprocating mass is fabricated from a generic steel/aluminum material choice.

At 8,000 RPM, preserving the current displacement with a 90 mm stroke would require approximately 99.51 mm bore; an 85 mm stroke would require approximately 102.40 mm bore. Mean piston speeds would be 24.00 and 22.67 m/s. These are alternative geometry studies, not approved modifications to the current block. A new bore, deck/chamber design, piston/rod package, head gasket, cooling passages and clearances would need engineering and validation.

## What actually limits maximum RPM

The app records six categories of missing limits: piston/rod/bolt fatigue; crankshaft and torsional vibration; valvetrain/springs/followers; bearings and oil supply; induction/accessory drives; and transmission/flywheel. A nonzero entered cap requires a part/test source. The lowest supplied cap is displayed as a supplied constraint only; it is not a proven engine redline, even when all rows are filled. The app cannot authenticate a typed source reference.

No universal piston-speed threshold is assumed. An optional mean-speed cap is explicitly a user-selected study assumption. Coil bind, valve float, spring dynamics, piston/valve contact, rod/block clearance, bearing oil film, crank resonance, blower drive loads and turbo shaft limits are not solved.

COMP Cams explains why the complete valvetrain and correct spring selection matter to achieving an RPM target; a high-RPM camshaft alone does not establish a usable limit. [Manufacturer guidance](https://www.compcams.com/valve-springs-101/). This does not specify a spring or RPM limit for this V10.

## Importing measured dyno data

Use **RPM & real data → Import measured dyno CSV**. Enter a source, test date and engine identifier, and select crank or wheel measurement basis. Torque and RPM must refer to the same shaft: raw wheel torque cannot be paired with engine RPM. The CSV must use invariant numeric values and these headers:

`rpm,torque_Nm,hp`

The optional `hp` column is mechanical horsepower, not metric PS. Torque is N·m. RPM rows must be strictly increasing. Horsepower is derived from measured torque using `P = torque * 2*pi*RPM/60`; a supplied HP column must agree within 1% or 1 hp. Nonfinite values and invalid units/header layouts are rejected. **Create blank data template** creates headers only, with no invented observations.

For crank data, the app overlays user-supplied torque-derived HP with the current full-throttle assumed model at the same RPM and reports HP RMSE for points within 7,000 RPM. Wheel data is plotted without a direct crank-model comparison; no drivetrain-loss conversion is assumed. The raw CSV, SHA-256, supplied provenance/basis, import time and exact model-input snapshot are saved. Points above the model range remain imported measurements; model power is not extrapolated to them. Importing data does not recalibrate the model or verify the measurement's authenticity. Record the correction standard and shaft/RPM basis in the source field.

The fast model and closed-cylinder solver remain uncalibrated. Their target-case power estimates disagree substantially, as documented in [the power-development report](1000_HP_Development.md). Neither output is a certified engine rating. Hardware specifications and instrumented testing are required before a real maximum RPM or horsepower can be claimed.
