"""Evan's Engines: closed-cylinder, variable-property air-standard model.

Uses Cantera 3.2 gas properties, not Cantera chemical kinetics. Heat release,
wall temperature/coefficient, trapped charge and losses are prescribed inputs.
No gas exchange, residual gas, dissociation, knock or turbo turbine solution.
The first-law ODE is integrated in crank degrees using fourth-order RK.
"""
from __future__ import annotations
import argparse
import csv
import json
import math
import pathlib
import sys

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parent if HERE.name == "Source" else HERE
sys.path.insert(0, str(ROOT / "Runtime" / "Lib"))
import cantera as ct
import numpy as np

W_PER_HP = 745.6998715822702

def finite(value, low, high, name):
    if not math.isfinite(value) or not low <= value <= high:
        raise ValueError(f"{name} must be finite and within [{low}, {high}]")

def solve(request, step=0.5, fuel=True, constant_cv=None):
    p = request["Inputs"]
    op = request["OperatingPoint"]
    rpm = float(op["RPM"])
    finite(rpm, 100, 7000, "RPM")
    bore, stroke, rod = (p[k] / 1000 for k in ("BoreMm", "StrokeMm", "RodMm"))
    cr = p["CompressionRatio"]
    finite(bore, .04, .2, "Bore")
    finite(stroke, .03, .2, "Stroke")
    finite(rod, stroke / 2 + .001, .4, "Rod")
    finite(cr, 5, 16, "Compression ratio")
    finite(step, .025, 2, "Crank step")
    area = math.pi * bore**2 / 4
    swept = area * stroke
    clearance = swept / (cr - 1)
    radius = stroke / 2
    cylinders = int(p["Cylinders"])
    finite(cylinders, 1, 16, "Cylinder count")
    start = 360 + request.get("StartDeg", -15)
    duration = request.get("DurationDeg", 60)
    wall_k = request.get("WallK", 450)
    htc = request.get("HeatCoefficient", 250)
    finite(start, 325, 370, "Heat-release start")
    finite(duration, 15, 120, "Heat-release duration")
    finite(wall_k, 280, 650, "Wall temperature")
    finite(htc, 0, 1000, "Heat transfer")
    gas = ct.Solution("air.yaml")
    initial_t = op["ChargeC"] + 273.15
    finite(initial_t, 200, 1000, "Charge temperature")
    gas.TPX = initial_t, 101325, "O2:0.21,N2:0.79"
    gas_r = ct.gas_constant / gas.mean_molecular_weight
    air_mass = op["AirKgS"] / (rpm / 120 * cylinders)
    finite(air_mass, 1e-8, .1, "Trapped air mass")
    fuel_mass = min(air_mass / p["AFR"], air_mass / p["StoichAFR"])
    q_total = fuel_mass * p["LHV_MJkg"] * 1e6 * p["CombustionEfficiency"] if fuel else 0
    seconds_per_deg = 1 / (6 * rpm)

    def geometry(deg):
        theta = deg * math.pi / 180
        s, c = math.sin(theta), math.cos(theta)
        root = math.sqrt(rod**2 - radius**2 * s**2)
        x = radius * (1 - c) + rod - root
        dvddeg = area * (radius * s + radius**2 * s * c / root) * math.pi / 180
        volume = clearance + area * x
        wall_area = 2 * area + math.pi * bore * volume / area
        return volume, dvddeg, wall_area

    # Normalized Wiebe burn fraction: exactly 0 -> 1 over the prescribed interval.
    def heat_rate(deg):
        z = (deg - start) / duration
        if not 0 < z < 1:
            return 0.0
        a, exponent = 6.9, 3
        return q_total * a * exponent * z**(exponent-1) * math.exp(-a * z**exponent) / duration / (1 - math.exp(-a))

    def derivative(deg, state):
        t = float(state[0])
        if not 100 < t < 6000:
            raise ValueError("Temperature left the model's permitted range; adjust assumptions.")
        volume, dvddeg, wall_area = geometry(deg)
        gas.TD = t, air_mass / volume
        cv = float(gas.cv_mass) if constant_cv is None else constant_cv
        pressure = air_mass * gas_r * t / volume
        dq = heat_rate(deg)
        qw = htc * wall_area * (t - wall_k) * seconds_per_deg
        dw = pressure * dvddeg
        return np.array([(dq - qw - dw) / (air_mass * cv), dw, dq, qw])

    def internal_u(t):
        gas.TP = t, 101325
        return gas.int_energy_mass if constant_cv is None else constant_cv * t

    y = np.array([initial_t, 0.0, 0.0, 0.0])
    u_start = internal_u(initial_t)
    trace = []
    # Include burn boundaries exactly, to avoid stepping over heat-rate switches.
    boundaries = sorted(set([180.0, 540.0, start, start + duration]))
    angles = [180.0]
    for left, right in zip(boundaries[:-1], boundaries[1:]):
        if right <= 180 or left >= 540:
            continue
        left, right = max(180, left), min(540, right)
        count = math.ceil((right-left)/step)
        angles.extend(left+(right-left)*i/count for i in range(1,count+1))
    def sample(deg):
        volume, _, _ = geometry(deg)
        trace.append({"crank_deg":deg-360,"volume_cc":volume*1e6,"pressure_bar":air_mass*gas_r*y[0]/volume/1e5,"temperature_K":float(y[0]),"work_J":float(y[1]),"heat_release_J":float(y[2]),"wall_heat_J":float(y[3])})
    sample(180)
    for left, right in zip(angles[:-1], angles[1:]):
        h = right-left
        k1=derivative(left,y)
        k2=derivative(left+h/2,y+h*k1/2)
        k3=derivative(left+h/2,y+h*k2/2)
        k4=derivative(right,y+h*k3)
        y += h*(k1+2*k2+2*k3+k4)/6
        sample(right)
    delta_u=air_mass*(internal_u(float(y[0]))-u_start)
    residual=float(y[2]-y[3]-y[1]-delta_u)
    gross_kw=float(y[1])*rpm/120*cylinders/1000
    brake_kw=gross_kw-op["FrictionKW"]-op["PumpingKW"]-op["SuperchargerDriveKW"]-op["AccessoryKW"]
    return {"solver":"0D closed-cylinder air-standard RK4 / Cantera "+ct.__version__,"data_status":"UNVALIDATED SCENARIO", "step_deg":step,"trapped_air_kg_per_cylinder":air_mass,"closed_work_J":float(y[1]),"heat_release_J":float(y[2]),"specified_heat_release_J":q_total,"wall_heat_J":float(y[3]),"delta_internal_energy_J":delta_u,"energy_residual_J":residual,"peak_pressure_bar":max(r["pressure_bar"] for r in trace),"gross_indicated_kW":gross_kw,"brake_hp_estimate":brake_kw*1000/W_PER_HP,"trace":trace}

