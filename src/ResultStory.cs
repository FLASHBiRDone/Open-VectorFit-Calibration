using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace VectorFit {
    public class ComparisonRecord {
        public string Key="",Source="",CreatedUtc="",Verdict="";
        public bool Complete,Passed,PrecisionFirst,EvidenceSupported;
        public double MeanPrecisionA,MeanPrecisionB;
        public double ErrorA,ErrorB,TimeA,TimeB,PrecisionA,PrecisionB;
        public int FlickN,HitsA,HitsB,PrecisionN,PrecisionHitsA,PrecisionHitsB,MissesA,MissesB;
    }
    public class InitialReference {
        public string CreatedUtc;
        public bool BalancedTargets,PrecisionFirst,SmallGainProtocol;
        public Setup Setup; public Correction Input;
        public CircleData Circles;
        public List<Trial> Flick,Precision;
    }
    public static class ResultStory {
        static string T(string nb,string en){return DriverSetup.Text(nb,en);}
        public static InitialReference Initial(Session s){
            if(s.InitialBaseline!=null)return s.InitialBaseline;
            string note;var history=History.Load(s,Driver.Data,out note);var first=history[0].Session;
            if(first.Round!=1||first.Baseline.Count==0)return null;
            return new InitialReference{SmallGainProtocol=first.SmallGainProtocol,Circles=JObject.FromObject(first.Circles).ToObject<CircleData>(),CreatedUtc=first.CreatedUtc,BalancedTargets=first.BalancedTargets,PrecisionFirst=first.PrecisionFirst,Setup=JObject.FromObject(first.Setup).ToObject<Setup>(),Input=first.InputCorrection.Copy(),Flick=JArray.FromObject(first.Baseline).ToObject<List<Trial>>(),Precision=JArray.FromObject(first.PrecisionBaseline).ToObject<List<Trial>>()};
        }
        public static bool Confirmed(Session s){if(s.RobustConfirmation)return ProfileEvidence.RobustConfirmed(s);var r=Record(s);var previous=s.PriorComparisons.Where(p=>p.Key==r.Key&&p.Complete).LastOrDefault();return s.FrozenProposal&&r.Complete&&r.Passed&&previous!=null&&previous.Passed;}
        public static string SimpleVerdict(Session s){var r=Record(s);if(!r.Complete)return T("Ikke ferdig testet","Test not complete");if(Confirmed(s))return T("Ny profil er bedre · anbefalt bytte","New profile is better · switching recommended");if(r.Passed)return T("Ny profil er lovende · bekreft én gang til","New profile is promising · confirm once more");if(r.PrecisionHitsB<r.PrecisionHitsA&&r.MeanPrecisionB>=r.MeanPrecisionA||r.MeanPrecisionB>r.MeanPrecisionA*1.02&&r.HitsB<=r.HitsA)return T("Forrige profil fungerte bedre i denne testen","Previous profile performed better in this test");return T("Ingen klar vinner · behold profilen foreløpig","No clear winner · keep your profile for now");}
        public static string SimpleSummary(Session s){var r=Record(s);if(!r.Complete)return T("Fullfør testen. Grønt og rødt viser foreløpige forskjeller.","Finish the test. Green and red show provisional differences.");var pa=s.PrecisionValidation.Where(t=>!t.Corrected).Select(Trajectory.Measure).ToList();var pb=s.PrecisionValidation.Where(t=>t.Corrected).Select(Trajectory.Measure).ToList();string control=pa.All(q=>q.Available)&&pb.All(q=>q.Available)?(pb.Average(q=>q.CorrectionDistance)<pa.Average(q=>q.CorrectionDistance)?T("mindre etterkorrigering","less corrective travel"):T("mer eller lik etterkorrigering","more or equal corrective travel")):T("ukjent etterkorrigering","unknown corrective travel");return T("Presisjon, gjennomsnitt: ","Precision, mean: ")+r.MeanPrecisionA.ToString("F0")+" → "+r.MeanPrecisionB.ToString("F0")+" ms; "+control+".\n"+T("Flick: ","Flick: ")+r.HitsA+" → "+r.HitsB+T(" bullseye-treff. Se poeng og tid nedenfor."," bullseye hits. See score and time below.")+"\n"+(Confirmed(s)?T("Fordelen holdt i bekreftelsesrunden. Aktivering er ditt valg.","The benefit held in confirmation. Activation is your choice."):T("Test samme forslag igjen før du justerer videre. Forrige profil er ikke nødvendigvis bedre selv om forslaget ikke er godkjent.","Repeat the same proposal before adjusting further. A proposal that is not approved does not automatically make the previous profile better."));}
        public static string Explanation(Session s){
            var r=Record(s);if(!r.Complete)return T("Samle en full A/B-test før profilen vurderes. Målerne viser bare foreløpige forskjeller.","Complete the A/B test before judging the proposal. Gauges show provisional differences only.");
            var pa=s.PrecisionValidation.Where(t=>!t.Corrected).ToList();var pb=s.PrecisionValidation.Where(t=>t.Corrected).ToList();
            var qa=pa.Select(Trajectory.Measure).ToList();var qb=pb.Select(Trajectory.Measure).ToList();
            string control=qa.All(q=>q.Available)&&qb.All(q=>q.Available)?T("Etterkorrigering: ","Corrective travel: ")+(100*qa.Average(q=>q.CorrectionDistance)).ToString("F1")+" → "+(100*qb.Average(q=>q.CorrectionDistance)).ToString("F1")+" %.":T("Etterkorrigering mangler.","Corrective travel unavailable.");
            var lines=new List<string>{T("Presisjon, gjennomsnitt: ","Precision, mean: ")+r.MeanPrecisionA.ToString("F0")+" → "+r.MeanPrecisionB.ToString("F0")+T(" ms i gjennomsnitt, inkludert bom og korrigering. Treff: "," ms on average, including misses and corrections. Hits: ")+r.PrecisionHitsA+" → "+r.PrecisionHitsB+" / "+r.PrecisionN+". "+control};
            lines.Add(T("Flick: bullseye-treff ","Flick: bullseye hits ")+r.HitsA+" → "+r.HitsB+" / "+r.FlickN+T(". Grønt på én måler betyr ikke at hele profilen er bedre.",". Green on one gauge does not mean the whole profile is better."));
            string why;PrecisionCheck.Overall(s,SequentialComparison.Evaluate(s),out why);
            lines.Add(why);
            if(!SequentialComparison.Evaluate(s).GuardsPassed)lines.Add(T("Flick-kontrollen stopper også anbefalingen: ","The flick safeguards also block approval: ")+SequentialComparison.Evaluate(s).Note);
            lines.Add(r.Passed?T("Neste steg: bekreft samme forslag på nytt før videre justering.","Next: confirm the same proposal again before further adjustment."):T("Neste steg: behold måleprofilen foreløpig og bekreft samme forslag på nytt. Det beregner ingen ny korrigering.","Next: keep the measured profile for now and repeat the same proposal. This does not fit another correction."));
            return String.Join("\n\n",lines);
        }

        public static string Key(Session s){
            var obj=new JObject(new JProperty("Setup",JObject.FromObject(s.Setup)),new JProperty("Mode",s.Mode.ToString()),new JProperty("Input",JObject.FromObject(s.InputCorrection)),new JProperty("Candidate",JObject.FromObject(s.Correction)),new JProperty("Curve",JObject.FromObject(s.ActiveCurve)));
            if(s.SmallGainProtocol)obj["Revision"]="small-gain-confirmation-large-targets-100-v1";
            if(s.ControlPriority)obj["Objective"]="precision-speed-or-control-v1";
            if(s.BalancedTargets)obj["TargetPlan"]="balanced-distance-v1";
            if(s.PrecisionFirst)obj["Protocol"]="precision-first-rings-v1";
            using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(obj.ToString()))).Replace("-","");
        }
        public static ComparisonRecord Record(Session s,string source=""){
            var a=s.Validation.Where(t=>!t.Corrected).ToList();var b=s.Validation.Where(t=>t.Corrected).ToList();
            var pa=s.PrecisionValidation.Where(t=>!t.Corrected).ToList();var pb=s.PrecisionValidation.Where(t=>t.Corrected).ToList();
            var decision=SequentialComparison.Evaluate(s);string note;bool precision=PrecisionCheck.Overall(s,decision,out note);
            return new ComparisonRecord{EvidenceSupported=ProfileEvidence.Supported(s),PrecisionFirst=s.PrecisionFirst,MeanPrecisionA=pa.Count>0?pa.Average(t=>PrecisionCheck.Time(t)):0,MeanPrecisionB=pb.Count>0?pb.Average(t=>PrecisionCheck.Time(t)):0,Key=Key(s),Source=source,CreatedUtc=s.CreatedUtc,Complete=decision.Finished&&s.PrecisionValidation.Count==32,Passed=precision,
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
