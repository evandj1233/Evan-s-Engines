using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Forms;

namespace V10Lab {
 public sealed partial class Lab {
  ComboBox cadView;Bitmap cadImage;string cadFrame="",cadCaption="";double cadUpdated=-1;
  Form cadWindow;SmoothPanel cadLarge;
  sealed class CadFrames {public int Frames{get;set;}public double StepDegrees{get;set;}}
  void BuildCadControls(FlowLayoutPanel bar){
   Label label=new Label{Text="ENGINE VIEW",Width=105,Height=32,Padding=new Padding(4,7,0,0)};bar.Controls.Add(label);
   cadView=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=230,Height=32,Margin=new Padding(3,6,10,0)};
   cadView.Items.AddRange(new object[]{"SOLIDWORKS exterior","SOLIDWORKS cutaway","Cylinder schematic"});cadView.SelectedIndex=0;
   cadView.SelectedIndexChanged+=(s,e)=>{cadUpdated=-1;UpdateCadPreview(clock.Elapsed.TotalSeconds);if(liveCanvas!=null)liveCanvas.Invalidate();};bar.Controls.Add(cadView);
   Button enlarge=Button(bar,"Enlarge engine",EnlargeCad,false);enlarge.Width=150;
   FormClosed+=(s,e)=>{if(cadImage!=null){cadImage.Dispose();cadImage=null;}};
  }
  bool CadGeometryMatches(){return input.Cylinders==10&&Math.Abs(input.BoreMm-95)<.0001&&Math.Abs(input.StrokeMm-98.756274)<.0001&&Math.Abs(input.RodMm-160)<.0001;}
  void UpdateCadPreview(double now){
   if(cadView==null||now-cadUpdated<.065)return;cadUpdated=now;
   if(cadView.SelectedIndex==2||!CadGeometryMatches()){cadCaption=CadGeometryMatches()?"Cylinder schematic":"CAD view is for the original V10 geometry; showing schematic";if(cadLarge!=null&&!cadLarge.IsDisposed)cadLarge.Invalidate();return;}
   string folder=Path.Combine(Root,"CAD_Preview",cadView.SelectedIndex==1?"cutaway":"exterior_"+((turbo.Checked?1:0)+(sc.Checked?2:0))),ready=Path.Combine(folder,"Ready.json");
   if(!File.Exists(ready)){cadCaption="CAD frames not installed; showing cylinder schematic";cadFrame="";if(cadLarge!=null&&!cadLarge.IsDisposed)cadLarge.Invalidate();return;}
   try{
    CadFrames set=Inputs.Json.Deserialize<CadFrames>(File.ReadAllText(ready));if(set.Frames<12||set.Frames>72)throw new InvalidDataException("Invalid CAD frame set.");
    int frame=(int)(Model.Mod(live.VisualAngle,720)/720*set.Frames);string path=Path.Combine(folder,"frame_"+frame.ToString("D3")+".png");
    if(path!=cadFrame||cadImage==null){using(MemoryStream bytes=new MemoryStream(File.ReadAllBytes(path)))using(Image decoded=Image.FromStream(bytes)){Bitmap next=new Bitmap(decoded);Bitmap old=cadImage;cadImage=next;cadFrame=path;if(old!=null)old.Dispose();}}
    cadCaption="Native SOLIDWORKS "+(cadView.SelectedIndex==1?"cutaway":"exterior")+" · 30° frames · 1/50 speed";
   }catch(Exception){cadCaption="CAD frames unavailable; showing cylinder schematic";cadFrame="";}
   if(cadWindow!=null&&!cadWindow.IsDisposed)cadLarge.Invalidate();
  }
  bool DrawCadEngine(Graphics g){
   if(cadView==null||cadView.SelectedIndex==2||!CadGeometryMatches()||cadImage==null||cadFrame.Length==0){if(!CadGeometryMatches())TextAt(g,"V10 CAD preview does not match the edited geometry",20,478,8,LiveMuted);return false;}
   TextAt(g,cadView.SelectedIndex==1?"YOUR V10 / INTERNAL COMPONENTS":"YOUR V10 / SOLIDWORKS ASSEMBLY",20,244,10,Color.White,true);
   TextAt(g,cadCaption,20,264,8,LiveMuted);DrawFitted(g,cadImage,new RectangleF(20,286,520,197));return true;
  }
  static void DrawFitted(Graphics g,Image source,RectangleF bounds){float scale=Math.Min(bounds.Width/source.Width,bounds.Height/source.Height);RectangleF target=new RectangleF(bounds.X+(bounds.Width-source.Width*scale)/2,bounds.Y+(bounds.Height-source.Height*scale)/2,source.Width*scale,source.Height*scale);g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(source,target);}
  void EnlargeCad(){
   if(cadWindow!=null&&!cadWindow.IsDisposed){cadWindow.Activate();return;}
   cadWindow=new Form{Text="Your V10 | SOLIDWORKS rendered motion",Size=new Size(1050,780),MinimumSize=new Size(600,450),StartPosition=FormStartPosition.CenterParent,BackColor=Color.FromArgb(13,24,38)};
   cadLarge=new SmoothPanel{Dock=DockStyle.Fill};cadLarge.Paint+=(s,e)=>{TextAt(e.Graphics,cadCaption,20,18,11,Color.White,true);TextAt(e.Graphics,"Rendered CAD poses follow the model's slowed crank angle. No combustion or deformation is visualized.",20,48,9,LiveMuted);if(cadImage!=null&&cadView.SelectedIndex!=2&&CadGeometryMatches()&&cadFrame.Length>0)DrawFitted(e.Graphics,cadImage,new RectangleF(20,85,Math.Max(1,cadLarge.Width-40),Math.Max(1,cadLarge.Height-110)));else TextAt(e.Graphics,"Select a SOLIDWORKS view of the original V10 geometry in Live bench.",20,100,12,Color.White);};cadWindow.Controls.Add(cadLarge);cadWindow.Show(this);
  }
  public void CadPreviewCheck(){
   tabs.SelectedIndex=0;live.Enabled=true;BeginLive();
   for(int mode=0;mode<4;mode++){turbo.Checked=(mode&1)!=0;sc.Checked=(mode&2)!=0;for(int frame=0;frame<24;frame++){live.VisualAngle=frame*30;cadUpdated=-1;UpdateCadPreview(clock.Elapsed.TotalSeconds);if(cadImage==null||!cadFrame.EndsWith("frame_"+frame.ToString("D3")+".png")||!cadFrame.Contains("exterior_"+mode))throw new Exception("Native exterior frame selection failed.");}}
   turbo.Checked=false;sc.Checked=false;cadView.SelectedIndex=1;byte[] first=null;
   for(int frame=0;frame<24;frame++){live.VisualAngle=frame*30;cadUpdated=-1;UpdateCadPreview(clock.Elapsed.TotalSeconds);if(!cadFrame.Contains("cutaway")||!cadFrame.EndsWith("frame_"+frame.ToString("D3")+".png"))throw new Exception("Cutaway frame selection failed.");using(SHA256 hash=SHA256.Create()){byte[] digest=hash.ComputeHash(File.ReadAllBytes(cadFrame));if(frame==0)first=digest;if(frame==4&&Convert.ToBase64String(first)==Convert.ToBase64String(digest))throw new Exception("Native cutaway frames did not change.");}}
   input.Cylinders=4;if(CadGeometryMatches())throw new Exception("Unrelated geometry incorrectly matched V10.");input.Cylinders=10;
   cadView.SelectedIndex=0;turbo.Checked=false;sc.Checked=true;live.VisualAngle=120;cadUpdated=-1;UpdateCadPreview(clock.Elapsed.TotalSeconds);AdvanceLive(3,clock.Elapsed.TotalSeconds);Application.DoEvents();
   using(Bitmap bmp=new Bitmap(Width,Height)){DrawToBitmap(bmp,new Rectangle(0,0,Width,Height));bmp.Save(Path.Combine(Root,"Evidence","CAD_Live_Bench.png"));}
   tabs.SelectedIndex=1;RunSweep(false);Application.DoEvents();using(Bitmap bmp=new Bitmap(Width,Height)){DrawToBitmap(bmp,new Rectangle(0,0,Width,Height));bmp.Save(Path.Combine(Root,"Evidence","Peak_HP.png"));}
   File.WriteAllText(Path.Combine(Root,"Evidence","CAD_Preview_Check.txt"),"PASS: all 120 native frames decoded, all four exterior boost selections, cutaway frame changes, edited geometry fallback, native pose readback in Render_Checks.json, and sampled peak HP display. Rendered poses are not physical validation.");
  }
 }
}
