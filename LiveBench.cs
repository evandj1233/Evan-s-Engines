using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace V10Lab {
 // The first-order transition is display smoothing, not a calibrated engine
 // transient. The analytical update is independent of UI frame rate.
 public sealed class LiveState {
  public bool Enabled;public double RPM,VisualAngle;
  public void Advance(double dt,double target){
   Inputs.Range(dt,0,60,"Display time step");Inputs.Range(target,0,7000,"RPM target");
   double goal=Enabled?target:0,tau=Enabled?.7:.45,old=RPM,decay=Math.Exp(-dt/tau);
   RPM=goal+(old-goal)*decay;
   double revIntegral=goal*dt+(old-goal)*tau*(1-decay);
   VisualAngle=Model.Mod(VisualAngle+revIntegral*6/50,720);
  }
 }
 public sealed class LiveSample {
  public double Seconds;public string UTC,InputId;public Point Point;
 }
 public sealed partial class Lab {
  readonly LiveState live=new LiveState();SmoothPanel liveCanvas;Button liveStart,recordButton;
  Point livePoint;double lastLiveSample=-1;bool recording;
  readonly List<LiveSample> liveHistory=new List<LiveSample>(),recorded=new List<LiveSample>();
  readonly Dictionary<string,Inputs> recordedInputs=new Dictionary<string,Inputs>();
  void BuildLiveBench(){
   TabPage page=Tab("Live bench");FlowLayoutPanel bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=100,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Padding=new Padding(8),BackColor=Color.White,WrapContents=true};
   liveStart=Button(bar,"Start engine model",()=>{live.Enabled=!live.Enabled;liveStart.Text=live.Enabled?"Stop engine model":"Start engine model";},true);liveStart.Width=180;
   recordButton=Button(bar,"Record live data",()=>{recording=!recording;recordButton.Text=recording?"Stop recording":"Record live data";if(recording&&recorded.Count>=6000){recorded.Clear();recordedInputs.Clear();}},false);recordButton.Width=160;
   Button save=Button(bar,"Save live log",SaveLive,false);save.Width=135;
   Label label=new Label{Text="LIVE CALCULATED DATA\nUse RPM, throttle and boost at left",Width=270,Height=40,Padding=new Padding(6,4,0,0),Font=new Font("Segoe UI",9)};bar.Controls.Add(label);bar.SetFlowBreak(label,true);BuildCadControls(bar);
   liveCanvas=new SmoothPanel{Dock=DockStyle.Fill,BackColor=Color.FromArgb(13,24,38)};liveCanvas.Paint+=DrawLive;page.Controls.Add(liveCanvas);page.Controls.Add(bar);
  }
  void AdvanceLive(double dt,double now){
   if(liveCanvas==null||input==null||rpm==null)return;
   live.Advance(Math.Min(dt,60),(double)rpm.Value);
   livePoint=Model.Calculate(input,live.RPM,(double)throttle.Value/100,live.Enabled&&turbo.Checked,live.Enabled&&sc.Checked);
   if(!live.Enabled){livePoint=Model.Calculate(input,0,(double)throttle.Value/100,false,false);livePoint.RPM=live.RPM;livePoint.MeanPistonMS=2*input.StrokeMm/1000*live.RPM/60;livePoint.ManifoldKPa=input.AmbientKPa;livePoint.ChargeC=input.AmbientC;}
   if(lastLiveSample<0)lastLiveSample=now-.2;
   if(now-lastLiveSample>=.2){lastLiveSample=now-((now-lastLiveSample)%.2);LiveSample sample=new LiveSample{Seconds=now,UTC=DateTime.UtcNow.ToString("o"),Point=livePoint,InputId=""};liveHistory.Add(sample);if(liveHistory.Count>300)liveHistory.RemoveAt(0);
    if(recording){string json=Inputs.Json.Serialize(input),id;using(SHA256 hash=SHA256.Create())id=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(json))).Replace("-","").Substring(0,16);sample.InputId=id;if(!recordedInputs.ContainsKey(id))recordedInputs.Add(id,input.Copy());recorded.Add(sample);if(recorded.Count>=6000){recording=false;recordButton.Text="Record live data";status.Text="Live recording reached 6000 samples. Save the log before starting another.";}}
   }
   UpdateCadPreview(now);if(tabs.SelectedIndex==0)liveCanvas.Invalidate();
  }
  void SaveLive(){if(recorded.Count==0)throw new Exception("Press Record live data, run the model, then stop recording and save the log.");using(SaveFileDialog d=new SaveFileDialog{Filter="Live data (*.csv)|*.csv",InitialDirectory=Path.Combine(Root,"Runs"),FileName="Live_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".csv"})if(d.ShowDialog()==DialogResult.OK){WriteLiveLog(d.FileName);status.Text="Saved "+recorded.Count+" live samples with timestamps and input snapshots.";}}
  void WriteLiveLog(string path){
   StringBuilder output=new StringBuilder("elapsed_seconds,utc,input_id,");string header=Model.CSV(new List<Point>()).Trim();output.Append(header+"\r\n");
   foreach(LiveSample sample in recorded){string row=Model.CSV(new List<Point>{sample.Point}).Split(new[]{"\r\n"},StringSplitOptions.RemoveEmptyEntries)[1];output.Append(sample.Seconds.ToString("G17",CultureInfo.InvariantCulture)+","+sample.UTC+","+sample.InputId+","+row+"\r\n");}
   File.WriteAllText(path,output.ToString());File.WriteAllText(path+".inputs.json",Inputs.Json.Serialize(new{DataSource="Live calculated mean-value scenarios; no hardware telemetry",InputSets=recordedInputs,DisplayRPMTransition="First-order smoothing: 0.7 s on, 0.45 s off; not a physical transient",Samples=recorded.Count}));
  }
  static readonly Color LiveBlue=Color.FromArgb(84,176,255),LiveOrange=Color.FromArgb(255,165,75),LiveGreen=Color.FromArgb(73,220,170),LiveMuted=Color.FromArgb(147,167,189);
  static void TextAt(Graphics g,string text,float x,float y,float size,Color color,bool bold=false,bool center=false){using(Font font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular))using(SolidBrush brush=new SolidBrush(color)){if(center)x-=g.MeasureString(text,font).Width/2;g.DrawString(text,font,brush,x,y);}}
  static void Gauge(Graphics g,float x,float y,float radius,string name,string unit,double value,double min,double max,Color color){
   using(SolidBrush dial=new SolidBrush(Color.FromArgb(18,34,51)))g.FillEllipse(dial,x-radius,y-radius,2*radius,2*radius);
   using(Pen rim=new Pen(Color.FromArgb(51,71,91),5))g.DrawArc(rim,x-radius+6,y-radius+6,2*radius-12,2*radius-12,135,270);
   double fraction=Math.Max(0,Math.Min(1,(value-min)/(max-min)));
   using(Pen active=new Pen(color,5))g.DrawArc(active,x-radius+6,y-radius+6,2*radius-12,2*radius-12,135,(float)(270*fraction));
   for(int i=0;i<=10;i++){double a=(135+i*27)*Math.PI/180;float inner=radius-14,outer=radius-7;using(Pen tick=new Pen(LiveMuted,i%2==0?2:1))g.DrawLine(tick,x+inner*(float)Math.Cos(a),y+inner*(float)Math.Sin(a),x+outer*(float)Math.Cos(a),y+outer*(float)Math.Sin(a));if(i%2==0)TextAt(g,(min+(max-min)*i/10).ToString("0",CultureInfo.InvariantCulture),x+(radius-28)*(float)Math.Cos(a),y+(radius-28)*(float)Math.Sin(a)-5,7,LiveMuted,false,true);}
   double theta=(135+270*fraction)*Math.PI/180;using(Pen needle=new Pen(color,3)){needle.StartCap=LineCap.Round;needle.EndCap=LineCap.Round;g.DrawLine(needle,x,y,x+(radius-32)*(float)Math.Cos(theta),y+(radius-32)*(float)Math.Sin(theta));}
   using(SolidBrush hub=new SolidBrush(color))g.FillEllipse(hub,x-5,y-5,10,10);
   TextAt(g,name,x,y-radius-23,10,Color.White,true,true);TextAt(g,value.ToString("0.0",CultureInfo.InvariantCulture),x,y+28,20,Color.White,true,true);TextAt(g,unit,x,y+55,8,LiveMuted,false,true);
  }
  void DrawLive(object sender,PaintEventArgs e){
   Graphics g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;float scale=Math.Min(liveCanvas.ClientSize.Width/900f,liveCanvas.ClientSize.Height/610f);if(scale<=0)return;g.TranslateTransform((liveCanvas.ClientSize.Width-900*scale)/2,0);g.ScaleTransform(scale,scale);
   Point p=livePoint??Model.Calculate(input,0,0,false,false);bool on=live.Enabled;
   TextAt(g,on?"RUNNING  /  DYNO SPEED HOLD":"ENGINE MODEL STOPPED",20,9,11,on?LiveGreen:LiveMuted,true);TextAt(g,recording?"REC  "+recorded.Count+" samples":"~5 Hz data  /  ~30 Hz display",650,10,9,recording?LiveOrange:LiveMuted,false,false);
   double hpMax=Math.Max(750,Math.Ceiling(Math.Abs(p.HP)/250)*250),torqueMax=Math.Max(1000,Math.Ceiling(Math.Abs(p.TorqueNm)/500)*500);
   Gauge(g,112,130,76,"ENGINE SPEED","RPM",p.RPM,0,7000,LiveBlue);Gauge(g,337,130,76,"BRAKE POWER","hp · estimate",p.HP,Math.Min(0,Math.Floor(p.HP/250)*250),hpMax,LiveOrange);Gauge(g,562,130,76,"TORQUE","N m · estimate",p.TorqueNm,Math.Min(0,Math.Floor(p.TorqueNm/500)*500),torqueMax,LiveBlue);Gauge(g,787,130,76,"MANIFOLD","psi gauge",(p.ManifoldKPa-input.AmbientKPa)*1000/Model.PascalPerPsi,-15,40,LiveGreen);
   if(liveSweepPeak!=null)TextAt(g,String.Format(CultureInfo.InvariantCulture,"{0}  |  Full-throttle peak estimate: {1:F1} hp @ {2:0} RPM (200 RPM samples)",point.Mode,liveSweepPeak.HP,liveSweepPeak.RPM),20,211,9,LiveMuted);
   using(Pen rule=new Pen(Color.FromArgb(42,64,86),1))g.DrawLine(rule,20,230,880,230);
   if(!DrawCadEngine(g)){
   TextAt(g,input.Cylinders+"-CYLINDER MECHANISM",20,244,10,Color.White,true);TextAt(g,"Visible motion: 1/50 crank speed",20,264,9,LiveMuted);
   int columns=(input.Cylinders+1)/2;
   for(int i=0;i<input.Cylinders;i++){
    int bank=i%2,col=i/2;float spacing=Math.Min(86,490f/Math.Max(1,columns)),x=35+col*spacing,y=296+bank*99;
    double cycle=Model.Mod(live.VisualAngle-Model.Phase(i+1,input),720),below=Model.PistonBelowTDC(cycle,input),il=CamProfile.ValveLift(cycle,true,0),el=CamProfile.ValveLift(cycle,false,0);
    float strokePixels=31,pistonY=y+14+(float)(below/input.StrokeMm)*strokePixels;
    using(Pen wall=new Pen(Color.FromArgb(155,175,193),2))g.DrawRectangle(wall,x,y+8,48,52);
    Color charge=cycle<180?Color.FromArgb(90,65,25):cycle<360?Color.FromArgb(60,39,34):Color.FromArgb(25,54,80);
    using(SolidBrush gas=new SolidBrush(charge))g.FillRectangle(gas,x+2,y+10,44,Math.Max(0,pistonY-y-10));
    using(LinearGradientBrush piston=new LinearGradientBrush(new RectangleF(x+2,pistonY,44,8),Color.FromArgb(220,230,237),Color.FromArgb(122,146,168),90))g.FillRectangle(piston,x+2,pistonY,44,8);
    float cx=x+24,cy=y+80;double a=cycle*Math.PI/180;float jx=cx+10*(float)Math.Sin(a),jy=cy-10*(float)Math.Cos(a);using(Pen rod=new Pen(Color.FromArgb(184,204,222),3))g.DrawLine(rod,cx,pistonY+5,jx,jy);using(Pen crank=new Pen(LiveBlue,3)){g.DrawEllipse(crank,cx-11,cy-11,22,22);g.DrawLine(crank,cx,cy,jx,jy);}
    using(Pen valve=new Pen(LiveBlue,2))g.DrawLine(valve,x+15,y-2+(float)il*.5f,x+15,y+8+(float)il*.5f);using(Pen valve=new Pen(LiveOrange,2))g.DrawLine(valve,x+33,y-2+(float)el*.5f,x+33,y+8+(float)el*.5f);
    using(SolidBrush fire=new SolidBrush(on&&cycle<25?LiveOrange:Color.FromArgb(50,67,83)))g.FillEllipse(fire,x+37,y-7,7,7);TextAt(g,(i+1).ToString(),x+19,y+61,7,LiveMuted);
   }
   }
   TextAt(g,"LIVE READINGS",570,244,10,Color.White,true);
   string[] names={"Time","Throttle","Airflow","Fuel flow","Charge temp","Manifold abs.","Piston speed","Blower drive","Turbo demand"};
   string[] values={clock.Elapsed.TotalSeconds.ToString("F1")+" s",throttle.Value+" %",p.AirKgS.ToString("F4")+" kg/s",p.FuelKgS.ToString("F4")+" kg/s",p.ChargeC.ToString("F1")+" C",p.ManifoldKPa.ToString("F2")+" kPa",p.MeanPistonMS.ToString("F2")+" m/s",p.SuperchargerDriveKW.ToString("F2")+" kW",p.TurboCompressorKW.ToString("F2")+" kW"};
   for(int i=0;i<names.Length;i++){TextAt(g,names[i],570,270+i*19,9,LiveMuted);TextAt(g,values[i],735,270+i*19,9,Color.White,true);}
   string[] blocks={"AIR",turbo.Checked?"TWIN TURBO":"TURBO OFF",sc.Checked?"BLOWER":"BLOWER OFF","ENGINE","DYNO"};
   for(int i=0;i<5;i++){float x=20+i*104;using(Pen box=new Pen(on?LiveBlue:Color.FromArgb(65,83,101),1))g.DrawRectangle(box,x,498,94,29);TextAt(g,blocks[i],x+47,506,8,i==1&&!turbo.Checked||i==2&&!sc.Checked?LiveMuted:Color.White,false,true);if(i<4)TextAt(g,"→",x+94,503,12,LiveBlue);}
   if(on){for(int i=0;i<4;i++){float x=114+i*104+(float)(Model.Mod(clock.Elapsed.TotalSeconds*2,1)*10);using(SolidBrush flow=new SolidBrush(LiveBlue))g.FillEllipse(flow,x,487,4,4);}}
   for(int stage=0;stage<2;stage++){bool active=on&&(stage==0?turbo.Checked:sc.Checked);for(int rotor=0;rotor<(stage==0?2:1);rotor++){float x=stage==0?153+rotor*27:275,y=541;double a=live.VisualAngle*Math.PI/180*(stage==0?8:1.5);using(Pen blades=new Pen(active?LiveGreen:Color.FromArgb(60,79,96),1.5f)){g.DrawEllipse(blades,x-9,y-9,18,18);for(int blade=0;blade<4;blade++){double b=a+blade*Math.PI/2;g.DrawLine(blades,x,y,x+8*(float)Math.Cos(b),y+8*(float)Math.Sin(b));}}}}
   TextAt(g,"ROLLING RPM",570,457,8,LiveBlue);TextAt(g,"POWER",699,457,8,LiveOrange);using(Pen border=new Pen(Color.FromArgb(50,70,90),1))g.DrawRectangle(border,570,478,305,70);
   if(liveHistory.Count>1){PointF[] rpmPoints=new PointF[liveHistory.Count],hpPoints=new PointF[liveHistory.Count];for(int i=0;i<liveHistory.Count;i++){float x=570+305f*i/Math.Max(1,liveHistory.Count-1);rpmPoints[i]=new PointF(x,546-(float)(liveHistory[i].Point.RPM/7000)*66);hpPoints[i]=new PointF(x,546-(float)Math.Max(0,Math.Min(1,liveHistory[i].Point.HP/hpMax))*66);}using(Pen pen=new Pen(LiveBlue,2))g.DrawLines(pen,rpmPoints);using(Pen pen=new Pen(LiveOrange,2))g.DrawLines(pen,hpPoints);}
   TextAt(g,"Calculated scenario · no sensor connection · speed transition smoothed for display",20,565,9,LiveMuted);
   TextAt(g,"RPM range is not a validated redline. Power and boost require measured calibration.",20,585,9,LiveMuted);
  }
  public void LiveCheck(){
   tabs.SelectedIndex=0;live.Enabled=true;liveStart.Text="Stop engine model";recording=true;
   for(int i=1;i<=60;i++)AdvanceLive(.05,i*.05+clock.Elapsed.TotalSeconds);
   if(Math.Abs(live.RPM-(double)rpm.Value)>40)throw new Exception("Live RPM did not approach its setpoint.");
   if(liveHistory.Count<10||recorded.Count<10)throw new Exception("Live history / recording did not update.");
   double expected=Model.Calculate(input,live.RPM,(double)throttle.Value/100,turbo.Checked,sc.Checked).HP;if(Math.Abs(livePoint.HP-expected)>1e-9)throw new Exception("Live gauges are not reading the model.");
   recording=false;recordButton.Text="Record live data";WriteLiveLog(Path.Combine(Root,"Evidence","Live_Test.csv"));
   Application.DoEvents();using(Bitmap bmp=new Bitmap(Width,Height)){DrawToBitmap(bmp,new Rectangle(0,0,Width,Height));bmp.Save(Path.Combine(Root,"Evidence","Live_Bench.png"));}
   File.WriteAllText(Path.Combine(Root,"Evidence","Live_Check.txt"),"PASS: Start/Stop state, smoothed RPM, calculated gauges, 5 Hz rolling history, live CSV recording and input snapshots. Display transition is not a physical transient model.");
  }
  public void BeginLive(){live.Enabled=true;liveStart.Text="Stop engine model";}
 }
}
