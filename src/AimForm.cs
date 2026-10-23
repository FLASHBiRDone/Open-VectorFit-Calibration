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
        bool rings,balanced,largeTargets;
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
        double introShown,introExit;
        IntPtr selected=IntPtr.Zero;bool captured;int finished,goal;
        Font title=new Font("Segoe UI",28,FontStyle.Bold,GraphicsUnit.Pixel),body=new Font("Segoe UI",16,FontStyle.Regular,GraphicsUnit.Pixel),small=new Font("Segoe UI",13,FontStyle.Regular,GraphicsUnit.Pixel);
        public int CalibrationGoal {get{return calibrationGoal;}}
        public int ComparisonGoal {get{return validation?(precision||precisionComparisonDone?flickComparisonGoal:goal):sequential?32:64;}}
        public bool Sequential {get{return sequential;}}
        public AimForm(Session session,bool ab,bool integrated=false,bool startAutomatically=false):this(session.Setup,session.Correction,ab,session.ActiveCurve,session.InputCorrection) {
            largeTargets=session.SmallGainProtocol;balanced=session.BalancedTargets;rings=session.PrecisionFirst;hardwareCorrection=session.TestInputCorrection??session.InputCorrection;confirmation=session.FrozenProposal;dualTask=session.DualTask;round=session.Round;Text=L.T("VectorFit | Runde "+round+(ab?" · A/B: "+session.ModeName:" · Kalibrering og A/B"));
            adaptive=session.AdaptiveCalibration;autoBegin=startAutomatically&&!dualTask;calibrationGoal=adaptive?48:80;circleGoal=session.Circles.Goal;circlesFinished=ab?0:Math.Min(circleGoal,session.Circles.Laps.Count(session.Circles.Counts));
            sequential=session.AdaptiveValidation;baselineOnly=session.GuidedCalibration&&session.Round==1&&!ab;
            comparisonSession=new Session{FitReady=session.FitReady,Repeatable=session.Repeatable,Mode=session.Mode,AdaptiveValidation=sequential};
            PrepareTargets(ab);
            if(integrated&&!ab)automaticSession=new Session{SmallGainProtocol=session.SmallGainProtocol,ControlPriority=session.ControlPriority,BalancedTargets=session.BalancedTargets,PrecisionFirst=session.PrecisionFirst,DualTask=dualTask,Setup=session.Setup,Mode=session.Mode,Round=session.Round,CalibrateSensitivity=session.CalibrateSensitivity,RequireCloserClicks=session.RequireCloserClicks,AdaptiveCalibration=adaptive,AdaptiveValidation=sequential,TrajectoryFitting=session.TrajectoryFitting,ConservativeSensitivity=session.ConservativeSensitivity,Circles=session.Circles,InputCorrection=session.InputCorrection.Copy()};
        }
        public AimForm(Setup s,Correction c,bool ab,CurveProfile cp=null,Correction input=null) {
            setup=s;correction=c;inputCorrection=input??new Correction();hardwareCorrection=inputCorrection;validation=ab;curve=new CurveEngine(cp??new CurveProfile());Text=ab?"VectorFit | A/B-test":"VectorFit | Kalibrering";
            BackColor=Color.FromArgb(17,21,24);ForeColor=Color.White;DoubleBuffered=true;KeyPreview=true;
            AutoScaleMode=AutoScaleMode.None;WindowState=FormWindowState.Maximized;FormBorderStyle=FormBorderStyle.None;
            PrepareTargets(ab);
            timer.Interval=8;timer.Tick+=Tick;
            Shown+=delegate {introShown=clock.Elapsed.TotalMilliseconds;CaptureInput();timer.Start();if(autoBegin)BeginNext();};
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
                    Direction=d,Radius=balanced?BalancedRadius(ab?rep:block,d):ab?radii[rep*8+d]:8+random.NextDouble()*10,Angle=balanced?d*Math.PI/4:ab?angles[rep*8+d]:d*Math.PI/4+(random.NextDouble()-.5)*Math.PI/9,Corrected=ab&&(((block==1||block==2)^flip))});
                foreach(var t in temp.OrderBy(x=>random.Next()))queue.Enqueue(t);
            }
            goal=ab?64:calibrationGoal;
        }
        internal static double BalancedRadius(int block,int direction){return (block+direction)%2==0?8:18;}
        Target ComparisonTarget(int direction,int id,bool corrected,double radius,double angle){return new Target{Direction=direction,PairId=id,Corrected=corrected,Radius=radius,Angle=angle};}
        void AddComparisonStage(int stage){
            foreach(int d in Enumerable.Range(0,8).OrderBy(v=>random.Next())){
                int id=++nextPair;double radius=balanced?BalancedRadius(stage==2?1:0,d):8+random.NextDouble()*10,angle=balanced?d*Math.PI/4:d*Math.PI/4+(random.NextDouble()-.5)*Math.PI/9;
                bool firstB=stage==0?random.Next(2)==1:stage==1?extensionFirstB[d]:!extensionFirstB[d];
                queue.Enqueue(ComparisonTarget(d,id,firstB,radius,angle));queue.Enqueue(ComparisonTarget(d,id,!firstB,radius,angle));
                if(stage==0){id=++nextPair;radius=balanced?BalancedRadius(1,d):8+random.NextDouble()*10;angle=balanced?d*Math.PI/4:d*Math.PI/4+(random.NextDouble()-.5)*Math.PI/9;queue.Enqueue(ComparisonTarget(d,id,!firstB,radius,angle));queue.Enqueue(ComparisonTarget(d,id,firstB,radius,angle));}
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
                for(int block=0;block<2;block++)foreach(int d in Enumerable.Range(0,8).OrderBy(v=>random.Next()))queue.Enqueue(new Target{Direction=d,Radius=balanced?BalancedRadius(block,d):8+random.NextDouble()*10,Angle=balanced?d*Math.PI/4:d*Math.PI/4+(random.NextDouble()-.5)*Math.PI/9});
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
            if(phase=="intro-out"&&clock.Elapsed.TotalMilliseconds-introExit>=280){introPending=false;BeginNext();}
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
        void ShowIntro(){introShown=clock.Elapsed.TotalMilliseconds;phase="ready";introPending=true;Invalidate();}
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
            if(phase=="ready"){if(dualTask){introExit=clock.Elapsed.TotalMilliseconds;phase="intro-out";}else{introPending=false;BeginNext();}}
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

            if(rings&&!precision)tr.FlickScore=clicked?(largeTargets?ScoreFlick(AimGeometry.Offset(tr.TargetX,tr.TargetY,cameraX,cameraY).Length/Math.Tan(tr.HitRadiusDegrees*Math.PI/180)):FlickScoring.Score(AimGeometry.Offset(tr.TargetX,tr.TargetY,cameraX,cameraY).Length/Math.Tan(tr.HitRadiusDegrees*Math.PI/180))):0;
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
        int ScoreFlick(double radii){return setup.WideFlick?FlickScoring.WideScore(radii):FlickScoring.Score100(radii);}
        float TargetRadius {get {return (float)Math.Max(!precision&&largeTargets&&setup.WideFlick?3:5,ClientSize.Width/(2*Math.Tan(setup.Hfov*Math.PI/360))*Math.Tan((precision?.35:largeTargets?(setup.WideFlick?.35:.8):.65)*Math.PI/180));}}
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
        public void PreviewCamera(double x,double y){cameraX=x;cameraY=y;}
        public void Preview(bool hit){WindowState=FormWindowState.Normal;Size=new Size(1280,800);warmupFinished=8;finished=24;phase="countdown";clickPulse=clock.Elapsed.TotalMilliseconds;if(hit){hitPulse=clickPulse;hitPosition=new PointF(Width/2+3,Height/2+2);}}
#endif
        PointF Project(double yaw,double pitch) {
            var offset=AimGeometry.Offset(yaw,pitch,cameraX,cameraY);double f=ClientSize.Width/(2*Math.Tan(setup.Hfov*Math.PI/360));return new PointF((float)(ClientSize.Width/2+f*offset.X),(float)(ClientSize.Height/2+f*offset.Y));
        }
        string PartTitle {get{return precision?"DEL 2 · PRESISJON":!validation&&warmupFinished<8?"ØVELSE · FLICK":"DEL 1 · FLICK";}}
        void DrawInstructions(Graphics g){
            float width=Math.Min(1000,ClientSize.Width-80),left=(ClientSize.Width-width)/2f,top=ClientSize.Height/2f-225;
            using(var instructionFont=new Font("Segoe UI",24,FontStyle.Regular,GraphicsUnit.Pixel))
            using(var detailFont=new Font("Segoe UI",18,FontStyle.Regular,GraphicsUnit.Pixel))
            using(var headingFont=new Font("Segoe UI",36,FontStyle.Bold,GraphicsUnit.Pixel)){
            using(var bg=new SolidBrush(BackColor))g.FillRectangle(bg,left-20,top-20,width+40,445);
            using(var sf=new StringFormat{Alignment=StringAlignment.Center}){
                Action<string,Font,Brush,float,float> line=(text,font,brush,y,h)=>g.DrawString(L.T(text),font,brush,new RectangleF(left,y,width,h),sf);
                line(confirmation?"BEKREFTELSE · SAMME A OG B":"FASE "+round+(validation?" · A/B-KONTROLL":baselineOnly?" · BASELINE":" · MÅLING MED FASEPROFIL"),instructionFont,Brushes.White,top,40);
                line(PartTitle,headingFont,Brushes.White,top+46,55);
                line(precision?"Treff den grønne prikken så raskt du klarer med kontroll.":DriverSetup.Text("Sikt raskt mot det røde sentrumet og klikk én gang.","Aim quickly at the red centre and click once."),instructionFont,Brushes.White,top+112,52);
                line(precision?"Ved bom: korriger og klikk igjen på samme mål. Klokken fortsetter.":"Første klikk går videre, også ved bom. Ikke vent for å finjustere.",instructionFont,Brushes.White,top+172,68);
                line("Maks 3 sekunder per mål. Både tid og treff teller.",detailFont,Brushes.White,top+250,40);
                line(DriverSetup.Text("Plasser musen midt på underlaget, med plass i alle retninger. Finn en behagelig håndstilling.","Center your mouse on the pad, with room in every direction. Find a comfortable hand position."),detailFont,Brushes.White,top+294,58);
                if(setup.Arena){using(var accent=new SolidBrush(Color.FromArgb(109,218,182)))g.FillRectangle(accent,left+width/2-165,top+366,330,48);line("Klikk for å starte denne delen",instructionFont,Brushes.Black,top+374,38);}
                else line("Klikk for å starte denne delen",instructionFont,Brushes.White,top+374,38);
            }
        }
        }
        void DrawFadingInstructions(Graphics g){
            double now=clock.Elapsed.TotalMilliseconds;
            float opacity=(float)Analysis.Clamp(phase=="intro-out"?1-(now-introExit)/280:(now-introShown)/360,0,1);
            opacity=opacity*opacity*(3-2*opacity);
            using(var layer=new Bitmap(ClientSize.Width,ClientSize.Height))
            using(var canvas=Graphics.FromImage(layer))
            using(var attributes=new System.Drawing.Imaging.ImageAttributes()){
                canvas.SmoothingMode=SmoothingMode.AntiAlias;DrawInstructions(canvas);
                var matrix=new System.Drawing.Imaging.ColorMatrix();matrix.Matrix33=opacity;attributes.SetColorMatrix(matrix);
                g.DrawImage(layer,ClientRectangle,0,0,layer.Width,layer.Height,GraphicsUnit.Pixel,attributes);
            }
        }
#if DEVELOPMENT
        public void PreviewInstructions(bool precise){WindowState=FormWindowState.Normal;Size=new Size(1280,800);dualTask=true;round=2;warmupFinished=8;precision=precise;phase="ready";introShown=-1000;}
#endif
        void DrawArenaHud(Graphics g){
            int w=ClientSize.Width,h=ClientSize.Height;
            using(var background=new SolidBrush(Color.FromArgb(235,19,24,27))) {g.FillRectangle(background,0,0,w,92);g.FillRectangle(background,0,h-94,w,94);}
            using(var muted=new SolidBrush(Color.FromArgb(169,184,188)))using(var mint=new SolidBrush(Color.FromArgb(109,218,182)))using(var heading=new Font("Segoe UI",20,FontStyle.Bold,GraphicsUnit.Pixel)) {
                g.DrawString("TRUETRACK",heading,Brushes.White,30,18);
                g.DrawString(setup.DottedCalibration?DriverSetup.Text("KALIBRERING","CALIBRATION"):DriverSetup.Text("TRENINGSSAL / 01","TRAINING ROOM / 01"),small,muted,30,51);
                string task=precision?DriverSetup.Text("PRESISJON","PRECISION"):"FLICK";
                using(var center=new StringFormat{Alignment=StringAlignment.Center}){
                    g.DrawString(task,heading,mint,new RectangleF(w/2-170,17,340,30),center);
                    g.DrawString(DriverSetup.Text("Runde ","Round ")+round+"  /  "+(confirmation?DriverSetup.Text("Bekreftelse","Confirmation"):validation?DriverSetup.Text("Sammenligning","Comparison"):baselineOnly?"Baseline":DriverSetup.Text("Kartlegging","Mapping")),small,muted,new RectangleF(w/2-170,50,340,25),center);
                }
                using(var right=new StringFormat{Alignment=StringAlignment.Far}){
                    g.DrawString(finished+" / "+goal,heading,Brushes.White,new RectangleF(w-250,17,220,30),right);
                    g.DrawString(current!=null&&current.Warmup?DriverSetup.Text("Øvelse · lagres ikke","Warm-up · not saved"):DriverSetup.Text("Registrerte forsøk","Recorded attempts"),small,muted,new RectangleF(w-290,50,260,25),right);
                }
                string instruction=precision?DriverSetup.Text("Treff med kontroll. Ved bom fortsetter klokken.","Hit with control. Misses keep the clock running."):DriverSetup.Text("Ett klikk per mål · sentrum 100 · ytterkant 1 · bom 0","One click per target · center 100 · outer edge 1 · miss 0");
                if(!largeTargets&&!precision)instruction=DriverSetup.Text("Ett klikk per mål · også ved bom","One click per target · including misses");
                g.DrawString(instruction,body,Brushes.White,30,h-83);
                var progress=Progress;g.DrawString(progress.Label,small,muted,30,h-56);
                using(var right=new StringFormat{Alignment=StringAlignment.Far})g.DrawString("Esc · "+DriverSetup.Text("Avslutt","Exit")+"   /   Alt+Tab · "+DriverSetup.Text("Pause","Pause"),small,muted,new RectangleF(w-330,h-29,300,22),right);
                ProgressStrip.DrawBar(g,new RectangleF(30,h-38,w-390,32),progress.Fraction,progress.Done+" / "+progress.Total);
            }
        }
        static readonly PointD[] calibrationDots=MakeCalibrationDots();
        static PointD[] MakeCalibrationDots(){var rng=new Random(8147);return Enumerable.Range(0,900).Select(i=>new PointD(rng.NextDouble()*180-90,rng.NextDouble()*120-60)).ToArray();}
        void DrawCalibrationDots(Graphics g){
            // Uniform, low-contrast dots: no grid, walls, horizon or unique landmarks.
            using(var brush=new SolidBrush(Color.FromArgb(51,64,67)))foreach(var dot in calibrationDots){var p=Project(dot.X,dot.Y);if(p.X>=0&&p.X<ClientSize.Width&&p.Y>=0&&p.Y<ClientSize.Height)g.FillEllipse(brush,p.X-1.3f,p.Y-1.3f,2.6f,2.6f);}
        }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            if(setup.DottedCalibration)DrawCalibrationDots(g);
            else if(setup.Arena)ArenaScene.Draw(g,ClientSize,setup.Hfov,cameraX,cameraY);
            else using(var grid=new SolidBrush(Color.FromArgb(40,49,54)))
                for(int x=-48;x<=48;x+=6)for(int y=-30;y<=30;y+=6){var p=Project(x,y);if(p.X>0&&p.X<Width&&p.Y>0&&p.Y<Height)g.FillEllipse(grid,p.X-1,p.Y-1,2,2);}
            int cx=ClientSize.Width/2,cy=ClientSize.Height/2;
            Sight.Draw(g,cx,cy,setup.Crosshair);
            if(!setup.Arena)using(var muted=new SolidBrush(Color.FromArgb(164,179,185))) {
                PaintText.Draw(g,"FASE "+round+(validation?" · KONTROLLERER FORSLAGET":baselineOnly?" · BASELINE / DATAINNHENTING":" · DATAINNHENTING MED FASEPROFIL"),small,muted,32,24);
                PaintText.Draw(g,finished+" / "+goal+(precision?" mål":validation&&sequential?" registrerte forsøk":" registrerte klikk"),body,Brushes.White,32,49);
                if(validation&&sequential)PaintText.Draw(g,"A/B vurderes etter 32, 48 og høyst 64 forsøk. Tydelige resultater stopper testen tidligere.",small,muted,32,80);
                if(!validation&&adaptive&&calibrationGoal>48)PaintText.Draw(g,"Målingene varierer. Vi samler 16 ekstra klikk før sammenligningen.",small,muted,32,104);
                PaintText.Draw(g,"Esc avslutter  ·  Alt+Tab setter på pause",small,muted,32,Height-30);
            }
            if(phase=="flick"&&current!=null) {
                double a=current.Angle;var p=Project(Math.Cos(a)*current.Radius,Math.Sin(a)*current.Radius);
                float radius=TargetRadius;
                if(rings&&!precision)for(int ring=5;ring>=2;ring--){float rr=radius*(largeTargets&&setup.WideFlick?1+19f*(ring-1)/4: ring);using(var fill=new SolidBrush(Color.FromArgb(32+ring*7,64,114,126)))g.FillEllipse(fill,p.X-rr,p.Y-rr,rr*2,rr*2);using(var line=new Pen(Color.FromArgb(150,153,193,199),1))g.DrawEllipse(line,p.X-rr,p.Y-rr,rr*2,rr*2);}

                using(var brush=new SolidBrush(precision?Color.FromArgb(109,218,182):Color.FromArgb(255,82,91)))g.FillEllipse(brush,p.X-radius,p.Y-radius,2*radius,2*radius);
                using(var pen=new Pen(Color.FromArgb(160,109,218,182),1))g.DrawEllipse(pen,p.X-radius-5,p.Y-radius-5,2*radius+10,2*radius+10);
            }else {
                string text=L.T(dualTask&&(phase=="ready"||phase=="intro-out")?"":phase=="countdown"?"":phase=="ready"?"Klikk når du er klar":feedback);
                SizeF size=g.MeasureString(text,body);PaintText.Draw(g,text,body,Brushes.White,cx-size.Width/2,cy+64);
                if(phase=="ready"&&!dualTask) {
                    string sub=L.T(finished==0?"Klikk én gang for å starte. Deretter ett klikk per mål, treff eller bom.":feedback);
                    var sz=g.MeasureString(sub,small);using(var brush=new SolidBrush(Color.FromArgb(164,179,185)))PaintText.Draw(g,sub,small,brush,cx-sz.Width/2,cy+98);
                }
            }
            if(!setup.Arena&&rings&&!precision)PaintText.Draw(g,(largeTargets?DriverSetup.Text("Sentrum 100 · poengområde 1–100 · bom 0","Center 100 · scoring area 1–100 · miss 0"):DriverSetup.Text("Bullseye 10 · ringer 8 / 6 / 4 / 2 · bom 0", "Bullseye 10 · rings 8 / 6 / 4 / 2 · miss 0"))+(Results.Count>0&&Results.Last().FlickScore.HasValue?"  |  "+DriverSetup.Text("Siste: ","Last: ")+Results.Last().FlickScore+(largeTargets?" / 100":" / 10"):""),small,Brushes.White,32,Height-108);
            if(!setup.Arena&&current!=null&&current.Warmup)PaintText.Draw(g,"Øvelsesrunde: lagres ikke",body,Brushes.White,32,80);
            if(dualTask){if(phase=="ready"||phase=="intro-out")DrawFadingInstructions(g);else if(!setup.Arena)using(var b=new SolidBrush(Color.FromArgb(109,218,182)))PaintText.Draw(g,PartTitle,body,b,32,130);}
            DrawEffects(g);
            if(setup.Arena){DrawArenaHud(g);return;}
            var progress=Progress;
            ProgressStrip.DrawBar(g,new RectangleF(32,Height-65,Width-64,32),progress.Fraction,progress.Done+" / "+progress.Total);
            using(var brush=new SolidBrush(Color.FromArgb(164,179,185)))PaintText.Draw(g,progress.Label,small,brush,32,Height-82);
        }
    }
}
namespace VectorFit {
    // Original room geometry. Positive Y points down, matching AimGeometry.
    static class ArenaScene {
        internal struct Vertex {
            public double X,Y,Z;
            public Vertex(double x,double y,double z){X=x;Y=y;Z=z;}
        }
        internal static Vertex Camera(Vertex v,double yaw,double pitch){
            double r=System.Math.PI/180,cy=System.Math.Cos(yaw*r),sy=System.Math.Sin(yaw*r),cp=System.Math.Cos(pitch*r),sp=System.Math.Sin(pitch*r);
            double x=v.X*cy-v.Z*sy,z=v.X*sy+v.Z*cy;
            return new Vertex(x,v.Y*cp-z*sp,v.Y*sp+z*cp);
        }
        internal static System.Collections.Generic.List<Vertex> Clip(System.Collections.Generic.List<Vertex> input){
            var result=new System.Collections.Generic.List<Vertex>();if(input.Count==0)return result;
            var a=input[input.Count-1];foreach(var b in input){
                if((a.Z>=.2)!=(b.Z>=.2)){double t=(.2-a.Z)/(b.Z-a.Z);result.Add(new Vertex(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t,.2));}
                if(b.Z>=.2)result.Add(b);a=b;
            }return result;
        }
        public static void Draw(System.Drawing.Graphics g,System.Drawing.Size size,double fov,double yaw,double pitch){
            double focal=size.Width/(2*System.Math.Tan(fov*System.Math.PI/360));
            System.Func<Vertex,System.Drawing.PointF> project=v=>new System.Drawing.PointF((float)(size.Width/2.0+focal*v.X/v.Z),(float)(size.Height/2.0+focal*v.Y/v.Z));
            System.Action<System.Drawing.Color,Vertex[]> face=(color,vertices)=>{
                var list=Clip(vertices.Select(v=>Camera(v,yaw,pitch)).ToList());
                if(list.Count>=3)using(var brush=new System.Drawing.SolidBrush(color))g.FillPolygon(brush,list.Select(project).ToArray());
            };
            System.Action<Vertex,Vertex,System.Drawing.Color> line=(a,b,color)=>{
                a=Camera(a,yaw,pitch);b=Camera(b,yaw,pitch);if(a.Z<.2&&b.Z<.2)return;
                if(a.Z<.2){double t=(.2-a.Z)/(b.Z-a.Z);a=new Vertex(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t,.2);}
                if(b.Z<.2){double t=(.2-b.Z)/(a.Z-b.Z);b=new Vertex(b.X+(a.X-b.X)*t,b.Y+(a.Y-b.Y)*t,.2);}
                using(var pen=new System.Drawing.Pen(color,1))g.DrawLine(pen,project(a),project(b));
            };
            var floor=System.Drawing.Color.FromArgb(36,42,43);var wall=System.Drawing.Color.FromArgb(32,40,43);
            g.Clear(System.Drawing.Color.FromArgb(23,29,32));
            face(wall,new[]{new Vertex(-30,-17,32),new Vertex(30,-17,32),new Vertex(30,14,32),new Vertex(-30,14,32)});
            face(floor,new[]{new Vertex(-30,14,-32),new Vertex(30,14,-32),new Vertex(30,14,32),new Vertex(-30,14,32)});
            face(System.Drawing.Color.FromArgb(27,34,37),new[]{new Vertex(-30,-17,-32),new Vertex(-30,-17,32),new Vertex(-30,14,32),new Vertex(-30,14,-32)});
            face(System.Drawing.Color.FromArgb(29,36,39),new[]{new Vertex(30,-17,32),new Vertex(30,-17,-32),new Vertex(30,14,-32),new Vertex(30,14,32)});
            face(wall,new[]{new Vertex(30,-17,-32),new Vertex(-30,-17,-32),new Vertex(-30,14,-32),new Vertex(30,14,-32)});
            var seam=System.Drawing.Color.FromArgb(48,57,59);
            for(int x=-30;x<=30;x+=6){line(new Vertex(x,14,-32),new Vertex(x,14,32),seam);line(new Vertex(x,-17,32),new Vertex(x,14,32),System.Drawing.Color.FromArgb(38,47,50));}
            for(int z=-32;z<=32;z+=4)line(new Vertex(-30,14,z),new Vertex(30,14,z),seam);
            var trim=System.Drawing.Color.FromArgb(70,107,99);
            line(new Vertex(-30,14,32),new Vertex(30,14,32),trim);
            line(new Vertex(-30,14,-32),new Vertex(-30,14,32),trim);
            line(new Vertex(30,14,-32),new Vertex(30,14,32),trim);
            // Paired wall strips give the room its own architectural rhythm.
            foreach(int x in new[]{-24,-18,18,24}){
                face(System.Drawing.Color.FromArgb(61,87,82),new[]{new Vertex(x-.06,-12,31.98),new Vertex(x+.06,-12,31.98),new Vertex(x+.06,9,31.98),new Vertex(x-.06,9,31.98)});
            }
        }
    }
}
