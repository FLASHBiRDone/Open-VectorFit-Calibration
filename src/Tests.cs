using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace VectorFit {
    static class Tests {
        static int checks;static List<string> log;
        static void Check(bool ok,string what){checks++;if(!ok)throw new Exception("FAIL: "+what);log.Add("PASS: "+what);}
        static List<Trial> Synthetic(int count,bool biased,bool corrected) {
            var ts=new List<Trial>();
            for(int rep=0;rep<count;rep++)for(int d=0;d<8;d++){
                double angle=d*Math.PI/4,x=Math.Cos(angle)*15,y=Math.Sin(angle)*15;
                double gx=biased?(x<0?.92:1.10):1,gy=biased?(y<0?1.06:.94):1;
                ts.Add(new Trial{Direction=d,TargetX=x,TargetY=y,EndX=x*.90*gx,EndY=y*.90*gy,Valid=true,Corrected=corrected,TotalMs=440+(rep%3)*10,ClickError=Math.Sqrt(Math.Pow(x*.90*gx-x,2)+Math.Pow(y*.90*gy-y,2))/15});
            }return ts;
        }
        static List<Motion> Trace(double hz) {
            var ts=new List<Motion>();
            for(double t=0;t<=700;t+=1000/hz){
                double x=0;
                if(t>=160&&t<360){double u=(t-160)/200;x=13.5*(.5-.5*Math.Cos(u*Math.PI));}
                else if(t>=360&&t<450)x=13.5;
                else if(t>=450&&t<560)x=13.5+1.5*(t-450)/110;
                else if(t>=560)x=15;
                ts.Add(new Motion(t,x,0));
            }ts.Add(new Motion(700,15,0));return ts;
        }
        public static int Run(string path) {
            log=new List<string>();checks=0;
            try{
                Check(Math.Abs(new Setup{Dpi=800,Cm360=40}.DegreesPerCount-.028575)<1e-9,"DPI/cm360 conversion");
                Check(Math.Abs(GameMath.Distance(16000,1600)-25.4)<1e-9,"Blind distance converts counts using DPI");
                Check(Math.Abs(GameMath.Distance(-16000,1600)-25.4)<1e-9,"Blind distance accepts either horizontal direction");
                Check(Math.Abs(GameMath.Sensitivity(2,30,60)-1)<1e-9,"Longer target distance lowers game sensitivity");
                Check(Math.Abs(GameMath.HorizontalFov(90,1)-90)<1e-9,"Square aspect preserves vertical/horizontal FOV");
                Check(Math.Abs(GameMath.HorizontalFov(90,16.0/9)-121.2844929)<1e-6,"Vertical FOV conversion uses aspect ratio");
                bool invalidFov=false;try{GameMath.HorizontalFov(Double.NaN,1);}catch(ArgumentException){invalidFov=true;}Check(invalidFov,"Invalid FOV is rejected");
                var gameSetup=new Setup{Dpi=800,Cm360=55,Hfov=100};
                using(var setupDialog=new GameSetupForm(gameSetup,delegate{})){
                    Check(setupDialog.Dpi==800&&setupDialog.Cm360==55&&setupDialog.Hfov==100,"Optional game setup starts with existing values");
                    Check(gameSetup.Dpi==800&&gameSetup.Cm360==55,"Opening optional setup does not mutate measurements");
                    Check(setupDialog.CancelButton!=null&&setupDialog.CancelButton.DialogResult==System.Windows.Forms.DialogResult.Cancel,"Optional setup has a cancel/skip action");
                }
                Check(new Setup().Dpi==1600,"Default DPI is 1600");
                Check(!new Session().UsesInterception,"New calibrations default to Raw Accel without Interception");
                using(var global=new GlobalForm()) {
                    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                    Func<System.Windows.Forms.Control,IEnumerable<System.Windows.Forms.Control>> tree=null;
                    tree=control=>control.Controls.Cast<System.Windows.Forms.Control>().SelectMany(child=>new[]{child}.Concat(tree(child)));
                    Check(tree(global).OfType<System.Windows.Forms.Button>().Any(b=>b.Text=="Aktiver globalt"),"Global profile has explicit activation control");
                    typeof(GlobalForm).GetField("stop",flags).SetValue(global,false);
                    var stopButton=tree(global).OfType<System.Windows.Forms.Button>().Single(b=>b.Text=="Stopp");
                    typeof(System.Windows.Forms.Button).GetMethod("OnClick",flags).Invoke(stopButton,new object[]{EventArgs.Empty});
                    Check((bool)typeof(GlobalForm).GetField("stop",flags).GetValue(global),"Stop remains usable while global correction is active");
                    var sessionForGlobal=new Session{RawAccelAdaptiveValidation=false,InterceptionAdaptiveValidation=false,TrajectoryFitting=false,ConservativeSensitivity=false,Mode=ProfileMode.Interception,FitReady=true,ValidationPassed=true};
                    global.UseSession(sessionForGlobal);
                    Check(Object.ReferenceEquals(typeof(GlobalForm).GetField("session",flags).GetValue(global),sessionForGlobal),"Integrated global window reuses selected measurement");
                    global.UseSession(new Session{RawAccelAdaptiveValidation=false,InterceptionAdaptiveValidation=false,TrajectoryFitting=false,ConservativeSensitivity=false,FitReady=true,ValidationPassed=false});
                    Check(typeof(GlobalForm).GetField("session",flags).GetValue(global)==null,"Failed A/B cannot leave an old profile selected");
                    global.UseSession(new Session{RawAccelAdaptiveValidation=false,InterceptionAdaptiveValidation=false,TrajectoryFitting=false,ConservativeSensitivity=false,FitReady=true,ValidationPassed=true});
                    Check(typeof(GlobalForm).GetField("session",flags).GetValue(global)==null,"Raw Accel approval cannot activate an Interception filter");
                }
                var curves=new List<Trial>();
                for(int d=0;d<8;d++)for(int rep=0;rep<6;rep++) {
                    double a=d*Math.PI/4;var samples=new List<Motion>{new Motion(0,0,0)};
                    for(int i=1;i<=150;i++){double x=i*.1,y=.008*x*x;samples.Add(new Motion(100+i,x*Math.Cos(a)-y*Math.Sin(a),x*Math.Sin(a)+y*Math.Cos(a)));}
                    curves.Add(Analysis.Extract(d,15*Math.Cos(a),15*Math.Sin(a),samples,250,false));
                }
                var curveProfile=CurveFit.Learn(curves);
                Check(curveProfile.Safe()&&curveProfile.SupportedBins>0,"Repeated bends produce a bounded curve profile");
                var curveValidation=new Session{RawAccelAdaptiveValidation=false,InterceptionAdaptiveValidation=false,TrajectoryFitting=false,ConservativeSensitivity=false,RequireCloserClicks=false,Mode=ProfileMode.Interception,FitReady=true,Repeatable=true,Curve=curveProfile,Validation=new List<Trial>(curves)};
                foreach(var t in curves){var line=t.Samples.Select(p=>new Motion(p.T,t.EndX*p.T/250,t.EndY*p.T/250)).ToList();curveValidation.Validation.Add(Analysis.Extract(t.Direction,t.TargetX,t.TargetY,line,250,true));}
                Analysis.Validate(curveValidation);Check(curveValidation.ValidationPassed,"Reduced bend can pass A/B without changing endpoint accuracy or speed");
                curveValidation.RequireCloserClicks=true;Analysis.Validate(curveValidation);Check(!curveValidation.ValidationPassed,"New curve profiles cannot pass A/B without closer clicks");curveValidation.RequireCloserClicks=false;
                var rawCurveValidation=new Session{RawAccelAdaptiveValidation=false,InterceptionAdaptiveValidation=false,TrajectoryFitting=false,ConservativeSensitivity=false,FitReady=true,Repeatable=true,Curve=curveProfile,Validation=new List<Trial>(curveValidation.Validation)};
                Analysis.Validate(rawCurveValidation);Check(!rawCurveValidation.ValidationPassed,"Bend improvement alone cannot approve the Raw Accel profile");
                curveValidation.Validation=curves.Concat(curves.Select(t=>new Trial{Direction=t.Direction,TargetX=t.TargetX,TargetY=t.TargetY,EndX=t.EndX,EndY=t.EndY,TotalMs=t.TotalMs,ClickError=t.ClickError,Samples=t.Samples,Valid=true,Corrected=true})).ToList();
                Analysis.Validate(curveValidation);Check(!curveValidation.ValidationPassed,"Unchanged bend cannot pass curve A/B gate");
                var straightTrials=curves.Select(t=>new Trial{Valid=true,Samples=t.Samples.Select(p=>new Motion(p.T,p.X,0)).ToList()}).ToList();
                Check(CurveFit.Learn(straightTrials.Take(6)).SupportedBins==0,"Straight movements do not train curvature");
                var engine=new CurveEngine(curveProfile);double ox=0,oy=0;var source=curves[0].Samples;
                for(int i=1;i<source.Count;i++){var p=engine.Transform(source[i].X-source[i-1].X,source[i].Y-source[i-1].Y,source[i].T);ox+=p.X;oy+=p.Y;}
                Check(oy>0&&oy<source.Last().Y*.8,"Causal correction reduces synthetic bend without targeting information");
                var beforeIdle=engine.Transform(1,0,1000);Check(beforeIdle.X==1&&beforeIdle.Y==0,"Idle gap starts a new uncorrected stroke");
                var zero=engine.Transform(0,0,1001);Check(zero.X==0&&zero.Y==0,"No motion never generates correction movement");
                var gameSession=new Session{RawAccelAdaptiveValidation=false,InterceptionAdaptiveValidation=false,TrajectoryFitting=false,ConservativeSensitivity=false,Mode=ProfileMode.Interception,Setup=new Setup(),Curve=curveProfile,Correction=new Correction{Rotation=1}};
                var game=new MouseTransform(gameSession);var local=new CurveEngine(curveProfile);double expectedX=0,expectedY=0,actualX=0,actualY=0;
                for(int i=0;i<100;i++){
                    var stroke=new InterceptionApi.Stroke{X=4,Y=i/25,State=1,Wheel=120,Information=123};
                    var q=local.Transform(stroke.X*gameSession.Setup.DegreesPerCount,stroke.Y*gameSession.Setup.DegreesPerCount,100+i);
                    q=gameSession.Correction.Transform(q.X,q.Y);expectedX+=q.X/gameSession.Setup.DegreesPerCount;expectedY+=q.Y/gameSession.Setup.DegreesPerCount;
                    game.Apply(ref stroke,100+i,true);actualX+=stroke.X;actualY+=stroke.Y;
                    if(i==99)Check(stroke.State==1&&stroke.Wheel==120&&stroke.Information==123,"Global transform preserves buttons, wheel and metadata");
                }
                Check(Math.Abs(expectedX-actualX)<=.50001&&Math.Abs(expectedY-actualY)<=.50001,"Global output matches local correction with fractional count carry");
                var bypass=new InterceptionApi.Stroke{X=7,Y=3,Flags=1};game.Apply(ref bypass,400,true);Check(bypass.X==7&&bypass.Y==3,"Absolute input passes unchanged");
                bypass.Flags=0;game.Apply(ref bypass,401,false);Check(bypass.X==7&&bypass.Y==3,"Stopped filter passes input unchanged");
                Check(System.Runtime.InteropServices.Marshal.SizeOf(typeof(InterceptionApi.Stroke))==20&&System.Runtime.InteropServices.Marshal.OffsetOf(typeof(InterceptionApi.Stroke),"X").ToInt32()==8,"Interception native packet layout matches API");
                using(var rawAim=new AimForm(new Session{RawAccelAdaptiveValidation=false,InterceptionAdaptiveValidation=false,TrajectoryFitting=false,ConservativeSensitivity=false,Curve=curveProfile},true)) {
                    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                    var rawEngine=(CurveEngine)typeof(AimForm).GetField("curve",flags).GetValue(rawAim);
                    double rx=0,ry=0;
                    for(int i=1;i<source.Count;i++){var p=rawEngine.Transform(source[i].X-source[i-1].X,source[i].Y-source[i-1].Y,source[i].T);rx+=p.X;ry+=p.Y;}
                    Check(Math.Abs(rx-source.Last().X)<1e-9&&Math.Abs(ry-source.Last().Y)<1e-9,"Raw Accel A/B leaves measured bends unchanged even when a curve model exists");
                }
                using(var curveAim=new AimForm(gameSession,true)) {
                    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                    var selectedEngine=(CurveEngine)typeof(AimForm).GetField("curve",flags).GetValue(curveAim);
                    double ry=0;
                    for(int i=1;i<source.Count;i++)ry+=selectedEngine.Transform(source[i].X-source[i-1].X,source[i].Y-source[i-1].Y,source[i].T).Y;
                    Check(Math.Abs(ry-oy)<1e-9,"Interception A/B still uses the learned curvature model");
                }
                using(var form=new AimForm(new Setup(),new Correction(),true)) {
                    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;var type=typeof(AimForm);
                    var targets=((System.Collections.IEnumerable)type.GetField("queue",flags).GetValue(form)).Cast<object>().ToList();
                    Func<object,bool> corrected=o=>(bool)o.GetType().GetField("Corrected").GetValue(o);
                    Func<object,string> geometry=o=>((int)o.GetType().GetField("Direction").GetValue(o))+":"+((double)o.GetType().GetField("Radius").GetValue(o)).ToString("R",CultureInfo.InvariantCulture)+":"+((double)o.GetType().GetField("Angle").GetValue(o)).ToString("R",CultureInfo.InvariantCulture);
                    Check(targets.Count==64&&targets.Where(corrected).Select(geometry).OrderBy(v=>v).SequenceEqual(targets.Where(t=>!corrected(t)).Select(geometry).OrderBy(v=>v)),"Random A/B target angles and distances remain exactly matched");
                    Check(targets.All(t=>{double radius=(double)t.GetType().GetField("Radius").GetValue(t);return radius>=8&&radius<=18;}),"Random distances stay within configured range");
                    type.GetMethod("ClickRaw",flags).Invoke(form,null);
                    var clock=(System.Diagnostics.Stopwatch)type.GetField("clock",flags).GetValue(form);
                    double remaining=(double)type.GetField("deadline",flags).GetValue(form)-clock.Elapsed.TotalMilliseconds;
                    Check(remaining>=140&&remaining<=900,"Next target delay is bounded and does not include old feedback pause");
                    type.GetField("deadline",flags).SetValue(form,0.0);type.GetMethod("Tick",flags).Invoke(form,new object[]{null,EventArgs.Empty});
                    type.GetField("t0",flags).SetValue(form,-4000.0);type.GetMethod("Tick",flags).Invoke(form,new object[]{null,EventArgs.Empty});
                    Check(form.Results.Count==1&&!form.Results[0].Valid&&(string)type.GetField("phase",flags).GetValue(form)=="countdown","Timeout records failure and automatically advances");
                    type.GetMethod("Pause",flags).Invoke(form,null);
                    Check((string)type.GetField("phase",flags).GetValue(form)=="paused"&&type.GetField("current",flags).GetValue(form)==null,"Pause requeues interrupted target and stops automatic advancement");
                }
                foreach(double hz in new[]{125.0,1000.0,8000.0}) {
                    var t=Analysis.Extract(0,15,0,Trace(hz),700,false);
                    Check(t.Valid,"Click recorded at "+hz+" Hz");
                    Check(Math.Abs(t.EndX-15)<.001,"Exact click endpoint used at "+hz+" Hz");
                    Check(t.Samples.Count==Trace(hz).Count,"Full movement trace preserved");
                }
                var early=Trace(1000).Select(p=>new Motion(Math.Max(0,p.T-150),p.X,p.Y)).ToList();
                Check(Analysis.Extract(0,15,0,early,550,false).Valid,"Early click movement retained");
                Check(!Analysis.Extract(0,15,0,new List<Motion>{new Motion(0,0,0)},700,false).Valid,"Missing motion rejected");
                foreach(var endpoint in new[]{new PointD(3,0),new PointD(28,0),new PointD(12,10),new PointD(-5,2)}) {
                    var trace=new List<Motion>{new Motion(0,0,0),new Motion(150,0,0),new Motion(180,endpoint.X*.5,endpoint.Y*.5),new Motion(210,endpoint.X,endpoint.Y)};
                    var miss=Analysis.Extract(0,15,0,trace,210,false);
                    Check(miss.Valid,"Miss retained: "+endpoint.X+","+endpoint.Y);
                    Check(miss.EndX==endpoint.X&&miss.EndY==endpoint.Y&&miss.ErrorX==endpoint.X-15&&miss.ErrorY==endpoint.Y,"Signed miss and click endpoint preserved");
                    Check(miss.Samples.Count==trace.Count&&miss.Samples.Last().T==210,"Curve preserved through click without stopping");
                }
                var stationary=Analysis.Extract(0,15,0,new List<Motion>{new Motion(0,0,0),new Motion(100,0,0)},100,false);
                Check(!stationary.Valid&&stationary.ClickError==1&&stationary.Samples.Count==2,"Stationary click retained with error but excluded from fitting");
                var curved=new List<Motion>{new Motion(0,0,0),new Motion(100,6,4),new Motion(180,18,2),new Motion(230,12,-3)};
                var click=Analysis.Extract(0,15,0,curved,230,false);
                Check(click.Valid&&click.EndX==12&&click.EndY==-3&&click.Samples[1].Y==4,"Curved overshoot and return preserved; fit uses final click");
                foreach(double x in new[]{3.0,28.0,0.0}) {
                    using(var form=new AimForm(new Setup(),new Correction(),false)) {
                        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                        var type=typeof(AimForm);
                        type.GetMethod("ClickRaw",flags).Invoke(form,null);
                        var target=type.GetField("current",flags).GetValue(form);
                        target.GetType().GetField("Warmup").SetValue(target,false);
                        type.GetField("cameraX",flags).SetValue(form,x);
                        type.GetField("motion",flags).SetValue(form,new List<Motion>{new Motion(0,0,0),new Motion(1,x,0)});
                        type.GetMethod("Finish",flags).Invoke(form,new object[]{true,true});
                        Check(form.Results.Count==1&&form.Results[0].EndX==x,"Window saves missed or stationary click");
                        Check((int)type.GetField("finished",flags).GetValue(form)==1,"Window counts click independently of target hit or fitting eligibility");
                        Check((string)type.GetField("phase",flags).GetValue(form)=="countdown"&&type.GetField("current",flags).GetValue(form)!=null,"Click automatically queues next target without confirmation");
                        var nextTarget=type.GetField("current",flags).GetValue(form);
                        type.GetMethod("ClickRaw",flags).Invoke(form,null);
                        Check(Object.ReferenceEquals(nextTarget,type.GetField("current",flags).GetValue(form))&&form.Results.Count==1,"Early extra click cannot skip target or create a measurement");
                        type.GetField("deadline",flags).SetValue(form,0.0);
                        type.GetMethod("Tick",flags).Invoke(form,new object[]{null,EventArgs.Empty});
                        Check((string)type.GetField("phase",flags).GetValue(form)=="flick","Timer reveals next target without another click");
                    }
                }
                var neutral=new Session{RawAccelAdaptiveValidation=false,InterceptionAdaptiveValidation=false,TrajectoryFitting=false,ConservativeSensitivity=false,Baseline=Synthetic(10,false,false)};Analysis.Compute(neutral);
                Check(neutral.FitReady&&neutral.Repeatable,"Stable baseline eligible for A/B");
                Check(Math.Abs(neutral.Correction.Right-1)<.001&&Math.Abs(neutral.Correction.Rotation)<.001,"Uniform undershoot does not introduce directional distortion");
                var biased=new Session{RawAccelAdaptiveValidation=false,InterceptionAdaptiveValidation=false,TrajectoryFitting=false,ConservativeSensitivity=false,Baseline=Synthetic(10,true,false)};Analysis.Compute(biased);
                Check(biased.Correction.Right<1&&biased.Correction.Left>1&&biased.Correction.Up<1&&biased.Correction.Down>1,"Correction counters all four directional biases");
                Check(biased.Correction.Safe(),"Correction limited to conservative bounds");
                using(var main=new MainForm()) {
                    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                    var preview=new Session{RawAccelAdaptiveValidation=false,InterceptionAdaptiveValidation=false,TrajectoryFitting=false,ConservativeSensitivity=false,Baseline=Synthetic(10,true,false),FitReady=true,Repeatable=false};
                    typeof(MainForm).GetField("session",flags).SetValue(main,preview);
                    typeof(MainForm).GetMethod("ShowPage",flags).Invoke(main,new object[]{1});
                    Func<System.Windows.Forms.Control,IEnumerable<System.Windows.Forms.Control>> descendants=null;
                    descendants=control=>control.Controls.Cast<System.Windows.Forms.Control>().SelectMany(child=>new[]{child}.Concat(descendants(child)));
                    var button=descendants(main).OfType<System.Windows.Forms.Button>().Single(b=>b.Text.StartsWith("Start A/B"));
                    Check(button.Enabled,"Computed but unstable profile can run local A/B preview");
                    preview.Validation=Synthetic(4,true,false).Concat(Synthetic(4,false,true)).ToList();
                    Analysis.Validate(preview);
                    Check(!preview.ValidationPassed&&preview.CanTryRawAccel,"Unstable calibration can be tried without claiming a validated improvement");
                    preview.FitReady=false;
                    typeof(MainForm).GetMethod("ShowPage",flags).Invoke(main,new object[]{1});
                    button=descendants(main).OfType<System.Windows.Forms.Button>().Single(b=>b.Text.StartsWith("Start A/B")||b.Text.StartsWith("Bekreft samme"));
                    Check(!button.Enabled,"Missing computed profile still blocks A/B preview");
                    typeof(MainForm).GetMethod("ShowPage",flags).Invoke(main,new object[]{2});
                    var selector=descendants(main).OfType<System.Windows.Forms.ComboBox>().Single(b=>(string)b.Tag!="language");
                    Check(selector.SelectedIndex==0&&descendants(main).OfType<System.Windows.Forms.Button>().Any(b=>b.Text=="Aktiver med Raw Accel"),"Global page defaults to the direct Raw Accel route");
                    Check(descendants(main).OfType<System.Windows.Forms.Button>().Any(b=>b.Text=="Sett opp Raw Accel"),"Missing driver has an explicit setup route");
                    selector.SelectedIndex=1;
                    Check(preview.UsesInterception&&descendants(main).OfType<System.Windows.Forms.Button>().Any(b=>b.Text=="Åpne Interception-valg"),"UI explicitly selects the separate Interception route");
                    descendants(main).OfType<System.Windows.Forms.ComboBox>().Single(b=>(string)b.Tag!="language").SelectedIndex=0;
                    Check(!preview.UsesInterception&&descendants(main).OfType<System.Windows.Forms.Button>().Any(b=>b.Text=="Eksporter Raw Accel-profil"),"UI can return to the standard export without a native Interception component");
                }
                double product=biased.Correction.Right*biased.Correction.Left*biased.Correction.Up*biased.Correction.Down;
                Check(Math.Abs(product-1)<.002,"Global geometric sensitivity preserved before clipping");
                var missing=new Session{RawAccelAdaptiveValidation=false,InterceptionAdaptiveValidation=false,TrajectoryFitting=false,ConservativeSensitivity=false,Baseline=Synthetic(10,true,false).Where(t=>t.Direction!=7).ToList()};Analysis.Compute(missing);
                Check(!missing.FitReady,"Incomplete directions cannot produce eligible profile");
                var nonfinite=new Correction{Right=Double.NaN};Check(!nonfinite.Safe(),"Non-finite gain rejected");
                biased.Validation=Synthetic(4,true,false).Concat(Synthetic(4,false,true)).ToList();
                Analysis.Validate(biased);Check(biased.ValidationPassed,"Balanced A/B improvement passes");
                var rawResults=biased.Validation;
                biased.SelectMode(ProfileMode.Interception);
                Check(biased.Validation.Count==0&&!biased.ValidationPassed,"Changing to Interception requires its own A/B test");
                Check(Object.ReferenceEquals(biased.RawAccelValidation,rawResults),"Changing profile type preserves the Raw Accel A/B results");
                biased.Validation=Synthetic(4,true,false).Concat(Synthetic(4,false,true)).ToList();Analysis.Validate(biased);
                Check(biased.ReadyForInterception&&!biased.ReadyForRawAccel,"Approved Interception profile is not eligible for Raw Accel export");
                bool rejected=false;try{Driver.Export(biased,"must-not-export-interception");}catch{rejected=true;}
                Check(rejected&&!File.Exists(Path.Combine(Driver.Data,"must-not-export-interception.rawaccel.json")),"Exporter rejects a curve profile before writing a misleading Raw Accel file");
                var curveResults=biased.Validation;biased.SelectMode(ProfileMode.RawAccel);
                Check(biased.ReadyForRawAccel&&Object.ReferenceEquals(biased.Validation,rawResults)&&Object.ReferenceEquals(biased.InterceptionValidation,curveResults),"Switching back restores the separately tested Raw Accel profile");
                var saved=JObject.FromObject(biased);var roundtrip=Session.FromJson(saved.ToString());
                Check(roundtrip.ReadyForRawAccel&&roundtrip.RawAccelValidation.Count==64&&roundtrip.InterceptionValidation.Count==64,"Both A/B histories survive saving and loading");
                Check(saved["Validation"]==null&&(string)saved["Mode"]=="RawAccel","Saved data explicitly identifies the profile type and separate A/B histories");
                var legacy=(JObject)saved.DeepClone();legacy["Version"]="0.3.0";legacy["Validation"]=legacy["InterceptionValidation"].DeepClone();
                legacy.Remove("Mode");legacy.Remove("RawAccelValidation");legacy.Remove("InterceptionValidation");
                var migrated=Session.FromJson(legacy.ToString());
                Check(migrated.Version=="0.13.0"&&!migrated.UsesInterception&&!migrated.ValidationPassed&&migrated.Validation.Count==0,"Legacy 0.3 curves never silently approve a standard profile");
                Check(migrated.InterceptionValidation.Count==64&&migrated.Baseline.Count==80,"Legacy measurements and curve A/B results remain available");
                migrated.SelectMode(ProfileMode.Interception);Check(migrated.ReadyForInterception,"Legacy curve A/B can be revisited only in the matching profile type");
                legacy["Version"]="0.2.0";migrated=Session.FromJson(legacy.ToString());
                Check(migrated.RawAccelValidation.Count==0&&migrated.InterceptionValidation.Count==0,"Legacy 0.2 requires fresh validation as before");
                saved["ValidationPassed"]=true;saved["RawAccelValidation"]=new JArray();
                Check(!Session.FromJson(saved.ToString()).ValidationPassed,"Saved approval flags cannot bypass a missing A/B test");
                saved["Mode"]=999;rejected=false;try{Session.FromJson(saved.ToString());}catch{rejected=true;}
                Check(rejected,"Unknown profile types cannot fall through to a global backend");
                var approvedFile=Driver.Export(biased,"standard-profile-test");
                Check(JToken.DeepEquals(JObject.Parse(File.ReadAllText(approvedFile)),Driver.Config(biased.Correction)),"Approved standard export contains exactly the tested Raw Accel transformation");
                biased.Validation.First(t=>t.Corrected).TargetX*=1.2;
                Analysis.Validate(biased);Check(!biased.ValidationPassed,"Unmatched A/B target distances cannot claim improvement");
                biased.Validation=Synthetic(4,false,false).Concat(Synthetic(4,false,true)).ToList();
                Analysis.Validate(biased);Check(!biased.ValidationPassed,"No measurable directional bias does not claim improvement");
                biased.Validation=Synthetic(4,true,false).Concat(Synthetic(4,false,true).Select(t=>{t.TotalMs=700;return t;})).ToList();
                Analysis.Validate(biased);Check(!biased.ValidationPassed,"Slower A/B result cannot activate");
                bool manualRequired=false;try{Driver.Export(biased,"unconfirmed-default");}catch{manualRequired=true;}
                Check(manualRequired,"Unconfirmed profile is not exported without an explicit trial choice");
                var trialFile=Driver.Export(biased,"unconfirmed-trial",true);
                Check(File.Exists(trialFile)&&!biased.ValidationPassed,"Explicit trial export preserves the unsuccessful A/B result");
                biased.Correction.Right=5;manualRequired=false;try{Driver.Export(biased,"unsafe-trial",true);}catch{manualRequired=true;}
                Check(manualRequired,"Manual trial cannot exceed the bounded correction limits");
                var first=new Session{RawAccelAdaptiveValidation=false,InterceptionAdaptiveValidation=false,TrajectoryFitting=false,ConservativeSensitivity=false,Baseline=Synthetic(10,true,false)};Analysis.Compute(first);
                var next=first.NextRound("previous.vectorfit.json");
                Check(next.Round==2&&next.ParentMeasurement=="previous.vectorfit.json"&&next.Validation.Count==0&&next.Baseline.Count==0,"A new round retains its parent but starts with fresh measurement and A/B data");
                Check(!Object.ReferenceEquals(first.Correction,next.InputCorrection)&&next.InputCorrection.Right==first.Correction.Right,"Round input profile is a copy of the applied candidate");
                var retained=first.NextRound("previous.vectorfit.json",false);
                Check(retained.InputCorrection.Right==1&&retained.InputCorrection.Rotation==0,"Keeping the measured profile does not silently adopt a worse candidate");
                Func<Correction,List<Trial>> measuredWith=corr=>Synthetic(10,true,false).Select(t=>{var p=corr.Transform(t.EndX,t.EndY);t.EndX=p.X;t.EndY=p.Y;return t;}).ToList();
                next.Baseline=measuredWith(next.InputCorrection);double observed=next.Baseline[0].EndX;Analysis.Compute(next);
                var targetFit=Analysis.Fit(first.Baseline,Analysis.Median(first.Baseline.Select(t=>t.Reach)));
                Func<Correction,double> gap=corr=>Math.Abs(corr.Right-targetFit.Right)+Math.Abs(corr.Left-targetFit.Left)+Math.Abs(corr.Up-targetFit.Up)+Math.Abs(corr.Down-targetFit.Down);
                Check(gap(next.Correction)<gap(first.Correction)&&next.Correction.Safe(),"Second measured round approaches the fitted profile without doubling the applied correction");
                Check(next.Baseline[0].EndX==observed,"Refinement preserves the actual observed endpoints");
                var nextSaved=Session.FromJson(JObject.FromObject(next).ToString());
                Check(nextSaved.Round==2&&nextSaved.ParentMeasurement==next.ParentMeasurement&&Math.Abs(nextSaved.Correction.Right-next.Correction.Right)<1e-10,"Saved round reconstructs the same proposal with its input profile");
                var oldFormat=JObject.FromObject(first);oldFormat["Version"]="0.4.0";oldFormat.Remove("InputCorrection");oldFormat.Remove("Round");oldFormat.Remove("ParentMeasurement");
                var oldLoaded=Session.FromJson(oldFormat.ToString());Check(oldLoaded.Round==1&&oldLoaded.InputCorrection.Right==1,"Version 0.4 measurements start from the original neutral input profile");
                var roundProfile=next;
                for(int n=0;n<20;n++){var later=roundProfile.NextRound("round.vectorfit.json");later.Baseline=measuredWith(later.InputCorrection);Analysis.Compute(later);roundProfile=later;}
                Check(roundProfile.Correction.Safe()&&gap(roundProfile.Correction)<=gap(next.Correction)+1e-9,"Repeated simulated rounds stay within absolute limits and approach the stable fit");
                var changed=next.NextRound("round.vectorfit.json");changed.Baseline=Synthetic(10,false,false).Select(t=>{var p=changed.InputCorrection.Transform(t.EndX,t.EndY);t.EndX=p.X;t.EndY=p.Y;return t;}).ToList();Analysis.Compute(changed);
                Check(Math.Abs(changed.Correction.Right-1)<Math.Abs(changed.InputCorrection.Right-1),"A changed movement pattern can reduce an earlier correction instead of accumulating it");
                var inverseCorrection=new Correction{Right=.94,Left=1.06,Up=.97,Down=1.03,Rotation=2};
                var movements=new[]{new PointD(8,3),new PointD(-9,2),new PointD(5,-7),new PointD(-1,4)};
                double physicalX=0,physicalY=0,measuredX=0,measuredY=0;var measuredTrace=new List<Motion>{new Motion(0,0,0)};
                foreach(var movement in movements){var transformed=inverseCorrection.Transform(movement.X,movement.Y);var original=inverseCorrection.Inverse(transformed.X,transformed.Y);Check(Math.Abs(original.X-movement.X)<1e-10&&Math.Abs(original.Y-movement.Y)<1e-10,"Directional transform can be inverted in every quadrant");physicalX+=movement.X;physicalY+=movement.Y;measuredX+=transformed.X;measuredY+=transformed.Y;measuredTrace.Add(new Motion(measuredTrace.Count,measuredX,measuredY));}
                var restored=Analysis.UndoInputCorrection(new Trial{Valid=true,TargetX=15,EndX=measuredX,EndY=measuredY,Samples=measuredTrace},inverseCorrection);
                Check(Math.Abs(restored.EndX-physicalX)<1e-10&&Math.Abs(restored.EndY-physicalY)<1e-10,"Round fitting reverses each movement even when the path crosses direction boundaries");
                using(var aim=new AimForm(next,true)){
                    var transform=typeof(AimForm).GetMethod("TransformMovement",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                    var input=next.InputCorrection.Transform(4,-2);
                    var a=(PointD)transform.Invoke(aim,new object[]{input.X,input.Y,100.0,false});var b=(PointD)transform.Invoke(aim,new object[]{input.X,input.Y,100.0,true});var expected=next.Correction.Transform(4,-2);
                    Check(a.X==input.X&&a.Y==input.Y&&Math.Abs(b.X-expected.X)<1e-10&&Math.Abs(b.Y-expected.Y)<1e-10,"Round A/B compares the existing profile against the absolute new candidate exactly once");
                }
                using(var integrated=new AimForm(new Session{RawAccelAdaptiveValidation=false,InterceptionAdaptiveValidation=false,TrajectoryFitting=false,ConservativeSensitivity=false,AdaptiveCalibration=false},false,true)){
                    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;var type=typeof(AimForm);
                    integrated.Results=Synthetic(10,true,false);var targets=type.GetField("queue",flags).GetValue(integrated);targets.GetType().GetMethod("Clear").Invoke(targets,null);type.GetField("finished",flags).SetValue(integrated,80);
                    type.GetMethod("BeginNext",flags).Invoke(integrated,null);
                    Check(integrated.ValidationStarted&&integrated.CalibrationResults.Count==80&&integrated.Results.Count==0,"Integrated calibration retains all 80 measurements when A/B begins");
                    Check((int)type.GetField("goal",flags).GetValue(integrated)==64&&(string)type.GetField("phase",flags).GetValue(integrated)=="countdown"&&type.GetField("current",flags).GetValue(integrated)!=null,"Integrated A/B starts automatically without a menu, confirmation, or extra start click");
                    Check(first.Validation.Count==0,"In-window A/B preparation does not overwrite the saved measurement before the driver check");
                }
                string matching=Driver.Config(next.InputCorrection).ToString();
                Check(Driver.MatchesCorrection(matching,next.InputCorrection),"Next round recognizes its exact input configuration");
                Check(!Driver.MatchesCorrection(matching,new Correction()),"A round cannot accidentally measure a different active profile");
                var wrongDriver=JObject.Parse(matching);wrongDriver["profiles"][0]["Degrees of angle snapping"]=5;
                Check(!Driver.MatchesCorrection(wrongDriver.ToString(),next.InputCorrection),"Extra driver transformations block an incorrectly based round");
                NewFeatures(path);
                TargetDistanceGoal();
                CircleAndAdaptiveFeatures(path);
                PrecisionFeatures(path);
                MedianCircleFeatures(path);
                NaturalCircleFeatures(path);
                DualTaskFeatures();
                ConfirmationFeatures();
                Check(DriverSetup.RawUrl.StartsWith("https://github.com/RawAccelOfficial/")&&DriverSetup.InterceptionUrl.StartsWith("https://github.com/oblitum/"),"Installer sources pinned to official repositories");
                var setupStart=DriverSetup.StartInfo(@"C:verified\installer.exe",false,false);
                Check(setupStart.Verb=="runas"&&setupStart.UseShellExecute&&setupStart.Arguments=="","Raw installer requires Windows elevation and has no silent flags");
                Check(DriverSetup.StartInfo("installer.exe",true,true).Arguments=="/uninstall","Interception removal is explicit");
                Check(DriverSetup.StartInfo("installer.exe",true,false).Arguments=="/install","Interception installation uses official switch");
                foreach(bool optionalDriver in new[]{false,true}){
                    string archive=Path.Combine(Driver.Root,optionalDriver?"interception-setup.zip":"rawaccel-setup.zip");
                    if(File.Exists(archive)){
                        string unpack=Path.Combine(Driver.Root,"test-package-"+Guid.NewGuid().ToString("N"));
                        string executable=DriverSetup.Extract(archive,unpack,optionalDriver);
                        Check(File.Exists(executable),"Verified official package extracts without executing installer");
                        Check(File.Exists(Path.Combine(Path.GetDirectoryName(executable),optionalDriver?"install-interception.exe":"installer.exe")),"Installer path exists in pinned archive");
                    }
                }
                var badZip=Path.Combine(Driver.Data,"test-invalid-installer.zip");Directory.CreateDirectory(Driver.Data);File.WriteAllText(badZip,"invalid");
                bool setupRejected=false;try{DriverSetup.Extract(badZip,Path.Combine(Driver.Data,"rejected-setup"),false);}catch{setupRejected=true;}
                Check(setupRejected&&!Directory.Exists(Path.Combine(Driver.Data,"rejected-setup")),"Invalid installer rejected before extraction or execution");
                File.Delete(badZip);
                using(var setupForm=new DriverSetupForm(false))Check(setupForm.Text.Contains("Raw Accel"),"Raw setup can open without installing");
                using(var setupForm=new DriverSetupForm(true))Check(setupForm.Text.Contains("Interception"),"Optional setup can open without installing");

                var c=new Correction{Right=.96,Left=1.04,Down=1.025,Up=.98,Rotation=1.5,Sensitivity=.85};
                string tmp=Path.Combine(Path.GetDirectoryName(path),"test-profile.json");File.WriteAllText(tmp,Driver.Config(c).ToString());
                var validated=JObject.Parse(Driver.Run("validate",tmp));Check((string)validated["profiles"][0]["name"]=="VectorFit","Official runtime accepts generated profile");
                var lines=Driver.Run("simulate",tmp).Trim().Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries);
                foreach(string line in lines){var halves=line.Split(':');var input=halves[0].Split(',');var output=halves[1].Split(',');
                    var expected=c.Transform(Double.Parse(input[0],CultureInfo.InvariantCulture),Double.Parse(input[1],CultureInfo.InvariantCulture));
                    Check(Math.Abs(expected.X-Double.Parse(output[0],CultureInfo.InvariantCulture))<1e-7&&Math.Abs(expected.Y-Double.Parse(output[1],CultureInfo.InvariantCulture))<1e-7,"Local A/B transform matches official runtime: "+halves[0]);
                }
                Check(Driver.Neutral(Driver.Config(new Correction()).ToString()),"Neutral configuration recognized");
                Check(!Driver.Neutral(Driver.Config(c).ToString()),"Active profile blocks double calibration");
                var unicode=Driver.Config(c);unicode["profiles"][0]["name"]="ÆØÅ G703";File.WriteAllText(tmp,unicode.ToString());
                Check((string)JObject.Parse(Driver.Run("validate",tmp))["profiles"][0]["name"]=="ÆØÅ G703","Unicode profile names preserved across bridge");
                log.Add("\n"+checks+" checks passed. No driver installed or settings activated by these tests.");File.WriteAllLines(path,log);return 0;
            }catch(Exception e){log.Add(e.ToString());File.WriteAllLines(path,log);return 1;}
        }
        static void ConfirmationFeatures(){
            var original=new Session{DualTask=true,Baseline=Synthetic(10,true,false)};Analysis.Compute(original);original.Validation=PrecisionPairs(16,1.15,1.02);
            string before=JObject.FromObject(original).ToString(),key=ResultStory.Key(original);var repeat=original.Confirmation("original.json");
            Check(JObject.FromObject(original).ToString()==before&&repeat.FrozenProposal&&repeat.FrozenKey==key&&repeat.Round==original.Round,"Confirmation freezes the same proposal without modifying or advancing the original phase");
            Check(repeat.Validation.Count==0&&repeat.PrecisionValidation.Count==0&&repeat.PriorComparisons.Count==1&&!repeat.ValidationPassed,"Confirmation starts fresh observations while preserving the earlier result");
            repeat.Baseline.Clear();var loaded=Session.FromJson(JObject.FromObject(repeat).ToString());
            Check(ResultStory.Key(loaded)==key&&loaded.FitReady&&loaded.Correction.Right==original.Correction.Right,"Reopening confirmation does not refit even if calibration samples differ");
            var tampered=JObject.FromObject(repeat);tampered["Correction"]["Sensitivity"]=.9;bool rejected=false;try{Session.FromJson(tampered.ToString());}catch{rejected=true;}Check(rejected,"Changed frozen configuration is rejected rather than mislabeled a repeat");
            var second=repeat.Confirmation("partial.json");Check(second.PriorComparisons.Count==1&&second.FrozenKey==key,"Retry before any new observations does not duplicate evidence");
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            original.TestInputCorrection=original.Correction;
            using(var aim=new AimForm(original,true)){
                var physical=new PointD(4,-3);var hardware=original.Correction.Transform(physical.X,physical.Y);
                var a=(PointD)typeof(AimForm).GetMethod("TransformMovement",flags).Invoke(aim,new object[]{hardware.X,hardware.Y,100.0,false});
                var b=(PointD)typeof(AimForm).GetMethod("TransformMovement",flags).Invoke(aim,new object[]{hardware.X,hardware.Y,100.0,true});var expected=original.InputCorrection.Transform(physical.X,physical.Y);
                Check(Math.Abs(a.X-expected.X)<1e-9&&Math.Abs(a.Y-expected.Y)<1e-9&&Math.Abs(b.X-hardware.X)<1e-9&&Math.Abs(b.Y-hardware.Y)<1e-9,"Confirmation can compare unchanged A and B when B is already global without another driver activation");
            }
            Check(ResultStory.Headline(repeat).Contains("klar for bekreftelsesrunde"),"Empty confirmation explains its next step without claiming success");
            Check(!ResultStory.Promising(new ComparisonRecord{Complete=false,ErrorA=10,ErrorB=1,TimeA=400,TimeB=300}),"Incomplete observations cannot become a promising-result claim");
            Check(ResultStory.Trend(10,8,true)=="mindre avvik"&&ResultStory.Trend(400,460,false)=="langsommere","Narrative changes separately for accuracy and speed");
            foreach(var file in Directory.GetFiles(Driver.Data,"*v12.vectorfit.json")){
                var session=Session.FromJson(File.ReadAllText(file));if(session.Validation.Count==0)continue;
                var r=ResultStory.Record(session);Check(r.Complete&&!r.Passed&&ResultStory.Headline(session).Contains("ikke bekreftet"),"Real 0.12 results remain promising but unconfirmed in the new explanation");
                var frozen=session.Confirmation(Path.GetFileName(file));var roundtrip=Session.FromJson(JObject.FromObject(frozen).ToString());Check(ResultStory.Key(roundtrip)==ResultStory.Key(session),"Real measured profile and candidate survive confirmation save/reload exactly");
            }
        }
        public static int Audit(string folder,string output){
            var rows=new JArray();string error;string active=Driver.TryRead(out error);
            foreach(var file in Directory.GetFiles(folder,"*.vectorfit.json").OrderBy(f=>f)){
                var session=Session.FromJson(File.ReadAllText(file));if(session.Circles.Laps.Count==0)continue;
                var median=CircleAnalysis.Median(session.Circles,session.InputCorrection);var cw=CircleAnalysis.Median(session.Circles,session.InputCorrection,null,true);var ccw=CircleAnalysis.Median(session.Circles,session.InputCorrection,null,false);
                var candidate=CircleAnalysis.Propose(session.Circles,session.InputCorrection,session.InputCorrection);
                var trialData=JObject.FromObject(session.Circles).ToObject<CircleData>();if(trialData.Protocol==2)trialData.Protocol=3;var trend=CircleAnalysis.Median(trialData,session.InputCorrection);var trendCandidate=CircleAnalysis.Propose(trialData,session.InputCorrection,session.InputCorrection);
                rows.Add(JObject.FromObject(new{File=Path.GetFileName(file),session.Round,Protocol=session.Circles.Protocol,Count=session.Circles.Laps.Count(session.Circles.Counts),median.Report.Stable,median.Report.AxisRatio,median.Report.Roundness,median.Spread,median.DirectionGap,median.TimeGap,TrendStable=trend.Report.Stable,TrendStrength=trend.TrendStrength,TrendCandidate=trendCandidate,ClockwiseRatio=cw.Report.AxisRatio,CounterclockwiseRatio=ccw.Report.AxisRatio,ClockwiseAngle=cw.Ellipse.Angle*180/Math.PI,CounterclockwiseAngle=ccw.Ellipse.Angle*180/Math.PI,median.Report.Note,session.CircleNote,Input=session.InputCorrection,CircleOnlyCandidate=candidate,NextCandidate=session.Correction,ActiveMatchesInput=active!=null&&Driver.MatchesCorrection(active,session.InputCorrection),ActiveMatchesCandidate=active!=null&&Driver.MatchesCorrection(active,session.Correction)}));
            }
            File.WriteAllText(output,new JObject(new JProperty("DriverReadable",active!=null),new JProperty("DriverError",error),new JProperty("Measurements",rows)).ToString());return 0;
        }
        static void DualTaskFeatures(){
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;var type=typeof(AimForm);
            Action<AimForm,string,object> set=(f,n,v)=>type.GetField(n,flags).SetValue(f,v);
            Func<AimForm,string,object> get=(f,n)=>type.GetField(n,flags).GetValue(f);
            Action<AimForm,string> call=(f,n)=>type.GetMethod(n,flags).Invoke(f,null);
            Action<AimForm> clear=f=>get(f,"queue").GetType().GetMethod("Clear").Invoke(get(f,"queue"),null);
            Action<AimForm,bool> shoot=(f,hit)=>{
                if((string)get(f,"phase")=="ready")call(f,"ClickRaw");
                if((string)get(f,"phase")=="countdown"){set(f,"deadline",0.0);type.GetMethod("Tick",flags).Invoke(f,new object[]{null,EventArgs.Empty});}
                var target=get(f,"current");var tt=target.GetType();double a=(double)tt.GetField("Angle").GetValue(target),r=(double)tt.GetField("Radius").GetValue(target);
                double x=hit?Math.Cos(a)*r:0,y=hit?Math.Sin(a)*r:0;
                var clock=(System.Diagnostics.Stopwatch)get(f,"clock");set(f,"t0",clock.Elapsed.TotalMilliseconds-400-100*((List<AimClick>)get(f,"clicks")).Count);set(f,"phase","flick");set(f,"cameraX",x);set(f,"cameraY",y);
                set(f,"motion",new List<Motion>{new Motion(0,0,0),new Motion(100,x*.2,y*.2),new Motion(400,x,y)});call(f,"ClickRaw");
            };
            Func<int,Session> make=round=>new Session{Round=round,DualTask=true,GuidedCalibration=true,Baseline=Enumerable.Range(0,48).Select(i=>PrecisionTrial(i%8,1.08,1.08,false,0)).ToList()};
            using(var aim=new AimForm(make(1),false,true,true)){
                Check(!(bool)get(aim,"autoBegin")&&(string)get(aim,"phase")=="ready","Dual-task entry requires a deliberate click even after circles");
                aim.Results=make(1).Baseline;set(aim,"finished",48);set(aim,"warmupFinished",8);clear(aim);call(aim,"BeginNext");
                Check((bool)get(aim,"precision")&&(string)get(aim,"phase")=="ready"&&get(aim,"current")==null,"Baseline flick completion opens precision instructions without starting a target");
                call(aim,"ClickRaw");var target=get(aim,"current");shoot(aim,false);
                Check(Object.ReferenceEquals(target,get(aim,"current"))&&aim.PrecisionResults.Count==0&&((List<AimClick>)get(aim,"clicks")).Count==1,"Precision miss keeps the same target and records its click");
                var timer=(System.Diagnostics.Stopwatch)get(aim,"clock");double t0=(double)get(aim,"t0");call(aim,"ClickRaw");
                Check((double)get(aim,"t0")==t0&&((List<AimClick>)get(aim,"clicks")).Count==2,"Repeated precision misses never reset the target clock");
                shoot(aim,true);Check(aim.PrecisionResults.Count==1&&aim.PrecisionResults[0].Hit==true&&aim.PrecisionResults[0].Clicks.Count==3,"Precision hit advances once and preserves preceding misses");
                for(int i=1;i<16;i++)shoot(aim,true);
                Check(aim.Completed&&!aim.ValidationStarted&&aim.Results.Count==48&&aim.PrecisionResults.Count==16,"Phase one completes only after separate flick and precision measurements");
                Check(aim.Progress.Done==aim.Progress.Total,"Phase-one progress includes all precision targets");
            }
            using(var aim=new AimForm(make(2),false,true)){
                aim.Results=make(2).Baseline;set(aim,"finished",48);set(aim,"warmupFinished",8);clear(aim);call(aim,"BeginNext");
                for(int i=0;i<16;i++)shoot(aim,true);
                Check(aim.ValidationStarted&&aim.CalibrationResults.Count==48&&aim.PrecisionResults.Count==16&&(string)get(aim,"phase")=="ready","Phase two preserves both measurements and requires a click before flick A/B");
                aim.Results=PrecisionPairs(16,1.15,1.02);set(aim,"finished",32);clear(aim);call(aim,"BeginNext");
                Check((bool)get(aim,"precision")&&(string)get(aim,"phase")=="ready"&&!aim.Completed,"Flick A/B completion opens precision A/B instructions; whole test is not yet complete");
                for(int i=0;i<32;i++)shoot(aim,true);
                Check(aim.Completed&&aim.PrecisionValidationResults.Count==32&&aim.Results.Count==32&&aim.ComparisonGoal==32,"Precision A/B saves separately and preserves the flick stopping count");
                string note;Check(PrecisionCheck.Acceptable(aim.PrecisionValidationResults,out note),"Matched precision pairs with equal time and hits pass the regression guard");
                var session=make(2);session.PrecisionBaseline=aim.PrecisionResults;session.PrecisionValidation=aim.PrecisionValidationResults;var loaded=Session.FromJson(JObject.FromObject(session).ToString());
                Check(loaded.DualTask&&loaded.PrecisionValidation.Count==32&&loaded.PrecisionBaseline.Count==16&&loaded.PrecisionValidation.All(t=>t.Clicks.Count==1),"Both precision datasets and individual clicks survive save and reload");
                foreach(var t in aim.PrecisionValidationResults.Where(t=>t.Corrected))t.TotalMs=900;
                Check(!PrecisionCheck.Acceptable(aim.PrecisionValidationResults,out note),"Perfect hits obtained by taking longer cannot pass precision regression guard");
                Check(!PrecisionCheck.Acceptable(aim.PrecisionValidationResults.Take(31).ToList(),out note),"Incomplete precision comparison cannot approve a profile");
                foreach(var t in aim.PrecisionValidationResults){t.Hit=false;t.Valid=false;t.TotalMs=3000;t.Clicks.Clear();}
                Check(!PrecisionCheck.Acceptable(aim.PrecisionValidationResults,out note),"Two equally failed precision blocks are not evidence of preserved control");
            }
            using(var aim=new AimForm(make(1),false,true)){
                type.GetMethod("StartPrecision",flags).Invoke(aim,new object[]{false});call(aim,"ClickRaw");set(aim,"phase","flick");
                var timer=(System.Diagnostics.Stopwatch)get(aim,"clock");set(aim,"t0",timer.Elapsed.TotalMilliseconds-3100);call(aim,"ClickRaw");
                Check(aim.PrecisionResults.Count==1&&aim.PrecisionResults[0].Hit==false&&aim.PrecisionResults[0].TotalMs==3000&&aim.PrecisionResults[0].Clicks.Count==0,"Late clicks cannot turn a precision timeout into a hit or a retried target");
            }
            var trendData=new CircleData{Protocol=3,Laps=CircleData.Plan().Select((l,i)=>{var lap=MakeCircle(12,1.3+.25*Math.Sin((i%30)*2*Math.PI/10),l.Clockwise);lap.Counted=true;return lap;}).ToList()};
            var trend=CircleAnalysis.Median(trendData);var proposal=CircleAnalysis.Propose(trendData,new Correction(),new Correction());
            Check(trend.Report.Stable&&trend.Spread>.10&&proposal.Right<1&&proposal.Down>1,"Common ellipse tendency survives ordinary variation and proposes opposite X/Y changes");
            Check(proposal.Right>=.98&&proposal.Down<=1/.98&&proposal.Sensitivity==1&&proposal.Rotation==0,"Trend proposal remains bounded and does not invent sensitivity or rotation");
            var historical=new Session{Circles=JObject.FromObject(trendData).ToObject<CircleData>(),Baseline=make(1).Baseline,Validation=PrecisionPairs(16,1.15,1.02)};historical.Circles.Protocol=2;
            string original=JObject.FromObject(historical).ToString();var reanalyzed=historical.ReanalyzeCircleTrend("original-v11.vectorfit.json");
            Check(JObject.FromObject(historical).ToString()==original&&reanalyzed.Circles.Protocol==3&&!reanalyzed.ValidationPassed&&reanalyzed.Validation.Count==0&&reanalyzed.ReanalyzedFrom=="original-v11.vectorfit.json","Explicit tendency reanalysis preserves the old measurement and discards evidence for the old candidate");
            trendData.Laps=CircleData.Plan().Select(l=>{var lap=MakeCircle(12,l.Clockwise?1.5:.7,l.Clockwise);lap.Counted=true;return lap;}).ToList();
            Check(!CircleAnalysis.Median(trendData).Report.Stable&&CircleAnalysis.Propose(trendData,new Correction(),new Correction()).Right==1,"Opposing clockwise and counterclockwise tendencies do not produce a common correction");
            var prior=new Correction{Right=.96,Left=1.04,Down=1.03,Up=.97,Rotation=1.5,Sensitivity=.85};
            using(var aim=new AimForm(new Session{DualTask=true,Round=2,InputCorrection=prior,Correction=new Correction{Sensitivity=.93}},true)){
                foreach(var delta in new[]{new PointD(8,3),new PointD(-9,2),new PointD(5,-7),new PointD(-1,-4)}){
                    var measured=prior.Transform(delta.X,delta.Y);var a=(PointD)type.GetMethod("TransformMovement",flags).Invoke(aim,new object[]{measured.X,measured.Y,100.0,false});var b=(PointD)type.GetMethod("TransformMovement",flags).Invoke(aim,new object[]{measured.X,measured.Y,100.0,true});
                    Check(Math.Abs(a.X-measured.X)<1e-9&&Math.Abs(a.Y-measured.Y)<1e-9&&Math.Abs(b.X-delta.X*.93)<1e-9&&Math.Abs(b.Y-delta.Y*.93)<1e-9,"Phase two passes measured input once and replaces rather than multiplies its full correction in A/B");
                }
            }
        }
        static void NewFeatures(string path){
            Func<double,Session> uniform=reach=>{var s=new Session{RawAccelAdaptiveValidation=false,InterceptionAdaptiveValidation=false,TrajectoryFitting=false,ConservativeSensitivity=false,Baseline=Synthetic(10,false,false)};foreach(var t in s.Baseline){t.EndX=t.TargetX*reach;t.EndY=t.TargetY*reach;t.ClickError=Math.Abs(reach-1);}Analysis.Compute(s);return s;};
            var over=uniform(1.2);Check(over.Correction.Sensitivity>=.95&&over.Correction.Sensitivity<1,"Repeated overshoot lowers overall sensitivity by at most five percent");
            var under=uniform(.85);Check(under.Correction.Sensitivity>1&&under.Correction.Sensitivity<=1.05,"Repeated undershoot raises sensitivity by at most five percent");
            var accurate=uniform(1.01);Check(accurate.Correction.Sensitivity==1,"Two-percent dead zone avoids correcting small natural differences");
            accurate.Baseline[0].EndX*=3;Analysis.Compute(accurate);Check(accurate.Correction.Sensitivity==1,"A single outlier does not trigger sensitivity fitting");
            var alternating=uniform(1);for(int i=0;i<alternating.Baseline.Count;i++){var t=alternating.Baseline[i];double reach=(i/8)%2==0?1.05:1.25;t.EndX=t.TargetX*reach;t.EndY=t.TargetY*reach;}Analysis.Compute(alternating);
            Check(alternating.Correction.Sensitivity==1,"Inconsistent split-half overshoot keeps sensitivity unchanged");
            var legacy=JObject.FromObject(over);legacy["Version"]="0.5.0";legacy.Remove("CalibrateSensitivity");legacy.Remove("Correction");
            var old=Session.FromJson(legacy.ToString());Check(!old.CalibrateSensitivity&&old.Correction.Sensitivity==1,"Older measurements retain the original sensitivity and tested proposal");
            var restored=Session.FromJson(JObject.FromObject(over).ToString());Check(restored.CalibrateSensitivity&&restored.Correction.Sensitivity==over.Correction.Sensitivity,"Sensitivity proposal survives save and load exactly");
            var round=over;
            for(int n=0;n<25;n++){var next=round.NextRound("prior.vectorfit.json");next.Baseline=uniform(1.2).Baseline;foreach(var t in next.Baseline){var point=next.InputCorrection.Transform(t.EndX,t.EndY);t.EndX=point.X;t.EndY=point.Y;}Analysis.Compute(next);Check(Math.Abs(next.Correction.Sensitivity/next.InputCorrection.Sensitivity-1)<=.050001&&next.Correction.Safe(),"Refinement keeps sensitivity within per-round and absolute limits");round=next;}
            Check(Math.Abs(1.2*round.Correction.Sensitivity-1)<.021,"Repeated stable overshoot approaches target reach without double applying the profile");
            Check(!new Correction{Sensitivity=Double.NaN}.Safe()&&!new Correction{Sensitivity=.7}.Safe()&&!new Correction{Sensitivity=Double.PositiveInfinity}.Safe(),"Invalid global sensitivity cannot reach a driver");
            var corrected=new Correction{Sensitivity=.8,Right=.96,Left=1.04,Rotation=2};var sample=corrected.Transform(-7,3);var inverse=corrected.Inverse(sample.X,sample.Y);
            Check(Math.Abs(inverse.X+7)<1e-10&&Math.Abs(inverse.Y-3)<1e-10,"Sensitivity and directional correction invert together");
            over.Validation=Synthetic(4,false,false).Concat(Synthetic(4,false,true)).ToList();foreach(var t in over.Validation)t.ClickError=t.Corrected?.04:.15;Analysis.Validate(over);
            Check(over.ValidationPassed,"Lower endpoint error can validate sensitivity even without directional bias");
            foreach(var t in over.Validation)if(t.Corrected)t.TotalMs=700;Analysis.Validate(over);Check(!over.ValidationPassed,"Sensitivity improvement still requires the speed guard");
            Check(RoundStatistics.Value(new List<Trial>(),0)==null&&RoundStatistics.Value(new List<Trial>(),1)==null,"Missing chart data remains missing rather than zero");
            Check(RoundStatistics.Delta(0,0,"pp").IndexOf("NaN")<0&&RoundStatistics.Delta(0,1,"pp").IndexOf("Infinity")<0,"Zero baseline differences do not create invalid percentages");
            string folder=Path.Combine(Path.GetDirectoryName(path),"history-test");Directory.CreateDirectory(folder);
            var first=uniform(1.1);File.WriteAllText(Path.Combine(folder,"first.vectorfit.json"),JObject.FromObject(first).ToString());
            var second=first.NextRound("first.vectorfit.json");second.Baseline=uniform(1.04).Baseline;string note;
            var history=History.Load(second,folder,out note);Check(history.Count==2&&history[0].Session.Round==1&&note=="","Charts load only the linked lineage in order");
            second.Setup.Dpi=800;Check(History.Load(second,folder,out note).Count==1&&note!="","History rejects a different mouse setup");second.Setup.Dpi=1600;
            second.ParentMeasurement="../first.vectorfit.json";Check(History.Load(second,folder,out note).Count==1&&note!="","History rejects paths outside the measurement folder");
            var p=new CalibrationProgress{Warmup=8,Measured=40};Check(p.Total==152&&p.Done==48&&p.Fraction>0&&p.Fraction<1,"Integrated progress includes warm-up, measurement and A/B");
            Check(CalibrationProgress.Clicks(new[]{new Trial{Rejection="Tiden gikk ut"},new Trial{Valid=false,Rejection="Klikk uten bevegelse"}})==1,"Timeouts do not advance progress but actual clicks do");
            using(var aim=new AimForm(new Session(),false,true)){
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;var type=typeof(AimForm);aim.Preview(false);
                var hit=type.GetMethod("HitsTarget",flags);float radius=(float)type.GetProperty("TargetRadius",flags).GetValue(aim,null);
                Check((bool)hit.Invoke(aim,new object[]{new System.Drawing.PointF(aim.Width/2,aim.Height/2)})&&!(bool)hit.Invoke(aim,new object[]{new System.Drawing.PointF(aim.Width/2+radius+2,aim.Height/2)}),"Hit feedback uses the filled green disk and excludes its decorative ring");
                type.GetMethod("BeginNext",flags).Invoke(aim,null);var current=type.GetField("current",flags).GetValue(aim);
                int before=aim.Progress.Done;type.GetMethod("ClickRaw",flags).Invoke(aim,null);
                Check(aim.Progress.Done==before&&Object.ReferenceEquals(current,type.GetField("current",flags).GetValue(aim)),"Click animation during the random delay never consumes a target");
                type.GetField("phase",flags).SetValue(aim,"flick");current.GetType().GetField("Warmup").SetValue(current,false);
                type.GetMethod("ClickRaw",flags).Invoke(aim,null);
                Check(aim.Results.Count==1&&aim.Results[0].Hit==false&&(string)type.GetField("phase",flags).GetValue(aim)=="countdown","An animated miss is recorded and immediately schedules the next measurement");
            }
            L.English=true;
            Check(L.T("Bruk forslaget og ta ny runde")=="Use proposal and start next round"&&L.T("Runde 3 · Måling 8/80")=="Round 3 · Measurement 8/80","English labels translate both complete and dynamic text");
            Check(L.T("C:\\data\\maaling-123.json")=="C:\\data\\maaling-123.json","Translation preserves measurement paths");
            using(var label=new System.Windows.Forms.Label{Text="Kalibrering"}){L.Controls(label);Check(label.Text=="Calibration","English UI text is applied");L.English=false;L.Controls(label);Check(label.Text=="Kalibrering","Switching back restores original Norwegian text");}
            L.English=false;
            string languageFile=Path.Combine(Driver.Data,"language.txt");string priorLanguage=File.Exists(languageFile)?File.ReadAllText(languageFile):null;
            try {L.English=true;L.Save();L.English=false;L.Load();Check(L.English,"Language preference survives a restart");}finally{if(priorLanguage==null)File.Delete(languageFile);else File.WriteAllText(languageFile,priorLanguage);L.English=false;}
            foreach(string file in Directory.GetFiles(Driver.Data,"*.vectorfit.json").Where(f=>f.EndsWith("v05.vectorfit.json")||f.EndsWith("v06.vectorfit.json")||f.EndsWith("v08.vectorfit.json")||f.EndsWith("v09.vectorfit.json")||f.EndsWith("v10.vectorfit.json"))){
                var json=JObject.Parse(File.ReadAllText(file));var loaded=Session.FromJson(json.ToString());var prior=json["Correction"].ToObject<Correction>();
                Check(JToken.DeepEquals(Driver.Config(loaded.Correction),Driver.Config(prior)),"Existing user round keeps exactly the previously tested driver profile: "+Path.GetFileName(file));
                Check(loaded.ValidationPassed==(bool)json["ValidationPassed"]&&loaded.Round==(int)json["Round"],"Existing user A/B assessment and round survive migration");
            }
            using(var main=new MainForm()){
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                var sampleSession=uniform(1.2);typeof(MainForm).GetField("session",flags).SetValue(main,sampleSession);
                Func<System.Windows.Forms.Control,IEnumerable<System.Windows.Forms.Control>> tree=null;tree=c=>c.Controls.Cast<System.Windows.Forms.Control>().SelectMany(child=>new[]{child}.Concat(tree(child)));
                L.English=true;
                for(int page=0;page<4;page++){typeof(MainForm).GetMethod("ShowPage",flags).Invoke(main,new object[]{page});
                    var labels=tree(main).Where(c=>c is System.Windows.Forms.Label||c is System.Windows.Forms.Button||c is System.Windows.Forms.CheckBox).Select(c=>c.Text).ToList();
                    Check(!labels.Any(t=>System.Text.RegularExpressions.Regex.IsMatch(t,"[æøåÆØÅ]")),"English page has no leftover Norwegian display text: "+page);
                }
                L.English=false;typeof(MainForm).GetMethod("ShowPage",flags).Invoke(main,new object[]{0});Check(tree(main).OfType<System.Windows.Forms.Button>().Any(b=>b.Text=="Start kalibrering"),"Main form switches back to Norwegian without replacing measurements");
                Check(Object.ReferenceEquals(sampleSession,typeof(MainForm).GetField("session",flags).GetValue(main)),"Language switching leaves calibration data intact");
            }
        }
        static void NaturalCircleFeatures(string path){
            var narrow=new CircleTracker(new CircleLap{Radius=12,Clockwise=true},12,0,0,null,true);bool counted=false;
            for(int i=1;i<=720;i++)counted=narrow.Add(12*Math.Cos(i*2*Math.PI/720),.6*Math.Sin(i*2*Math.PI/720),i*4);
            Check(counted&&narrow.Lap.Complete,"Natural narrow ellipses complete without a minimum radius or roundness requirement");
            var guided=new Session{GuidedCalibration=true,Baseline=Enumerable.Range(0,48).Select(i=>PrecisionTrial(i%8,1.08,1.08,false,0)).ToList()};
            var privateFlags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            using(var aim=new AimForm(guided,false,true)){
                aim.Results=guided.Baseline;typeof(AimForm).GetField("finished",privateFlags).SetValue(aim,48);
                bool advanced=(bool)typeof(AimForm).GetMethod("BeginAutomaticValidation",privateFlags).Invoke(aim,null);
                Check(!advanced&&!aim.ValidationStarted&&aim.Results.Count==48&&!aim.Progress.IncludeComparison,"Guided phase one ends at baseline analysis without silently adding a corrected A/B stage");
            }
            Analysis.Compute(guided);var nextPhase=guided.NextRound("baseline.vectorfit.json");
            Check(nextPhase.GuidedCalibration&&nextPhase.Round==2&&JToken.DeepEquals(Driver.Config(nextPhase.InputCorrection),Driver.Config(guided.Correction)),"Phase two records the exact proposed correction as its measured input basis");
            using(var aim=new AimForm(nextPhase,false,true)){Check(aim.Progress.IncludeComparison,"Later guided phases retain controlled checks of their next proposal");}
            Check(!CalibrationProgress.FromSession(guided).IncludeComparison,"Baseline progress does not promise an A/B stage that is intentionally deferred");
            var data=new CircleData{Protocol=2,Laps=CircleData.Plan().Select(l=>MakeCircle(12,1.8,l.Clockwise)).ToList()};foreach(var lap in data.Laps)lap.Counted=true;
            var before=JObject.FromObject(data).ToString();var median=CircleAnalysis.Median(data);
            Check(median.Report.Valid==60&&median.Report.Stable&&Math.Abs(median.Report.Roundness-1.8)<.04,"Strong natural ellipses contribute all sixty laps without a roundness gate");
            Check(JObject.FromObject(data).ToString()==before,"Natural median processing leaves every recorded sample untouched");
            foreach(var lap in data.Laps){lap.Complete=false;lap.Reason="Tiden gikk ut";}
            Check(CircleAnalysis.Median(data).Report.Valid==60,"Counted incomplete laps with movement remain in the natural median");
            var irregular=new CircleData{Protocol=2,Laps=CircleData.Plan().Select(l=>{var lap=MakeCircle(12,1.3,l.Clockwise);lap.Counted=true;foreach(var p in lap.Samples){double a=Math.Atan2(p.Y,p.X),r=1+.3*Math.Cos(3*a);p.X*=r;p.Y*=r;}return lap;}).ToList()};
            Check(CircleAnalysis.Median(irregular).Report.Valid==60,"Lumpy non-elliptical paths are not excluded for ellipse residual");
            var missing=new CircleData{Protocol=2,Laps=CircleData.Plan().Select(l=>new CircleLap{Radius=12,Clockwise=l.Clockwise,Counted=true,Reason="Tiden gikk ut",Samples=new List<Motion>{new Motion(0,12,0)}}).ToList()};
            var unavailable=CircleAnalysis.Median(missing);Check(unavailable.Report.Total==60&&unavailable.Report.Valid==0&&!unavailable.Report.Stable&&missing.Laps.Count(missing.Counts)==60,"No-motion attempts count without manufacturing a perfect median or correction");
            var s=new Session{Circles=data};var reopened=Session.FromJson(JObject.FromObject(s).ToString());Check(reopened.Circles.Protocol==2&&reopened.Circles.Laps.Count(reopened.Circles.Counts)==60,"Natural protocol and counted timeout outcomes survive reopening");
            var progress=CalibrationProgress.FromSession(reopened);Check(progress.Circles==60&&progress.CircleGoal==60,"Progress counts recorded natural rounds instead of judging their shape");
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            using(var main=new MainForm()){typeof(MainForm).GetField("session",flags).SetValue(main,reopened);Check((bool)typeof(MainForm).GetProperty("CanResumeCircles",flags).GetValue(main,null),"Sixty counted natural rounds can continue into mapping even when winding completion differs");}
            var values=new Session{InputCorrection=new Correction{Sensitivity=.9,Right=.98,Rotation=.2},Correction=new Correction{Sensitivity=.945,Right=1.01,Rotation=-.1}};var rows=ProposalValues.Rows(values);
            Check(rows.Length==6&&Math.Abs(rows[0].Change-5)<1e-8&&Math.Abs(rows[1].Change-(1.01/.98-1)*100)<1e-8,"Proposal compares scalar and axis multipliers with the actual measured input, not neutral defaults");
            Check(Math.Abs(rows[5].Change+.3)<1e-8&&rows[5].Degrees,"Rotation difference uses degrees rather than dividing by zero or reporting a percentage");
            L.English=true;Check(L.T("Prøv forslaget og ta ny runde")=="Try proposal and start next round"&&L.T("Fase 1 · Baseline analysert")=="Phase 1 · Baseline analyzed","Trial-and-next-round action and composite phase heading are translated");L.English=false;
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(path),"natural-demo.vectorfit.json"),JObject.FromObject(new Session{Circles=irregular}).ToString());
        }
        static void MedianCircleFeatures(string path){
            var plan=CircleData.Plan();Check(plan.Count==60&&plan.Take(30).All(l=>l.Clockwise)&&plan.Skip(30).All(l=>!l.Clockwise)&&plan.All(l=>l.Radius==12),"New protocol is exactly 30 clockwise then 30 counterclockwise at one size");
            var data=new CircleData{Protocol=1,Laps=plan.Select(l=>MakeCircle(l.Radius,1.2,l.Clockwise,0,true)).ToList()};
            var median=CircleAnalysis.Median(data);Check(median.Report.Stable&&median.Report.Valid==60&&median.Clockwise==30&&median.Counterclockwise==30,"Sixty complete consistent laps support a median profile");
            Check(median.Points.Count==72&&Math.Abs(median.Report.Roundness-1.2)<.02,"Angular median recovers ellipse shape despite uneven drawing speed");
            var proposed=CircleAnalysis.Propose(data,new Correction(),new Correction());var predicted=CircleAnalysis.Median(data,new Correction(),proposed);
            Check(proposed.Safe()&&proposed.Right<1&&proposed.Up>1&&predicted.Report.Roundness<median.Report.Roundness-.02,"Bounded axis correction makes a consistent horizontal ellipse rounder in simulation");
            Check(proposed.Rotation==0&&proposed.Sensitivity==1&&proposed.Right>=.98&&proposed.Up<=1/.98,"Circle fitting cannot silently change sensitivity or rotate aim");
            data.Laps[0]=MakeCircle(12,.7,true);Check(CircleAnalysis.Median(data).Report.Stable&&Math.Abs(CircleAnalysis.Median(data).Report.Roundness-1.2)<.02,"One unusual lap does not dominate the pointwise median");
            foreach(var lap in data.Laps)foreach(var point in lap.Samples){point.X+=2;point.Y-=1;}
            Check(Math.Abs(CircleAnalysis.Median(data).Report.Roundness-1.2)<.02,"Translation of the drawn center does not masquerade as ellipse shape");
            var tilted=new CircleData{Protocol=1,Laps=plan.Select(l=>MakeCircle(12,1.25,l.Clockwise,Math.PI/4)).ToList()};var tiltMedian=CircleAnalysis.Median(tilted);
            Check(tiltMedian.Report.Stable&&Math.Abs(tiltMedian.Report.Roundness-1.25)<.02&&Math.Abs(tiltMedian.Report.AxisRatio-1)<.01,"Median detects a tilted ellipse even when width equals height");
            Check(JToken.DeepEquals(Driver.Config(CircleAnalysis.Propose(tilted,new Correction(),new Correction())),Driver.Config(new Correction())),"A 45-degree ellipse cannot justify unsupported rotation or fictitious XY improvement");
            var opposite=new CircleData{Protocol=1,Laps=plan.Select(l=>MakeCircle(12,1.25,l.Clockwise,l.Clockwise?Math.PI/4:-Math.PI/4)).ToList()};
            Check(!CircleAnalysis.Median(opposite).Report.Stable,"Opposite tilted ellipses cannot cancel into a falsely stable round median");
            var drift=new CircleData{Protocol=1,Laps=plan.Select((l,i)=>MakeCircle(12,i%30<15?1.2:1,l.Clockwise)).ToList()};
            Check(!CircleAnalysis.Median(drift).Report.Stable,"Early versus late drift in either direction blocks circle fitting");
            var partial=new CircleData{Protocol=1,Laps=plan.Take(59).Select(l=>MakeCircle(12,1.2,l.Clockwise)).ToList()};Check(!CircleAnalysis.Median(partial).Report.Stable,"A missing complete rotation cannot silently satisfy 30 plus 30");
            var s=new Session{Circles=data,Baseline=Enumerable.Range(0,48).Select(i=>PrecisionTrial(i%8,1,1,false,0)).ToList()};Analysis.Compute(s);
            Check(JToken.DeepEquals(Driver.Config(Session.FromJson(JObject.FromObject(s).ToString()).Correction),Driver.Config(s.Correction)),"New median proposal and protocol survive reopening exactly");
            using(var aim=new AimForm(s,false,true,true)){Check(aim.Progress.Circles==60&&aim.Progress.CircleGoal==60&&aim.Progress.Total==148,"New progress includes 60 rotations, 8 warmups, 48 mapping and minimum 32 A/B attempts");}
            var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;var ct=typeof(CircleForm);
            using(var circle=new CircleForm(new Setup())){
                ct.GetField("index",flags).SetValue(circle,5);ct.GetField("tracker",flags).SetValue(circle,new CircleTracker(new CircleLap{Radius=12,Complete=false,Reason="Tiden gikk ut"},12,0,0));ct.GetMethod("FinishLap",flags).Invoke(circle,null);
                Check((int)ct.GetField("index",flags).GetValue(circle)==6&&circle.Data.Laps.Count==1&&!circle.Data.Laps[0].Complete&&circle.Data.Laps[0].Counted,"Timeout counts and is retained honestly without forcing a redraw");
            }
            using(var circle=new CircleForm(new Setup())){
                for(int i=0;i<60;i++){ct.GetField("tracker",flags).SetValue(circle,new CircleTracker(new CircleLap{Radius=12,Complete=true,Clockwise=i<30},12,0,i*1000));ct.GetMethod("FinishLap",flags).Invoke(circle,null);if(i==29)Check(!circle.Completed&&(string)ct.GetField("phase",flags).GetValue(circle)=="countdown","Live circle flow pauses after exactly 30 clockwise rotations");}
                Check(circle.Completed&&circle.Data.Laps.Count(l=>l.Complete&&l.Clockwise)==30&&circle.Data.Laps.Count(l=>l.Complete&&!l.Clockwise)==30,"Live circle flow finishes with exactly thirty completed laps each way");
            }
            var savedCircles=new Session{Circles=data};var reopened=Session.FromJson(JObject.FromObject(savedCircles).ToString());
            using(var main=new MainForm()){
                typeof(MainForm).GetField("session",flags).SetValue(main,reopened);
                Check((bool)typeof(MainForm).GetProperty("CanResumeCircles",flags).GetValue(main,null),"Saved completed circle test can resume target mapping without repeating circles");
                reopened.Circles.Laps.RemoveAt(0);Check(!(bool)typeof(MainForm).GetProperty("CanResumeCircles",flags).GetValue(main,null),"Incomplete saved circle test cannot bypass required rotations");
            }
            var historical=Directory.GetFiles(Driver.Data,"*v09.vectorfit.json");foreach(string file in historical){
                var old=Session.FromJson(File.ReadAllText(file));var before=Driver.Config(old.Correction);Check(!old.ConservativeSensitivity&&old.Circles.Protocol==0&&old.Circles.Goal==15,"Existing v09 file retains its original circle and sensitivity algorithms");
                var next=old.NextRound(Path.GetFileName(file),false);Check(next.ConservativeSensitivity,"Following round enables the stronger sensitivity evidence requirement");
                old.ConservativeSensitivity=true;old.Validation.Clear();Analysis.Compute(old);Check(old.Correction.Sensitivity==old.InputCorrection.Sensitivity,"User's unstable v09 first strokes no longer trigger a sensitivity increase");
            }
            using(var warning=new ActiveProfileDialog(true,"Aktiv profil")){Check(warning.Controls.OfType<System.Windows.Forms.Button>().Any(b=>b.Text=="Nøytraliser profil")&&((System.Windows.Forms.Button)warning.AcceptButton).DialogResult==System.Windows.Forms.DialogResult.Cancel,"Active-profile warning exposes neutralization and does not activate it with an accidental Enter");}
            L.English=true;Check(L.T("Nøytraliser profil")=="Neutralize profile"&&L.T("Medianform")=="Median shape","New median and neutralization controls support English");L.English=false;
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(path),"median-demo.vectorfit.json"),JObject.FromObject(s).ToString());
        }
        static CircleLap MakeCircle(double radius,double ratio,bool clockwise,double rotation=0,bool uneven=false){
            var lap=new CircleLap{Radius=radius,Clockwise=clockwise,Complete=true};
            for(int i=0;i<=720;i++){double u=i/720.0,a=(clockwise?1:-1)*Math.PI*2*(uneven?u*u*u:u);double x=radius*ratio*Math.Cos(a),y=radius*Math.Sin(a);lap.Samples.Add(new Motion(i*5,x*Math.Cos(rotation)-y*Math.Sin(rotation),x*Math.Sin(rotation)+y*Math.Cos(rotation)));}return lap;
        }
        static void CircleAndAdaptiveFeatures(string path){
            var plan=CircleData.LegacyPlan();Check(plan.Count==15&&plan.GroupBy(p=>p.Radius).All(g=>g.Count()==5&&g.Any(l=>l.Clockwise)&&g.Any(l=>!l.Clockwise)),"Fifteen circles cover three sizes and both directions at every size");
            Check(CircleData.LegacyPlan(true).Zip(plan,(a,b)=>a.Clockwise!=b.Clockwise).All(v=>v),"Initial circle direction can be counterbalanced");
            foreach(bool uneven in new[]{false,true})foreach(double ratio in new[]{1.0,1.2,.8}){
                var shape=CircleAnalysis.Measure(MakeCircle(12,ratio,true,0,uneven));
                Check(shape.Valid&&Math.Abs(shape.AxisRatio-ratio)<.015&&Math.Abs(shape.Roundness-Math.Max(ratio,1/ratio))<.015,"Circle fitting recovers width/height despite uneven drawing speed: "+ratio+" / "+uneven);
            }
            var rotated=CircleAnalysis.Measure(MakeCircle(8,1.25,false,Math.PI/4));
            Check(rotated.Valid&&Math.Abs(rotated.AxisRatio-1)<.01&&Math.Abs(rotated.Roundness-1.25)<.015,"A rotated ellipse reports unequal axes even when horizontal and vertical extents match");
            var incomplete=MakeCircle(12,1,true);incomplete.Complete=false;
            Check(!CircleAnalysis.Measure(incomplete).Valid,"Incomplete circle is retained but cannot train shape fitting");
            var line=new CircleLap{Radius=12,Complete=true,Samples=Enumerable.Range(0,300).Select(i=>new Motion(i,i*.01,0)).ToList()};
            Check(!CircleAnalysis.Measure(line).Valid,"Straight movement cannot be accepted as a circle");
            var outlier=MakeCircle(12,1.12,true);foreach(var p in outlier.Samples.Skip(190).Take(4)){p.X*=1.5;p.Y*=1.5;}
            var robust=CircleAnalysis.Measure(outlier);Check(robust.Valid&&Math.Abs(robust.AxisRatio-1.12)<.025,"A brief path excursion does not dominate the fitted ellipse");
            var data=new CircleData{Laps=plan.Select(l=>MakeCircle(l.Radius,1.16,l.Clockwise)).ToList()};
            var report=CircleAnalysis.Summarize(data);Check(report.Stable&&report.Valid==15&&Math.Abs(report.AxisRatio-1.16)<.01,"Consistent width bias across sizes and directions supports a weak prior");
            data.Laps[1]=MakeCircle(data.Laps[1].Radius,.8,data.Laps[1].Clockwise);report=CircleAnalysis.Summarize(data);
            Check(report.Stable&&Math.Abs(report.AxisRatio-1.16)<.01,"Median circle summary resists one unusual lap");
            var inconsistent=new CircleData{Laps=plan.Select(l=>MakeCircle(l.Radius,l.Clockwise?1.25:.8,l.Clockwise)).ToList()};
            Check(!CircleAnalysis.Summarize(inconsistent).Stable,"Different clockwise and counterclockwise shapes cannot steer the profile");
            var sizeBias=new CircleData{Laps=plan.Select(l=>MakeCircle(l.Radius,l.Radius==8?1.3:1,l.Clockwise)).ToList()};
            Check(!CircleAnalysis.Summarize(sizeBias).Stable,"A size-specific shape bias cannot steer the profile");
            var session=new Session{RawAccelAdaptiveValidation=false,InterceptionAdaptiveValidation=false,TrajectoryFitting=false,ConservativeSensitivity=false,Baseline=Synthetic(6,false,false),Circles=data};Analysis.Compute(session);
            Check(session.FitReady&&session.Repeatable&&session.Correction.Right<1&&session.Correction.Down>1&&session.Correction.Safe(),"Forty-eight stable flicks plus circles produce a bounded proposal");
            var prior=Driver.Config(session.Correction);Analysis.Compute(session);Check(JToken.DeepEquals(prior,Driver.Config(session.Correction)),"Reopening or recomputing never compounds the circle contribution");
            var loaded=Session.FromJson(JObject.FromObject(session).ToString());Check(loaded.Circles.Laps.Count==15&&JToken.DeepEquals(prior,Driver.Config(loaded.Correction)),"Circle data and exact proposal survive save and reload");
            var noCircle=new Session{RawAccelAdaptiveValidation=false,InterceptionAdaptiveValidation=false,TrajectoryFitting=false,ConservativeSensitivity=false,Baseline=Synthetic(6,false,false)};Analysis.Compute(noCircle);session.Circles=inconsistent;Analysis.Compute(session);
            Check(JToken.DeepEquals(Driver.Config(noCircle.Correction),Driver.Config(session.Correction)),"Unstable circles do not change the flick-derived proposal");
            foreach(double hz in new[]{125.0,1000.0,8000.0}){
                var lap=new CircleLap{Radius=12,Clockwise=true};var tracker=new CircleTracker(lap,12,0,0);bool complete=false;int count=(int)(3*hz);
                for(int i=1;i<=count;i++){double angle=Math.PI*2*i/count;complete=tracker.Add(12*Math.Cos(angle),12*Math.Sin(angle),3000.0*i/count);if(i<count)CheckLoop(!complete,"Early circle completion");}
                Check(complete&&lap.Complete&&lap.Samples.Count<=752&&CircleAnalysis.Measure(lap).Valid,"Circle completion and bounded storage work at "+hz+" Hz");
            }
            var reverse=new CircleTracker(new CircleLap{Radius=12,Clockwise=false},12,0,0);bool reverseDone=false;
            for(int i=1;i<=360;i++)reverseDone=reverse.Add(12*Math.Cos(-i*Math.PI/180),12*Math.Sin(-i*Math.PI/180),i*5);
            Check(reverseDone,"Counterclockwise turns complete in the requested direction");
            var backtrack=new CircleTracker(new CircleLap{Radius=12,Clockwise=true},12,0,0);int time=0;
            foreach(int degrees in Enumerable.Range(1,180).Concat(Enumerable.Range(90,90).Reverse()).Concat(Enumerable.Range(91,269))){double a=degrees*Math.PI/180;CheckLoop(!backtrack.Add(12*Math.Cos(a),12*Math.Sin(a),++time*5),"Backtracking counted as an extra turn");}
            Check(backtrack.Add(12,0,++time*5),"Reversing briefly does not count as an extra rotation");
            var input=new Correction{Right=.95,Left=1.05,Down=1.04,Up=.96,Rotation=2,Sensitivity=.9};
            var sampled=new CircleTracker(new CircleLap{Radius=12,Clockwise=true},12,0,0,input);double ox=12,oy=0,rx=12,ry=0;
            for(int i=1;i<=1000;i++){double dx=i%2==0?-.03:.035,dy=i%3==0?-.01:.012;rx+=dx;ry+=dy;var move=input.Transform(dx,dy);ox+=move.X;oy+=move.Y;sampled.Add(ox,oy,i*.125);}
            var final=sampled.Lap.RawSamples.Last();int lastIndex=(int)Math.Round(final.T/.125);double ex=12,ey=0;for(int i=1;i<=lastIndex;i++){ex+=i%2==0?-.03:.035;ey+=i%3==0?-.01:.012;}
            Check(Math.Abs(final.X-ex)<1e-9&&Math.Abs(final.Y-ey)<1e-9,"Existing input profile is undone before sample reduction, preserving small reversals");
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;var type=typeof(AimForm);
            Action<AimForm,int,List<Trial>> finish=(aim,count,trials)=>{aim.Results=trials;var queue=type.GetField("queue",flags).GetValue(aim);queue.GetType().GetMethod("Clear").Invoke(queue,null);type.GetField("finished",flags).SetValue(aim,count);type.GetMethod("BeginNext",flags).Invoke(aim,null);};
            using(var aim=new AimForm(new Session{RawAccelAdaptiveValidation=false,InterceptionAdaptiveValidation=false,TrajectoryFitting=false,ConservativeSensitivity=false,Circles=data},false,true,true)){
                finish(aim,48,Synthetic(6,true,false));
                Check(aim.ValidationStarted&&aim.CalibrationResults.Count==48&&aim.CalibrationGoal==48,"Stable mapping starts A/B automatically after 48 clicks");
                Check(aim.Progress.MeasurementGoal==48&&aim.Progress.Circles==15&&aim.Progress.Total==135,"Progress reflects the shorter mapping and optional circle phase");
            }
            using(var aim=new AimForm(new Session(),false,true)){
                var trials=Synthetic(6,true,false);foreach(var t in trials.Where(t=>t.Direction==0))t.Valid=false;
                finish(aim,48,trials);Check(!aim.ValidationStarted&&aim.CalibrationGoal==64,"Insufficient directional data adds sixteen clicks");
                finish(aim,64,trials);Check(!aim.ValidationStarted&&aim.CalibrationGoal==80,"A second extension is bounded at eighty clicks");
                finish(aim,80,trials);Check(!aim.ValidationStarted&&aim.CalibrationGoal==80,"Insufficient data never starts an uncomputed A/B or extends indefinitely");
            }
            using(var aim=new AimForm(new Session(),false,true)){
                var trials=Synthetic(6,false,false);for(int i=0;i<trials.Count;i++){var t=trials[i];double reach=(i/8)%2==0?1.05:1.25;t.EndX=t.TargetX*reach;t.EndY=t.TargetY*reach;}
                finish(aim,48,trials);Check(!aim.ValidationStarted&&aim.CalibrationGoal==64,"Inconsistent overall reach extends mapping even if directional shape is stable");
            }
            using(var main=new MainForm()){
                main.StartPosition=System.Windows.Forms.FormStartPosition.Manual;main.Location=new System.Drawing.Point(-20000,-20000);main.ShowInTaskbar=false;main.Show();System.Windows.Forms.Application.DoEvents();main.Size=main.MinimumSize;System.Windows.Forms.Application.DoEvents();
                Func<System.Windows.Forms.Control,IEnumerable<System.Windows.Forms.Control>> tree=null;tree=c=>c.Controls.Cast<System.Windows.Forms.Control>().SelectMany(child=>new[]{child}.Concat(tree(child)));
                var start=tree(main).OfType<System.Windows.Forms.Button>().Single(b=>b.Text=="Start kalibrering");var stack=tree(main).OfType<System.Windows.Forms.FlowLayoutPanel>().Single(p=>p.AutoScroll);
                var at=stack.PointToClient(start.PointToScreen(System.Drawing.Point.Empty));
                Check(at.Y>=0&&at.Y+start.Height<=stack.ClientSize.Height&&!stack.HorizontalScroll.Visible,"Start stays visible and horizontal scrolling stays off at minimum window size");
                var circles=tree(main).OfType<System.Windows.Forms.CheckBox>().Single(c=>c.Text.StartsWith("Ta sirkeltest"));var choiceAt=stack.PointToClient(circles.PointToScreen(System.Drawing.Point.Empty));
                Check(choiceAt.Y+circles.Height<=stack.ClientSize.Height,"Optional circle choice stays visible before starting at minimum window size");main.Close();
            }
            var demo=new Session{RawAccelAdaptiveValidation=false,InterceptionAdaptiveValidation=false,TrajectoryFitting=false,ConservativeSensitivity=false,Circles=data,Baseline=Synthetic(6,true,false)};Analysis.Compute(demo);demo.Validation=Synthetic(4,true,false).Concat(Synthetic(4,false,true)).ToList();demo.Notes=Analysis.Validate(demo);
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(path),"circle-demo.vectorfit.json"),JObject.FromObject(demo).ToString());
            L.English=true;Check(L.T("Tegn runde bevegelser gjennom de fire punktene og tilbake til det grønne. Banen vises ikke.").StartsWith("Draw round movements"),"Circle instructions have an English translation");L.English=false;
        }
        static void CheckLoop(bool ok,string message){if(!ok)throw new Exception(message);}
        static Trial PrecisionTrial(int direction,double endReach,double peakReach,bool corrected,int pair,double hz=1000,double duration=400){
            double angle=direction*Math.PI/4,tx=15*Math.Cos(angle),ty=15*Math.Sin(angle);var points=new List<Motion>();
            Func<double,double> position=time=>{double t=time/duration*400;if(t<80)return 0;if(t<220)return peakReach*(1-Math.Cos((t-80)/140*Math.PI))/2;if(t<260)return peakReach;if(t<360)return peakReach+(endReach-peakReach)*(1-Math.Cos((t-260)/100*Math.PI))/2;return endReach;};
            for(double t=0;t<duration;t+=1000/hz)points.Add(new Motion(t,tx*position(t),ty*position(t)));points.Add(new Motion(duration,tx*endReach,ty*endReach));
            var trial=Analysis.Extract(direction,tx,ty,points,duration,corrected);trial.PairId=pair;trial.HitRadiusDegrees=.65;trial.Hit=AimGeometry.Inside(trial,points.Last());return trial;
        }
        static List<Trial> PrecisionPairs(int pairs,double aEnd,double bEnd,double aPeak=0,double bPeak=0){
            var result=new List<Trial>();for(int i=0;i<pairs;i++){int d=i%8,id=i+1;result.Add(PrecisionTrial(d,aEnd,aPeak==0?aEnd:aPeak,false,id));result.Add(PrecisionTrial(d,bEnd,bPeak==0?bEnd:bPeak,true,id));}return result;
        }
        static void PrecisionFeatures(string path){
            foreach(double hz in new[]{125.0,1000.0,8000.0}){
                var hit=PrecisionTrial(0,1,1.2,false,1,hz);var q=Trajectory.Measure(hit);
                Check(hit.Hit==true&&q.Available&&q.PrimaryDetected&&q.HitAfterOvershoot&&q.Overshoot>.18&&q.ReturnDistance>.17&&q.CorrectionDistance>.17,"A final hit retains overshoot and corrective travel at "+hz+" Hz");
                Check(q.Reentries>=1&&q.FirstEntryMs.HasValue&&q.CorrectionMs>100,"Target exit/re-entry and correction time remain visible at "+hz+" Hz");
                var fitted=Trajectory.ForFit(hit,q);Check(fitted.Reach>1.18&&fitted.Reach<1.21,"The first stroke, not the corrected hit, trains consistent overshoot at "+hz+" Hz");
            }
            foreach(int d in Enumerable.Range(0,8)){var direct=PrecisionTrial(d,1,1,false,1);var q=Trajectory.Measure(direct);Check(direct.Hit==true&&!q.HitAfterOvershoot&&q.ReturnDistance<.005&&q.CorrectionDistance<.005,"Direct hits are distinguished from corrected hits in direction "+d);}
            var under=PrecisionTrial(2,1,.8,false,1);var uq=Trajectory.Measure(under);Check(uq.PrimaryDetected&&uq.PrimaryReach<.82&&uq.CorrectionDistance>.17,"An initial undershoot followed by a hit is measured separately");
            var blank=new Trial{TargetX=15,TotalMs=400};Check(!Trajectory.Measure(blank).Available,"Missing trajectories are unknown, not perfect control");
            var malformed=PrecisionTrial(0,1,1,false,1);malformed.Samples[5].T=-1;Check(!Trajectory.Measure(malformed).Available,"Nonmonotonic sample time cannot train a first stroke");
            var session=new Session{Baseline=Enumerable.Range(0,48).Select(i=>PrecisionTrial(i%8,1,1.2,false,0)).ToList()};Analysis.Compute(session);
            Check(session.FitReady&&session.Repeatable&&session.Baseline.All(t=>t.Hit==true)&&session.Correction.Sensitivity<1,"Repeated overshoot lowers proposed sensitivity even when every final click hits");
            var legacyJson=JObject.FromObject(session);legacyJson["Version"]="0.8.0";var old=Session.FromJson(legacyJson.ToString());
            Check(!old.TrajectoryFitting&&!old.AdaptiveValidation&&old.Correction.Sensitivity==1,"Version 0.8 retains its endpoint-only fit and original protocol");
            var saved=Session.FromJson(JObject.FromObject(session).ToString());Check(saved.TrajectoryFitting&&saved.AdaptiveValidation&&JToken.DeepEquals(Driver.Config(saved.Correction),Driver.Config(session.Correction)),"First-stroke fitting survives reload without changing the proposal");
            var one=new Session{Baseline=Enumerable.Range(0,48).Select(i=>PrecisionTrial(i%8,1,i==0?1.3:1,false,0)).ToList()};Analysis.Compute(one);Check(one.Correction.Sensitivity==1,"One corrected overshoot does not change global sensitivity");
            var low=new Session{Baseline=Enumerable.Range(0,48).Select(i=>PrecisionTrial(i%8,1,.8,false,0)).ToList()};Analysis.Compute(low);Check(low.Correction.Sensitivity>1,"Consistent initial undershoot can increase proposed sensitivity despite final hits");
            var returns=new Session{Mode=ProfileMode.Interception,Baseline=Enumerable.Range(0,48).Select(i=>PrecisionTrial(i%8,1,1.2,false,0)).ToList()};
            foreach(var t in returns.Baseline)foreach(var p in t.Samples.Where(p=>p.T>260&&p.T<360)){double bend=2*Math.Sin((p.T-260)/100*Math.PI),angle=t.Direction*Math.PI/4;p.X-=Math.Sin(angle)*bend;p.Y+=Math.Cos(angle)*bend;}
            Analysis.Compute(returns);Check(returns.Curve.SupportedBins==0,"Intentional return corrections do not train the optional curve-removal profile");
            Check(Math.Abs(SequentialComparison.SignTail(14,16)-.0020904541015625)<1e-12&&SequentialComparison.SignTail(13,16)>SequentialComparison.Alpha,"Exact sign-test thresholds require a clear majority at the first look");
            double bound=0;foreach(int n in new[]{16,24,32}){int wins=Enumerable.Range(0,n+1).First(k=>SequentialComparison.SignTail(k,n)<=SequentialComparison.Alpha);bound+=3*SequentialComparison.SignTail(wins,n);}Check(bound<=.05,"All three improvement routes across three looks stay inside the prespecified null error budget");
            Func<List<Trial>,Session> comparison=trials=>new Session{FitReady=true,Repeatable=true,Validation=trials};
            var better=comparison(PrecisionPairs(16,1.12,1.06));var report=SequentialComparison.Evaluate(better);
            Check(report.Decision==ComparisonDecision.Improved&&report.Pairs==16&&report.CloserPairs==16,"Clear, consistent closer misses can stop A/B after 32 attempts");
            var moreHits=comparison(PrecisionPairs(16,1,1));for(int i=8;i<16;i++)moreHits.Validation[2*i]=PrecisionTrial(i%8,1.1,1.1,false,i+1);report=SequentialComparison.Evaluate(moreHits);
            Check(report.Decision==ComparisonDecision.Improved&&report.ExtraHits==8&&report.HitP<=SequentialComparison.Alpha&&report.DistanceP>SequentialComparison.Alpha,"Actual hit improvements have their own paired evidence route");
            var corrected=comparison(PrecisionPairs(16,1,1,1.25,1.05));report=SequentialComparison.Evaluate(corrected);
            Check(report.Decision==ComparisonDecision.Improved&&report.LessCorrectionPairs==16,"Less return correction can improve already accurate hits without inventing closer endpoints");
            better.Validation[1]=PrecisionTrial(0,1.2,1.2,true,1);better.Validation[0]=PrecisionTrial(0,1,1,false,1);
            Check(SequentialComparison.Evaluate(better).Decision!=ComparisonDecision.Improved,"A lower median cannot approve a proposal with fewer actual hits");
            var slower=comparison(PrecisionPairs(16,1.12,1.06));foreach(var t in slower.Validation.Where(t=>t.Corrected))t.TotalMs*=1.3;
            Check(SequentialComparison.Evaluate(slower).Decision!=ComparisonDecision.Improved,"Closer but slower clicks cannot pass adaptive A/B");
            var slowMovement=comparison(PrecisionPairs(16,1.12,1.06));foreach(var t in slowMovement.Validation.Where(t=>t.Corrected))t.FlickMs*=1.3;Check(SequentialComparison.Evaluate(slowMovement).Decision!=ComparisonDecision.Improved,"Faster reaction cannot conceal slower mouse movement");
            var curveWorse=comparison(PrecisionPairs(16,1.12,1.06,1.12,1.4));Check(SequentialComparison.Evaluate(curveWorse).Decision!=ComparisonDecision.Improved,"Closer endpoints cannot hide larger overshoot and corrective travel");
            var worse=comparison(PrecisionPairs(16,1.06,1.12));Check(SequentialComparison.Evaluate(worse).Decision==ComparisonDecision.Worse,"Clearly worse proposals stop early without being applied");
            foreach(int pairs in new[]{16,24,32}){var equal=comparison(PrecisionPairs(pairs,1.1,1.1));var r=SequentialComparison.Evaluate(equal);Check(r.Decision==(pairs==32?ComparisonDecision.Inconclusive:ComparisonDecision.Continue),"Unclear evidence continues only until the bounded final look: "+pairs*2);}
            var partial=comparison(PrecisionPairs(12,1.12,1.06));Check(SequentialComparison.Evaluate(partial).Decision==ComparisonDecision.Incomplete,"Strong partial results cannot bypass the first balanced checkpoint");
            var missing=comparison(PrecisionPairs(16,1.12,1.06));missing.Validation[1].PairId=2;Check(SequentialComparison.Evaluate(missing).Decision==ComparisonDecision.Incomplete,"Duplicate or missing pairs block early approval");
            var mismatch=comparison(PrecisionPairs(16,1.12,1.06));mismatch.Validation[1].TargetY+=.1;Check(SequentialComparison.Evaluate(mismatch).Decision==ComparisonDecision.Incomplete,"Matched direction alone is insufficient when actual target geometry differs");
            var unknown=comparison(PrecisionPairs(16,1.12,1.06));unknown.Validation[1].Hit=null;Check(SequentialComparison.Evaluate(unknown).Decision==ComparisonDecision.Incomplete,"Unknown historical hit outcomes cannot approve the new test");
            var timeout=comparison(PrecisionPairs(16,1.12,1.06));timeout.Validation[0].Valid=false;timeout.Validation[0].Rejection="Tiden gikk ut";timeout.Validation[0].Hit=false;timeout.Validation[0].Samples.Clear();timeout.Validation[0].TotalMs=3000;
            Check(SequentialComparison.Evaluate(timeout).Decision==ComparisonDecision.Improved,"Timeouts remain counted as failures instead of discarding the harder A attempts");
            var separate=new Session{RawAccelAdaptiveValidation=false,InterceptionAdaptiveValidation=true,RawAccelComparisonGoal=64,InterceptionComparisonGoal=32};separate.Mode=ProfileMode.Interception;Check(separate.AdaptiveValidation&&separate.ComparisonGoal==32,"Raw Accel and Interception retain separate comparison protocols");separate.Mode=ProfileMode.RawAccel;Check(!separate.AdaptiveValidation&&separate.ComparisonGoal==64,"Changing profile type restores that profile's own protocol");
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;var type=typeof(AimForm);
            using(var aim=new AimForm(comparison(new List<Trial>()),true)){
                var queue=(System.Collections.IEnumerable)type.GetField("queue",flags).GetValue(aim);var targets=queue.Cast<object>().ToList();
                Check(targets.Count==32&&targets.GroupBy(t=>(int)t.GetType().GetField("PairId").GetValue(t)).All(g=>g.Count()==2&&g.Select(t=>(bool)t.GetType().GetField("Corrected").GetValue(t)).Distinct().Count()==2),"Initial live A/B queue contains sixteen matched A/B pairs");
                type.GetMethod("BeginNext",flags).Invoke(aim,null);type.GetField("phase",flags).SetValue(aim,"flick");type.GetMethod("Finish",flags).Invoke(aim,new object[]{false,true});
                Check(aim.Results.Count==1&&aim.Results[0].Hit==false&&aim.Results[0].PairId>0&&(int)type.GetField("finished",flags).GetValue(aim)==1&&((System.Collections.IEnumerable)type.GetField("queue",flags).GetValue(aim)).Cast<object>().Count()==30,"A timed-out A/B attempt advances once and is never silently retried");
            }
            using(var aim=new AimForm(comparison(new List<Trial>()),true)){
                type.GetMethod("AddComparisonStage",flags).Invoke(aim,new object[]{1});type.GetMethod("AddComparisonStage",flags).Invoke(aim,new object[]{2});
                var targets=((System.Collections.IEnumerable)type.GetField("queue",flags).GetValue(aim)).Cast<object>().Select(t=>new{Direction=(int)t.GetType().GetField("Direction").GetValue(t),Pair=(int)t.GetType().GetField("PairId").GetValue(t),B=(bool)t.GetType().GetField("Corrected").GetValue(t),Radius=(double)t.GetType().GetField("Radius").GetValue(t),Angle=(double)t.GetType().GetField("Angle").GetValue(t)}).ToList();
                Check(targets.Count==64&&targets.GroupBy(t=>t.Pair).All(g=>g.Count()==2&&g.Select(t=>t.Radius).Distinct().Count()==1&&g.Select(t=>t.Angle).Distinct().Count()==1),"All extensions retain exactly matched target size, distance and direction");
                Check(targets.Take(48).GroupBy(t=>t.Direction).All(g=>g.Count(t=>t.B)==3&&g.Count(t=>!t.B)==3)&&targets.GroupBy(t=>t.Direction).All(g=>g.GroupBy(t=>t.Pair).Count(pair=>pair.First().B)==2),"Every checkpoint balances directions and the full plan counterbalances A/B order per direction");
            }
            using(var aim=new AimForm(comparison(new List<Trial>()),true)){
                aim.Results=PrecisionPairs(16,1.12,1.06);var queue=type.GetField("queue",flags).GetValue(aim);queue.GetType().GetMethod("Clear").Invoke(queue,null);type.GetField("finished",flags).SetValue(aim,32);type.GetMethod("BeginNext",flags).Invoke(aim,null);
                Check(aim.Completed&&aim.ComparisonGoal==32&&aim.Results.Count==32,"A clear result actually ends the live test at its first checkpoint");
            }
            using(var aim=new AimForm(comparison(new List<Trial>()),true)){
                aim.Results=PrecisionPairs(16,1.1,1.1);var queue=type.GetField("queue",flags).GetValue(aim);queue.GetType().GetMethod("Clear").Invoke(queue,null);type.GetField("finished",flags).SetValue(aim,32);type.GetMethod("BeginNext",flags).Invoke(aim,null);
                Check(!aim.Completed&&aim.ComparisonGoal==48&&((System.Collections.IEnumerable)queue).Cast<object>().Count()==15,"Unclear live A/B schedules exactly sixteen more attempts without an extra click");
            }
            using(var circle=new CircleForm(new Setup())){
                var ct=typeof(CircleForm);ct.GetField("index",flags).SetValue(circle,29);ct.GetField("tracker",flags).SetValue(circle,new CircleTracker(new CircleLap{Radius=12,Complete=true},12,0,0));ct.GetMethod("FinishLap",flags).Invoke(circle,null);
                var timer=(System.Diagnostics.Stopwatch)ct.GetField("clock",flags).GetValue(circle);double remaining=(double)ct.GetField("deadline",flags).GetValue(circle)-timer.Elapsed.TotalMilliseconds;
                Check((string)ct.GetField("phase",flags).GetValue(circle)=="countdown"&&remaining>2300&&(string)ct.GetField("transition",flags).GetValue(circle)=="Bytter dreieretning","Changing rotation inserts a visible 2.5-second unmeasured pause");
            }
            var demo=new Session{Baseline=session.Baseline,Validation=PrecisionPairs(16,1,1,1.25,1.05)};Analysis.Compute(demo);demo.Notes=Analysis.Validate(demo);Check(demo.ValidationPassed&&demo.ComparisonGoal==32,"Saved completion and progress use the actual early-stop count");
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(path),"precision-demo.vectorfit.json"),JObject.FromObject(demo).ToString());
            var historical=new List<object>();foreach(string file in Directory.GetFiles(Driver.Data,"*v08.vectorfit.json")){
                var previous=Session.FromJson(File.ReadAllText(file));var movement=previous.Baseline.Where(t=>t.Valid).Select(t=>new{Trial=t,Quality=Trajectory.Measure(t)}).ToList();
                historical.Add(new{Measurement=Path.GetFileName(file),AttemptCount=movement.Count,RecordedHits=movement.Count(t=>t.Trial.Hit==true),FirstStrokes=movement.Count(t=>t.Quality.PrimaryDetected),MedianPeakOvershoot=Analysis.Median(movement.Select(t=>t.Quality.Overshoot)),MedianCorrectionTravel=Analysis.Median(movement.Select(t=>t.Quality.CorrectionDistance)),MedianInitialReach=Analysis.Median(movement.Select(t=>t.Quality.PrimaryReach))});
            }
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(path),"precision-historical-diagnostics.json"),Newtonsoft.Json.JsonConvert.SerializeObject(historical,Newtonsoft.Json.Formatting.Indented));
        }
        static void TargetDistanceGoal(){
            Func<bool,Func<int,double>,List<Trial>> measurements=(corrected,error)=>{
                var trials=new List<Trial>();for(int rep=0;rep<4;rep++)for(int d=0;d<8;d++){
                    double x=15*Math.Cos(d*Math.PI/4),y=15*Math.Sin(d*Math.PI/4),reach=1+error(d);
                    var trial=Analysis.Extract(d,x,y,new List<Motion>{new Motion(0,0,0),new Motion(150,x*reach,y*reach)},450,corrected);
                    trial.Hit=false;trials.Add(trial);
                }return trials;
            };
            var session=new Session{RawAccelAdaptiveValidation=false,InterceptionAdaptiveValidation=false,TrajectoryFitting=false,ConservativeSensitivity=false,FitReady=true,Repeatable=true,CalibrateSensitivity=false};
            var a=measurements(false,d=>d%2==0?.12:-.12);
            session.Validation=a.Concat(measurements(true,d=>.125)).ToList();Analysis.Validate(session);
            Check(!session.ValidationPassed,"Better directional spread cannot approve clicks farther from the center");
            session.RequireCloserClicks=false;Analysis.Validate(session);Check(session.ValidationPassed,"Legacy assessment preserves the original directional criterion");
            session.RequireCloserClicks=true;session.Validation=a.Concat(measurements(true,d=>.12)).ToList();Analysis.Validate(session);
            Check(!session.ValidationPassed,"Unchanged target distance cannot be called an improvement");
            session.Validation=a.Concat(measurements(true,d=>.08)).ToList();Analysis.Validate(session);
            Check(session.ValidationPassed&&session.Validation.All(t=>t.Valid&&t.Hit==false),"Closer misses can pass A/B even when no click hits the green dot and sensitivity fitting is disabled");
            session.Validation=a.Concat(measurements(true,d=>.117)).ToList();Analysis.Validate(session);
            Check(!session.ValidationPassed,"A tiny distance reduction below five percent is not confirmed");
            session.Validation=a.Concat(measurements(true,d=>.08)).ToList();foreach(var t in session.Validation.Where(t=>t.Corrected))t.TotalMs=600;Analysis.Validate(session);
            Check(!session.ValidationPassed,"Slower clicks cannot pass merely by being closer");
            session.Validation=measurements(false,d=>.12).Concat(measurements(true,d=>d%2==0?.08:-.08)).ToList();Analysis.Validate(session);
            Check(!session.ValidationPassed,"Closer median distance still guards against much worse directional consistency");
            var old=new Session{RawAccelAdaptiveValidation=false,InterceptionAdaptiveValidation=false,TrajectoryFitting=false,ConservativeSensitivity=false,RequireCloserClicks=false,CalibrateSensitivity=true,Baseline=Synthetic(10,true,false)};Analysis.Compute(old);
            var json=JObject.FromObject(old);json["Version"]="0.6.0";json.Remove("RequireCloserClicks");var loaded=Session.FromJson(json.ToString());
            Check(!loaded.RequireCloserClicks&&loaded.CalibrateSensitivity&&loaded.Correction.Sensitivity==old.Correction.Sensitivity,"Version 0.6 keeps its sensitivity fitting and historic assessment");
            var next=loaded.NextRound("previous-v06.vectorfit.json");Check(next.RequireCloserClicks&&next.CalibrateSensitivity&&next.InputCorrection.Sensitivity==old.Correction.Sensitivity,"Existing calibrated profiles continue into a new round with the closer-click requirement");
            Check(Session.FromJson(JObject.FromObject(next).ToString()).RequireCloserClicks,"The new target-distance requirement survives save and reload");
            L.English=true;Check(L.T("klikkene er ikke minst 5 % nærmere målets sentrum")=="clicks are not at least 5% closer to the target center","Closer-click assessment is translated into English");L.English=false;
        }
    }
}


