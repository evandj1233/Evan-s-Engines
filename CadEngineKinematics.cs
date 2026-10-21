using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;

public class EngineInstance {
 public string Name,File,Role;public int Side,Cylinder,Pair,Type;public double[] Base;
 public string CSV(){List<string> f=new List<string>(new string[]{Name,File,Role,Side.ToString(),Cylinder.ToString(),Pair.ToString(),Type.ToString()});foreach(double x in Base)f.Add(x.ToString("R",CultureInfo.InvariantCulture));return String.Join(",",f.ToArray());}
 public static EngineInstance Parse(string s){string[] f=s.Split(',');EngineInstance i=new EngineInstance();i.Name=f[0];i.File=f[1];i.Role=f[2];i.Side=int.Parse(f[3]);i.Cylinder=int.Parse(f[4]);i.Pair=int.Parse(f[5]);i.Type=int.Parse(f[6]);i.Base=new double[16];for(int k=0;k<16;k++)i.Base[k]=double.Parse(f[k+7],CultureInfo.InvariantCulture);return i;}
}
public static class EngineKinematics {
 public const double Q=.7071067811865475244,R=49.378137,L=160,A=14*Math.PI/180,ValveR=257.5,CamR=330;
 public static readonly double CamT=22+(CamR-ValveR)*Math.Tan(A);
 public static double Mod(double x,double p){return (x%p+p)%p;}
 public static double Lift(double crank,bool intake,double advance){double center=intake?470:250,half=intake?150:144,h=intake?11:10;double a=Mod(crank+advance-center+360,720)-360;return Math.Abs(a)<half?.5*h*(1+Math.Cos(Math.PI*a/half)):0;}
 public static double[] Identity(double x,double y,double z){return new double[]{1,0,0,0,1,0,0,0,1,x/1000,y/1000,z/1000,1,0,0,0};}
 public static double[] AxisY(double ux,double uy,double x,double y,double z){return new double[]{uy,-ux,0,ux,uy,0,0,0,1,x/1000,y/1000,z/1000,1,0,0,0};}
 public static double[] RotateZ(double angle,double x,double y,double z){double c=Math.Cos(angle),s=Math.Sin(angle);return new double[]{c,s,0,-s,c,0,0,0,1,x/1000,y/1000,z/1000,1,0,0,0};}
 public static double[] AxisZ(double ux,double uy,double x,double y,double z){return new double[]{uy,-ux,0,0,0,-1,ux,uy,0,x/1000,y/1000,z/1000,1,0,0,0};}
 public static double[] BankPoint(double t,double r,int side){return new double[]{side*Q*r+Q*t,Q*r-side*Q*t};}
 public static double[] Pose(EngineInstance i,double crank,double intakeAdvance,double exhaustAdvance,double exploded) {
  if(i.Role.StartsWith("trans_"))return TransmissionKinematics.Pose(i,crank,exploded);
  double theta=crank*Math.PI/180;int side=i.Side;double[] b=(double[])i.Base.Clone();
  double z=65+100*i.Cylinder+(side<0?24:0),nx=side*Q,ny=Q,tx=Q,ty=-side*Q;
  if(i.Role=="crank")b=RotateZ(theta,0,0,0);
  else if(i.Role=="accessory_drive")b=RotateZ(theta*(i.Type==1?1:i.Type==2?73.0/48:i.Type==3?73.0/40:73.0/31),i.Base[9]*1000,i.Base[10]*1000,i.Base[11]*1000);
  else if(i.Role=="boost_turbo_rotor"||i.Role=="boost_sc_rotor"||i.Role=="boost_sc_drive"||i.Role=="boost_sc_clutch"){
   double angle=i.Role=="boost_turbo_rotor"?theta*8:i.Role=="boost_sc_rotor"?(i.Type==2?-1.5*theta+Math.PI/2:1.5*theta):i.Role=="boost_sc_drive"&&i.Type==1?theta:theta*1.5;
   b=RotateZ(angle,i.Base[9]*1000,i.Base[10]*1000,i.Base[11]*1000);
  }
  else if(i.Role=="piston"||i.Role=="rings"||i.Role=="pin"||i.Role=="rod"||i.Role=="rod_bearing"){
   double beta=Math.PI/4-144*i.Cylinder*Math.PI/180+theta,cx=R*Math.Cos(beta),cy=R*Math.Sin(beta);
   double perpendicular=cx*tx+cy*ty;
   double along=cx*nx+cy*ny+Math.Sqrt(L*L-perpendicular*perpendicular);
   double px=along*nx,py=along*ny;
   if(i.Role=="rod"||i.Role=="rod_bearing")b=AxisY((px-cx)/L,(py-cy)/L,cx,cy,z);
   else b=AxisY(nx,ny,px,py,z);
  }
  else if(i.Role=="cam"||i.Role=="cam_gear"){
   bool intake=i.Type==1;int sign=intake?-side:side;double[] p=BankPoint(sign*CamT,CamR,side);
   b=RotateZ((theta+(intake?intakeAdvance:exhaustAdvance)*Math.PI/180)/2,p[0],p[1],i.Role=="cam"?0:(side>0?-31:-47));
  }
  else if(i.Role=="crank_gear")b=RotateZ(theta,0,0,side>0?-31:-47);
  else if(i.Role=="valve") {
   bool intake=i.Type==1;int sign=intake?-side:side;
   double ux=nx*Math.Cos(A)+tx*sign*Math.Sin(A),uy=ny*Math.Cos(A)+ty*sign*Math.Sin(A);
   double[] p=BankPoint(sign*22,ValveR,side);
   double cycle=crank-144*i.Cylinder-(side<0?90:0);
   double lift=Lift(cycle,intake,intake?intakeAdvance:exhaustAdvance);
   b=AxisY(ux,uy,p[0]-ux*lift,p[1]-uy*lift,z+(i.Pair==0?-18:18));
  }
  if(exploded>0){double radial=0,vertical=0,longitudinal=0;
   if(i.Role=="cover"||i.Role=="badge")radial=200;
   else if(i.Role=="head"||i.Role=="cam_ladder")radial=115;
   else if(i.Role=="gasket")radial=65;
   else if(i.Role=="cam"||i.Role=="cam_gear"||i.Role=="valve")radial=160;
   else if(i.Role=="intake"||i.Role=="runner"||i.Role=="fuel")vertical=240;
   else if(i.Role=="pan"||i.Role=="pan_gasket")vertical=-180;
   else if(i.Role=="timing"||i.Role=="pulley"||i.Role=="accessory_drive"||i.Role=="accessory")longitudinal=-180;
   else if(i.Role=="exhaust")radial=95;
   b[9]+=side*Q*radial*exploded/1000;b[10]+=(Q*radial+vertical)*exploded/1000;b[11]+=longitudinal*exploded/1000;
  }
  return b;
 }
 public static bool Visible(EngineInstance i,string view,int cylinder) {
  if(view=="All parts"||view=="Exterior")return true;
  if(view=="Engine exterior")return !i.Role.StartsWith("trans_");
  if(view=="Transmission")return i.Role.StartsWith("trans_");
  if(view=="Transmission internals")return i.Role.StartsWith("trans_")&&i.Role!="trans_case"&&i.Role!="trans_bell"&&i.Role!="trans_pan"&&i.Role!="trans_converter_shell";
  if(view=="Boost system")return i.Role.StartsWith("boost_");
  if(view=="Boost internals")return i.Role=="boost_sc_rotor"||i.Role=="boost_turbo_rotor"||i.Role=="boost_sc_drive"||i.Role=="boost_sc_clutch";
  if(view=="Gaskets and seals")return i.Role=="gasket"||i.Role=="pan_gasket"||i.Role=="seal";
  if(view=="Valvetrain")return i.Role=="cam"||i.Role=="valve"||i.Role=="cam_gear"||i.Role=="crank_gear"||i.Role=="cam_ladder";
  if(view=="Piston and rod")return (i.Cylinder==cylinder && (i.Role=="piston"||i.Role=="rod"||i.Role=="pin"||i.Role=="rings"||i.Role=="rod_bearing"));
  if(view=="Cams only")return i.Role=="cam";
  return i.Role=="crank"||i.Role=="piston"||i.Role=="rod"||i.Role=="pin"||i.Role=="rings"||i.Role=="rod_bearing"||i.Role=="cam"||i.Role=="valve"||i.Role=="cam_gear"||i.Role=="crank_gear"||i.Role=="main_bearing"||i.Role=="cam_ladder";
 }
 public static List<EngineInstance> Read(string path){List<EngineInstance> list=new List<EngineInstance>();foreach(string line in File.ReadAllLines(path))if(line.Length>0&&!line.StartsWith("name,"))list.Add(EngineInstance.Parse(line));return list;}
}
