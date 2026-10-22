using System;
using System.Collections.Generic;
using System.Linq;

namespace VectorFit {
    public class MotionQuality {
        public bool Available,PrimaryDetected,HitAfterOvershoot,TargetKnown;
        public double PrimaryTime,PrimaryReach,Overshoot,ReturnDistance,CorrectionDistance,CorrectionMs,PathExcess;
        public double? FirstEntryMs;
        public int Reentries;
    }
    public static class AimGeometry {
        // Same perspective projection for the displayed target and recorded target entries.
        public static PointD Offset(double yaw,double pitch,double cameraX,double cameraY){
            double r=Math.PI/180,wy=Math.Sin(pitch*r),wx=Math.Sin(yaw*r)*Math.Cos(pitch*r),wz=Math.Cos(yaw*r)*Math.Cos(pitch*r);
            double cy=Math.Cos(cameraX*r),sy=Math.Sin(cameraX*r),cp=Math.Cos(cameraY*r),sp=Math.Sin(cameraY*r);
            double x=wx*cy-wz*sy,z=wx*sy+wz*cy,y=wy*cp-z*sp;z=wy*sp+z*cp;
            return z<=.05?new PointD(1e6,1e6):new PointD(x/z,y/z);
        }
        public static bool Inside(Trial trial,Motion sample,double factor=1){return trial.HitRadiusDegrees>0&&Offset(trial.TargetX,trial.TargetY,sample.X,sample.Y).Length<=Math.Tan(trial.HitRadiusDegrees*Math.PI/180)*factor;}
    }
    public static class Trajectory {
        static List<Motion> Resample(Trial trial){
            var source=trial.Samples;if(source==null||source.Count<3||trial.Radius<=0||trial.TotalMs<=0||trial.TotalMs>10000)return new List<Motion>();
            if(source.Any(p=>p==null||Double.IsNaN(p.T)||Double.IsInfinity(p.T)||Double.IsNaN(p.X)||Double.IsInfinity(p.X)||Double.IsNaN(p.Y)||Double.IsInfinity(p.Y)))return new List<Motion>();
            for(int i=1;i<source.Count;i++)if(source[i].T<source[i-1].T)return new List<Motion>();
            var result=new List<Motion>();int next=1;double end=Math.Min(trial.TotalMs,source.Last().T);
            for(double time=Math.Max(0,source[0].T);time<=end;time+=4){
                while(next<source.Count-1&&source[next].T<time)next++;
                var a=source[next-1];var b=source[next];double u=b.T==a.T?1:Analysis.Clamp((time-a.T)/(b.T-a.T),0,1);
                result.Add(new Motion(time,a.X+(b.X-a.X)*u,a.Y+(b.Y-a.Y)*u));
            }
            if(result.Count>0&&result.Last().T<end)result.Add(new Motion(end,source.Last().X,source.Last().Y));return result;
        }
        public static MotionQuality Measure(Trial trial){
            var q=new MotionQuality();var points=Resample(trial);if(points.Count<10)return q;
            q.Available=true;q.TargetKnown=trial.HitRadiusDegrees>0;double radius=trial.Radius;
            // A 12 ms position filter and spatial thresholds reduce polling-rate/jitter dependence.
            var smooth=new List<Motion>();for(int i=0;i<points.Count;i++){var near=points.Skip(Math.Max(0,i-1)).Take(i==0||i==points.Count-1?2:3).ToArray();smooth.Add(new Motion(points[i].T,near.Average(p=>p.X),near.Average(p=>p.Y)));}
            var reach=smooth.Select(p=>(p.X*trial.TargetX+p.Y*trial.TargetY)/(radius*radius)).ToArray();
            double peak=reach[0],maxSpeed=0,path=0,negative=0;int peakIndex=0,primary=-1,slowStart=-1;double onset=smooth.FirstOrDefault(p=>p.Length/radius>.01)==null?0:smooth.First(p=>p.Length/radius>.01).T;
            double reversal=Math.Max(.025,trial.HitRadiusDegrees/radius*.5);
            for(int i=1;i<smooth.Count;i++){
                double step=new PointD(smooth[i].X-smooth[i-1].X,smooth[i].Y-smooth[i-1].Y).Length/radius;path+=step;
                double speed=step/Math.Max(1,smooth[i].T-smooth[i-1].T);maxSpeed=Math.Max(maxSpeed,speed);
                if(reach[i]>peak){peak=reach[i];peakIndex=i;}
                if(reach[i]<reach[i-1])negative+=reach[i-1]-reach[i];
                if(primary<0&&peak>.35&&smooth[i].T-onset>=32){
                    if(peak-reach[i]>=reversal&&smooth[i].T-smooth[peakIndex].T>=12)primary=peakIndex;
                    if(speed<maxSpeed*.12){if(slowStart<0)slowStart=i;if(smooth[i].T-smooth[slowStart].T>=32)primary=slowStart;}else slowStart=-1;
                }
            }
            q.Overshoot=Math.Max(0,reach.Max()-1);q.ReturnDistance=Math.Max(0,negative-.01);q.PathExcess=Math.Max(0,path-new PointD(trial.EndX,trial.EndY).Length/radius);
            q.PrimaryDetected=primary>=0&&primary<smooth.Count-2;
            q.PrimaryTime=q.PrimaryDetected?smooth[primary].T:trial.TotalMs;q.PrimaryReach=q.PrimaryDetected?reach[primary]:trial.Reach;
            if(q.PrimaryDetected){for(int i=primary+1;i<smooth.Count;i++)q.CorrectionDistance+=new PointD(smooth[i].X-smooth[i-1].X,smooth[i].Y-smooth[i-1].Y).Length/radius;q.CorrectionDistance=Math.Max(0,q.CorrectionDistance-.01);q.CorrectionMs=Math.Max(0,trial.TotalMs-q.PrimaryTime);}
            bool inside=false;int entries=0;
            foreach(var point in points){if(!inside&&AimGeometry.Inside(trial,point)){inside=true;entries++;if(!q.FirstEntryMs.HasValue)q.FirstEntryMs=point.T;}else if(inside&&!AimGeometry.Inside(trial,point,1.08))inside=false;}
            q.Reentries=Math.Max(0,entries-1);q.HitAfterOvershoot=q.TargetKnown&&trial.Hit==true&&q.Overshoot>Math.Max(.025,trial.HitRadiusDegrees/radius)&&q.ReturnDistance>.025;
            return q;
        }
        public static Trial ForFit(Trial raw,MotionQuality observed){
            if(!observed.PrimaryDetected||observed.PrimaryTime<=0)return raw;
            var samples=raw.Samples.Where(p=>p.T<=observed.PrimaryTime).ToList();if(samples.Count<2)return raw;
            var before=samples.Last();var after=raw.Samples.FirstOrDefault(p=>p.T>observed.PrimaryTime);
            if(after!=null&&after.T>before.T){double u=(observed.PrimaryTime-before.T)/(after.T-before.T);samples.Add(new Motion(observed.PrimaryTime,before.X+u*(after.X-before.X),before.Y+u*(after.Y-before.Y)));}
            var trial=Analysis.Extract(raw.Direction,raw.TargetX,raw.TargetY,samples,observed.PrimaryTime,raw.Corrected);trial.Hit=raw.Hit;trial.HitRadiusDegrees=raw.HitRadiusDegrees;return trial.Valid?trial:raw;
        }
    }
}
