using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using SolidWorks.Interop.sldworks;

// Optional V10 CAD renderer. Renders a COPY of the open assembly, never saves
// the original and never edits a native part. Requires licensed SOLIDWORKS.
public static class CadAnimationBuilder {
 public sealed class Request {public string EngineFolder,AppFolder,AssemblyFile="Ford_427_V10_Development.SLDASM";public int Frames=24;}
 static readonly string[] Moving={"crank","cam","cam_gear","crank_gear","piston","rings","pin","rod","rod_bearing","valve","boost_turbo_rotor","boost_sc_rotor","boost_sc_drive","boost_sc_clutch","accessory_drive"};
 [STAThread]public static int Main(string[] args){ISldWorks sw=null;ModelDoc2 source=null,copy=null;string output="";try{
  if(args.Length!=1)throw new Exception("Pass one CAD render request JSON path.");Request request=new JavaScriptSerializer().Deserialize<Request>(File.ReadAllText(args[0]));if(request.Frames<12||request.Frames>72)throw new Exception("Frame count outside 12..72.");
  output=Path.Combine(request.AppFolder,"CAD_Preview");Directory.CreateDirectory(output);string work=Path.Combine(output,"Working");Directory.CreateDirectory(work);
  sw=(ISldWorks)Marshal.GetActiveObject("SldWorks.Application");string original=Path.Combine(request.EngineFolder,request.AssemblyFile);source=(ModelDoc2)sw.GetOpenDocumentByName(original);if(source==null)throw new Exception("Open the V10 development assembly in SOLIDWORKS first.");
  string sourcePath=source.GetPathName(),clonePath=Path.Combine(work,"Evans_Engine_Preview_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".SLDASM");int errors=0,warnings=0;
  if(!source.Extension.SaveAs(clonePath,0,3,null,ref errors,ref warnings)||errors!=0)throw new Exception("Cannot create the preview assembly copy: "+errors);
  if(!String.Equals(source.GetPathName(),sourcePath,StringComparison.OrdinalIgnoreCase))throw new Exception("Save-copy changed the source identity unexpectedly.");
  copy=(ModelDoc2)sw.OpenDoc6(clonePath,2,1,"",ref errors,ref warnings);if(copy==null)throw new Exception("Cannot open preview copy: "+errors);int active=0;sw.ActivateDoc3(copy.GetTitle(),false,0,ref active);
  AssemblyDoc assembly=(AssemblyDoc)copy;MathUtility math=(MathUtility)sw.GetMathUtility();List<EngineInstance> items=EngineKinematics.Read(Path.Combine(request.EngineFolder,"Engine_components.csv"));Dictionary<string,Component2> components=new Dictionary<string,Component2>();
  foreach(EngineInstance i in items){Component2 component=assembly.GetComponentByName(i.Name);if(component==null)throw new Exception("Missing preview component "+i.Name);components.Add(i.Name,component);}
  List<object> checks=new List<object>();Stopwatch clock=Stopwatch.StartNew();int rendered=0,total=request.Frames*4;
  // Cutaway is shared by all induction packages; outer housings are removed.
  for(int pass=0;pass<4;pass++){
   bool cutaway=pass==0;int mode=cutaway?0:pass-1;string name=cutaway?"cutaway":"exterior_"+mode;string destination=Path.Combine(output,name);Directory.CreateDirectory(destination);
   ModelView view=(ModelView)copy.ActiveView;view.EnableGraphicsUpdate=false;sw.CommandInProgress=true;
   foreach(EngineInstance i in items)components[i.Name].Transform2=(MathTransform)math.CreateTransform(EngineKinematics.Pose(i,0,0,0,0));
   view.EnableGraphicsUpdate=true;copy.ClearSelection2(true);DispatchWrapper[] all=items.Select(i=>new DispatchWrapper(components[i.Name])).ToArray();if(copy.Extension.MultiSelect2(all,false,null)!=all.Length)throw new Exception("Preview hide selection failed");assembly.HideComponent();copy.ClearSelection2(true);
   var shown=items.Where(i=>i.Role!="legacy_hidden"&&i.Role!="development_remote"&&EngineKinematics.Visible(i,cutaway?"Internals":"Engine exterior",0)&&(!i.Role.StartsWith("boost_turbo")||(mode&1)!=0)&&(!i.Role.StartsWith("boost_sc")||(mode&2)!=0)&&(!i.Role.StartsWith("boost_common")||mode!=0)&&(i.Role!="boost_turbo_lid"||(mode&2)==0)).ToList();
   DispatchWrapper[] selected=shown.Select(i=>new DispatchWrapper(components[i.Name])).ToArray();if(copy.Extension.MultiSelect2(selected,false,null)!=selected.Length)throw new Exception("Preview show selection failed");assembly.ShowComponent();copy.ClearSelection2(true);assembly.UpdateBox();copy.ViewDisplayShaded();copy.ShowNamedView2("*Front",1);((ModelView)copy.ActiveView).RotateAboutCenter(.42,2.57);copy.ViewZoomtofit2();copy.ViewZoomout();copy.ViewZoomout();sw.CommandInProgress=false;
   foreach(EngineInstance i in items)if((components[i.Name].Visible!=0)!=shown.Contains(i))throw new Exception("Preview native visibility mismatch "+i.Name);
   double worst=0;Rectangle crop=Rectangle.Empty;
   for(int frame=0;frame<request.Frames;frame++){
    double angle=frame*720.0/request.Frames;view=(ModelView)copy.ActiveView;view.EnableGraphicsUpdate=false;sw.CommandInProgress=true;
    foreach(EngineInstance i in items){if(!Moving.Contains(i.Role))continue;double[] expected=EngineKinematics.Pose(i,angle,0,0,0);components[i.Name].Transform2=(MathTransform)math.CreateTransform(expected);if(i.Role=="crank"||i.Role=="cam"||i.Role=="piston"){double[] actual=(double[])components[i.Name].Transform2.ArrayData;for(int k=0;k<13;k++)worst=Math.Max(worst,Math.Abs(expected[k]-actual[k]));}}
    sw.CommandInProgress=false;view.EnableGraphicsUpdate=true;copy.GraphicsRedraw2();string bitmap=Path.Combine(destination,"render.bmp");if(!copy.SaveBMP(bitmap,1400,1000))throw new Exception("SOLIDWORKS frame export failed");string png=Path.Combine(destination,"frame_"+frame.ToString("D3")+".png");using(Bitmap picture=new Bitmap(bitmap)){if(frame==0)crop=FindEngine(picture);using(Bitmap framed=picture.Clone(crop,PixelFormat.Format24bppRgb))framed.Save(png,ImageFormat.Png);}
    rendered++;File.WriteAllText(Path.Combine(output,"Progress.txt"),"Rendering your SOLIDWORKS engine: "+rendered+" / "+total+" frames");Console.WriteLine(name+" "+frame+" / "+request.Frames);Console.Out.Flush();
   }
   if(worst>1e-8)throw new Exception("Native transform readback mismatch "+worst);
   File.WriteAllText(Path.Combine(destination,"Ready.json"),new JavaScriptSerializer().Serialize(new{Frames=request.Frames,StepDegrees=720.0/request.Frames,Source="Native SOLIDWORKS assembly render",View=name,NativeTransformMaxError=worst,VisibleComponents=shown.Count}));checks.Add(new{View=name,Frames=request.Frames,NativeTransformMaxError=worst,VisibleComponents=shown.Count});
  }
  File.WriteAllText(Path.Combine(output,"Render_Checks.json"),new JavaScriptSerializer().Serialize(new{Status="PASS",PreviewIsSeparateCopy=true,SourceAssemblySaved=false,RenderSeconds=clock.Elapsed.TotalSeconds,Checks=checks}));File.WriteAllText(Path.Combine(output,"Progress.txt"),"SOLIDWORKS engine animation ready");return 0;
 }catch(Exception e){if(output.Length>0){File.WriteAllText(Path.Combine(output,"Error.txt"),e.ToString());File.WriteAllText(Path.Combine(output,"Progress.txt"),"CAD animation unavailable: "+e.Message);}Console.Error.WriteLine(e.ToString());return 1;}
 finally{if(sw!=null){sw.CommandInProgress=false;if(copy!=null){try{((ModelView)copy.ActiveView).EnableGraphicsUpdate=true;sw.CloseDoc(copy.GetTitle());}catch{}}if(source!=null){try{int error=0;sw.ActivateDoc3(source.GetTitle(),false,0,ref error);}catch{}}}}
 }
 static Rectangle FindEngine(Bitmap image){int left=image.Width,top=image.Height,right=0,bottom=0;for(int y=0;y<image.Height;y+=2)for(int x=0;x<image.Width;x+=2){Color c=image.GetPixel(x,y);if((c.R+c.G+c.B)/3<190){left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x);bottom=Math.Max(bottom,y);}}if(right<=left)return new Rectangle(0,0,image.Width,image.Height);left=Math.Max(0,left-45);top=Math.Max(0,top-45);right=Math.Min(image.Width,right+45);bottom=Math.Min(image.Height,bottom+45);return Rectangle.FromLTRB(left,top,right,bottom);}
}
