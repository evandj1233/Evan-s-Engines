using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace V10Lab {
 public static class Checks {
  static int count;static List<string> log=new List<string>();
  static void Near(double a,double b,double tol,string label){count++;if(double.IsNaN(a)||double.IsInfinity(a)||Math.Abs(a-b)>tol)throw new Exception(label+": "+a.ToString("R")+" != "+b.ToString("R"));}
  static void True(bool condition,string label){count++;if(!condition)throw new Exception(label);}
  public static int Run(){
   Inputs p=new Inputs();Near(Model.DisplacementM3(p)*1000,7.000060409177659,1e-12,"Displacement");
   Near(Model.PistonBelowTDC(0,p),0,1e-10,"TDC");Near(Model.PistonBelowTDC(180,p),p.StrokeMm,1e-10,"BDC");
   Near(Model.VE(p,2000),.825,1e-12,"VE interpolation");Near(Model.VE(p,0),.68,1e-12,"VE lower bound");Near(Model.VE(p,9000),.82,1e-12,"VE upper bound");
   List<Point> cases=new List<Point>();
   foreach(int cylinders in new[]{1,4,8,10,16})foreach(int speed in new[]{0,800,1500,3500,5500,7000})foreach(double throttle in new[]{0,.25,.5,1})for(int mode=0;mode<4;mode++){
    Inputs q=p.Copy();q.Cylinders=cylinders;Point r=Model.Calculate(q,speed,throttle,(mode&1)!=0,(mode&2)!=0);cases.Add(r);
    Near(r.BrakeKW,r.IndicatedKW-r.FrictionKW-r.PumpingKW-r.SuperchargerDriveKW-r.AccessoryKW,1e-10,"Power balance");
    Near(r.HP*Model.WattPerHP/1000,r.BrakeKW,1e-10,"HP conversion");
    Near(r.TorqueNm/Model.NmPerLbFt,r.TorqueLbFt,1e-10,"Torque conversion");
    Near(r.TorqueNm*(speed*2*Math.PI/60)/1000,r.BrakeKW,1e-10,"Torque power identity");
    True(r.AirKgS>=0&&r.FuelKgS>=0&&r.ChargeC>=q.AmbientC-1e-10,"Finite physical sign");
    True(r.IndicatedKW<=r.FuelKW+1e-10,"Energy input bound");
    double pr=((mode&1)!=0?1+q.TurboPsi*Model.PascalPerPsi/(q.AmbientKPa*1000):1)*((mode&2)!=0?1+q.SuperchargerPsi*Model.PascalPerPsi/(q.AmbientKPa*1000):1);
    Near(r.PressureRatio,pr,1e-12,"Series stage ratios; parallel turbos not doubled");
    Near(r.MeanPistonMS,2*q.StrokeMm/1000*speed/60,1e-12,"Mean piston speed");
   }
   double worstI=0,worstE=0;
   for(int i=0;i<=72000;i++){double a=i*.01;worstI=Math.Max(worstI,Math.Abs(Model.Lift(a,true,0)-CamProfile.ValveLift(a,true,0)));worstE=Math.Max(worstE,Math.Abs(Model.Lift(a,false,0)-CamProfile.ValveLift(a,false,0)));}
   True(worstI<.001&&worstE<.001,"Ported cam interpolation vs CAD law");
   Near(CamProfile.ValveLift(470,true,0),11,1e-10,"Intake peak");Near(CamProfile.ValveLift(250,false,0),10,1e-10,"Exhaust peak");
   for(int i=1;i<=10;i++){Near(Model.Phase(i),144*((i-1)/2)+(i%2==0?90:0),1e-10,"V10 phase");for(int a=0;a<720;a+=9){Near(Model.Lift(a,true,10),Model.Lift(a+10,true,0),1e-10,"Advance convention");}}
   Inputs ideal=p.Copy();ideal.TurboPsi=0;ideal.SuperchargerPsi=0;Point na=Model.Calculate(ideal,3500,1,false,false),both=Model.Calculate(ideal,3500,1,true,true);Near(na.BrakeKW,both.BrakeKW,1e-10,"Zero boost reduces to NA");
   Near(Model.CompressorOutK(300,1,.7),300,1e-12,"Unity pressure ratio");Near(Model.CompressorOutK(300,2,1),300*Math.Pow(2,2.0/7),1e-10,"Isentropic compressor");
   Inputs rich=p.Copy();rich.AFR=10;Inputs lean=p.Copy();lean.AFR=14.7;Near(Model.Calculate(rich,3500,1,false,false).IndicatedKW,Model.Calculate(lean,3500,1,false,false).IndicatedKW,1e-10,"Oxygen-limited rich fuel");
   foreach(int rpm in new[]{800,3500,7000})foreach(double ratio in Model.Gears){Point r=Model.Calculate(p,rpm,1,false,false);double outputRpm=rpm/ratio,outputNm=r.TorqueNm*ratio*.95;Near(outputRpm*2*Math.PI/60*outputNm/1000,r.BrakeKW*.95,1e-9,"Gear power conservation");}
   Inputs cloned=p.Copy();cloned.Ve[0]=1;Near(p.Ve[0],.68,1e-12,"Snapshot deep copy");
   bool rejected=false;try{Inputs bad=p.Copy();bad.RodMm=10;bad.Validate();}catch(ArgumentException){rejected=true;}True(rejected,"Impossible rod rejected");
   rejected=false;try{Inputs bad=p.Copy();bad.VeRPM[1]=500;bad.Validate();}catch(ArgumentException){rejected=true;}True(rejected,"Unordered VE rejected");
   rejected=false;try{Inputs bad=p.Copy();bad.TurboEfficiency=double.NaN;bad.Validate();}catch(ArgumentException){rejected=true;}True(rejected,"Non-finite input rejected");
   True(Model.Sweep(p,1,false,false).Count==32,"Sweep endpoints");
   string root=AppDomain.CurrentDomain.BaseDirectory;Directory.CreateDirectory(Path.Combine(root,"Evidence"));File.WriteAllText(Path.Combine(root,"Evidence","Numeric_Cases.json"),Inputs.Json.Serialize(cases));
   log.Add("PASS: "+count+" numerical assertions; "+cases.Count+" scenario points.");log.Add("Worst sampled cam/CAD motion error: intake "+worstI.ToString("R",CultureInfo.InvariantCulture)+" mm; exhaust "+worstE.ToString("R",CultureInfo.InvariantCulture)+" mm.");
   log.Add("Checks include units, energy accounting, zero speed, oxygen-limited fuel, compounded boost, ideal gearbox power, JSON deep copy, input rejection and sweep endpoints.");
   log.Add("This verifies calculation consistency. No measured engine calibration or physical validation is claimed.");File.WriteAllLines(Path.Combine(root,"Evidence","Numerical_Checks.txt"),log);return 0;
  }
 }
}
