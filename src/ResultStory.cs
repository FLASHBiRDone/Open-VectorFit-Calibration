using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace VectorFit {
    public class ComparisonRecord {
        public string Key="",Source="",CreatedUtc="",Verdict="";
        public bool Complete,Passed,PrecisionFirst;
        public double MeanPrecisionA,MeanPrecisionB;
        public double ErrorA,ErrorB,TimeA,TimeB,PrecisionA,PrecisionB;
        public int FlickN,HitsA,HitsB,PrecisionN,PrecisionHitsA,PrecisionHitsB,MissesA,MissesB;
    }
    public static class ResultStory {
        public static string Key(Session s){
            var obj=new JObject(new JProperty("Setup",JObject.FromObject(s.Setup)),new JProperty("Mode",s.Mode.ToString()),new JProperty("Input",JObject.FromObject(s.InputCorrection)),new JProperty("Candidate",JObject.FromObject(s.Correction)),new JProperty("Curve",JObject.FromObject(s.ActiveCurve)));
            if(s.PrecisionFirst)obj["Protocol"]="precision-first-rings-v1";
            using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(obj.ToString()))).Replace("-","");
        }
        public static ComparisonRecord Record(Session s,string source=""){
            var a=s.Validation.Where(t=>!t.Corrected).ToList();var b=s.Validation.Where(t=>t.Corrected).ToList();
            var pa=s.PrecisionValidation.Where(t=>!t.Corrected).ToList();var pb=s.PrecisionValidation.Where(t=>t.Corrected).ToList();
            var decision=SequentialComparison.Evaluate(s);string note;bool precision=PrecisionCheck.Overall(s,decision,out note);
            return new ComparisonRecord{PrecisionFirst=s.PrecisionFirst,MeanPrecisionA=pa.Count>0?pa.Average(t=>PrecisionCheck.Time(t)):0,MeanPrecisionB=pb.Count>0?pb.Average(t=>PrecisionCheck.Time(t)):0,Key=Key(s),Source=source,CreatedUtc=s.CreatedUtc,Complete=decision.Finished&&s.PrecisionValidation.Count==32,Passed=precision,
                ErrorA=Analysis.Median(a.Select(t=>100*(t.Valid?t.ClickError:Math.Max(1,t.ClickError)))),ErrorB=Analysis.Median(b.Select(t=>100*(t.Valid?t.ClickError:Math.Max(1,t.ClickError)))),
                TimeA=Analysis.Median(a.Select(t=>t.TotalMs)),TimeB=Analysis.Median(b.Select(t=>t.TotalMs)),PrecisionA=Analysis.Median(pa.Select(PrecisionCheck.Time)),PrecisionB=Analysis.Median(pb.Select(PrecisionCheck.Time)),
                FlickN=Math.Min(a.Count,b.Count),HitsA=a.Count(t=>t.Hit==true),HitsB=b.Count(t=>t.Hit==true),PrecisionN=Math.Min(pa.Count,pb.Count),PrecisionHitsA=pa.Count(t=>t.Hit==true),PrecisionHitsB=pb.Count(t=>t.Hit==true),MissesA=pa.Sum(t=>PrecisionCheck.Misses(t)),MissesB=pb.Sum(t=>PrecisionCheck.Misses(t)),Verdict=decision.Note+" "+note};
        }
        public static bool Promising(ComparisonRecord r){if(r.PrecisionFirst)return r.Complete&&r.MeanPrecisionB<r.MeanPrecisionA*.95&&r.PrecisionHitsB>=r.PrecisionHitsA&&r.HitsB>=r.HitsA&&r.TimeB<=r.TimeA*1.05;return r.Complete&&r.ErrorB<r.ErrorA*.95&&r.TimeB<=r.TimeA*1.05&&r.HitsB>=r.HitsA&&r.PrecisionB<=r.PrecisionA*1.05&&r.PrecisionHitsB>=r.PrecisionHitsA&&r.MissesB<=r.MissesA;}
        public static string Headline(Session s){
            if(s.Validation.Count==0)return s.FrozenProposal?"Samme forslag · klar for bekreftelsesrunde":"Baseline klar · forslaget er ikke testet";
            var r=Record(s);if(!r.Complete)return "Testen er ufullført · ingen konklusjon ennå";
            var previous=s.PriorComparisons.Where(p=>p.Key==r.Key&&p.Complete).ToList();
            if(r.Passed&&previous.Count>0&&previous.Last().Passed)return "Forbedringen er støttet i to påfølgende kontroller";
            if(r.Passed)return "Denne kontrollen støtter forbedring · gjenta samme forslag";
            if(Promising(r))return previous.Count>0&&Promising(previous.Last())?"Lovende tendens gjentatt · fortsatt ikke bekreftet":"Lovende utvikling · ikke bekreftet ennå";
            if(r.ErrorB<r.ErrorA*.95&&r.TimeB>r.TimeA*1.05)return "Nærmere klikk, men langsommere";
            if(r.ErrorB>r.ErrorA*1.05||r.HitsB<r.HitsA)return "Svakere flickresultat · behold måleprofilen";
            return "Blandet eller liten forskjell · ingen klar forbedring";
        }
        public static string Trend(double before,double after,bool accuracy){
            if(before<=0)return "Ikke nok data";
            double change=100*(after/before-1);
            return Math.Abs(change)<5?"omtrent uendret":change<0?(accuracy?"mindre avvik":"raskere"):(accuracy?"større avvik":"langsommere");
        }
        public static string Flick(Session s){
            var r=Record(s);if(r.FlickN==0)return "Flick: A/B mangler. Kartlegging alene bekrefter ikke profileffekt.";
            return "Flick: "+Trend(r.ErrorA,r.ErrorB,true)+" · "+Trend(r.TimeA,r.TimeB,false)+".\nTreff: "+r.HitsA+" → "+r.HitsB+" av "+r.FlickN+" per profil. Avvik: "+r.ErrorA.ToString("F1")+" % → "+r.ErrorB.ToString("F1")+" %. Tid: "+r.TimeA.ToString("F0")+" → "+r.TimeB.ToString("F0")+" ms.";
        }
        public static string Precision(Session s){
            var r=Record(s);if(r.PrecisionN==0)return "Presisjon: A/B mangler. Ingen konklusjon om kontroll ennå.";
            return "Presisjon: "+Trend(r.PrecisionA,r.PrecisionB,false)+".\nTreff: "+r.PrecisionHitsA+" → "+r.PrecisionHitsB+" av "+r.PrecisionN+" per profil. Bomklikk: "+r.MissesA+" → "+r.MissesB+". Tid per mål (inkl. bom; tidsavbrudd = 3000 ms): "+r.PrecisionA.ToString("F0")+" → "+r.PrecisionB.ToString("F0")+" ms.";
        }
        public static string RepeatSummary(Session s){
            var r=Record(s);var runs=s.PriorComparisons.Where(p=>p.Key==r.Key).ToList();
            if(runs.Count==0)return "Neste steg: test samme forslag på nytt før du legger på flere justeringer.";
            var lines=new List<string>{"Samme A- og B-profil · tidligere kontroller beholdes"};
            int n=0;foreach(var p in runs.Concat(new[]{r}))lines.Add("Kontroll "+(++n)+": "+(!p.Complete?"ufullført":p.Passed?"støtter forbedring":Promising(p)?"lovende, ikke bekreftet":"blandet eller svakere")+" · flick "+p.ErrorA.ToString("F1")+" → "+p.ErrorB.ToString("F1")+" % · presisjon "+p.PrecisionA.ToString("F0")+" → "+p.PrecisionB.ToString("F0")+" ms");
            lines.Add("Kontrollene er vurdert hver for seg. Gjentatte tester er ingen samlet statistisk garanti.");return String.Join("\n",lines);
        }
    }
}
