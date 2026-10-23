using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace VectorFit {
    class AimForm:Form {
        CurveEngine curve;readonly Setup setup;Correction correction;readonly Correction inputCorrection;Correction hardwareCorrection;bool confirmation;bool validation;
        Session automaticSession,comparisonSession;int round=1,warmupFinished,calibrationGoal=80,circlesFinished,circleGoal=15,nextPair;bool adaptive,autoBegin,sequential,baselineOnly;
        bool rings;
        bool dualTask,precision,precisionMappingDone,precisionComparisonDone,introPending;
        int flickFinished,flickComparisonGoal=32;
        List<AimClick> clicks=new List<AimClick>();
        public List<Trial> PrecisionResults=new List<Trial>(),PrecisionValidationResults=new List<Trial>();
        bool[] extensionFirstB=new bool[8];
        double clickPulse=-1000,hitPulse=-1000;PointF hitPosition;
        public List<Trial> Results=new List<Trial>(),CalibrationResults=new List<Trial>();public string DeviceName="";public bool Completed,ValidationStarted;
        class Target {public int Direction,PairId;public double Radius,Angle;public bool Corrected,Warmup;public int Attempts;}
        Queue<Target> queue=new Queue<Target>();Target current;
        readonly Random random=new Random();readonly Stopwatch clock=Stopwatch.StartNew();
        readonly Timer timer=new Timer();List<Motion> motion=new List<Motion>();
        string phase="ready",feedback="Klikk for å starte";double deadline,t0,cameraX,cameraY;
        IntPtr selected=IntPtr.Zero;bool captured;int finished,goal;
        Font title=new Font("Segoe UI",28,FontStyle.Bold,GraphicsUnit.Pixel),body=new Font("Segoe UI",16,FontStyle.Regular,GraphicsUnit.Pixel),small=new Font("Segoe UI",13,FontStyle.Regular,GraphicsUnit.Pixel);
        public int CalibrationGoal {get{return calibrationGoal;}}
        public int ComparisonGoal {get{return validation?(precision||precisionComparisonDone?flickComparisonGoal:goal):sequential?32:64;}}
        public bool Sequential {get{return sequential;}}
        public AimForm(Session session,bool ab,bool integrated=false,bool startAutomatically=false):this(session.Setup,session.Correction,ab,session.ActiveCurve,session.InputCorrection) {
            rings=session.PrecisionFirst;hardwareCorrection=session.TestInputCorrection??session.InputCorrection;confirmation=session.FrozenProposal;dualTask=session.DualTask;round=session.Round;Text=L.T("VectorFit | Runde "+round+(ab?" · A/B: "+session.ModeName:" · Kalibrering og A/B"));
            adaptive=session.AdaptiveCalibration;autoBegin=startAutomatically&&!dualTask;calibrationGoal=adaptive?48:80;circleGoal=session.Circles.Goal;circlesFinished=ab?0:Math.Min(circleGoal,session.Circles.Laps.Count(session.Circles.Counts));
            sequential=session.AdaptiveValidation;baselineOnly=session.GuidedCalibration&&session.Round==1&&!ab;
            comparisonSession=new Session{FitReady=session.FitReady,Repeatable=session.Repeatable,Mode=session.Mode,AdaptiveValidation=sequential};
            PrepareTargets(ab);
            if(integrated&&!ab)automaticSession=new Session{PrecisionFirst=session.PrecisionFirst,DualTask=dualTask,Setup=session.Setup,Mode=session.Mode,Round=session.Round,CalibrateSensitivity=session.CalibrateSensitivity,RequireCloserClicks=session.RequireCloserClicks,AdaptiveCalibration=adaptive,AdaptiveValidation=sequential,TrajectoryFitting=session.TrajectoryFitting,ConservativeSensitivity=session.ConservativeSensitivity,Circles=session.Circles,InputCorrection=session.InputCorrection.Copy()};
        }
        public AimForm(Setup s,Correction c,bool ab,CurveProfile cp=null,Correction input=null) {
            setup=s;correction=c;inputCorrection=input??new Correction();hardwareCorrection=inputCorrection;validation=ab;curve=new CurveEngine(cp??new CurveProfile());Text=ab?"VectorFit | A/B-test":"VectorFit | Kalibrering";
            BackColor=Color.FromArgb(17,21,24);ForeColor=Color.White;DoubleBuffered=true;KeyPreview=true;
            AutoScaleMode=AutoScaleMode.None;WindowState=FormWindowState.Maximized;FormBorderStyle=FormBorderStyle.None;
            PrepareTargets(ab);
            timer.Interval=8;timer.Tick+=Tick;
            Shown+=delegate {CaptureInput();timer.Start();if(autoBegin)BeginNext();};
            Deactivate+=delegate {if(captured)Pause();};
            KeyDown+=delegate(object sender,KeyEventArgs e) {
                if(e.KeyCode==Keys.Escape){Close();e.Handled=true;}
                if(e.KeyCode==Keys.Space&&phase=="paused"){CaptureInput();if(dualTask)ShowIntro();else BeginNext();e.Handled=true;}
            };
            FormClosing+=delegate {if(dualTask&&phase=="flick"&&current!=null)Finish(false,false);};
            FormClosed+=delegate {timer.Stop();ReleaseInput();title.Dispose();body.Dispose();small.Dispose();};
            L.Controls(this);
        }
        void PrepareTargets(bool ab) {
            queue.Clear();finished=0;
            if(ab&&sequential){nextPair=0;goal=32;Array.Clear(extensionFirstB,0,extensionFirstB.Length);foreach(int d in Enumerable.Range(0,8).OrderBy(v=>random.Next()).Take(4))extensionFirstB[d]=true;AddComparisonStage(0);return;}
            if(!ab)for(int i=0;i<8;i++)queue.Enqueue(new Target{Direction=i,Radius=12,Angle=i*Math.PI/4,Warmup=true});
            // A/B blocks follow ABBA or BAAB. Each block covers all directions twice.
            bool flip=random.Next(2)==1;
            // Shared random geometry per A/B block: equal difficulty, shuffled order.
            var radii=new double[16];var angles=new double[16];
            for(int i=0;i<16;i++){radii[i]=8+random.NextDouble()*10;angles[i]=(i%8)*Math.PI/4+(random.NextDouble()-.5)*Math.PI/9;}
            int blocks=ab?4:calibrationGoal/8;
            for(int block=0;block<blocks;block++) {
                var temp=new List<Target>();
                for(int rep=0;rep<(ab?2:1);rep++)for(int d=0;d<8;d++)temp.Add(new Target{
                    Direction=d,Radius=ab?radii[rep*8+d]:8+random.NextDouble()*10,Angle=ab?angles[rep*8+d]:d*Math.PI/4+(random.NextDouble()-.5)*Math.PI/9,Corrected=ab&&(((block==1||block==2)^flip))});
                foreach(var t in temp.OrderBy(x=>random.Next()))queue.Enqueue(t);
            }
            goal=ab?64:calibrationGoal;
        }
        Target ComparisonTarget(int direction,int id,bool corrected,double radius,double angle){return new Target{Direction=direction,PairId=id,Corrected=corrected,Radius=radius,Angle=angle};}
        void AddComparisonStage(int stage){
            foreach(int d in Enumerable.Range(0,8).OrderBy(v=>random.Next())){
                int id=++nextPair;double radius=8+random.NextDouble()*10,angle=d*Math.PI/4+(random.NextDouble()-.5)*Math.PI/9;
                bool firstB=stage==0?random.Next(2)==1:stage==1?extensionFirstB[d]:!extensionFirstB[d];
                queue.Enqueue(ComparisonTarget(d,id,firstB,radius,angle));queue.Enqueue(ComparisonTarget(d,id,!firstB,radius,angle));
                if(stage==0){id=++nextPair;radius=8+random.NextDouble()*10;angle=d*Math.PI/4+(random.NextDouble()-.5)*Math.PI/9;queue.Enqueue(ComparisonTarget(d,id,!firstB,radius,angle));queue.Enqueue(ComparisonTarget(d,id,firstB,radius,angle));}
            }
        }
        bool ExtendComparison(){
            comparisonSession.Validation=Results;var report=SequentialComparison.Evaluate(comparisonSession);feedback=report.Note;
            if(report.Decision==ComparisonDecision.Continue&&goal<64){int stage=goal==32?1:2;goal+=16;AddComparisonStage(stage);return true;}
            Completed=report.Finished;return false;
        }
        bool BeginAutomaticValidation() {
            if(automaticSession==null||validation||finished!=goal)return false;
            automaticSession.Baseline=Results;Analysis.Compute(automaticSession);
            if(adaptive&&(!automaticSession.FitReady||!automaticSession.Repeatable)&&calibrationGoal<80){
                for(int block=0;block<2;block++)foreach(int d in Enumerable.Range(0,8).OrderBy(v=>random.Next()))queue.Enqueue(new Target{Direction=d,Radius=8+random.NextDouble()*10,Angle=d*Math.PI/4+(random.NextDouble()-.5)*Math.PI/9});
                calibrationGoal+=16;goal=calibrationGoal;feedback="Målingene varierer. Vi samler 16 ekstra klikk før sammenligningen.";return true;
            }
            if(dualTask&&!precisionMappingDone){StartPrecision(false);return true;}
            if(!automaticSession.FitReady)return false;
            if(baselineOnly)return false;
            CalibrationResults=Results;Results=new List<Trial>();ValidationStarted=validation=true;
            correction=automaticSession.Correction;curve=new CurveEngine(automaticSession.ActiveCurve);
            comparisonSession=automaticSession;
            PrepareTargets(true);if(dualTask)ShowIntro();feedback="Måledelen er ferdig. Nå sammenlignes måleprofilen med det nye forslaget. Fortsett å sikte og klikke.";
            return true;
        }
        void CaptureInput() {
            try {Native.Register(Handle,true);Cursor.Clip=RectangleToScreen(ClientRectangle);Cursor.Hide();captured=true;}
            catch(Exception e){UiMessage.Show(e.Message,"Kunne ikke lese musen");Close();}
        }
        void ReleaseInput() {
            if(!captured)return;captured=false;Cursor.Clip=Rectangle.Empty;Cursor.Show();try{Native.Register(Handle,false);}catch{}
        }
        void Pause() {ReleaseInput();if(current!=null&&phase=="flick"&&dualTask)Finish(false,false);else if(current!=null&&phase!="ready"){queue.Enqueue(current);current=null;}phase="paused";feedback="Pause. Trykk mellomrom for å fortsette.";Invalidate();}
        void Tick(object sender,EventArgs args) {
            if(phase=="countdown"&&clock.Elapsed.TotalMilliseconds>=deadline) {
                cameraX=cameraY=0;t0=clock.Elapsed.TotalMilliseconds;motion=new List<Motion>{new Motion(0,0,0)};clicks=new List<AimClick>();phase="flick";
            }
            if(phase=="flick"&&clock.Elapsed.TotalMilliseconds-t0>=PrecisionCheck.TimeLimit)Finish(false);

            Invalidate();
        }
        protected override void WndProc(ref Message m) {
            if(m.Msg==0xFF&&captured) {
                IntPtr device;int dx,dy;ushort buttons;bool absolute;
                if(Native.Read(m.LParam,out device,out dx,out dy,out buttons,out absolute)) {
                    if(absolute){feedback="Bruk en vanlig mus med relativ bevegelse.";base.WndProc(ref m);return;}
                    if(selected==IntPtr.Zero&&((buttons&1)!=0||(autoBegin&&setup.Device!=""))){
                        string name=Native.Name(device);
                        if(setup.Device!=""&&setup.Device!=name){feedback="Bruk samme mus og tilkobling som i kalibreringen.";base.WndProc(ref m);return;}
                        selected=device;DeviceName=name;
                    }
                    if(selected==device) {
                        if(phase=="flick") {
                            var raw=new PointD(dx*setup.DegreesPerCount,dy*setup.DegreesPerCount);
                            var p=TransformMovement(raw.X,raw.Y,clock.Elapsed.TotalMilliseconds,current.Corrected);
                            cameraX+=p.X;cameraY+=p.Y;
                            if(dx!=0||dy!=0)motion.Add(new Motion(clock.Elapsed.TotalMilliseconds-t0,cameraX,cameraY));
                        }
                        if((buttons&1)!=0)ClickRaw();
                    }
                }
            }
            base.WndProc(ref m);
        }
        PointD TransformMovement(double x,double y,double time,bool corrected) {
            if(!corrected&&Object.ReferenceEquals(hardwareCorrection,inputCorrection))return new PointD(x,y);
            var original=hardwareCorrection.Inverse(x,y);
            if(!corrected)return inputCorrection.Transform(original.X,original.Y);
            var bent=curve.Transform(original.X,original.Y,time);
            return correction.Transform(bent.X,bent.Y);
        }
        void ShowIntro(){phase="ready";introPending=true;Invalidate();}
        void StartPrecision(bool comparing){
            flickFinished=finished;if(comparing)flickComparisonGoal=goal;
            precision=true;Completed=false;queue.Clear();finished=0;goal=comparing?PrecisionCheck.ComparisonCount:PrecisionCheck.MeasurementCount;
            int id=0;
            for(int rep=0;rep<2;rep++)foreach(int d in Enumerable.Range(0,8).OrderBy(v=>random.Next())){
                // Fixed task geometry across phases; A/B receives identical target pairs.
                double radius=rep==0?8:14,angle=d*Math.PI/4;bool firstB=(d+rep)%2==0;
                if(comparing){id++;queue.Enqueue(ComparisonTarget(d,id,firstB,radius,angle));queue.Enqueue(ComparisonTarget(d,id,!firstB,radius,angle));}
                else queue.Enqueue(new Target{Direction=d,Radius=radius,Angle=angle});
            }
            ShowIntro();
        }
        void BeginNext() {
            if(precision&&queue.Count==0){
                precision=false;
                if(validation){precisionComparisonDone=true;Completed=true;Close();return;}
                precisionMappingDone=true;finished=flickFinished;goal=calibrationGoal;
            }
            if(queue.Count==0&&validation){
                if(sequential&&ExtendComparison()){}
                else {if(dualTask&&!precisionComparisonDone){StartPrecision(true);return;}Close();return;}
            }
            if(queue.Count==0&&!BeginAutomaticValidation()){Completed=finished==goal;Close();return;}
            if(introPending)return;
            current=queue.Dequeue();cameraX=cameraY=0;curve.Reset();
            phase="countdown";deadline=clock.Elapsed.TotalMilliseconds+150+random.Next(751);
        }
        void ClickRaw() {
            clickPulse=clock.Elapsed.TotalMilliseconds;
            if(phase=="ready"){introPending=false;BeginNext();}
            else if(phase=="flick"){
                double elapsed=clock.Elapsed.TotalMilliseconds-t0;
                if(elapsed>=PrecisionCheck.TimeLimit){Finish(false);return;}
                bool hit=HitsTarget(Project(Math.Cos(current.Angle)*current.Radius,Math.Sin(current.Angle)*current.Radius));
                clicks.Add(new AimClick(elapsed,cameraX,cameraY,hit));
                if(precision&&!hit){feedback="Bomklikk lagret. Korriger og treff samme mål.";return;}
                Finish(true);
            }
            // Shots before a target appears never consume or restart the next trial.
        }
        void Finish(bool clicked,bool advance=true) {
            double t=clock.Elapsed.TotalMilliseconds-t0;motion.Add(new Motion(t,cameraX,cameraY));
            double angle=current.Angle;
            var tr=Analysis.Extract(current.Direction,Math.Cos(angle)*current.Radius,Math.Sin(angle)*current.Radius,motion,t,current.Corrected);
            tr.Clicks=new List<AimClick>(clicks);tr.PairId=current.PairId;double focal=ClientSize.Width/(2*Math.Tan(setup.Hfov*Math.PI/360));tr.HitRadiusDegrees=Math.Atan(TargetRadius/focal)*180/Math.PI;
            if(!clicked){tr.Valid=false;tr.Hit=false;tr.Rejection="Tiden gikk ut";}
            else {
                var point=Project(tr.TargetX,tr.TargetY);tr.Hit=HitsTarget(point);
                if(tr.Hit==true){hitPulse=clock.Elapsed.TotalMilliseconds;hitPosition=point;}
            }

            if(rings&&!precision)tr.FlickScore=clicked?FlickScoring.Score(AimGeometry.Offset(tr.TargetX,tr.TargetY,cameraX,cameraY).Length/Math.Tan(tr.HitRadiusDegrees*Math.PI/180)):0;
            if(precision){
                if(!clicked)tr.TotalMs=PrecisionCheck.TimeLimit;
                (validation?PrecisionValidationResults:PrecisionResults).Add(tr);finished++;
                feedback=tr.Hit==true?"Treff lagret":"Tidsgrensen er nådd. Målet er lagret som mislykket.";
            }
            else if(current.Warmup){if(clicked)warmupFinished++;else queue=new Queue<Target>(new[]{current}.Concat(queue));feedback="Øvelse. Flick og klikk naturlig, også når du bommer.";}
            else {
                Results.Add(tr);
                if(dualTask||validation&&sequential){finished++;feedback="Forsøket er lagret, også ved bom eller tidsavbrudd.";}
                else if(clicked){finished++;feedback="Klikk og bevegelse lagret"+(tr.Valid?". Avvik: "+(tr.ClickError*100).ToString("F1")+" % av målavstanden.":". "+tr.Rejection);}
                else {feedback=tr.Rejection+". Forsøket gjentas.";if(++current.Attempts<6)queue.Enqueue(current);}
            }
            bool practiceEnded=dualTask&&current.Warmup&&warmupFinished==8;current=null;if(!advance)return;if(practiceEnded){ShowIntro();return;}BeginNext();
        }
        float TargetRadius {get {return (float)Math.Max(5,ClientSize.Width/(2*Math.Tan(setup.Hfov*Math.PI/360))*Math.Tan((precision?.35:.65)*Math.PI/180));}}
        bool HitsTarget(PointF p) {double dx=p.X-ClientSize.Width/2,dy=p.Y-ClientSize.Height/2;return dx*dx+dy*dy<=TargetRadius*TargetRadius;}
        public CalibrationProgress Progress {get {
            bool integrated=automaticSession!=null;
            return new CalibrationProgress{IncludePrecision=dualTask,PrecisionMeasured=PrecisionResults.Count,PrecisionCompared=PrecisionValidationResults.Count,Round=round,Warmup=warmupFinished,Measured=validation?(integrated?calibrationGoal:0):precision?flickFinished:finished,MeasurementGoal=calibrationGoal,Circles=circlesFinished,CircleGoal=circleGoal,
                Compared=validation?(precision||precisionComparisonDone?flickFinished:finished):0,ComparisonGoal=ComparisonGoal,IncludeWarmup=!validation||integrated,IncludeBaseline=!validation||integrated,IncludeComparison=!baselineOnly&&(validation||integrated),
                Stage=phase=="paused"?"Pause":precision?"Presisjon · "+finished+" / "+goal:validation?"Kontrollerer forslaget":warmupFinished<8?"Øvelse":baselineOnly?"Baseline · datainnhenting":"Datainnhenting med faseprofil"};
        }}
        void DrawEffects(Graphics g) {
            double now=clock.Elapsed.TotalMilliseconds,age=now-clickPulse;
            if(age>=0&&age<200){float radius=10+(float)(age*.10);using(var pen=new Pen(Color.FromArgb((int)(200*(1-age/200)),232,240,244),2))g.DrawEllipse(pen,ClientSize.Width/2-radius,ClientSize.Height/2-radius,2*radius,2*radius);}
            age=now-hitPulse;
            if(age>=0&&age<300){float radius=TargetRadius+7+(float)(age*.11);using(var pen=new Pen(Color.FromArgb((int)(230*(1-age/300)),109,218,182),2.5f)){
                g.DrawEllipse(pen,hitPosition.X-radius,hitPosition.Y-radius,2*radius,2*radius);
                for(int i=0;i<8;i++){double a=i*Math.PI/4;g.DrawLine(pen,hitPosition.X+(float)Math.Cos(a)*radius,hitPosition.Y+(float)Math.Sin(a)*radius,hitPosition.X+(float)Math.Cos(a)*(radius+7),hitPosition.Y+(float)Math.Sin(a)*(radius+7));}
            }}
        }
#if DEVELOPMENT
        public void PreviewRings(){WindowState=FormWindowState.Normal;Size=new Size(1280,800);rings=true;warmupFinished=8;current=new Target{Angle=0,Radius=10};phase="flick";}
        public void Preview(bool hit){WindowState=FormWindowState.Normal;Size=new Size(1280,800);warmupFinished=8;finished=24;phase="countdown";clickPulse=clock.Elapsed.TotalMilliseconds;if(hit){hitPulse=clickPulse;hitPosition=new PointF(Width/2+3,Height/2+2);}}
#endif
        PointF Project(double yaw,double pitch) {
            var offset=AimGeometry.Offset(yaw,pitch,cameraX,cameraY);double f=ClientSize.Width/(2*Math.Tan(setup.Hfov*Math.PI/360));return new PointF((float)(ClientSize.Width/2+f*offset.X),(float)(ClientSize.Height/2+f*offset.Y));
        }
        string PartTitle {get{return precision?"DEL 2 · PRESISJON":!validation&&warmupFinished<8?"ØVELSE · FLICK":"DEL 1 · FLICK";}}
        void DrawInstructions(Graphics g){
            float width=Math.Min(900,ClientSize.Width-80),left=(ClientSize.Width-width)/2f,top=ClientSize.Height/2f-170;
            using(var bg=new SolidBrush(BackColor))g.FillRectangle(bg,left-20,top-20,width+40,340);
            using(var sf=new StringFormat{Alignment=StringAlignment.Center}){
                Action<string,Font,Brush,float,float> line=(text,font,brush,y,h)=>g.DrawString(L.T(text),font,brush,new RectangleF(left,y,width,h),sf);
                line(confirmation?"BEKREFTELSE · SAMME A OG B":"FASE "+round+(validation?" · A/B-KONTROLL":baselineOnly?" · BASELINE":" · MÅLING MED FASEPROFIL"),body,Brushes.White,top,40);
                line(PartTitle,title,Brushes.White,top+46,55);
                line(precision?"Treff den grønne prikken så raskt du klarer med kontroll.":"Sikt raskt mot den grønne prikken og klikk én gang.",body,Brushes.White,top+112,52);
                line(precision?"Ved bom: korriger og klikk igjen på samme mål. Klokken fortsetter.":"Første klikk går videre, også ved bom. Ikke vent for å finjustere.",body,Brushes.White,top+164,54);
                line("Maks 3 sekunder per mål. Både tid og treff teller.",small,Brushes.White,top+224,30);
                line("Klikk for å starte denne delen",body,Brushes.White,top+270,38);
            }
        }
#if DEVELOPMENT
        public void PreviewInstructions(bool precise){WindowState=FormWindowState.Normal;Size=new Size(1280,800);dualTask=true;round=2;warmupFinished=8;precision=precise;phase="ready";}
#endif
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            using(var grid=new SolidBrush(Color.FromArgb(40,49,54)))
                for(int x=-48;x<=48;x+=6)for(int y=-30;y<=30;y+=6){var p=Project(x,y);if(p.X>0&&p.X<Width&&p.Y>0&&p.Y<Height)g.FillEllipse(grid,p.X-1,p.Y-1,2,2);}
            int cx=ClientSize.Width/2,cy=ClientSize.Height/2;
            using(var pen=new Pen(Color.White,1.5f)){g.DrawLine(pen,cx-7,cy,cx-2,cy);g.DrawLine(pen,cx+2,cy,cx+7,cy);g.DrawLine(pen,cx,cy-7,cx,cy-2);g.DrawLine(pen,cx,cy+2,cx,cy+7);}
            using(var muted=new SolidBrush(Color.FromArgb(164,179,185))) {
                PaintText.Draw(g,"FASE "+round+(validation?" · KONTROLLERER FORSLAGET":baselineOnly?" · BASELINE / DATAINNHENTING":" · DATAINNHENTING MED FASEPROFIL"),small,muted,32,24);
                PaintText.Draw(g,finished+" / "+goal+(precision?" mål":validation&&sequential?" registrerte forsøk":" registrerte klikk"),body,Brushes.White,32,49);
                if(validation&&sequential)PaintText.Draw(g,"A/B vurderes etter 32, 48 og høyst 64 forsøk. Tydelige resultater stopper testen tidligere.",small,muted,32,80);
                if(!validation&&adaptive&&calibrationGoal>48)PaintText.Draw(g,"Målingene varierer. Vi samler 16 ekstra klikk før sammenligningen.",small,muted,32,104);
                PaintText.Draw(g,"Esc avslutter  ·  Alt+Tab setter på pause",small,muted,32,Height-30);
            }
            if(phase=="flick"&&current!=null) {
                double a=current.Angle;var p=Project(Math.Cos(a)*current.Radius,Math.Sin(a)*current.Radius);
                float radius=TargetRadius;
                if(rings&&!precision)for(int ring=5;ring>=2;ring--){float rr=radius*ring;using(var fill=new SolidBrush(Color.FromArgb(32+ring*7,64,114,126)))g.FillEllipse(fill,p.X-rr,p.Y-rr,rr*2,rr*2);using(var line=new Pen(Color.FromArgb(150,153,193,199),1))g.DrawEllipse(line,p.X-rr,p.Y-rr,rr*2,rr*2);}

                using(var brush=new SolidBrush(Color.FromArgb(109,218,182)))g.FillEllipse(brush,p.X-radius,p.Y-radius,2*radius,2*radius);
                using(var pen=new Pen(Color.FromArgb(160,109,218,182),1))g.DrawEllipse(pen,p.X-radius-5,p.Y-radius-5,2*radius+10,2*radius+10);
            }else {
                string text=L.T(dualTask&&phase=="ready"?"":phase=="countdown"?"":phase=="ready"?"Klikk når du er klar":feedback);
                SizeF size=g.MeasureString(text,body);PaintText.Draw(g,text,body,Brushes.White,cx-size.Width/2,cy+64);
                if(phase=="ready"&&!dualTask) {
                    string sub=L.T(finished==0?"Klikk én gang for å starte. Deretter ett klikk per mål, treff eller bom.":feedback);
                    var sz=g.MeasureString(sub,small);using(var brush=new SolidBrush(Color.FromArgb(164,179,185)))PaintText.Draw(g,sub,small,brush,cx-sz.Width/2,cy+98);
                }
            }
            if(rings&&!precision)PaintText.Draw(g,DriverSetup.Text("Bullseye 10 · ringer 8 / 6 / 4 / 2 · bom 0", "Bullseye 10 · rings 8 / 6 / 4 / 2 · miss 0")+(Results.Count>0&&Results.Last().FlickScore.HasValue?"  |  "+DriverSetup.Text("Siste: ","Last: ")+Results.Last().FlickScore+" / 10":""),small,Brushes.White,32,Height-55);
            if(current!=null&&current.Warmup)PaintText.Draw(g,"Øvelsesrunde: lagres ikke",body,Brushes.White,32,80);
            if(dualTask){if(phase=="ready")DrawInstructions(g);else using(var b=new SolidBrush(Color.FromArgb(109,218,182)))PaintText.Draw(g,PartTitle,body,b,32,130);}
            DrawEffects(g);
            var progress=Progress;
            using(var track=new SolidBrush(Color.FromArgb(40,49,54)))g.FillRectangle(track,32,Height-55,Width-64,5);
            using(var fill=new SolidBrush(Color.FromArgb(109,218,182)))g.FillRectangle(fill,32,Height-55,(float)((Width-64)*progress.Fraction),5);
            using(var brush=new SolidBrush(Color.FromArgb(164,179,185)))PaintText.Draw(g,progress.Label,small,brush,32,Height-82);
        }
    }
}
