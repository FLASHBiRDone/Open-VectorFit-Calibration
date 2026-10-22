using System;
using System.Collections.Generic;
using System.Linq;

namespace VectorFit {
    public class AimClick : Motion {
        public bool Hit;
        public AimClick(double t,double x,double y,bool hit):base(t,x,y){Hit=hit;}
    }
    public static class PrecisionCheck {
        public const int MeasurementCount=16, ComparisonCount=32;
        public const double TimeLimit=3000;
        public static double Time(Trial t){return t.Hit==true?Math.Min(TimeLimit,t.TotalMs):TimeLimit;}
        public static int Misses(Trial t){return t.Clicks.Count(c=>!c.Hit);}
        public static string Summary(IEnumerable<Trial> source){
            var ts=source.ToList();if(ts.Count==0)return "Ingen presisjonsmåling ennå.";
            return "Treff: "+ts.Count(t=>t.Hit==true)+" / "+ts.Count+" · Tid per mål: "+Analysis.Median(ts.Select(Time)).ToString("F0")+" ms · Bomklikk: "+ts.Sum(t=>Misses(t));
        }
        public static bool Acceptable(List<Trial> ts,out string note){
            note="Presisjonskontrollen er ikke fullført. Ingen samlet forbedring er bekreftet.";
            if(ts==null||ts.Count!=ComparisonCount||ts.Any(t=>t==null||t.Clicks==null||!t.Hit.HasValue||t.PairId<=0||!Finite(t.TotalMs)||t.TotalMs<0||!Finite(t.TargetX)||!Finite(t.TargetY)||!Finite(t.HitRadiusDegrees)||t.HitRadiusDegrees<=0))return false;
            var groups=ts.GroupBy(t=>t.PairId).ToList();
            if(groups.Count!=16||groups.Any(g=>g.Count()!=2||g.Count(t=>t.Corrected)!=1))return false;
            var a=groups.Select(g=>g.Single(t=>!t.Corrected)).ToList();var b=groups.Select(g=>g.Single(t=>t.Corrected)).ToList();
            if(Enumerable.Range(0,8).Any(d=>a.Count(t=>t.Direction==d)!=2)||Enumerable.Range(0,16).Any(i=>a[i].Direction!=b[i].Direction||Math.Abs(a[i].TargetX-b[i].TargetX)>1e-8||Math.Abs(a[i].TargetY-b[i].TargetY)>1e-8||Math.Abs(a[i].HitRadiusDegrees-b[i].HitRadiusDegrees)>1e-8))return false;
            if(ts.Any(t=>t.Clicks.Any(c=>!Finite(c.T)||!Finite(c.X)||!Finite(c.Y)||c.T<0||c.T>t.TotalMs)||t.Clicks.Count(c=>c.Hit)!=(t.Hit==true?1:0)||t.Hit==true&&(!t.Valid||t.Clicks.Count==0||!t.Clicks.Last().Hit)))return false;
            if(a.Count(t=>t.Hit==true)<8||b.Count(t=>t.Hit==true)<8){note="For få presisjonstreff til å bekrefte kontroll. Ingen samlet forbedring er bekreftet.";return false;}
            // A regression guard, not a separate statistical claim of improvement.
            bool okay=b.Count(t=>t.Hit==true)>=a.Count(t=>t.Hit==true)&&Analysis.Median(b.Select(Time))<=Analysis.Median(a.Select(Time))*1.05&&b.Sum(t=>Time(t))<=a.Sum(t=>Time(t))*1.05&&b.Sum(t=>Misses(t))<=a.Sum(t=>Misses(t));
            note=okay?"Presisjonskontrollen viser ingen tydelig forverring innenfor grensene. Dette er ikke et eget bevis på forbedring.":"Presisjonsdelen fikk færre treff, flere bomklikk eller over 5 % lengre tid. Behold profilen.";
            return okay;
        }
        static bool Finite(double x){return !Double.IsNaN(x)&&!Double.IsInfinity(x);}
    }
}
