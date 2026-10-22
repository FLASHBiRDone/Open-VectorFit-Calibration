using System;
using System.Collections.Generic;
using System.Linq;

namespace VectorFit {
    public enum ComparisonDecision { Incomplete, Continue, Improved, Worse, Inconclusive }
    public class ComparisonReport {
        public ComparisonDecision Decision;
        public int Clicks,Pairs,CloserPairs,LessCorrectionPairs,ExtraHits;
        public double DistanceP=1,CorrectionP=1,HitP=1;
        public string Note="";
        public bool Finished {get{return Decision==ComparisonDecision.Improved||Decision==ComparisonDecision.Worse||Decision==ComparisonDecision.Inconclusive;}}
    }
    public static class SequentialComparison {
        // Three prespecified improvement routes, three prespecified looks: family alpha <= .05
        // under the paired sign-test assumptions. Repeated runs are separate experiments.
        public const double Alpha=.05/9;
        public static double SignTail(int wins,int n){if(n<=0||wins<=0)return 1;if(wins>n)return 0;double mass=Math.Pow(.5,n),sum=0;for(int k=0;k<=n;k++){if(k>=wins)sum+=mass;if(k<n)mass*=((double)n-k)/(k+1);}return Math.Min(1,sum);}
        static double Distance(Trial t){return t.Valid?t.ClickError:Math.Max(1,t.ClickError);}
        static double Percentile(IEnumerable<double> values,double fraction){var a=values.OrderBy(v=>v).ToArray();return a.Length==0?0:a[(int)Math.Ceiling(fraction*(a.Length-1))];}
        public static ComparisonReport Evaluate(Session session){
            var report=new ComparisonReport{Clicks=session.Validation.Count,Decision=ComparisonDecision.Incomplete};
            var trials=session.Validation;
            if(trials.Count!=32&&trials.Count!=48&&trials.Count!=64){report.Note="A/B er ikke ved et fullført kontrollpunkt ennå.";return report;}
            if(trials.Any(t=>t.PairId<=0||!t.Hit.HasValue||t.HitRadiusDegrees<=0||Double.IsNaN(t.ClickError)||Double.IsInfinity(t.ClickError)||t.TotalMs<0||Double.IsNaN(t.TotalMs)||Double.IsInfinity(t.TotalMs))){report.Note="A/B mangler sammenlignbare treff- eller bevegelsesdata.";return report;}
            var groups=trials.GroupBy(t=>t.PairId).OrderBy(g=>g.Key).ToList();
            if(groups.Any(g=>g.Count()!=2||g.Count(t=>t.Corrected)!=1)){report.Note="A/B mangler et komplett målpar.";return report;}
            var a=groups.Select(g=>g.Single(t=>!t.Corrected)).ToList();var b=groups.Select(g=>g.Single(t=>t.Corrected)).ToList();report.Pairs=a.Count;
            int perDirection=report.Pairs/8;
            if(Enumerable.Range(0,8).Any(d=>a.Count(t=>t.Direction==d)!=perDirection)||Enumerable.Range(0,a.Count).Any(i=>a[i].Direction!=b[i].Direction||Math.Abs(a[i].TargetX-b[i].TargetX)>1e-8||Math.Abs(a[i].TargetY-b[i].TargetY)>1e-8||Math.Abs(a[i].HitRadiusDegrees-b[i].HitRadiusDegrees)>1e-8)){
                report.Note="A/B har ulik fordeling av mål. Kjør en ny test.";return report;
            }
            var qa=a.Select(Trajectory.Measure).ToList();var qb=b.Select(Trajectory.Measure).ToList();
            double da=Analysis.Median(a.Select(Distance)),db=Analysis.Median(b.Select(Distance));
            double ta=Analysis.Median(a.Select(t=>t.TotalMs)),tb=Analysis.Median(b.Select(t=>t.TotalMs));
            double ca=Analysis.Median(qa.Select(q=>q.CorrectionDistance)),cb=Analysis.Median(qb.Select(q=>q.CorrectionDistance));
            bool pathAvailable=Enumerable.Range(0,a.Count).All(i=>(!a[i].Valid||qa[i].Available)&&(!b[i].Valid||qb[i].Available));
            bool hitsOkay=b.Count(t=>t.Hit==true)>=a.Count(t=>t.Hit==true);
            bool timeOkay=tb<=ta*1.05&&Percentile(b.Select(t=>t.TotalMs),.9)<=Percentile(a.Select(t=>t.TotalMs),.9)*1.10;
            Func<Trial,double> movementTime=t=>t.Valid&&t.FlickMs>0?t.FlickMs:t.TotalMs;
            timeOkay=timeOkay&&Analysis.Median(b.Select(movementTime))<=Analysis.Median(a.Select(movementTime))*1.05;
            bool tailOkay=Percentile(b.Select(Distance),.9)<=Percentile(a.Select(Distance),.9)+.01;
            bool directionOkay=Analysis.DirectionBias(b)<=Analysis.DirectionBias(a)+.005;
            bool pathOkay=pathAvailable&&cb<=ca+.02&&Analysis.Median(qb.Select(q=>q.Overshoot))<=Analysis.Median(qa.Select(q=>q.Overshoot))+.02&&Analysis.Median(qb.Select(q=>q.PathExcess))<=Analysis.Median(qa.Select(q=>q.PathExcess))+.03&&Analysis.Median(qb.Select(q=>(double)q.Reentries))<=Analysis.Median(qa.Select(q=>(double)q.Reentries))+.5;
            double correctionTimeA=Analysis.Median(qa.Select(q=>q.CorrectionMs)),correctionTimeB=Analysis.Median(qb.Select(q=>q.CorrectionMs));pathOkay=pathOkay&&correctionTimeB<=correctionTimeA+Math.Max(20,correctionTimeA*.1);
            var closer=new List<int>();var betterPath=new List<int>();var hitWins=new List<int>();int worse=0,hitLosses=0;
            for(int i=0;i<a.Count;i++){
                if(Distance(b[i])<Distance(a[i])*.95-.001)closer.Add(a[i].Direction);
                if(Distance(a[i])<Distance(b[i])*.95-.001)worse++;
                if(a[i].Hit==false&&b[i].Hit==true)hitWins.Add(a[i].Direction);if(a[i].Hit==true&&b[i].Hit==false)hitLosses++;
                if(pathAvailable&&qa[i].CorrectionDistance>.03&&qb[i].CorrectionDistance<qa[i].CorrectionDistance*.9-.005)betterPath.Add(a[i].Direction);
            }
            report.CloserPairs=closer.Count;report.LessCorrectionPairs=betterPath.Count;
            report.DistanceP=SignTail(closer.Count,report.Pairs);report.CorrectionP=SignTail(betterPath.Count,report.Pairs);
            report.ExtraHits=hitWins.Count-hitLosses;report.HitP=SignTail(hitWins.Count,hitWins.Count+hitLosses);
            bool distanceImproved=report.DistanceP<=Alpha&&closer.Distinct().Count()>=6&&da>1e-9&&db<=da*.95;
            bool hitsImproved=report.HitP<=Alpha&&hitWins.Distinct().Count()>=6&&report.ExtraHits>0&&db<=da+.002;
            // Equal-accuracy hits can improve through less corrective travel. Never trade away hits to do so.
            bool correctionImproved=pathAvailable&&report.CorrectionP<=Alpha&&betterPath.Distinct().Count()>=6&&ca>.03&&cb<=ca*.9&&db<=da+.002&&tailOkay&&tb<=ta*1.02;
            if(session.FitReady&&session.Repeatable&&hitsOkay&&timeOkay&&tailOkay&&directionOkay&&pathOkay&&(distanceImproved||correctionImproved||hitsImproved)){
                report.Decision=ComparisonDecision.Improved;
                report.Note=distanceImproved?"A/B støtter nærmere klikk uten færre treff eller tydelig dårligere kontroll.":hitsImproved?"A/B støtter flere faktiske treff med bevart klikkpresisjon og kontroll.":"A/B støtter mindre etterkorrigering med minst like mange treff og bevart klikkpresisjon.";
            }else if(SignTail(worse,report.Pairs)<=Alpha&&db>da*1.05+.001){report.Decision=ComparisonDecision.Worse;report.Note="A/B viser tydelig større klikkavvik med forslaget. Behold den aktive profilen.";}
            else if(trials.Count==64){report.Decision=ComparisonDecision.Inconclusive;report.Note="A/B er ferdig, men forbedring er ikke tydelig nok. Behold den aktive profilen.";}
            else {report.Decision=ComparisonDecision.Continue;report.Note="Forskjellen er fortsatt usikker. A/B fortsetter med 16 forsøk til.";}
            var guards=new List<string>();if(!hitsOkay)guards.Add("færre treff");if(!timeOkay)guards.Add("langsommere klikk");if(!tailOkay)guards.Add("større avvik i de svakeste forsøkene");if(!directionOkay)guards.Add("større retningsspredning");if(!pathOkay)guards.Add(pathAvailable?"mer etterkorrigering":"manglende bevegelsesbane");
            if(guards.Count>0)report.Note+=" "+"Vurderingen tar hensyn til: "+String.Join(", ",guards)+".";
            return report;
        }
    }
}