def run(request):
    coarse=solve(request,.5)
    fine=solve(request,.25)
    work_change=abs(coarse["closed_work_J"]-fine["closed_work_J"])/max(1,abs(fine["closed_work_J"]))*100
    pressure_change=abs(coarse["peak_pressure_bar"]-fine["peak_pressure_bar"])/fine["peak_pressure_bar"]*100
    energy_relative=abs(fine["energy_residual_J"])/max(1,abs(fine["heat_release_J"]))
    passed=work_change < .5 and pressure_change < 1 and energy_relative < 1e-4
    fine.update({"work_step_change_pct":work_change,"peak_pressure_step_change_pct":pressure_change,"energy_relative_residual":energy_relative,"numerical_check":"PASS" if passed else "FAIL: refine step / inspect inputs","coarse_step_deg":.5,"request":request})
    return fine

def self_test():
    # Independent constant-cv air-standard comparison: a motored reversible
    # compression/expansion must return to its original T and zero net work.
    p={"BoreMm":95,"StrokeMm":98.756274,"RodMm":160,"Cylinders":10,"CompressionRatio":10.5,"AFR":12.8,"StoichAFR":14.7,"LHV_MJkg":43,"CombustionEfficiency":.97}
    op={"RPM":3500,"ChargeC":25,"AirKgS":.22,"FrictionKW":20,"PumpingKW":0,"SuperchargerDriveKW":0,"AccessoryKW":3}
    req={"Inputs":p,"OperatingPoint":op,"HeatCoefficient":0}
    motored=solve(req,.25,fuel=False,constant_cv=718)
    assert abs(motored["closed_work_J"])<1e-5
    assert abs(motored["trace"][-1]["temperature_K"]-298.15)<1e-5
    gamma=1+ct.gas_constant/ct.Solution("air.yaml").mean_molecular_weight/718
    # The solver composition is O2/N2; use its exact R for this reference.
    gas=ct.Solution("air.yaml");gas.TPX=298.15,101325,"O2:.21,N2:.79"
    gamma=1+ct.gas_constant/gas.mean_molecular_weight/718
    tdc=min(motored["trace"],key=lambda r:abs(r["crank_deg"]))
    expected=298.15*10.5**(gamma-1)
    assert abs(tdc["temperature_K"]-expected)<1e-4
    checks=[]
    for rpm in (1000,3500,6500):
        for boost in (1,1.7):
            req["OperatingPoint"]={**op,"RPM":rpm,"AirKgS":.22*rpm/3500*boost,"ChargeC":25+(boost-1)*20}
            req["HeatCoefficient"]=250
            result=run(req)
            assert result["numerical_check"]=="PASS"
            checks.append({k:v for k,v in result.items() if k not in ("trace","request")})
    out={"status":"PASS","motored_net_work_J":motored["closed_work_J"],"motored_final_temperature_K":motored["trace"][-1]["temperature_K"],"constant_cv_tdc_error_K":tdc["temperature_K"]-expected,"fired_cases":checks,"limitations":"Numerical consistency only; imposed heat release and air properties do not validate a real engine."}
    (ROOT/"Evidence"/"Cylinder_Checks.json").write_text(json.dumps(out,indent=2))
    print(json.dumps({k:v for k,v in out.items() if k!="fired_cases"},indent=2))

def main():
    parser=argparse.ArgumentParser();parser.add_argument("--request",type=pathlib.Path);parser.add_argument("--self-test",action="store_true");args=parser.parse_args()
    if args.self_test:self_test();return
    if not args.request:parser.error("--request is required")
    request=json.loads(args.request.read_text(encoding="utf-8-sig"))
    result=run(request)
    output=pathlib.Path(request["OutputPath"])
    output.write_text(json.dumps(result,indent=2),encoding="utf-8")
    with output.with_suffix(".csv").open("w",newline="",encoding="utf-8") as f:
        writer=csv.DictWriter(f,fieldnames=list(result["trace"][0]));writer.writeheader();writer.writerows(result["trace"])
    print(result["numerical_check"])

if __name__=="__main__":main()
