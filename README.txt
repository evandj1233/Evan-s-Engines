EVAN'S ENGINES — 0.2.0
Local Windows engineering lab

START
Double-click EvansEngines.exe, or the Evan's Engines desktop shortcut.
The installed copy includes a local Python/Cantera runtime. It runs offline.
No administrator rights, subscription or SOLIDWORKS license is needed for
the engine, cylinder, gearbox or thermal scenario calculators.

FIRST TEST
Open Live bench and press Start engine model for moving gauges, cylinder
activity, pistons/valves and boost component activity. RPM, throttle and boost
controls at left update the calculations. The rolling RPM/power view retains
roughly one minute. Record live data captures about 5 samples/second, up to
6000 samples, and Save live log exports timestamps, readings and input sets.
This is live CALCULATED data, not ECU or sensor telemetry. RPM transitions
use frame-independent display smoothing, not a calibrated transient model.
Visible mechanism motion is slowed to 1/50 crank speed. No sound is generated.

1. Choose an engine example, or change geometry in Model inputs and Apply.
2. Set the operating RPM and throttle. The top numbers describe that point.
3. Run RPM sweep for curves, or Compare all four boost modes.
4. Open Cylinder physics and press Run cylinder physics for a pressure trace.
5. Save engine case to preserve inputs. Export current results writes CSV
   plus an input snapshot. Cylinder tests save their request, trace and checks
   automatically in Runs.
6. Parts & timing lets you inspect a cylinder and animate it in slow motion.
   The inspection view runs at 120 crank degrees/second, independently of RPM.

TEST MODULES
Performance: steady-state mean-value scenarios for 1–16 cylinders. Airflow is
prescribed by the editable volumetric-efficiency table, not solved from ports.
Fuel energy is oxygen limited. Indicated work uses ideal Otto efficiency with
an assumed realization multiplier. Friction, pumping, accessories and blower
drive are deducted. Both boost stages compound pressure ratios. The two
turbos are parallel and share flow; their boost is not doubled.

Cylinder physics: a new closed-cylinder air-standard model uses Cantera 3.2
temperature-dependent O2/N2 gas properties, slider-crank volume, imposed Wiebe
heat release and assumed wall heat transfer. It integrates compression and
expansion with fourth-order Runge–Kutta, repeats at half the crank step and
reports first-law energy balance. It does not use the fast model's Otto
realization factor, so the two models need not report the same horsepower.
Their assumptions differ; neither is calibrated against a real V10.

Parts & timing: exact slider-crank geometry and the CAD cosine valve-motion
law sampled with Engine Simulator's MIT-licensed triangular interpolation.
Worst sampled interpolation error against the CAD law is under 0.001 mm.
Changing cam advance affects this inspection motion; it does not automatically
change the prescribed VE table or cylinder solver's heat release.

Gearbox: six ratios from the CAD concept with an assumed 95% efficiency.
Thermal: independent lumped heat balance for one uniform object temperature.

SOLIDWORKS
The installed local copy can open your existing assembly/parts, and the
optional CadReadback helper reads the active document's assigned mass,
volume, surface area and material through the supported SOLIDWORKS API.
It does not alter or save CAD files. Your own licensed CAD installation is
required for these buttons. CAD mass is determined by its model and assigned
materials; it is not a measured production-engine mass.

ACCURACY
This is a working concept-test application, not a GT-POWER replacement.
GT-POWER is the workflow reference; none of its proprietary code is included.
No gas-exchange pressure-wave solver, combustion chemistry/knock model,
turbine/compressor map matching, emissions, structural strength, automatic
shift hydraulics or torque-converter fluid model is implemented.
Requested boost is assumed achieved. The turbo's exhaust energy supply is
not solved; its extra pumping loss is an explicit assumption.
7000 RPM is a calculation limit, not a proven mechanical redline.
Numerical PASS means consistent calculations, not experimentally proven power.

TO MAKE POWER PREDICTIONS MORE USEFUL
Use measured compression/chamber volumes, port/valve flow and VE, fuel data,
cylinder-pressure and burn-rate traces, friction/accessory maps, and compressor
and turbine maps. Compare against real dyno measurements at multiple speeds
and loads, retaining separate calibration and validation cases.

SOURCES AND REUSE
Engine Simulator: https://github.com/ange-yaghi/engine-sim
CamProfile.cs adapts src/function.cpp's sampleTriangle/closestSample under MIT.
The whole Engine Simulator combustion/audio solver was not ported.
Cantera: https://github.com/Cantera/cantera and https://cantera.org
Cantera supplies gas properties, not a validated gasoline-engine prediction.
REA 6.7.0: https://github.com/morluto/rea
Used for static inspection of the original CAD controller and new compiled
app; REA is an analysis tool and contributes no engine physics.
GT-POWER feature/workflow reference: https://global.gtisoft.com/products/gt-power/
Otto-cycle reference: https://web.mit.edu/16.unified/www/SPRING/propulsion/notes/node25.html
Compressor relation: https://www.grc.nasa.gov/www/k-12/airplane/compth.html
Third-party licenses are preserved in LICENSES and the installed runtime.

BUILD FROM SOURCE
Windows x64, .NET Framework 4.x and Python 3.12 x64 are required.
Run build.cmd with your Python executable path as its first argument.
It compiles the native UI, installs pinned Python dependencies into Runtime/Lib,
packages a local interpreter and runs numerical tests. The optional CAD helper
is built only when the SOLIDWORKS API interop DLL exists on this computer.
Source is included. Run EvansEngines.exe --self-test for numerical checks,
--smoke-test for all UI tabs and --integration-test for the local cylinder path.
These test switches produce files in Evidence and exit.
