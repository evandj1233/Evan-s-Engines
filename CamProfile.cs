using System;

namespace V10Lab {
    // C# adaptation of Function::sampleTriangle and closestSample from
    // Ange Yaghi's Engine Simulator, src/function.cpp. Copyright 2022
    // AngeTheGreat (Ange Yaghi), MIT. Full license is included with this app.
    // Uses immutable ordered arrays and rejects malformed/non-finite samples.
    public sealed class CamProfile {
        readonly double[] x,y; readonly double radius;
        public CamProfile(double[] angles,double[] lifts,double filterRadius){
            if(angles==null||lifts==null||angles.Length!=lifts.Length||angles.Length<2||filterRadius<=0||double.IsInfinity(filterRadius)||double.IsNaN(filterRadius))throw new ArgumentException("Invalid cam samples.");
            x=(double[])angles.Clone();y=(double[])lifts.Clone();radius=filterRadius;
            for(int i=0;i<x.Length;i++)if(double.IsNaN(x[i])||double.IsInfinity(x[i])||double.IsNaN(y[i])||double.IsInfinity(y[i])||y[i]<0||(i>0&&x[i]<=x[i-1]))throw new ArgumentException("Cam samples must be finite, increasing, and nonnegative.");
        }
        int Closest(double angle){int l=0,r=x.Length-1;if(angle<=x[l])return l;if(angle>=x[r])return r;while(l+1<r){int m=(l+r)/2;if(angle>x[m])l=m;else if(angle<x[m])r=m;else return m;}return angle-x[l]<x[r]-angle?l:r;}
        public double Sample(double angle){
            if(double.IsNaN(angle)||double.IsInfinity(angle))throw new ArgumentException("Non-finite cam angle.");
            if(angle>=x[x.Length-1])return y[y.Length-1];if(angle<=x[0])return y[0];
            int closest=Closest(angle);double sum=0,weight=0;
            for(int i=closest;i>=0;i--){if(x[i]>angle)continue;if(Math.Abs(x[i]-angle)>radius)break;double w=(radius-Math.Abs(x[i]-angle))/radius;sum+=w*y[i];weight+=w;}
            for(int i=closest;i<x.Length;i++){if(x[i]<=angle)continue;if(Math.Abs(x[i]-angle)>radius)break;double w=(radius-Math.Abs(x[i]-angle))/radius;sum+=w*y[i];weight+=w;}
            return weight!=0?sum/weight:0;
        }
        public static CamProfile Create(bool intake){double half=(intake?150:144)/2.0,h=intake?11:10,step=.75;int n=(int)Math.Round(2*half/step)+1;double[] a=new double[n+2],v=new double[n+2];a[0]=-180;a[n+1]=180;for(int i=0;i<n;i++){a[i+1]=-half+i*step;v[i+1]=.5*h*(1+Math.Cos(Math.PI*a[i+1]/half));}return new CamProfile(a,v,step);}
        public static readonly CamProfile Intake=Create(true),Exhaust=Create(false);
        public static double ValveLift(double cycle,bool intake,double advance){double center=intake?470:250;double offset=Model.Mod(cycle+advance-center+360,720)-360;return (intake?Intake:Exhaust).Sample(offset/2);}
    }
}
