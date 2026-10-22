using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace VectorFit {
    public class Setup {
        public string Mouse = "Logitech G703";
        public double Dpi = 1600, Cm360 = 40, Hfov = 103;
        public string Device = "";
        [JsonIgnore] public double DegreesPerCount { get { return 360.0 * 2.54 / (Dpi * Cm360); } }
        public bool Valid() { return Dpi>=100 && Dpi<=50000 && Cm360>=3 && Cm360<=200 && Hfov>=60 && Hfov<=140; }
    }
    public class PointD {
        public double X,Y;
        public PointD() {}
        public PointD(double x,double y) { X=x; Y=y; }
        [JsonIgnore] public double Length { get { return Math.Sqrt(X*X+Y*Y); } }
    }
    public class Motion : PointD {
        public double T;
        public Motion(double t,double x,double y):base(x,y) { T=t; }
    }
    public class Trial {
        public int Direction;
        public double TargetX,TargetY, EndX,EndY, ReactionMs, FlickMs, TotalMs, ClickError, ErrorX, ErrorY;
        public bool Valid, Corrected;
        public bool? Hit;
        public int PairId;
        public List<AimClick> Clicks=new List<AimClick>();
        public double HitRadiusDegrees;
        public string Rejection="";
        public List<Motion> Samples=new List<Motion>();
        [JsonIgnore] public double Radius { get { return Math.Sqrt(TargetX*TargetX+TargetY*TargetY); } }
        [JsonIgnore] public double Reach { get { return (EndX*TargetX+EndY*TargetY)/(Radius*Radius); } }
    }
    public class Correction {
        public double Right=1,Left=1,Down=1,Up=1,Rotation=0,Sensitivity=1;
        public PointD Transform(double x,double y) {
            double r=Rotation*Math.PI/180, c=Math.Cos(r),s=Math.Sin(r);
            double a=x*c-y*s, b=x*s+y*c;
            return new PointD(a*(a<0?Left:Right)*Sensitivity, b*(b<0?Up:Down)*Sensitivity);
        }
        public PointD Inverse(double x,double y) {
            double a=x/((x<0?Left:Right)*Sensitivity),b=y/((y<0?Up:Down)*Sensitivity),r=Rotation*Math.PI/180;
            return new PointD(a*Math.Cos(r)+b*Math.Sin(r),-a*Math.Sin(r)+b*Math.Cos(r));
        }
        public Correction Copy(){return new Correction{Right=Right,Left=Left,Down=Down,Up=Up,Rotation=Rotation,Sensitivity=Sensitivity};}
        public bool Safe() {
            return new[]{Right,Left,Down,Up}.All(x=>!Double.IsNaN(x)&&!Double.IsInfinity(x)&&x>=0.92-1e-8&&x<=1.08+1e-8)
                && !Double.IsNaN(Rotation) && Math.Abs(Rotation)<=3.0 && Sensitivity>=.75 && Sensitivity<=1.25;
        }
    }
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ProfileMode { RawAccel, Interception }
    public class Session {
        public string Version="0.13.0", CreatedUtc=DateTime.UtcNow.ToString("o");
        public bool ConservativeSensitivity=true;
        public bool GuidedCalibration, DualTask;
        public bool FrozenProposal;
        public string FrozenKey="";
        public List<ComparisonRecord> PriorComparisons=new List<ComparisonRecord>();
        [JsonIgnore] public Correction TestInputCorrection;
        public bool TrajectoryFitting=true,RawAccelAdaptiveValidation=true,InterceptionAdaptiveValidation=true;
        [JsonIgnore] public bool AdaptiveValidation {get{return UsesInterception?InterceptionAdaptiveValidation:RawAccelAdaptiveValidation;}set{if(UsesInterception)InterceptionAdaptiveValidation=value;else RawAccelAdaptiveValidation=value;}}
        public int RawAccelComparisonGoal=32,InterceptionComparisonGoal=32;
        [JsonIgnore] public int ComparisonGoal {get{return UsesInterception?InterceptionComparisonGoal:RawAccelComparisonGoal;}set{if(UsesInterception)InterceptionComparisonGoal=value;else RawAccelComparisonGoal=value;}}
        public bool RequireCloserClicks=true;
        public bool AdaptiveCalibration=true;
        public int BaselineGoal=48;
        public CircleData Circles=new CircleData();
        public string CircleNote="";
        public bool CalibrateSensitivity=true;
        public string SensitivityNote="";
        public int Round=1;
        public string ParentMeasurement="";
        public string ReanalyzedFrom="";
        public Correction InputCorrection=new Correction();
        public ProfileMode Mode=ProfileMode.RawAccel;
        public Setup Setup=new Setup();
        public List<Trial> PrecisionBaseline=new List<Trial>(),RawPrecisionValidation=new List<Trial>(),CurvePrecisionValidation=new List<Trial>();
        [JsonIgnore] public List<Trial> PrecisionValidation {get{return UsesInterception?CurvePrecisionValidation:RawPrecisionValidation;}set{if(UsesInterception)CurvePrecisionValidation=value;else RawPrecisionValidation=value;}}
        public List<Trial> Baseline=new List<Trial>();
        public List<Trial> RawAccelValidation=new List<Trial>(), InterceptionValidation=new List<Trial>();
        [JsonIgnore] public List<Trial> Validation {
            get { return Mode==ProfileMode.Interception?InterceptionValidation:RawAccelValidation; }
            set { if(Mode==ProfileMode.Interception)InterceptionValidation=value;else RawAccelValidation=value; }
        }
        public Correction Correction=new Correction(); public CurveProfile Curve=new CurveProfile();
        public bool FitReady, Repeatable, ValidationPassed;
        public string Notes="";
        public double ReferenceReach;
        [JsonIgnore] public bool UsesInterception { get { return Mode==ProfileMode.Interception; } }
        [JsonIgnore] public string ModeName { get { return UsesInterception?"Interception + kurvekorreksjon":"Raw Accel uten Interception"; } }
        [JsonIgnore] public CurveProfile ActiveCurve { get { return UsesInterception?Curve:new CurveProfile(); } }
        [JsonIgnore] public bool CanTryRawAccel { get { return Mode==ProfileMode.RawAccel&&Baseline.Count>0&&FitReady&&Correction!=null&&Correction.Safe(); } }
        [JsonIgnore] public bool ReadyForRawAccel { get { return CanTryRawAccel&&ValidationPassed&&Repeatable; } }
        [JsonIgnore] public bool ReadyForInterception { get { return UsesInterception&&FitReady&&ValidationPassed&&Correction.Safe()&&Curve.Safe(); } }
        public void SelectMode(ProfileMode mode) {
            if(!Enum.IsDefined(typeof(ProfileMode),mode))throw new ArgumentException("Ukjent profiltype.");
            if(FrozenProposal&&mode!=Mode)throw new Exception("Bekreftelsesrunden er låst til samme profiltype.");
            Mode=mode;if(!FrozenProposal)Analysis.Compute(this);if(Validation.Count>0)Notes=Analysis.Validate(this);
        }
        public Session NextRound(string parentMeasurement,bool useCandidate=true) {
            if(!CanTryRawAccel)throw new Exception("Beregn en standardprofil før du starter neste runde.");
            return new Session{GuidedCalibration=true,DualTask=true,Round=Round+1,ParentMeasurement=parentMeasurement,CalibrateSensitivity=CalibrateSensitivity,InputCorrection=(useCandidate?Correction:InputCorrection).Copy(),
                Setup=new Setup{Mouse=Setup.Mouse,Dpi=Setup.Dpi,Cm360=Setup.Cm360,Hfov=Setup.Hfov,Device=Setup.Device}};
        }
        public Session Confirmation(string source){
            if(!FitReady||!Correction.Safe())throw new Exception("Beregn et forslag før bekreftelsesrunden.");
            var result=JObject.FromObject(this).ToObject<Session>();
            if(Validation.Count>0)result.PriorComparisons.Add(ResultStory.Record(this,source));
            result.Version="0.13.0";result.CreatedUtc=DateTime.UtcNow.ToString("o");result.FrozenProposal=true;result.FrozenKey=ResultStory.Key(result);result.DualTask=true;
            result.Validation.Clear();result.PrecisionValidation.Clear();result.ValidationPassed=false;result.AdaptiveValidation=true;result.ComparisonGoal=32;
            result.Notes="Samme A og B testes på nytt. Ingen nye innstillinger beregnes eller aktiveres.";return result;
        }
        public Session ReanalyzeCircleTrend(string source){
            if(Circles.Protocol!=2||Circles.Laps.Count(Circles.Counts)!=60)throw new Exception("Denne målingen har ikke 60 naturlige sirkelrunder.");
            var result=JObject.FromObject(this).ToObject<Session>();
            result.Version="0.13.0";result.CreatedUtc=DateTime.UtcNow.ToString("o");result.ReanalyzedFrom=source;result.Circles.Protocol=3;result.FrozenProposal=false;result.FrozenKey="";result.PriorComparisons.Clear();
            result.RawAccelValidation.Clear();result.InterceptionValidation.Clear();result.RawPrecisionValidation.Clear();result.CurvePrecisionValidation.Clear();
            result.RawAccelComparisonGoal=result.InterceptionComparisonGoal=32;result.RawAccelAdaptiveValidation=result.InterceptionAdaptiveValidation=true;
            Analysis.Compute(result);result.Notes="Ny analyse av samme måling. Forslaget er ikke testet. "+result.CircleNote;return result;
        }
        public static Session FromJson(string json) {
            var data=JObject.Parse(json);string version=(string)data["Version"];
            if(!new[]{"0.2.0","0.3.0","0.4.0","0.5.0","0.6.0","0.7.0","0.8.0","0.9.0","0.10.0","0.11.0","0.12.0","0.13.0"}.Contains(version))throw new Exception("Denne målingen støttes ikke. Start en ny kalibrering.");
            var s=data.ToObject<Session>();
            if(s.Setup==null||!s.Setup.Valid()||s.Baseline==null||s.RawAccelValidation==null||s.InterceptionValidation==null||
               !Enum.IsDefined(typeof(ProfileMode),s.Mode)||s.InputCorrection==null||!s.InputCorrection.Safe()||s.Round<1)throw new Exception("Ugyldig målefil.");
            if(version=="0.2.0"||version=="0.3.0") {
                // Version 0.3 always tested the curve engine. Never reuse its A/B as Raw Accel evidence.
                s.RawAccelValidation=new List<Trial>();
                s.InterceptionValidation=version=="0.3.0"&&data["Validation"] is JArray?data["Validation"].ToObject<List<Trial>>():new List<Trial>();
                s.Mode=ProfileMode.RawAccel;
            }
            if(version!="0.5.0"&&version!="0.6.0"&&version!="0.7.0"&&version!="0.8.0"&&version!="0.9.0"&&version!="0.10.0"&&version!="0.11.0"&&version!="0.12.0"&&version!="0.13.0"){s.InputCorrection=new Correction();s.Round=1;s.ParentMeasurement="";}
            // Old A/B data was collected without overall sensitivity fitting. Preserve its exact proposal.
            if(version!="0.6.0"&&version!="0.7.0"&&version!="0.8.0"&&version!="0.9.0"&&version!="0.10.0"&&version!="0.11.0"&&version!="0.12.0"&&version!="0.13.0")s.CalibrateSensitivity=false;
            // Preserve historical assessments. Every newly created round uses the target-distance requirement.
            if(version!="0.7.0"&&version!="0.8.0"&&version!="0.9.0"&&version!="0.10.0"&&version!="0.11.0"&&version!="0.12.0"&&version!="0.13.0")s.RequireCloserClicks=false;
            if(version!="0.8.0"&&version!="0.9.0"&&version!="0.10.0"&&version!="0.11.0"&&version!="0.12.0"&&version!="0.13.0"){s.AdaptiveCalibration=false;s.BaselineGoal=80;s.Circles=new CircleData();}
            if(version!="0.9.0"&&version!="0.10.0"&&version!="0.11.0"&&version!="0.12.0"&&version!="0.13.0"){s.TrajectoryFitting=false;s.RawAccelAdaptiveValidation=s.InterceptionAdaptiveValidation=false;s.RawAccelComparisonGoal=s.InterceptionComparisonGoal=64;}
            if(version!="0.10.0"&&version!="0.11.0"&&version!="0.12.0"&&version!="0.13.0")s.ConservativeSensitivity=false;
            if(s.PrecisionBaseline==null||s.RawPrecisionValidation==null||s.CurvePrecisionValidation==null)throw new Exception("Ugyldig målefil.");
            if(s.Circles==null||s.Circles.Laps==null||!new[]{0,1,2,3}.Contains(s.Circles.Protocol)||s.Circles.Laps.Any(lap=>lap==null||lap.Samples==null)||s.BaselineGoal<48||s.BaselineGoal>80)throw new Exception("Ugyldig målefil.");
            if(!new[]{32,48,64}.Contains(s.RawAccelComparisonGoal)||!new[]{32,48,64}.Contains(s.InterceptionComparisonGoal))throw new Exception("Ugyldig målefil.");
            s.Version="0.13.0";
            if(s.PriorComparisons==null||s.PriorComparisons.Any(p=>p==null))throw new Exception("Ugyldig kontrollhistorikk.");
            if(s.FrozenProposal&&(s.Correction==null||!s.Correction.Safe()||s.Curve==null||!s.Curve.Safe()||s.FrozenKey!=ResultStory.Key(s)))throw new Exception("Den låste profilen er endret. Start en ny sammenligning.");
            s.SelectMode(s.Mode);return s;
        }
    }
    public static class Analysis {
        public static double Median(IEnumerable<double> numbers) {
            var a=numbers.Where(x=>!Double.IsNaN(x)&&!Double.IsInfinity(x)).OrderBy(x=>x).ToArray();
            if(a.Length==0) return 0;
            return a.Length%2==1?a[a.Length/2]:(a[a.Length/2-1]+a[a.Length/2])/2;
        }
        public static double Clamp(double n,double lo,double hi) { return Math.Max(lo,Math.Min(hi,n)); }
        public static Trial Extract(int direction,double tx,double ty,List<Motion> input,double clickMs,bool corrected) {
            var tr=new Trial { Direction=direction,TargetX=tx,TargetY=ty,TotalMs=clickMs,Corrected=corrected,Samples=new List<Motion>(input) };
            // Keep every received position, including the exact click endpoint. Misses are measurements.
            if(input.Count==0 || tr.Radius<=0) { tr.Rejection="Mangler bevegelsesdata"; return tr; }
            var last=input[input.Count-1];
            tr.EndX=last.X;tr.EndY=last.Y;
            tr.ErrorX=last.X-tx;tr.ErrorY=last.Y-ty;
            tr.ClickError=Math.Sqrt(tr.ErrorX*tr.ErrorX+tr.ErrorY*tr.ErrorY)/tr.Radius;
            if(input.Any(p=>Double.IsNaN(p.X)||Double.IsNaN(p.Y)||Double.IsInfinity(p.X)||Double.IsInfinity(p.Y))) {
                tr.Rejection="Ugyldige bevegelsesdata";return tr;
            }
            var onset=input.FirstOrDefault(p=>p.Length>0.001);
            if(onset==null) { tr.Rejection="Klikk uten bevegelse: lagret, men gir ingen retningsmåling";return tr; }
            tr.ReactionMs=onset.T;tr.FlickMs=Math.Max(0,clickMs-onset.T);
            tr.Valid=true;return tr;
        }
        static double Loss(List<Trial> ts, Correction c,double reference) {
            return ts.Average(t=> {
                var p=c.Transform(t.EndX,t.EndY);
                double e=Math.Sqrt(Math.Pow(p.X-reference*t.TargetX,2)+Math.Pow(p.Y-reference*t.TargetY,2))/t.Radius;
                // Huber loss reduces the influence of a few unusual flicks.
                return e<.12?e*e:.24*e-.0144;
            });
        }
        public static Correction Fit(List<Trial> ts,double reference) {
            if(ts.Count==0) return new Correction();
            Correction best=new Correction(); double bestLoss=Double.MaxValue;
            for(int step=-40;step<=40;step++) {
                double rot=step*.1, rad=rot*Math.PI/180;
                var num=new double[4];var den=new double[4];
                foreach(var t in ts) {
                    double x=t.EndX*Math.Cos(rad)-t.EndY*Math.Sin(rad),y=t.EndX*Math.Sin(rad)+t.EndY*Math.Cos(rad);
                    int ix=x<0?1:0,iy=y<0?3:2;
                    double w=1/(t.Radius*t.Radius);
                    num[ix]+=w*x*t.TargetX*reference; den[ix]+=w*x*x;
                    num[iy]+=w*y*t.TargetY*reference; den[iy]+=w*y*y;
                }
                double[] g=Enumerable.Range(0,4).Select(i=>den[i]<1e-9?1:Clamp(num[i]/den[i],.75,1.25)).ToArray();
                double normal=Math.Exp(g.Select(v=>Math.Log(v)).Average());
                var c=new Correction{Right=g[0]/normal,Left=g[1]/normal,Down=g[2]/normal,Up=g[3]/normal,Rotation=rot};
                double loss=Loss(ts,c,reference);
                if(loss<bestLoss) {bestLoss=loss;best=c;}
            }
            return best;
        }
        public static Trial UndoInputCorrection(Trial t,Correction input) {
            var samples=new List<Motion>();double x=0,y=0,previousX=0,previousY=0;
            // Invert each movement, not just the endpoint: the gains can differ by sign.
            foreach(var sample in t.Samples) {
                var delta=input.Inverse(sample.X-previousX,sample.Y-previousY);x+=delta.X;y+=delta.Y;
                samples.Add(new Motion(sample.T,x,y));previousX=sample.X;previousY=sample.Y;
            }
            var tail=input.Inverse(t.EndX-previousX,t.EndY-previousY);x+=tail.X;y+=tail.Y;
            return new Trial{Direction=t.Direction,TargetX=t.TargetX,TargetY=t.TargetY,EndX=x,EndY=y,
                Valid=t.Valid,Corrected=t.Corrected,Hit=t.Hit,HitRadiusDegrees=t.HitRadiusDegrees,PairId=t.PairId,TotalMs=t.TotalMs,ReactionMs=t.ReactionMs,FlickMs=t.FlickMs,
                ErrorX=x-t.TargetX,ErrorY=y-t.TargetY,ClickError=t.Radius>0?Math.Sqrt(Math.Pow(x-t.TargetX,2)+Math.Pow(y-t.TargetY,2))/t.Radius:0,Samples=samples};
        }
        public static void Compute(Session s) {
            if(s.InputCorrection==null||!s.InputCorrection.Safe())throw new Exception("Forrige profil ligger utenfor tillatte grenser.");
            var valid=s.Baseline.Where(t=>t.Valid).Select(t=>UndoInputCorrection(t,s.InputCorrection)).ToList();
            s.FitReady=false;s.Repeatable=false;s.ValidationPassed=false;s.SensitivityNote="";s.CircleNote="";s.Correction=new Correction();s.Curve=new CurveProfile();
            if(s.TrajectoryFitting){var observed=s.Baseline.Where(t=>t.Valid).ToList();valid=valid.Select((t,i)=>Trajectory.ForFit(t,Trajectory.Measure(observed[i]))).ToList();}
            s.Curve=CurveFit.Learn(valid);
            int minimum=s.AdaptiveCalibration?6:8;
            if(Enumerable.Range(0,8).Any(d=>valid.Count(t=>t.Direction==d)<minimum)) {
                s.Notes=s.AdaptiveCalibration?"Trenger minst 6 målinger med bevegelse i hver av de 8 retningene.":"Trenger minst 8 målinger med bevegelse i hver av de 8 retningene.";return;
            }
            s.ReferenceReach=Median(valid.Select(t=>t.Reach));
            var fit=Fit(valid,s.ReferenceReach);
            // Move halfway from the measured input profile toward the newly estimated absolute profile.
            // Bounds apply to the final profile, so repeated rounds never compound beyond these limits.
            var previous=s.InputCorrection;
            s.Correction=new Correction {
                Right=Clamp(Math.Sqrt(previous.Right*fit.Right),.92,1.08),Left=Clamp(Math.Sqrt(previous.Left*fit.Left),.92,1.08),
                Down=Clamp(Math.Sqrt(previous.Down*fit.Down),.92,1.08),Up=Clamp(Math.Sqrt(previous.Up*fit.Up),.92,1.08),Rotation=Clamp((previous.Rotation+fit.Rotation)*.5,-3,3),Sensitivity=previous.Sensitivity
            };
            CircleAnalysis.Contribute(s);
            var a=valid.Where((t,i)=>i%2==0).ToList();var b=valid.Where((t,i)=>i%2==1).ToList();
            // Split within each direction, so both fits see all eight directions.
            a.Clear();b.Clear();
            for(int d=0;d<8;d++) {var ds=valid.Where(t=>t.Direction==d).ToList();a.AddRange(ds.Where((t,i)=>i%2==0));b.AddRange(ds.Where((t,i)=>i%2==1));}
            var ca=Fit(a,Median(a.Select(t=>t.Reach)));var cb=Fit(b,Median(b.Select(t=>t.Reach)));
            s.Repeatable=Math.Abs(ca.Right-cb.Right)<.08&&Math.Abs(ca.Left-cb.Left)<.08&&Math.Abs(ca.Up-cb.Up)<.08&&Math.Abs(ca.Down-cb.Down)<.08&&Math.Abs(ca.Rotation-cb.Rotation)<2;
            if(s.CalibrateSensitivity) {
                // Predict reach from every delta with the candidate's directional shape at the current sensitivity.
                Func<Trial,double> reach=t=>TransformedReach(t,s.Correction);
                double median=Median(valid.Select(reach));
                var directions=Enumerable.Range(0,8).Select(d=>Median(valid.Where(t=>t.Direction==d).Select(reach))).ToArray();
                bool consistent=median>1.02?directions.Count(v=>v>1.02)>=6:median<.98&&directions.Count(v=>v<.98)>=6;
                bool stable=Math.Abs(Median(a.Select(reach))-Median(b.Select(reach)))<=.08;
                if(s.AdaptiveCalibration&&!stable)s.Repeatable=false;
                bool reliable=true;
                if(s.ConservativeSensitivity){
                    double scatter=Median(valid.Select(t=>Math.Abs(reach(t)-median)));
                    // valid preserves acquisition order; compare early and late observations, not alternating ones.
                    double early=Median(valid.Take(valid.Count/2).Select(reach)),late=Median(valid.Skip(valid.Count/2).Select(reach));
                    reliable=s.Repeatable&&Math.Abs(median-1)>=Math.Max(.02,.75*scatter)&&(median>1?early>1.02&&late>1.02:early<.98&&late<.98);
                }
                if(consistent&&stable&&reliable&&median>.25) {
                    s.Correction.Sensitivity=Clamp(previous.Sensitivity*Clamp(1/Math.Sqrt(median),.95,1.05),.75,1.25);
                    s.SensitivityNote=(median>1?"Gjentatt overskyting":"Gjentatt underskyting")+": følsomheten foreslås fra "+previous.Sensitivity.ToString("F3")+"× til "+s.Correction.Sensitivity.ToString("F3")+"×. A/B avgjør om det hjelper.";
                }else s.SensitivityNote=s.ConservativeSensitivity&&!reliable?"Følsomheten beholdes: forskjellen er for usikker i forhold til variasjonen mellom bevegelsene.":"Følsomheten beholdes: ingen stabil over- eller underskyting i minst 6 av 8 retninger.";
            }else s.SensitivityNote="Samlet følsomhet er beholdt for denne målingen.";
            s.FitReady=s.Correction.Safe();
            s.Notes=s.Repeatable?"Profilen er beregnet. A/B-testen kan sammenligne forslaget med oppsettet du målte med.":"Målingene varierer. Profilen kan prøves, men effekten er usikker. A/B-test anbefales.";
        }
        public static double TransformedReach(Trial t,Correction c) {
            double x=0,y=0,px=0,py=0;
            foreach(var sample in t.Samples){var delta=c.Transform(sample.X-px,sample.Y-py);x+=delta.X;y+=delta.Y;px=sample.X;py=sample.Y;}
            var tail=c.Transform(t.EndX-px,t.EndY-py);x+=tail.X;y+=tail.Y;
            return t.Radius>0?(x*t.TargetX+y*t.TargetY)/(t.Radius*t.Radius):0;
        }
        public static double DirectionBias(List<Trial> ts) {
            if(ts.Count==0)return 0;
            double median=Median(ts.Select(t=>t.Reach));
            return Math.Sqrt(Enumerable.Range(0,8).Average(d=>Math.Pow(Median(ts.Where(t=>t.Direction==d).Select(t=>t.Reach))-median,2)));
        }
        public static double Bend(Trial trial) {
            double chord=Math.Sqrt(trial.EndX*trial.EndX+trial.EndY*trial.EndY);if(chord<1e-8)return 0;
            double area=0,length=0;
            for(int i=1;i<trial.Samples.Count;i++) {
                var a=trial.Samples[i-1];var b=trial.Samples[i];double dx=b.X-a.X,dy=b.Y-a.Y,step=Math.Sqrt(dx*dx+dy*dy);
                double crossA=Math.Abs(a.X*trial.EndY-a.Y*trial.EndX)/chord,crossB=Math.Abs(b.X*trial.EndY-b.Y*trial.EndX)/chord;
                area+=(crossA+crossB)*.5*step;length+=step;
            }
            return length<1e-8?0:area/(length*trial.Radius);
        }
        public static string Validate(Session s) {
            if(s.AdaptiveValidation){var report=SequentialComparison.Evaluate(s);s.ValidationPassed=report.Decision==ComparisonDecision.Improved;if(report.Finished)s.ComparisonGoal=report.Clicks;string precisionNote="";if(s.DualTask){bool okay=PrecisionCheck.Acceptable(s.PrecisionValidation,out precisionNote);s.ValidationPassed=s.ValidationPassed&&okay;}return report.Note+(s.DualTask?" "+precisionNote:"");}
            var a=s.Validation.Where(t=>t.Valid&&!t.Corrected).ToList();var b=s.Validation.Where(t=>t.Valid&&t.Corrected).ToList();
            s.ValidationPassed=false;
            if(Enumerable.Range(0,8).Any(d=>a.Count(t=>t.Direction==d)<4||b.Count(t=>t.Direction==d)<4))return "A/B-testen er ikke fullført.";
            // A and B must contain the same distances in every direction; otherwise difficulty confounds the comparison.
            foreach(int d in Enumerable.Range(0,8)) {
                var da=a.Where(t=>t.Direction==d).Select(t=>Math.Round(t.Radius,4)).OrderBy(v=>v).ToArray();
                var db=b.Where(t=>t.Direction==d).Select(t=>Math.Round(t.Radius,4)).OrderBy(v=>v).ToArray();
                if(!da.SequenceEqual(db))return "A/B-testen har ulike målavstander. Kjør en ny, komplett test.";
                var ga=a.Where(t=>t.Direction==d).Select(t=>Tuple.Create(Math.Round(t.TargetX,4),Math.Round(t.TargetY,4))).OrderBy(t=>t.Item1).ThenBy(t=>t.Item2);
                var gb=b.Where(t=>t.Direction==d).Select(t=>Tuple.Create(Math.Round(t.TargetX,4),Math.Round(t.TargetY,4))).OrderBy(t=>t.Item1).ThenBy(t=>t.Item2);
                if(!ga.SequenceEqual(gb))return "A/B-testen har ulike målvinkler. Kjør en ny, komplett test.";
            }
            double ba=DirectionBias(a),bb=DirectionBias(b),ta=Median(a.Select(t=>t.TotalMs)),tb=Median(b.Select(t=>t.TotalMs));
            double ea=Median(a.Select(t=>t.ClickError)),eb=Median(b.Select(t=>t.ClickError));
            double bendA=Median(a.Select(Bend)),bendB=Median(b.Select(Bend));
            bool improved=ba>.015&&bb<=ba*.95;
            if(s.CalibrateSensitivity)improved=improved||(ea>.02&&eb<=ea*.95&&bb<=ba+.005);
            if(s.UsesInterception&&s.Curve.SupportedBins>0)improved=improved||(bendA>.005&&bendB<=bendA*.95&&bb<=ba+.005);
            // New rounds require actual target proximity to improve. Direction or bend alone is insufficient.
            if(s.RequireCloserClicks)improved=ea>1e-9&&eb<=ea*.95;
            bool directionOkay=!s.RequireCloserClicks||bb<=ba+.005;
            s.ValidationPassed=s.FitReady&&s.Repeatable&&improved&&directionOkay&&tb<=ta*1.05&&eb<=ea+.015;
            if(s.ValidationPassed)return s.RequireCloserClicks?"A/B viser minst 5 % mindre medianavstand til målets sentrum, uten tydelig tap i tid eller retning.":"A/B-testen for "+s.ModeName+" viser forbedring uten tydelig tap i tid eller treff.";
            var reasons=new List<string>();
            if(!s.Repeatable)reasons.Add("kalibreringen varierer");
            if(!improved)reasons.Add(s.RequireCloserClicks?"klikkene er ikke minst 5 % nærmere målets sentrum":"ingen tydelig forbedring i retning"+(s.CalibrateSensitivity?", klikkavvik":"")+(s.UsesInterception?" eller bøy":""));
            if(!directionOkay)reasons.Add("større retningsspredning");
            if(tb>ta*1.05)reasons.Add("langsommere klikk");
            if(eb>ea+.015)reasons.Add("større klikkavvik");
            return "A/B har ikke bekreftet forbedring: "+String.Join(", ",reasons)+".";
        }
        public static readonly string[] Directions={"Høyre","Ned-høyre","Ned","Ned-venstre","Venstre","Opp-venstre","Opp","Opp-høyre"};
    }
}
