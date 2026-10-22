using System;
using System.Collections.Generic;
using System.Linq;

namespace VectorFit {
    // Spatial sampling avoids depending on mouse polling frequency. Angles are radians.
    public class CurveProfile {
        public double[][] Turn=Enumerable.Range(0,8).Select(d=>new double[50]).ToArray();
        public int SupportedBins;
        public bool Safe() { return Turn!=null&&Turn.Length==8&&Turn.All(row=>row!=null&&row.Length==50&&row.All(v=>!Double.IsNaN(v)&&!Double.IsInfinity(v)&&Math.Abs(v)<=8*Math.PI/180+1e-9)); }
    }
    public static class CurveFit {
        public static double Wrap(double a) { while(a>Math.PI)a-=2*Math.PI;while(a< -Math.PI)a+=2*Math.PI;return a; }
        public static int Sector(double angle) { return ((int)Math.Round(angle/(Math.PI/4))+8)%8; }
        public static CurveProfile Learn(IEnumerable<Trial> trials) {
            var values=new List<double>[8,50];
            for(int d=0;d<8;d++)for(int b=0;b<50;b++)values[d,b]=new List<double>();
            foreach(var trial in trials.Where(t=>t.Valid&&t.Samples.Count>=3)) {
                var points=new List<PointD>{new PointD(0,0)};double length=0,next=.5;
                for(int i=1;i<trial.Samples.Count;i++) {
                    var a=trial.Samples[i-1];var z=trial.Samples[i];double dx=z.X-a.X,dy=z.Y-a.Y,segment=Math.Sqrt(dx*dx+dy*dy);
                    if(segment<1e-10)continue;
                    while(next<=length+segment&&points.Count<51){double f=(next-length)/segment;points.Add(new PointD(a.X+f*dx,a.Y+f*dy));next+=.5;}
                    length+=segment;if(points.Count>=51)break;
                }
                if(points.Count<5)continue;
                double heading=Math.Atan2(points[2].Y,points[2].X);int sector=Sector(heading);
                for(int b=2;b<points.Count-1;b++) {
                    double angle=Math.Atan2(points[b+1].Y-points[b-1].Y,points[b+1].X-points[b-1].X);
                    double turn=Wrap(angle-heading);
                    // A major turn terminates this stroke; it is not treated as an unwanted bend.
                    if(Math.Abs(turn)>Math.PI/4)break;
                    values[sector,b].Add(turn);
                }
            }
            var profile=new CurveProfile();
            for(int d=0;d<8;d++)for(int b=2;b<50;b++) {
                var v=values[d,b];if(v.Count<4)continue;
                double median=Analysis.Median(v),mad=Analysis.Median(v.Select(x=>Math.Abs(x-median)));
                if(Math.Abs(median)<.5*Math.PI/180||mad>3*Math.PI/180||v.Count(x=>Math.Sign(x)==Math.Sign(median))<v.Count*.75)continue;
                profile.Turn[d][b]=Analysis.Clamp(median*.5,-8*Math.PI/180,8*Math.PI/180);profile.SupportedBins++;
            }
            return profile;
        }
    }
    public class CurveEngine {
        readonly CurveProfile profile;double lastTime=-1,length,initialX,initialY,heading;int sector;bool locked,disabled;
        public CurveEngine(CurveProfile p) { if(p==null||!p.Safe())throw new ArgumentException("Ugyldig kurveprofil");profile=p; }
        public void Reset(){lastTime=-1;length=initialX=initialY=heading=0;locked=disabled=false;sector=0;}
        public PointD Transform(double dx,double dy,double timeMs) {
            double step=Math.Sqrt(dx*dx+dy*dy);if(step<1e-12)return new PointD(dx,dy);
            if(lastTime>=0&&(timeMs-lastTime>80||timeMs<lastTime))Reset();lastTime=timeMs;
            double midpoint=length+step*.5;length+=step;
            if(!locked){initialX+=dx;initialY+=dy;if(length>=1){heading=Math.Atan2(initialY,initialX);sector=CurveFit.Sector(heading);locked=true;}return new PointD(dx,dy);}
            if(Math.Abs(CurveFit.Wrap(Math.Atan2(dy,dx)-heading))>Math.PI/3)disabled=true;
            if(disabled||midpoint>=24.5)return new PointD(dx,dy);
            double bin=midpoint/.5;int i=Math.Min(48,(int)bin);double fraction=bin-i;
            double turn=profile.Turn[sector][i]*(1-fraction)+profile.Turn[sector][i+1]*fraction;
            return new PointD(dx*Math.Cos(turn)+dy*Math.Sin(turn),-dx*Math.Sin(turn)+dy*Math.Cos(turn));
        }
    }
}
