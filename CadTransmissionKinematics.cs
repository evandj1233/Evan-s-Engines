using System;
public static class TransmissionKinematics {
 public static string Range="P";public static int Gear=1;public static bool Locked=true;
 static double[] offsets=new double[8],rates=new double[8];static bool initialized=false;static double outputOffset=0,turbineOffset=0;
 // Rate order: A sun/ring/carrier/planet, B sun/ring/carrier/planet.
 public static double[] Rates(string range,int gear,bool locked){
  double input=locked?1:.9,as_=0,ar=0,ac=0,bs=0,br=0,bc=0;
  if(range=="R"){as_=input;ar=-30.0/78*input;ac=0;bs=br=bc=ar;}
  else if(range=="D"){
   if(gear<=2){as_=input;ar=0;ac=input*30.0/108;}
   else if(gear==3){as_=0;ar=input;ac=input*78.0/108;}
   else as_=ar=ac=input;
   double aout=ac;
   if(gear==1||gear==3||gear==4){bs=0;br=aout;bc=aout*78.0/108;}
   else if(gear==6){bs=0;bc=aout;br=aout*108.0/78;}
   else bs=br=bc=aout;
  }
  return new double[]{as_,ar,ac,ac-30.0/24*(as_-ac),bs,br,bc,bc-30.0/24*(bs-bc)};
 }
 public static double OutputRate(string range,int gear,bool locked){double[] r=Rates(range,gear,locked);return range=="R"?r[5]:range=="D"?(gear==6?r[5]:r[6]):0;}
 public static double Ratio(int gear){return 1/OutputRate("D",gear,true);}
 public static void Reset(){offsets=new double[8];rates=Rates(Range,Gear,Locked);initialized=false;outputOffset=turbineOffset=0;}
 public static void SetState(string range,int gear,bool locked,double crank){
  double[] next=Rates(range,gear,locked);if(initialized){for(int j=0;j<8;j++)offsets[j]+=(rates[j]-next[j])*crank;outputOffset+=(OutputRate(Range,Gear,Locked)-OutputRate(range,gear,locked))*crank;turbineOffset+=((Locked?1:.9)-(locked?1:.9))*crank;}
  Range=range;Gear=gear;Locked=locked;rates=next;initialized=true;
 }
 public static double[] Pose(EngineInstance i,double crank,double explode){
  double[] b=(double[])i.Base.Clone();double angle=0;double[] current=Rates(Range,Gear,Locked);int set=i.Type==2?4:0;
  if(i.Role=="trans_sun")angle=(current[set]*crank+offsets[set])*Math.PI/180;
  else if(i.Role=="trans_ring")angle=(current[set+1]*crank+offsets[set+1])*Math.PI/180;
  else if(i.Role=="trans_carrier")angle=(current[set+2]*crank+offsets[set+2])*Math.PI/180;
  else if(i.Role=="trans_planet"){
   double orbit=(current[set+2]*crank+offsets[set+2])*Math.PI/180+2*Math.PI*i.Pair/3;
   angle=(current[set+3]*crank+offsets[set+3])*Math.PI/180+Math.PI/24+2*Math.PI*i.Pair/3;
   b=EngineKinematics.RotateZ(angle,54*Math.Cos(orbit),54*Math.Sin(orbit),i.Base[11]*1000);
  }
  else if(i.Role=="trans_converter_impeller"||i.Role=="trans_converter_shell"||i.Role=="trans_flexplate")angle=crank*Math.PI/180;
  else if(i.Role=="trans_converter_turbine"||i.Role=="trans_input")angle=(crank*(Locked?1:.9)+turbineOffset)*Math.PI/180;
  else if(i.Role=="trans_output"){
   angle=(OutputRate(Range,Gear,Locked)*crank+outputOffset)*Math.PI/180;
  }
  else if(i.Role=="trans_clutch")angle=(current[set+(i.Pair%3)]*crank+offsets[set+(i.Pair%3)])*Math.PI/180;
  if(i.Role!="trans_planet"&&(i.Role=="trans_sun"||i.Role=="trans_ring"||i.Role=="trans_carrier"||i.Role=="trans_flexplate"||i.Role=="trans_converter_shell"||i.Role=="trans_converter_impeller"||i.Role=="trans_converter_turbine"||i.Role=="trans_input"||i.Role=="trans_output"||i.Role=="trans_clutch"))b=EngineKinematics.RotateZ(angle,b[9]*1000,b[10]*1000,b[11]*1000);
  if(explode>0){if(i.Role=="trans_case"||i.Role=="trans_bell")b[9]+=.28*explode;else if(i.Role=="trans_pan"||i.Role=="trans_valve")b[10]-=.2*explode;else if(i.Role.StartsWith("trans_"))b[11]+=.12*explode;}
  return b;
 }
 public static string SelfCheck(){
  double[] expected={4.9846153846153847,3.6,1.9171597633136095,1.3846153846153846,1,.7222222222222222};double rateError=0,closure=0,jump=0;
  for(int g=1;g<=6;g++){rateError=Math.Max(rateError,Math.Abs(Ratio(g)-expected[g-1]));double[] r=Rates("D",g,true);for(int s=0;s<2;s++)closure=Math.Max(closure,Math.Abs(30*r[4*s]+78*r[4*s+1]-108*r[4*s+2]));}
  EngineInstance shaft=new EngineInstance();shaft.Role="trans_output";shaft.Base=EngineKinematics.Identity(0,0,0);Reset();SetState("D",1,true,0);
  for(int g=2;g<=6;g++){double crank=540*g;double[] before=Pose(shaft,crank,0);SetState("D",g,true,crank);double[] after=Pose(shaft,crank,0);for(int k=0;k<9;k++)jump=Math.Max(jump,Math.Abs(before[k]-after[k]));}
  foreach(string range in new string[]{"R","N","P"}){double[] before=Pose(shaft,4320,0);SetState(range,1,false,4320);double[] after=Pose(shaft,4320,0);for(int k=0;k<9;k++)jump=Math.Max(jump,Math.Abs(before[k]-after[k]));}
  if(rateError>1e-12||closure>1e-12||jump>1e-10)throw new Exception("Transmission equation/continuity check failed");Range="P";Gear=1;Locked=true;Reset();return "Six reference forward ratios passed. Maximum ratio error="+rateError+"; maximum planetary rate residual="+closure+"; maximum output transform jump at gear/range changes="+jump+". Reverse=-2.6; Park/Neutral output rate=0. This checks prescribed kinematics only.";
 }
}
