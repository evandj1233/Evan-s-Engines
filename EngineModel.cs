using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Web.Script.Serialization;

namespace V10Lab {
    // SI internally. This is an explicit steady-state scenario model, not a gas-
    // exchange, combustion, knock, turbo-map or structural simulation.
    public sealed class Inputs {
        public double BoreMm=95, StrokeMm=98.756274, RodMm=160;
        public int Cylinders=10;
        public double CompressionRatio=10.5, AmbientKPa=101.325, AmbientC=25;
        public double AFR=12.8, StoichAFR=14.7, LHV_MJkg=43;
        public double Gamma=1.32, OttoRealization=0.62, CombustionEfficiency=0.97;
        public double VeScale=1, FrictionBaseBar=0.45, FrictionLinear=0.04, FrictionSquare=0.001;
        public double AccessoryKW=3, TurboPsi=6, SuperchargerPsi=4;
        public double TurboEfficiency=0.72, SuperchargerEfficiency=0.65;
        public double IntercoolerEffectiveness=0.7, DriveEfficiency=0.95;
        public double TurboExhaustLossBar=0.25;
        public double[] VeRPM={800,1500,2500,3500,4500,5500,6500,7000};
        public double[] Ve={0.68,0.78,0.87,0.94,0.98,0.95,0.88,0.82};
        public string ModelVersion="1.0.0";
        public string DataStatus="UNVALIDATED SCENARIO: airflow, combustion, losses and boost are assumptions";
        public Inputs Copy() {return Json.Deserialize<Inputs>(Json.Serialize(this));}
        public void Validate() {
            Range(BoreMm,40,200,"Bore"); Range(StrokeMm,30,200,"Stroke");
            Range(RodMm,StrokeMm/2+1,400,"Rod length");
            if(Cylinders<1||Cylinders>16)throw new ArgumentException("Choose 1 through 16 cylinders.");
            Range(CompressionRatio,5,16,"Compression ratio");Range(AmbientKPa,60,120,"Ambient pressure");Range(AmbientC,-20,60,"Ambient temperature");
            Range(AFR,8,20,"Air/fuel ratio");Range(StoichAFR,10,18,"Stoichiometric ratio");Range(LHV_MJkg,20,50,"Fuel heating value");
            Range(Gamma,1.1,1.67,"Specific-heat ratio");Range(OttoRealization,0.1,1,"Otto realization");Range(CombustionEfficiency,0.1,1,"Combustion efficiency");
            Range(VeScale,0.4,1.6,"VE multiplier");Range(FrictionBaseBar,0,3,"Friction base");Range(FrictionLinear,0,0.2,"Friction linear");Range(FrictionSquare,0,0.01,"Friction square");
            Range(AccessoryKW,0,30,"Accessory power");Range(TurboPsi,0,20,"Turbo stage boost");Range(SuperchargerPsi,0,15,"Supercharger stage boost");
            Range(TurboEfficiency,0.3,1,"Turbo compressor efficiency");Range(SuperchargerEfficiency,0.3,1,"Supercharger efficiency");
            Range(IntercoolerEffectiveness,0,1,"Intercooler effectiveness");Range(DriveEfficiency,0.5,1,"Drive efficiency");Range(TurboExhaustLossBar,0,3,"Turbo exhaust loss");
            if(Ve==null||VeRPM==null||Ve.Length!=VeRPM.Length||Ve.Length<2)throw new ArgumentException("VE table needs matching RPM and VE rows.");
            for(int i=0;i<Ve.Length;i++){Range(VeRPM[i],0,10000,"VE RPM");Range(Ve[i],0.1,1.6,"VE");if(i>0&&VeRPM[i]<=VeRPM[i-1])throw new ArgumentException("VE RPM rows must increase.");}
        }
        public static void Range(double x,double lo,double hi,string name) {if(double.IsNaN(x)||double.IsInfinity(x)||x<lo||x>hi)throw new ArgumentException(name+" must be between "+lo+" and "+hi+".");}
        public static JavaScriptSerializer Json {get{return new JavaScriptSerializer();}}
    }
    public sealed class Point {
        public double RPM,Throttle,DisplacementL,VE,ManifoldKPa,ChargeC,AirKgS,FuelKgS;
        public double PressureRatio,TurboCompressorKW,SuperchargerDriveKW;
        public double FuelKW,IndicatedKW,FrictionKW,PumpingKW,AccessoryKW,BrakeKW;
        public double HP,TorqueNm,TorqueLbFt,BMEPBar,MeanPistonMS,IdealOttoEfficiency,OverallEfficiency;
        public bool Turbo,Supercharger;
        public string Mode {get{return Turbo?(Supercharger?"Twin turbo + supercharger":"Twin turbo"):(Supercharger?"Supercharger":"Naturally aspirated");}}
    }
    public static class Model {
        public const double WattPerHP=745.6998715822702, NmPerLbFt=1.3558179483314004;
        public const double PascalPerPsi=6894.757293168, AirR=287.05, AirCp=1005, AirGamma=1.4;
        public static double DisplacementM3(Inputs p){return Math.PI*p.BoreMm*p.BoreMm*p.StrokeMm*p.Cylinders/4e9;}
        public static double VE(Inputs p,double rpm){
            if(rpm<=p.VeRPM[0])return p.Ve[0]*p.VeScale;
            for(int i=1;i<p.Ve.Length;i++)if(rpm<=p.VeRPM[i])return (p.Ve[i-1]+(p.Ve[i]-p.Ve[i-1])*(rpm-p.VeRPM[i-1])/(p.VeRPM[i]-p.VeRPM[i-1]))*p.VeScale;
            return p.Ve[p.Ve.Length-1]*p.VeScale;
        }
        public static double CompressorOutK(double t,double pr,double efficiency){return t*(1+(Math.Pow(pr,(AirGamma-1)/AirGamma)-1)/efficiency);}
        public static Point Calculate(Inputs p,double rpm,double throttle,bool turbo,bool sc){
            p.Validate();Inputs.Range(rpm,0,7000,"Operating RPM (simulation range)");Inputs.Range(throttle,0,1,"Throttle");
            Point r=new Point();r.RPM=rpm;r.Throttle=throttle;r.Turbo=turbo;r.Supercharger=sc;
            double ambient=p.AmbientKPa*1000,t0=p.AmbientC+273.15,t=t0,pressure=ambient;
            double turboDelta=0,scDelta=0;
            // Two parallel turbos share total airflow and the SAME pressure ratio.
            // Both devices on = turbo then supercharger in series, cooled after each.
            if(turbo){double pr=1+p.TurboPsi*PascalPerPsi/ambient;double hot=CompressorOutK(t,pr,p.TurboEfficiency);turboDelta=hot-t;t+=turboDelta*(1-p.IntercoolerEffectiveness);pressure*=pr;}
            if(sc){double pr=1+p.SuperchargerPsi*PascalPerPsi/ambient;double hot=CompressorOutK(t,pr,p.SuperchargerEfficiency);scDelta=hot-t;t+=scDelta*(1-p.IntercoolerEffectiveness);pressure*=pr;}
            r.PressureRatio=pressure/ambient;
            // Explicit assumed throttle pressure law; no idle governor or transient solver.
            pressure*=0.25+0.75*throttle;
            r.ManifoldKPa=pressure/1000;r.ChargeC=t-273.15;r.DisplacementL=DisplacementM3(p)*1000;r.VE=VE(p,rpm);
            r.AirKgS=pressure/(AirR*t)*DisplacementM3(p)*rpm/120*r.VE;
            r.FuelKgS=r.AirKgS/p.AFR;
            r.FuelKW=r.FuelKgS*p.LHV_MJkg*1000;
            r.IdealOttoEfficiency=1-Math.Pow(p.CompressionRatio,1-p.Gamma);
            // Rich excess fuel cannot create energy beyond the available oxygen.
            double burnableFuel=Math.Min(r.FuelKgS,r.AirKgS/p.StoichAFR);
            r.IndicatedKW=burnableFuel*p.LHV_MJkg*1000*p.CombustionEfficiency*r.IdealOttoEfficiency*p.OttoRealization;
            r.MeanPistonMS=2*p.StrokeMm/1000*rpm/60;
            double fmep=p.FrictionBaseBar+p.FrictionLinear*r.MeanPistonMS+p.FrictionSquare*r.MeanPistonMS*r.MeanPistonMS;
            r.FrictionKW=fmep*1e5*DisplacementM3(p)*rpm/120/1000;
            double exhaustExtra=turbo?p.TurboExhaustLossBar*(r.PressureRatio-1):0;
            double pmep=Math.Max(0,(ambient-pressure)/1e5)+exhaustExtra;
            r.PumpingKW=pmep*1e5*DisplacementM3(p)*rpm/120/1000;
            r.TurboCompressorKW=r.AirKgS*AirCp*turboDelta/1000;
            r.SuperchargerDriveKW=r.AirKgS*AirCp*scDelta/p.DriveEfficiency/1000;
            r.AccessoryKW=rpm>0?p.AccessoryKW:0;
            r.BrakeKW=r.IndicatedKW-r.FrictionKW-r.PumpingKW-r.SuperchargerDriveKW-r.AccessoryKW;
            r.HP=r.BrakeKW*1000/WattPerHP;
            r.TorqueNm=rpm>0?r.BrakeKW*1000/(rpm*2*Math.PI/60):0;
            r.TorqueLbFt=r.TorqueNm/NmPerLbFt;
            r.BMEPBar=rpm>0?r.TorqueNm*4*Math.PI/DisplacementM3(p)/1e5:0;
            r.OverallEfficiency=r.FuelKW>0?r.BrakeKW/r.FuelKW:0;
            return r;
        }
        public static double Mod(double x,double n){return (x%n+n)%n;}
        public static double Lift(double cycle,bool intake,double advance){double half=intake?150:144,center=intake?470:250,h=intake?11:10;double a=Mod(cycle+advance-center+360,720)-360;return Math.Abs(a)<half?0.5*h*(1+Math.Cos(Math.PI*a/half)):0;}
        public static double PistonBelowTDC(double cycle,Inputs p){double a=cycle*Math.PI/180,r=p.StrokeMm/2,l=p.RodMm;return r*(1-Math.Cos(a))+l-Math.Sqrt(l*l-r*r*Math.Sin(a)*Math.Sin(a));}
        public static double Phase(int cylinder){int j=(cylinder-1)/2;return 144*j+((cylinder%2==0)?90:0);}
        public static double Phase(int cylinder,Inputs p){return p.Cylinders==10?Phase(cylinder):(cylinder-1)*720.0/p.Cylinders;}
        public static readonly double[] Gears={4.984615384615,3.6,1.917159763314,1.384615384615,1,0.722222222222};
        public static List<Point> Sweep(Inputs p,double throttle,bool turbo,bool sc){List<Point> rows=new List<Point>();for(int rpm=800;rpm<=7000;rpm+=200)rows.Add(Calculate(p,rpm,throttle,turbo,sc));return rows;}
        public static string CSV(List<Point> points){
            string[] headers={"mode","rpm","throttle_fraction","brake_hp_ESTIMATE","torque_Nm_ESTIMATE","brake_kW","BMEP_bar","air_kg_s","fuel_kg_s","manifold_kPa_absolute","charge_C","VE","mean_piston_m_s","indicated_kW","friction_kW","pumping_kW","supercharger_drive_kW","turbo_compressor_kW","accessory_kW","data_status"};
            System.Text.StringBuilder b=new System.Text.StringBuilder(String.Join(",",headers)+"\r\n");
            foreach(Point r in points){b.Append(r.Mode);foreach(double v in new double[]{r.RPM,r.Throttle,r.HP,r.TorqueNm,r.BrakeKW,r.BMEPBar,r.AirKgS,r.FuelKgS,r.ManifoldKPa,r.ChargeC,r.VE,r.MeanPistonMS,r.IndicatedKW,r.FrictionKW,r.PumpingKW,r.SuperchargerDriveKW,r.TurboCompressorKW,r.AccessoryKW})b.Append(","+v.ToString("G17",CultureInfo.InvariantCulture));b.Append(",UNVALIDATED SCENARIO\r\n");}return b.ToString();
        }
    }
}
