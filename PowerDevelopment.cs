using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace V10Lab {
 public sealed class PowerCandidate {
  public string Name;public Inputs Inputs;public bool Turbo,Reached;public double BoostPsi,TargetHP,CoolingKW;public Point Peak,PeakTorque;public List<Point> Curve;
 }
 public static class PowerDevelopment {
  public const double StudyBoostLimit=30;
  // Reproducible nominal E83 surrogate, not a pump-fuel certificate. Linear
  // mass blending assumes additive volumes: gasoline 745 and ethanol 790 kg/m3.
  // Component LHV 43/26.8 MJ/kg, stoichiometric AFR 14.7/9.0 are assumptions.
  // Neither octane/knock nor liquid evaporation cooling is computed.
  public static Inputs Fuel(Inputs original,bool e83){Inputs p=original.Copy();if(e83){double w=.83*790/(.83*790+.17*745);p.LHV_MJkg=(1-w)*43+w*26.8;p.StoichAFR=(1-w)*14.7+w*9;p.AFR=.85*p.StoichAFR;p.DataStatus="UNVALIDATED E83 SURROGATE: 83 volume% ethanol, lambda 0.85; no knock/evaporation solver";}return p;}
  public static PowerCandidate Evaluate(Inputs p,bool turbo,double boost,string name,double target){
   Inputs q=p.Copy();q.TurboPsi=turbo?boost:0;q.SuperchargerPsi=turbo?0:boost;q.Validate();List<Point> curve=Model.Sweep(q,1,turbo,!turbo);Point peak=curve.OrderByDescending(x=>x.HP).First();
   double eta=turbo?q.TurboEfficiency:q.SuperchargerEfficiency,hot=Model.CompressorOutK(q.AmbientC+273.15,1+boost*Model.PascalPerPsi/(q.AmbientKPa*1000),eta);
   return new PowerCandidate{Name=name,Inputs=q,Turbo=turbo,BoostPsi=boost,TargetHP=target,Peak=peak,PeakTorque=curve.OrderByDescending(x=>x.TorqueNm).First(),Curve=curve,CoolingKW=peak.AirKgS*Model.AirCp*(hot-(peak.ChargeC+273.15))/1000};
  }
  public static PowerCandidate Find(Inputs p,bool turbo,string name,double target){
   Inputs.Range(target,100,1500,"Crank horsepower target");PowerCandidate low=Evaluate(p,turbo,0,name,target),best=low;
   if(low.Peak.HP>=target){low.Reached=true;return low;}
   // Exhaustively bracket in 1 psi increments. No assumption of global
   // monotonicity of supercharger net power beyond the first crossing.
   double a=0,b=1;for(;b<=StudyBoostLimit;b++){PowerCandidate candidate=Evaluate(p,turbo,b,name,target);if(candidate.Peak.HP>best.Peak.HP)best=candidate;if(candidate.Peak.HP>=target)break;a=b;}if(b>StudyBoostLimit)return best;
   for(int k=0;k<35;k++){double mid=(a+b)/2;if(Evaluate(p,turbo,mid,name,target).Peak.HP>=target)b=mid;else a=mid;}
   PowerCandidate result=Evaluate(p,turbo,b,name,target);result.Reached=true;return result;
  }
  public static List<PowerCandidate> Compare(Inputs source,double target,bool e83,double cooling,double blowerEta){
   Inputs.Range(cooling,0,1,"Proposed cooler effectiveness");Inputs.Range(blowerEta,.3,1,"Proposed blower efficiency");Inputs baseline=Fuel(source,e83),proposed=baseline.Copy();proposed.IntercoolerEffectiveness=cooling;proposed.SuperchargerEfficiency=blowerEta;
   return new List<PowerCandidate>{Find(baseline,true,"Current assumptions / twin turbo",target),Find(baseline,false,"Current assumptions / supercharger",target),Find(proposed,true,"Proposed package / twin turbo",target),Find(proposed,false,"Proposed package / supercharger",target)};
  }
  public static void Verify(){
   Inputs original=new Inputs();string snapshot=Inputs.Json.Serialize(original);List<PowerCandidate> study=Compare(original,1000,true,.85,.75);if(snapshot!=Inputs.Json.Serialize(original))throw new Exception("Development study changed source inputs.");
   foreach(PowerCandidate r in study){if(r.Reached&&Math.Abs(r.Peak.HP-1000)>1e-6)throw new Exception("Power target solve residual.");if(r.Turbo==r.Peak.Supercharger||r.Peak.Turbo!=r.Turbo)throw new Exception("Separate boost packages were mixed.");if(r.Inputs.VeScale!=original.VeScale||r.Inputs.OttoRealization!=original.OttoRealization||r.Inputs.CombustionEfficiency!=original.CombustionEfficiency)throw new Exception("Study silently changed airflow/combustion assumptions.");if(r.CoolingKW<0||r.Peak.IndicatedKW>r.Peak.FuelKW)throw new Exception("Development energy bounds.");if(Math.Abs(r.Peak.HP-r.Curve.Max(x=>x.HP))>1e-10)throw new Exception("Peak must belong to sampled curve.");}
   if(study[1].Reached||!study[2].Reached||!study[3].Reached)throw new Exception("Expected baseline/proposed target results changed.");
   PowerCandidate miss=Find(Fuel(original,true),false,"unreachable",1500);if(miss.Reached||miss.BoostPsi!=30)throw new Exception("Unreachable study target incorrectly reported success.");
   Inputs e=Fuel(original,true);if(e.StoichAFR<=9||e.StoichAFR>=14.7||e.LHV_MJkg<=26.8||e.LHV_MJkg>=43||Math.Abs(e.AFR/e.StoichAFR-.85)>1e-12)throw new Exception("Fuel blend/lambda conversion.");
   Directory.CreateDirectory(Path.Combine(Lab.Root,"Evidence"));File.WriteAllText(Path.Combine(Lab.Root,"Evidence","Power_Development_Check.json"),Inputs.Json.Serialize(new{Status="PASS",Checks="Independent boost packages, target residual, 30 psi no-hit status, unchanged source/VE/combustion, fuel blending/lambda, energy bounds, sampled-curve peak",Fuel=e,Candidates=study}));
  }
 }
 public sealed partial class Lab {
  TabPage developmentTab;NumericUpDown hpTarget,devCooling,devBlower;ComboBox devFuel;DataGridView devGrid;Chart devPlot;Label devSummary;List<PowerCandidate> development;
  void BuildPowerDevelopment(){
   developmentTab=Tab("Power development");TableLayoutPanel layout=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=4,ColumnCount=1,Padding=new Padding(10)};layout.RowStyles.Add(new RowStyle(SizeType.Absolute,94));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,150));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,112));developmentTab.Controls.Add(layout);
   FlowLayoutPanel controls=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=true};layout.Controls.Add(controls,0,0);
   controls.Controls.Add(new Label{Text="Crank hp target",Width=105,Padding=new Padding(0,7,0,0)});hpTarget=new NumericUpDown{Minimum=100,Maximum=1500,Value=1000,Increment=50,Width=80};controls.Controls.Add(hpTarget);
   devFuel=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=245};devFuel.Items.AddRange(new object[]{"Current model fuel","Nominal E83 study (E85 blend range)"});devFuel.SelectedIndex=1;controls.Controls.Add(devFuel);
   controls.Controls.Add(new Label{Text="Cooler %",Width=72,Padding=new Padding(0,7,0,0)});devCooling=new NumericUpDown{Minimum=0,Maximum=100,Value=85,Width=55};controls.Controls.Add(devCooling);
   controls.Controls.Add(new Label{Text="Blower eta %",Width=95,Padding=new Padding(0,7,0,0)});devBlower=new NumericUpDown{Minimum=30,Maximum=100,Value=75,Width=55};controls.Controls.Add(devBlower);
   Button compare=Button(controls,"Compare target packages",RunDevelopment,true);compare.Width=220;Button loadT=Button(controls,"Load turbo concept",()=>LoadDevelopment(true),false);loadT.Width=180;Button loadS=Button(controls,"Load blower concept",()=>LoadDevelopment(false),false);loadS.Width=180;Button notes=Button(controls,"Read design notes",()=>OpenPath(Path.Combine(Root,"1000_HP_Development.md")),false);notes.Width=170;
   devGrid=Grid();devGrid.ReadOnly=true;foreach(string name in new[]{"Scenario","Boost psi*","Peak hp*","RPM","Charge C*","Cooling kW*","Drive kW*"})devGrid.Columns.Add(name,name);devGrid.Columns[0].FillWeight=230;layout.Controls.Add(devGrid,0,1);
   devPlot=NewChart("Requested boost (psi gauge)","Peak crank hp (estimate)");devPlot.ChartAreas[0].AxisX.Minimum=0;devPlot.ChartAreas[0].AxisX.Maximum=30;devPlot.ChartAreas[0].AxisX.Interval=5;layout.Controls.Add(devPlot,0,2);
   devSummary=new Label{Dock=DockStyle.Fill,Padding=new Padding(6),Font=new Font("Segoe UI",9)};layout.Controls.Add(devSummary,0,3);
   RunDevelopment();
  }
  void RunDevelopment(){
   development=PowerDevelopment.Compare(input,(double)hpTarget.Value,devFuel.SelectedIndex==1,(double)devCooling.Value/100,(double)devBlower.Value/100);devGrid.Rows.Clear();devPlot.Series.Clear();
   foreach(PowerCandidate r in development)devGrid.Rows.Add(r.Name,r.BoostPsi.ToString("F2")+(r.Reached?"":" (limit)"),r.Peak.HP.ToString("F1")+(r.Reached?"":" / target missed"),r.Peak.RPM,r.Peak.ChargeC.ToString("F1"),r.CoolingKW.ToString("F1"),r.Peak.SuperchargerDriveKW.ToString("F1"));
   Color[] colors={Color.Gray,Color.FromArgb(160,120,80),Blue,Orange};string[] names={"Current: turbos","Current: blower","Proposed: turbos","Proposed: blower"};for(int i=0;i<development.Count;i++){PowerCandidate r=development[i];Series series=Line(devPlot,names[i],colors[i]);if(i<2){series.BorderDashStyle=ChartDashStyle.Dash;series.BorderWidth=1;}for(int boost=0;boost<=PowerDevelopment.StudyBoostLimit;boost++){PowerCandidate sample=PowerDevelopment.Evaluate(r.Inputs,r.Turbo,boost,r.Name,(double)hpTarget.Value);series.Points.AddXY(boost,sample.Peak.HP);}}
   Series target=Line(devPlot,"Engine hp target",Color.FromArgb(170,80,90));target.BorderDashStyle=ChartDashStyle.Dot;target.Points.AddXY(0,(double)hpTarget.Value);target.Points.AddXY(PowerDevelopment.StudyBoostLimit,(double)hpTarget.Value);devPlot.Titles.Clear();devPlot.Titles.Add("Separate turbo and blower concepts | * unvalidated model estimates");
   foreach(DataGridViewRow row in devGrid.Rows)foreach(DataGridViewCell cell in row.Cells)cell.ToolTipText=Convert.ToString(cell.Value);
   devSummary.Text="Boost is solved to meet the sampled target at full throttle, within a 0–30 psi STUDY range; this is not a safe-boost recommendation.\nProposed cooler and blower efficiency are assumptions requiring hardware maps and thermal validation. VE/combustion assumptions stay unchanged.\nE83 option: nominal 83 vol% ethanol, lambda 0.85; pump E85 varies. No knock, evaporation, turbo matching or durability solver.\nReferences: Demon 170 1025 hp (E85); ZR1/ZR1X LT7 1064 hp. ZR1X 1250 hp includes a separate 186 hp electric drive.";
   string id=DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff"),folder=Path.Combine(Root,"Runs","Power_Development_"+id);Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"Study.json"),Inputs.Json.Serialize(new{TargetHP=hpTarget.Value,OriginalInputs=input.Copy(),Fuel=devFuel.Text,CoolingPercent=devCooling.Value,BlowerEfficiencyPercent=devBlower.Value,Candidates=development}));
   foreach(PowerCandidate r in development)File.WriteAllText(Path.Combine(folder,r.Name.Replace(" / ","_")+".csv"),Model.CSV(r.Curve));
   File.WriteAllText(Path.Combine(folder,"Twin_Turbo_Concept.json"),Inputs.Json.Serialize(new CaseFile{Title="1000 hp target / twin turbo concept",Inputs=development[2].Inputs,RPM=development[2].Peak.RPM,Throttle=1,Turbo=true,Supercharger=false}));File.WriteAllText(Path.Combine(folder,"Supercharger_Concept.json"),Inputs.Json.Serialize(new CaseFile{Title="1000 hp target / supercharger concept",Inputs=development[3].Inputs,RPM=development[3].Peak.RPM,Throttle=1,Turbo=false,Supercharger=true}));
  }
  void LoadDevelopment(bool useTurbo){
   RunDevelopment();PowerCandidate r=development[useTurbo?2:3];updating=true;try{input=r.Inputs.Copy();rpm.Value=(decimal)r.Peak.RPM;throttle.Value=100;turbo.Checked=useTurbo;sc.Checked=!useTurbo;}finally{updating=false;}FillInputs();RefreshPoint();RunSweep(false);tabs.SelectedIndex=0;BeginLive();cadUpdated=-1;status.Text=r.Reached?"Loaded a target-matched concept; power, boost and cooling are unvalidated assumptions.":"Target was not reached within the study bound; loaded the maximum studied point.";
  }
  public void PowerDevelopmentCheck(){
   PowerDevelopment.Verify();RunDevelopment();if(development.Count!=4||devGrid.Rows.Count!=4||devPlot.Series.Count!=5)throw new Exception("Development UI comparison failed.");LoadDevelopment(true);if(!turbo.Checked||sc.Checked||Math.Abs(point.HP-1000)>1e-6)throw new Exception("Turbo concept loading failed.");LoadDevelopment(false);if(turbo.Checked||!sc.Checked||Math.Abs(point.HP-1000)>1e-6)throw new Exception("Blower concept loading failed.");input=new Inputs();turbo.Checked=false;sc.Checked=false;FillInputs();RefreshPoint();tabs.SelectedTab=developmentTab;RunDevelopment();Application.DoEvents();using(Bitmap bmp=new Bitmap(Width,Height)){DrawToBitmap(bmp,new Rectangle(0,0,Width,Height));bmp.Save(Path.Combine(Root,"Evidence","Power_Development.png"));}
   File.WriteAllText(Path.Combine(Root,"Evidence","Power_UI_Check.txt"),"PASS: separate 4-row target comparison, chart, full study/curve/case persistence, independent turbo/blower case loading at solved HP. No calibrated power/durability claim.");
  }
  public void OpenDevelopment(){tabs.SelectedTab=developmentTab;RunDevelopment();}
  public void StartDevelopmentConcept(bool useTurbo){LoadDevelopment(useTurbo);}
 }
}
