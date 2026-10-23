using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VectorFit {
    class MainForm:Form {
        Color bg=Color.FromArgb(19,24,27),panel=Color.FromArgb(28,34,38),ink=Color.FromArgb(237,244,242),muted=Color.FromArgb(169,184,188),accent=Color.FromArgb(109,218,182);
        Panel content;Label status;ProgressStrip progress;Button[] nav;Session session=new Session{SmallGainProtocol=true,ControlPriority=true,BalancedTargets=true,PrecisionFirst=true,GuidedCalibration=true,DualTask=true};string activeJson,driverError;bool sensitivityEnabled=true,circleEnabled=true;
        TextBox mouse;NumericUpDown dpi,cm,fov;int pageIndex;bool busy;Setup draft=new Setup();GlobalForm global;
        public MainForm() {
            Text="TrueTrack | Kalibrering og global profil";Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);ClientSize=new Size(1120,790);MinimumSize=new Size(1000,740);
            StartPosition=FormStartPosition.CenterScreen;BackColor=bg;ForeColor=ink;Font=new Font("Segoe UI",14,FontStyle.Regular,GraphicsUnit.Pixel);AutoScaleMode=AutoScaleMode.None;
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(30,20,30,18)};
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,85));root.RowStyles.Add(new RowStyle(SizeType.Absolute,53));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,36));Controls.Add(root);
            var head=new Panel{Dock=DockStyle.Fill};head.Controls.Add(new Label{Text="TrueTrack",Font=new Font("Segoe UI",34,FontStyle.Bold,GraphicsUnit.Pixel),ForeColor=ink,AutoSize=true,Location=new Point(75,0)});
            head.Controls.Add(new Label{Text="Mål bevegelsene. Test en personlig retningsprofil.",AutoSize=true,ForeColor=muted,Location=new Point(78,51)});root.Controls.Add(head,0,0);
            using(var stream=typeof(MainForm).Assembly.GetManifestResourceStream("TrueTrack.Logo")){if(stream!=null){using(var loaded=Image.FromStream(stream)){var logo=new PictureBox{Image=new Bitmap(loaded),SizeMode=PictureBoxSizeMode.Zoom,Location=new Point(0,0),Size=new Size(66,66),AccessibleName="TrueTrack"};logo.Disposed+=delegate{logo.Image.Dispose();};head.Controls.Add(logo);}}}
            var language=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=140,Location=new Point(900,10),Anchor=AnchorStyles.Top|AnchorStyles.Right,BackColor=panel,ForeColor=ink,AccessibleName="Language / Språk",Tag="language"};
            language.Items.AddRange(new object[]{"Norsk","English"});language.SelectedIndex=L.English?1:0;head.Controls.Add(language);
            language.SelectedIndexChanged+=delegate{Guard(delegate{L.English=language.SelectedIndex==1;L.Save();ShowPage(pageIndex);if(global!=null&&!global.IsDisposed)L.Controls(global);});};
            var tabs=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};nav=new Button[5];
            string[] names={"Kalibrer","Resultat",DriverSetup.Text("Windows / spill","Windows / games"),"Statistikk",DriverSetup.Text("Historikk / oppdatering","History / updates")};
            for(int i=0;i<nav.Length;i++){int n=i;nav[i]=Button(names[i],delegate{ShowPage(n);},175);tabs.Controls.Add(nav[i]);}root.Controls.Add(tabs,0,1);
            content=new Panel{Dock=DockStyle.Fill,Padding=new Padding(0,16,0,0)};root.Controls.Add(content,0,2);
            var footer=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,ColumnCount=1,Margin=new Padding(0)};footer.RowStyles.Add(new RowStyle(SizeType.Absolute,5));footer.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            progress=new ProgressStrip{Dock=DockStyle.Fill,Maximum=144,Margin=new Padding(0)};footer.Controls.Add(progress,0,0);
            status=new Label{Dock=DockStyle.Fill,TextAlign=ContentAlignment.BottomLeft,ForeColor=muted,Font=new Font("Segoe UI",12,FontStyle.Regular,GraphicsUnit.Pixel)};footer.Controls.Add(status,0,1);root.Controls.Add(footer,0,3);
            FormClosing+=delegate{if(global!=null&&!global.IsDisposed)global.Close();};RefreshDriver();ShowPage(0);Shown+=delegate{if(AppUpdates.Enabled)AppUpdates.Check(this,true);};
        }
        Button Button(string text,Action action,int width=230,bool primary=false) {
            var b=new Button{Text=text,Width=width,Height=40,FlatStyle=FlatStyle.Flat,BackColor=primary?accent:panel,ForeColor=primary?bg:ink,Margin=new Padding(0,0,10,10),Cursor=Cursors.Hand};
            b.FlatAppearance.BorderSize=0;b.Click+=delegate{if(!busy)Guard(action);};return b;
        }
        void Guard(Action action){try{action();}catch(Exception e){UiMessage.Show(this,e.Message,"TrueTrack",MessageBoxButtons.OK,MessageBoxIcon.Information);}}
        Label TextLabel(string text,int size=11,bool bold=false,int height=55) {return new Label{Text=text,ForeColor=size>=18?ink:muted,Font=new Font("Segoe UI",size*1.2f,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel),Dock=DockStyle.Top,Height=height,AutoEllipsis=false};}
        FlowLayoutPanel Stack() {var p=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false};bool resizing=false;p.SizeChanged+=delegate{if(resizing)return;resizing=true;try{p.SuspendLayout();p.AutoScroll=false;foreach(Control c in p.Controls)c.Width=Math.Max(600,p.Width-SystemInformation.VerticalScrollBarWidth-16);p.AutoScroll=true;p.ResumeLayout(true);}finally{resizing=false;}};return p;}
        void Add(FlowLayoutPanel stack,Control c) {c.Width=Math.Max(600,stack.Width-SystemInformation.VerticalScrollBarWidth-16);c.Margin=new Padding(0,0,0,14);stack.Controls.Add(c);}
        void RefreshDriver(){activeJson=Driver.TryRead(out driverError);UpdateProgress();}
        void UpdateProgress(){var p=CalibrationProgress.FromSession(session);progress.Maximum=p.Total;progress.Value=Math.Min(progress.Maximum,p.Done);progress.AccessibleName=p.Label;progress.Invalidate();status.Text=p.Label+"  ·  "+L.T(activeJson==null?"Driver ikke tilgjengelig":Driver.Neutral(activeJson)?"Raw Accel: nøytral":"Raw Accel: profil aktiv");}
        void ShowPage(int index) {
            if(pageIndex==0&&mouse!=null&&!mouse.IsDisposed)draft=new Setup{Mouse=mouse.Text,Dpi=(double)dpi.Value,Cm360=(double)cm.Value,Hfov=(double)fov.Value};
            pageIndex=index;foreach(Control old in content.Controls.Cast<Control>().ToArray())old.Dispose();for(int i=0;i<nav.Length;i++){nav[i].BackColor=i==index?accent:panel;nav[i].ForeColor=i==index?bg:ink;}
            if(index==0)SetupPage();else if(index==1)ResultPage();else if(index==2)DriverPage();else if(index==3)StatisticsPage();else HistoryPage();
            UpdateProgress();L.Controls(this);
        }
#if DEVELOPMENT
        public void PreviewPage(int index,string path=null,bool interception=false){
            if(path!=null)session=Session.FromJson(File.ReadAllText(path));
            if(interception)session.SelectMode(ProfileMode.Interception);
            ShowPage(index);
        }
        public void PreviewScroll(int pixels){var stack=content.Controls.OfType<FlowLayoutPanel>().FirstOrDefault();if(stack!=null)stack.AutoScrollPosition=new Point(0,pixels);}
        public string PreviewLayout(){var stack=content.Controls.OfType<FlowLayoutPanel>().First();return "Client: "+stack.ClientSize+" Display: "+stack.DisplayRectangle+" Min: "+stack.AutoScrollMinSize+" H: "+stack.HorizontalScroll.Visible+Environment.NewLine+String.Join(Environment.NewLine,stack.Controls.Cast<Control>().Select(c=>c.GetType().Name+" "+c.Bounds+" "+c.Text));}
        public void PreviewDetails(){foreach(var outer in content.Controls.OfType<FlowLayoutPanel>().First().Controls.OfType<Panel>())foreach(var button in outer.Controls.OfType<Button>())if(button.Text.StartsWith("+ "))button.PerformClick();}
#endif
        void SetupPage() {
            var stack=Stack();content.Controls.Add(stack);
            Add(stack,TextLabel("Start med standard eller eget oppsett",22,true,43));
            Add(stack,TextLabel(L.English?"You can start with the defaults. Game setup, circles and sensitivity adjustment are optional. Match your mouse DPI when known.":"Du kan starte med standardverdiene. Spilloppsett, sirkler og sensitivitetsjustering er valgfrie. Bruk musens faktiske DPI hvis du kjenner den.",11,false,42));
            var fields=new TableLayoutPanel{Height=82,ColumnCount=4,RowCount=2};
            for(int i=0;i<4;i++)fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,i==0?34:22));
            string[] labels={"Navn på mus / oppsett","DPI på musen","Følsomhet · cm / 360°","Horisontalt synsfelt"};
            for(int i=0;i<4;i++)fields.Controls.Add(new Label{Text=labels[i],ForeColor=muted,AutoSize=true,Margin=new Padding(0,0,12,6)},i,0);
            mouse=new TextBox{Text=draft.Mouse,Dock=DockStyle.Fill,BackColor=panel,ForeColor=ink,BorderStyle=BorderStyle.FixedSingle,MaxLength=80};
            dpi=Number(draft.Dpi,100,50000,0);cm=Number(draft.Cm360,3,200,1);fov=Number(draft.Hfov,60,140,0);
            fields.Controls.Add(mouse,0,1);fields.Controls.Add(dpi,1,1);fields.Controls.Add(cm,2,1);fields.Controls.Add(fov,3,1);Add(stack,fields);
            var buttons=new FlowLayoutPanel{Height=52};buttons.Controls.Add(Button("Start kalibrering",delegate{Start(false);},230,true));buttons.Controls.Add(Button("Åpne lagret måling",LoadSession,230));buttons.Controls.Add(Button(L.English?"Game setup / blind measurement":"Spilloppsett / blindmåling",OpenGameSetup,320));Add(stack,buttons);
            var choices=new Panel{Height=64};var circle=CircleChoice();var sensitivity=SensitivityChoice();circle.Dock=DockStyle.Top;sensitivity.Dock=DockStyle.Bottom;choices.Controls.Add(circle);choices.Controls.Add(sensitivity);Add(stack,choices);
            Add(stack,new CalibrationSteps());
            Add(stack,TextLabel("Fase 1 samler baseline og foreslår innstillinger. Velg forslaget for å starte fase 2 med korrigeringen aktiv. Hver fase har flick og presisjon, med instruksjoner og klikk for å starte hver del. Senere faser kontrollerer neste forslag.",10,false,43));
            AddDetails(stack,"Hjelp med museoppsettet",TextLabel("Slik finner du cm/360°: Mål hvor langt du flytter musen på musematten for én hel omdreining i spillet. Standardverdien 40 er et utgangspunkt, ikke en avlesning fra G703.",10,false,50),TextLabel("DPI må være lik innstillingen på musen. Synsfeltet bestemmer hvordan målene vises; bruk spillets horisontale synsfelt.",10,false,42));

        }
        void OpenGameSetup(){
            var setup=new Setup{Dpi=(double)dpi.Value,Cm360=(double)cm.Value,Hfov=(double)fov.Value};
            using(var f=new GameSetupForm(setup,delegate{
                if((global!=null&&global.IsFiltering)||Process.GetProcessesByName("VectorFit.Game").Length>0||Process.GetProcessesByName("VectorFit.Global").Length>0)throw new Exception(L.English?"Stop the global profile before measuring physical distance.":"Stopp den globale profilen før du måler fysisk avstand.");
                RefreshDriver();if((activeJson==null&&!Driver.KnownAbsent(driverError))||!Driver.Neutral(activeJson))throw new Exception(L.English?"Blind measurement requires a neutral Raw Accel profile and known driver status. Use Reset to neutral on the global profile page first.":"Blindmåling krever nøytral Raw Accel-profil og kjent driverstatus. Bruk Tilbakestill til nøytral på globalsiden først.");
            })){if(f.ShowDialog(this)==DialogResult.OK){dpi.Value=(decimal)f.Dpi;cm.Value=(decimal)f.Cm360;fov.Value=(decimal)f.Hfov;}}
        }
        NumericUpDown Number(double value,int min,int max,int decimals){return new NumericUpDown{Minimum=min,Maximum=max,Value=(decimal)Analysis.Clamp(value,min,max),DecimalPlaces=decimals,Dock=DockStyle.Top,BackColor=panel,ForeColor=ink,BorderStyle=BorderStyle.FixedSingle,Margin=new Padding(0,0,18,0)};}
        void Start(bool ab,bool refine=false,bool keepPrevious=false,bool resumeCircles=false) {
            if((global!=null&&global.IsFiltering)||Process.GetProcessesByName("VectorFit.Game").Length>0||Process.GetProcessesByName("VectorFit.Global").Length>0)throw new Exception("Stopp den globale profilen før kalibrering eller A/B-test, slik at input ikke korrigeres to ganger.");
            Session measured=session;
            if(resumeCircles&&(ab||session.Baseline.Count>0||session.Circles.Protocol<1||session.Circles.Laps.Count(l=>session.Circles.Counts(l)&&l.Clockwise)!=30||session.Circles.Laps.Count(l=>session.Circles.Counts(l)&&!l.Clockwise)!=30))throw new Exception("Fullfør sirkeltesten først.");
            if(!ab&&!resumeCircles) {
                if(refine){measured=session.NextRound(SessionName()+".vectorfit.json",!keepPrevious);measured.CalibrateSensitivity=sensitivityEnabled;}
                else {
                    var s=new Setup{Mouse=mouse.Text.Trim(),Dpi=(double)dpi.Value,Cm360=(double)cm.Value,Hfov=(double)fov.Value};
                    if(!s.Valid()||s.Mouse.Length==0)throw new Exception("Fyll ut museoppsettet først.");
                    if(session.Baseline.Count>0&&UiMessage.Show(this,"Starte en ny måling fra nøytralt oppsett? Bruk Ny runde med aktiv profil på Global profil for å bygge videre.","Ny måling",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
                    measured=new Session{SmallGainProtocol=true,ControlPriority=true,BalancedTargets=true,PrecisionFirst=true,GuidedCalibration=true,DualTask=true,Setup=s,Mode=session.Mode,CalibrateSensitivity=sensitivityEnabled};
                }
            }else if(!session.FitReady)throw new Exception("Fullfør kalibreringen slik at en profil kan beregnes først.");
            RefreshDriver();
            if(activeJson==null&&!Driver.KnownAbsent(driverError))throw new Exception("Driverstatus er usikker. Kalibrering er stoppet for å unngå dobbel korreksjon.\n"+driverError);
            bool candidateActive=ab&&!measured.UsesInterception&&Driver.MatchesCorrection(activeJson,measured.Correction);
            if(!candidateActive&&!Driver.MatchesCorrection(activeJson,measured.InputCorrection)){
                bool neutralBasis=Driver.Neutral(Driver.Config(measured.InputCorrection).ToString());
                if(!ResolveProfileWarning(neutralBasis))return;
                if(!Driver.MatchesCorrection(activeJson,measured.InputCorrection))throw new Exception("Profilen stemmer fortsatt ikke med målingen. Målingen er stoppet.");
            }
            if(!ab&&session.Baseline.Count>0)SaveSession();
            session=measured;
            string fingerprint=Driver.Fingerprint(activeJson);
            bool circlesComplete=resumeCircles;
            if(!ab&&circleEnabled&&!resumeCircles){
                using(var circles=new CircleForm(session.Setup,session.InputCorrection)){
                    circles.ShowDialog(this);VerifyDriver(fingerprint);
                    session.Circles=circles.Data;circlesComplete=circles.Completed;
                    if(circles.DeviceName!="")session.Setup.Device=circles.DeviceName;
                }
                if(!circlesComplete){SaveSession();ShowPage(3);return;}
                circleEnabled=false;
                SaveSession();
                using(var result=new CircleResultForm(session.Circles,session.InputCorrection)){if(result.ShowDialog(this)!=DialogResult.OK){ShowPage(3);return;}}
                VerifyDriver(fingerprint);
            }
            var testSettings=ab?new Session{SmallGainProtocol=session.SmallGainProtocol,ControlPriority=session.ControlPriority,BalancedTargets=session.BalancedTargets,PrecisionFirst=session.PrecisionFirst,FrozenProposal=session.FrozenProposal,TestInputCorrection=candidateActive?session.Correction:session.InputCorrection,DualTask=true,Setup=session.Setup,Mode=session.Mode,Round=session.Round,Correction=session.Correction,Curve=session.Curve,InputCorrection=session.InputCorrection,FitReady=session.FitReady,Repeatable=session.Repeatable,AdaptiveValidation=true,TrajectoryFitting=session.TrajectoryFitting,AdaptiveCalibration=session.AdaptiveCalibration}:session;
            using(var form=new AimForm(testSettings,ab,!ab,circlesComplete)) {
                form.ShowDialog(this);
                VerifyDriver(fingerprint);
                session.AdaptiveValidation=form.Sequential;session.ComparisonGoal=form.ComparisonGoal;
                if(ab){session.DualTask=true;session.PrecisionValidation=form.PrecisionValidationResults;session.Validation=form.Results;session.Notes=Analysis.Validate(session);}
                else{session.PrecisionBaseline=form.PrecisionResults;session.PrecisionValidation=form.PrecisionValidationResults;session.BaselineGoal=form.CalibrationGoal;session.Baseline=form.ValidationStarted?form.CalibrationResults:form.Results;if(form.DeviceName!="")session.Setup.Device=form.DeviceName;Analysis.Compute(session);if(form.ValidationStarted){session.Validation=form.Results;session.Notes=Analysis.Validate(session);}}
            }
            SaveSession();ShowPage(1);
        }
        void VerifyDriver(string fingerprint){string err;string after=Driver.TryRead(out err);if((after==null&&!Driver.KnownAbsent(err))||Driver.Fingerprint(after)!=fingerprint)throw new Exception("Driveroppsettet ble endret under testen. Målingen brukes ikke. Kalibrer på nytt.");}
        void ConfirmProposal(){
            SaveSession();session=session.Confirmation(SessionName()+".vectorfit.json");SaveSession();ShowPage(1);Start(true);
        }
        void ContinueRound(){
            RefreshDriver();
            if(Driver.MatchesCorrection(activeJson,session.Correction)){Start(false,true);return;}
            if(Driver.MatchesCorrection(activeJson,session.InputCorrection)){Start(false,true,true);return;}
            ResolveProfileWarning(false);
        }
        bool ResolveProfileWarning(bool resumeAfterNeutral){
            using(var warning=new ActiveProfileDialog(resumeAfterNeutral,"Den aktive Raw Accel-profilen stemmer ikke med oppsettet denne målingen krever.")){
                if(warning.ShowDialog(this)!=DialogResult.OK)return false;
            }
            if(session.Baseline.Count>0||session.Circles.Laps.Count>0)SaveSession();
            Neutralize();
            if(!resumeAfterNeutral){circleEnabled=true;ShowPage(0);return false;}
            return true;
        }
        string SessionName(){return "maaling-"+DateTime.Parse(session.CreatedUtc,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind).ToString("yyyyMMdd-HHmmss-fff")+"-v13";}
        string RawProfileName(){return SessionName()+(session.ReadyForRawAccel?"":"-proeveprofil");}
        void SaveSession() {
            if(session.InitialBaseline==null)session.InitialBaseline=ResultStory.Initial(session);
            Directory.CreateDirectory(Driver.Data);string path=Path.Combine(Driver.Data,SessionName()+".vectorfit.json");
            File.WriteAllText(path,JsonConvert.SerializeObject(session,Formatting.Indented));
        }
        void LoadSession() {
            using(var d=new OpenFileDialog{InitialDirectory=Driver.Data,Filter=L.T("VectorFit-målinger|*.vectorfit.json")})if(d.ShowDialog()==DialogResult.OK){
                if(new FileInfo(d.FileName).Length>128*1024*1024)throw new Exception("Målefilen er for stor.");
                EnsureFilterStopped();
                var s=Session.FromJson(File.ReadAllText(d.FileName));session=s;circleEnabled=s.Circles.Laps.Count(s.Circles.Counts)<s.Circles.Goal;
                ShowPage(1);draft=new Setup{Mouse=s.Setup.Mouse,Dpi=s.Setup.Dpi,Cm360=s.Setup.Cm360,Hfov=s.Setup.Hfov};
            }
        }
        Control ProfileSelector() {
            var row=new FlowLayoutPanel{Height=40,WrapContents=false};
            row.Controls.Add(new Label{Text="Profiltype",ForeColor=ink,AutoSize=true,Margin=new Padding(0,5,14,0)});
            var choice=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=465,BackColor=panel,ForeColor=ink};
            choice.Items.Add("Standard · Raw Accel (uten Interception)");
            choice.Items.Add("Valgfritt · Interception + kurvekorreksjon");
            choice.SelectedIndex=session.UsesInterception?1:0;row.Controls.Add(choice);
            bool restoring=false;
            choice.SelectedIndexChanged+=delegate{if(restoring||L.Applying)return;Guard(delegate{
                try {EnsureFilterStopped();session.SelectMode(choice.SelectedIndex==1?ProfileMode.Interception:ProfileMode.RawAccel);}
                catch {restoring=true;choice.SelectedIndex=session.UsesInterception?1:0;restoring=false;throw;}
                ShowPage(pageIndex);
            });};return row;
        }
        void EnsureFilterStopped() {
            if((global!=null&&!global.IsDisposed&&global.IsFiltering)||Process.GetProcessesByName("VectorFit.Game").Length>0||Process.GetProcessesByName("VectorFit.Global").Length>0)
                throw new Exception("Stopp Interception-profilen før du bytter profil eller endrer Raw Accel.");
        }
        Control SensitivityChoice(){var choice=new CheckBox{Text="Juster følsomheten hvis jeg ofte skyter for langt eller for kort",Checked=sensitivityEnabled,Height=30,ForeColor=ink};choice.CheckedChanged+=delegate{sensitivityEnabled=choice.Checked;};return choice;}
        Control CircleChoice(){var choice=new CheckBox{Text="Ta sirkeltest først · 30 med + 30 mot klokken (valgfritt)",Checked=circleEnabled,Height=30,ForeColor=ink};choice.CheckedChanged+=delegate{circleEnabled=choice.Checked;};return choice;}
        bool CanResumeCircles {get{return session.Baseline.Count==0&&session.Circles.Protocol>=1&&session.Circles.Laps.Count(l=>session.Circles.Counts(l)&&l.Clockwise)==30&&session.Circles.Laps.Count(l=>session.Circles.Counts(l)&&!l.Clockwise)==30;}}
        void AddDetails(FlowLayoutPanel stack,string title,params Control[] controls){
            var outer=new Panel{Height=44};var toggle=Button("+ "+title,delegate{},500);toggle.Margin=new Padding(0);outer.Controls.Add(toggle);
            var body=new FlowLayoutPanel{Location=new Point(0,52),FlowDirection=FlowDirection.TopDown,WrapContents=false,Visible=false};
            int height=0;foreach(var control in controls){control.Margin=new Padding(0,0,0,12);body.Controls.Add(control);height+=control.Height+12;}body.Height=height;outer.Controls.Add(body);
            outer.SizeChanged+=delegate{toggle.Width=outer.Width;body.Width=outer.Width;foreach(Control control in body.Controls)control.Width=body.ClientSize.Width;};
            toggle.Click+=delegate{body.Visible=!body.Visible;outer.Height=body.Visible?height+52:44;L.SetText(toggle,(body.Visible?"− ":"+ ")+title);};Add(stack,outer);
        }

        void HistoryPage(){
            var stack=Stack();content.Controls.Add(stack);
            Add(stack,TextLabel(DriverSetup.Text("Profilhistorikk og oppdateringer","Profile history and updates"),22,true,43));
            Add(stack,TextLabel(DriverSetup.Text("Åpne en måling endrer ikke den aktive profilen. Tilbakeføring endrer Raw Accel globalt etter bekreftelse.","Opening a measurement does not change the active profile. Restore changes Raw Accel globally after confirmation."),11,false,52));
            RefreshDriver();
            Add(stack,TextLabel(activeJson==null?DriverSetup.Text("Driverstatus utilgjengelig","Driver status unavailable"):DriverSetup.Text("Aktiv i Windows: ","Active in Windows: ")+(Driver.MatchesCorrection(activeJson,session.Correction)?DriverSetup.Text("valgt forslag","selected proposal"):Driver.MatchesCorrection(activeJson,session.InputCorrection)?DriverSetup.Text("måleprofil A","measurement profile A"):Driver.Neutral(activeJson)?DriverSetup.Text("nøytral","neutral"):DriverSetup.Text("annet oppsett","other configuration")),12,true,35));
            var recover=new FlowLayoutPanel{Height=100};
            recover.Controls.Add(Button(DriverSetup.Text("Tilbake til måleprofil A","Restore measurement profile A"),delegate{RestoreCorrection(session.InputCorrection);},310));
            if(session.InitialBaseline!=null)recover.Controls.Add(Button(DriverSetup.Text("Tilbake til første baseline","Restore first baseline profile"),delegate{RestoreCorrection(session.InitialBaseline.Input);},310));
            recover.Controls.Add(Button(DriverSetup.Text("Gjenopprett siste sikkerhetskopi","Restore last driver backup"),Restore,310));
            recover.Controls.Add(Button(DriverSetup.Text("Nøytral profil","Neutral profile"),Neutralize,240));Add(stack,recover);
            Add(stack,TextLabel(DriverSetup.Text("Lagrede målinger · nyeste først","Saved measurements · newest first"),16,true,32));
            int skipped=0;
            foreach(string path in Directory.GetFiles(Driver.Data,"*.vectorfit.json").OrderByDescending(File.GetLastWriteTimeUtc)){
                Session saved;try{if(new FileInfo(path).Length>128*1024*1024){skipped++;continue;}saved=Session.FromJson(File.ReadAllText(path));}catch{skipped++;continue;}
                var row=new Panel{Height=116};var label=TextLabel(DateTime.Parse(saved.CreatedUtc).ToLocalTime().ToString("g")+" · "+DriverSetup.Text("Fase ","Phase ")+saved.Round+" · "+saved.Setup.Mouse+"\n"+ResultStory.SimpleVerdict(saved)+"\n"+ProfileValues(saved.InputCorrection)+" → "+ProfileValues(saved.Correction),10,false,68);row.Controls.Add(label);
                var open=Button(DriverSetup.Text("Åpne resultat","Open result"),delegate{EnsureFilterStopped();session=saved;ShowPage(1);},190);open.Location=new Point(0,72);row.Controls.Add(open);
                var restore=Button(DriverSetup.Text("Gjenopprett måleprofil","Restore measured profile"),delegate{RestoreCorrection(saved.InputCorrection);},290);restore.Location=new Point(205,72);row.Controls.Add(restore);Add(stack,row);
            }
            if(skipped>0)Add(stack,TextLabel(skipped+DriverSetup.Text(" filer kunne ikke leses; originalene er beholdt."," files could not be read; originals are preserved."),10,false,35));
            var updates=new FlowLayoutPanel{Height=100};updates.Controls.Add(Button(DriverSetup.Text("Se etter oppdatering","Check for updates"),delegate{EnsureFilterStopped();AppUpdates.Check(this,false);},260));
            updates.Controls.Add(Button(DriverSetup.Text("Åpne prosjektsiden","Open project page"),delegate{Process.Start("https://github.com/FLASHBiRDone/TrueTrack");},240));
            updates.Controls.Add(Button(DriverSetup.Text("Åpne datamappen","Open data folder"),delegate{Process.Start(Driver.Data);},240));Add(stack,updates);
            var check=new CheckBox{Text=DriverSetup.Text("Sjekk GitHub ved oppstart (inkluderer eksperimentelle utgaver)","Check GitHub on startup (includes experimental releases)"),Checked=AppUpdates.Enabled,Height=32,AutoSize=false};check.CheckedChanged+=delegate{AppUpdates.Enabled=check.Checked;};Add(stack,check);
            Add(stack,TextLabel("TrueTrack "+AppUpdates.Version+"\n"+DriverSetup.Text("Målinger og profiler lagres separat fra programmet: ","Measurements and profiles are stored separately from the app: ")+Driver.Data,10,false,60));
        }
        static string ProfileValues(Correction c){return "Sens "+c.Sensitivity.ToString("F3")+" · L/R "+c.Left.ToString("F3")+"/"+c.Right.ToString("F3")+" · U/D "+c.Up.ToString("F3")+"/"+c.Down.ToString("F3");}
        void RestoreCorrection(Correction c){
            EnsureFilterStopped();if(!c.Safe())throw new Exception("Invalid profile");
            if(UiMessage.Show(this,DriverSetup.Text("Aktiver denne Raw Accel-profilen i Windows og spill? Nåværende oppsett sikkerhetskopieres.\n","Activate this Raw Accel profile in Windows and games? Current settings will be backed up.\n")+ProfileValues(c),"TrueTrack",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
            string path=Path.Combine(Driver.Data,"restore-selected.rawaccel.json");File.WriteAllText(path,Driver.Config(c).ToString());Driver.Apply(path,true);RefreshDriver();if(!Driver.MatchesCorrection(activeJson,c))throw new Exception("Restored profile could not be verified");ShowPage(4);
        }
        void StatisticsPage(){
            var stack=Stack();content.Controls.Add(stack);
            Add(stack,TextLabel("Statistikk · runde "+session.Round,22,true,43));
            if(session.Circles.Laps.Count>0){Add(stack,new CircleMedianChart(session.Circles,session.InputCorrection,session.FitReady?session.Correction:null));Add(stack,TextLabel(CircleAnalysis.Summarize(session.Circles).Note+" "+session.CircleNote,10,false,48));}
            if(session.Baseline.Count==0){if(CanResumeCircles)Add(stack,Button("Fortsett til måltest",delegate{Start(false,false,false,true);},240,true));Add(stack,TextLabel("Åpne en lagret måling for å se baseline, A/B og tidligere runder.",11,false,42));Add(stack,Button("Åpne lagret måling",delegate{LoadSession();ShowPage(3);},240));return;}
            Add(stack,TextLabel("Baseline er målingen før A/B. A er profilen du målte med; B er forslaget. Negative differanser betyr lavere avvik eller kortere tid. Avvik vises som prosent av målavstanden.",11,false,48));
            Add(stack,ProfileSelector());
            var counts=new RoundStatistics(session);Add(stack,TextLabel("Baseline: "+counts.Baseline.Count+"  ·  A: "+counts.A.Count+" / "+session.ComparisonGoal/2+"  ·  B: "+counts.B.Count+" / "+session.ComparisonGoal/2,10,false,24));
            Add(stack,TextLabel(ResultStory.SimpleVerdict(session),20,true,60));
            Add(stack,TextLabel(ResultStory.SimpleSummary(session),11,false,110));
            Add(stack,new OutcomeTimeline(session));
            Add(stack,new ConfigGauges(session));
            AddDetails(stack,DriverSetup.Text("Retningskart","Direction map"),new DirectionMap(session));
            AddDetails(stack,DriverSetup.Text("Utvikling fra første baseline","Progress from first baseline"),new ConfigGauges(session,true));
            Add(stack,new ComparisonChart(session));
            Add(stack,new MovementChart(session));
            if(session.AdaptiveValidation)Add(stack,TextLabel("A/B inkluderer bom, klikk uten bevegelse og tidsavbrudd. Ugyldige forsøk teller som bom med minst 100 % avvik. Ukjente bevegelsesmål vises som —.",10,false,42));
            string note;var rounds=History.Load(session,Driver.Data,out note);var first=rounds[0];var latest=rounds[rounds.Count-1];
            string delta=RoundStatistics.Delta(RoundStatistics.Value(first.Baseline,0),RoundStatistics.Value(latest.Baseline,0),"pp");
            Add(stack,TextLabel(rounds.Count+" sammenhengende runder · Endring i baseline: "+delta+"\nA/B i samme runde har like målvinkler og avstander. Mellom runder varierer målene; trendene er veiledende.",10,false,48));
            if(note!="")Add(stack,TextLabel(note,10,false,36));
            Add(stack,new HistoryChart(rounds,0));Add(stack,new HistoryChart(rounds,1));
            var known=session.Baseline.Where(t=>t.Hit.HasValue).ToList();
            Add(stack,TextLabel(known.Count==0?"Treffandel mangler i eldre målinger. Nye runder lagrer treff på den grønne prikken; bomskudd er fortsatt målinger.":"Treff i baseline: "+known.Count(t=>t.Hit==true)+" / "+known.Count+" klikk ("+(100.0*known.Count(t=>t.Hit==true)/known.Count).ToString("F1")+" %).",10,false,42));
            Add(stack,TextLabel("pp = prosentpoeng. Tomme resultater vises som —. Delvise A/B-tester er foreløpige; se vurderingen under Resultat og A/B.",10,false,42));
            Add(stack,Button("Åpne en annen måling",delegate{LoadSession();ShowPage(3);},250));
        }
        void CircleCheck(){
            EnsureFilterStopped();if(session.UsesInterception)throw new Exception(DriverSetup.Text("Sirkelkontroll støtter foreløpig Raw Accel-profiler.","Circle verification currently supports Raw Accel profiles."));
            var first=ResultStory.Initial(session);if(first==null||first.Circles==null||first.Circles.Laps.Count==0)throw new Exception(DriverSetup.Text("Første baseline mangler sirkeldata.","The first baseline has no circle data."));
            RefreshDriver();if(activeJson==null||!Driver.MatchesCorrection(activeJson,session.Correction))throw new Exception(DriverSetup.Text("Aktiver dette forslaget med Raw Accel først. Sirkelkontrollen endrer ikke profilen.","Activate this proposal with Raw Accel first. Circle verification does not change the profile."));
            string fingerprint=Driver.Fingerprint(activeJson);
            using(var test=new CircleForm(session.Setup,session.Correction)){test.ShowDialog(this);VerifyDriver(fingerprint);if(!test.Completed)return;session.VerifiedCircles=test.Data;session.VerifiedCircleProfile=session.Correction.Copy();}
            session.InitialBaseline=first;SaveSession();ShowPage(1);
        }
        Control Narrative(Session value){var label=TextLabel(ResultStory.Explanation(value),11,false,260);int lastWidth=-1;label.SizeChanged+=delegate{if(label.Width==lastWidth)return;lastWidth=label.Width;label.Height=Math.Max(90,TextRenderer.MeasureText(label.Text,label.Font,new Size(label.Width,Int32.MaxValue),TextFormatFlags.WordBreak).Height+24);};return label;}
        void AddCircleComparison(FlowLayoutPanel stack){
            var first=ResultStory.Initial(session);
            if(first==null||first.Circles==null||first.Circles.Laps.Count==0)return;
            Add(stack,Button(DriverSetup.Text("Valgfritt: mål sirkler med aktivt forslag","Optional: measure circles with active proposal"),CircleCheck,420));
            if(session.VerifiedCircles==null)return;
            Add(stack,TextLabel(DriverSetup.Text("Faktisk målte sirkler · første baseline og kontroll etter profilendring. Ingen simulering eller ny tilpasning.","Measured circles · first baseline and verification after profile change. No simulation or refitting."),11,false,45));
            Add(stack,new MeasuredCircleComparison(first.Circles,session.VerifiedCircles));
        }
        void ResultPage() {
            var stack=Stack();content.Controls.Add(stack);
            Add(stack,TextLabel(session.FitReady?"Fase "+session.Round+" · "+(session.Round==1?"Baseline analysert":"Måling analysert"):"Datainnhenting · ingen ferdig profil ennå",22,true,43));
            Add(stack,TextLabel(session.Baseline.Count==0?"Kalibrer musen først. Her ser du målingene og kan teste korreksjonen før den brukes utenfor programmet.":session.Validation.Count>0?"Resultatet gjelder dette profilparet. Ingen innstillinger endres når du leser analysen.":session.Notes,11,false,50));
            if(session.Baseline.Count==0){if(CanResumeCircles)Add(stack,Button("Fortsett til måltest",delegate{Start(false,false,false,true);},240,true));else Add(stack,Button("Gå til kalibrering",delegate{ShowPage(0);},230,true));return;}
            Add(stack,TextLabel(ResultStory.SimpleVerdict(session),20,true,60));
            Add(stack,TextLabel(ResultStory.SimpleSummary(session),11,false,110));
            Add(stack,new OutcomeTimeline(session));
            Add(stack,new ConfigGauges(session));
            AddDetails(stack,DriverSetup.Text("Retningskart","Direction map"),new DirectionMap(session));
            AddDetails(stack,DriverSetup.Text("Utvikling fra første baseline","Progress from first baseline"),new ConfigGauges(session,true));
            if(session.Validation.Count>0||session.FrozenProposal){
                var confirm=Button(session.Validation.Count>0?"Bekreft samme forslag på nytt":"Start bekreftelsesrunden",ConfirmProposal,350,true);confirm.Enabled=session.FitReady;Add(stack,confirm);
                AddDetails(stack,"Hvorfor denne vurderingen?",Narrative(session));
                AddDetails(stack,"Kontroller av samme forslag",TextLabel(ResultStory.RepeatSummary(session),10,false,Math.Max(100,55+session.PriorComparisons.Count*30)));
            }
            AddCircleComparison(stack);
            if(session.FitReady){if(session.Validation.Count>0||session.FrozenProposal)AddDetails(stack,"Innstillingene som sammenlignes",new ProposalView(session));else Add(stack,new ProposalView(session));
                if(session.CanTryRawAccel){var next=new FlowLayoutPanel{Height=52};
                    next.Controls.Add(Button("Prøv forslaget · start fase "+(session.Round+1),delegate{if(ActivateProfile(false))Start(false,true);},320,session.ReadyForRawAccel||session.Validation.Count==0));
                    next.Controls.Add(Button("Behold profilen · start fase "+(session.Round+1),ContinueRound,320,!session.ReadyForRawAccel&&session.Validation.Count>0));Add(stack,next);
                }
                Add(stack,TextLabel(session.ValidationPassed?"A/B støtter forslaget. Innstillingene brukes først når du velger å aktivere dem.":"Prøveforslag, ikke bekreftet bedre. Knappen lar deg prøve innstillingene globalt og måle neste runde.",10,false,38));
                Add(stack,TextLabel(session.SensitivityNote,10,false,42));
                string historyNote;var history=History.Load(session,Driver.Data,out historyNote);
                if(history.Count>1){var first=history[0];var previous=history[history.Count-2];var current=history[history.Count-1];
                    Add(stack,TextLabel("Utvikling i datainnhenting · avvik og bevegelsestid\n"+PhaseDelta("Fra baseline",first,current)+"\n"+PhaseDelta("Fra forrige fase",previous,current)+"\nUlik tidsbruk kan påvirke presisjonen. Dette alene bekrefter ikke profileffekt.",10,false,90));
                }
            }
            if(session.ReanalyzedFrom!="")Add(stack,TextLabel("Ny analyse av samme data · ikke en ny måling. Kilde: "+session.ReanalyzedFrom,10,false,36));
            if(session.Circles.Protocol==2&&session.Circles.Laps.Count(session.Circles.Counts)==60){
                Add(stack,Button("Beregn forslag fra ellipsetendensen",delegate{SaveSession();session=session.ReanalyzeCircleTrend(SessionName()+".vectorfit.json");SaveSession();ShowPage(1);},380));
                Add(stack,TextLabel("Lager en ny analyse av de lagrede sirklene. Originalen beholdes. Tidligere A/B godkjenner ikke det nye forslaget. Ingen profil aktiveres.",10,false,44));
            }
            if(session.Validation.Count>0) {
                var stats=new RoundStatistics(session);var a=RoundStatistics.Value(stats.A,0);var b=RoundStatistics.Value(stats.B,0);
                string av=a.HasValue?a.Value.ToString("F1")+" %":"—",bv=b.HasValue?b.Value.ToString("F1")+" %":"—";
                Add(stack,TextLabel("Avstand fra klikk til sentrum: "+av+" → "+bv+"   ·   "+RoundStatistics.Delta(a,b,"pp")+"\nA = profilen du målte med. B = forslaget. Tallene er medianer, i prosent av målavstanden.",12,true,59));
                if(session.AdaptiveValidation)Add(stack,TextLabel("Treff ved klikk: "+stats.A.Count(t=>t.Hit==true)+" / "+stats.A.Count+" → "+stats.B.Count(t=>t.Hit==true)+" / "+stats.B.Count+"  ·  "+"Treff etter overskyting: "+stats.A.Count(t=>Trajectory.Measure(t).HitAfterOvershoot)+" → "+stats.B.Count(t=>Trajectory.Measure(t).HitAfterOvershoot),10,false,24));
            }
            if(session.DualTask){
                Add(stack,TextLabel("Presisjon · treff uten unødvendig tidsbruk",16,true,32));
                Add(stack,TextLabel("Måleprofil: "+PrecisionCheck.Summary(session.PrecisionBaseline),10,false,32));
                if(session.PrecisionValidation.Count>0){Add(stack,TextLabel("A: "+PrecisionCheck.Summary(session.PrecisionValidation.Where(t=>!t.Corrected))+"\nB: "+PrecisionCheck.Summary(session.PrecisionValidation.Where(t=>t.Corrected)),10,false,52));}
                Add(stack,TextLabel("Tidsavbrudd teller som 3000 ms og mislykket mål. Alle bomklikk beholdes. Presisjon brukes som kontroll; flickbevegelsene beregner profilforslaget.",10,false,44));
            }
            Add(stack,TextLabel(session.AdaptiveValidation?"A/B vurderer klikkavstand, faktiske treff, overskyting, etterkorrigering og tid. Mindre korrigering kan gi forbedring ved bevart presisjon og minst like mange treff.":session.RequireCloserClicks?"Målet er mindre avstand til sentrum ved klikk. Både treff og bom teller. A/B krever minst 5 % mindre medianavstand for bekreftet forbedring.":"Eldre måling: den opprinnelige A/B-vurderingen er beholdt. Neste runde bruker den nye vurderingen av presisjon og kontroll.",10,false,42));
            var buttons=new FlowLayoutPanel{Height=52};var test=Button(session.Validation.Count>0?"Bekreft samme forslag på nytt":"Start A/B · flick + presisjon",delegate{ConfirmProposal();},320,session.Validation.Count==0);test.Enabled=session.FitReady;if(session.Validation.Count==0&&!session.FrozenProposal)buttons.Controls.Add(test);
            buttons.Controls.Add(Button("Grafer og historikk",delegate{ShowPage(3);},220));
            buttons.Controls.Add(Button("Bruk i Windows og spill",delegate{ShowPage(2);},230));Add(stack,buttons);
            AddDetails(stack,"Valg for neste runde",SensitivityChoice(),CircleChoice());
            var row=new TableLayoutPanel{Height=270,ColumnCount=2};row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,49));row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,51));
            row.Controls.Add(new Radar(session){Dock=DockStyle.Fill},0,0);
            var c=session.Correction;
            string text=session.Setup.Mouse+"  ·  "+session.Setup.Dpi+" DPI\n\n";
            text+="Målinger med bevegelse: "+session.Baseline.Count(t=>t.Valid)+"\n";
            if(session.FitReady){text+="\nSamlet følsomhet  "+c.Sensitivity.ToString("F3")+"× · "+(session.Setup.Cm360/c.Sensitivity).ToString("F1")+" cm/360°*\nHøyre  "+c.Right.ToString("F3")+"×       Venstre  "+c.Left.ToString("F3")+"×\nNed      "+c.Down.ToString("F3")+"×       Opp        "+c.Up.ToString("F3")+"×\nRotasjon  "+c.Rotation.ToString("F2")+"°\n\nRetning maks ±8 %, rotasjon ±3°.\nFølsomhet: maks 5 % per runde, totalt 0,75–1,25×.\n*Før retningsjustering. DPI på musen endres ikke.";}
            row.Controls.Add(new Label{Dock=DockStyle.Fill,Text=text,ForeColor=ink,Padding=new Padding(20,8,0,0)},1,0);
            AddDetails(stack,"Profildetaljer og valgfri kurvekorreksjon",ProfileSelector(),TextLabel(session.UsesInterception?"A/B tester følsomhet, retningsprofil og kurvekorreksjon. Global bruk krever Interception og at VectorFit kjører.":"A/B tester følsomhet, retning og rotasjon slik Raw Accel bruker dem. Kurver og bomskudd måles fortsatt. Ingen Interception kreves.",10,false,45),row,TextLabel(session.CircleNote,10,false,42),TextLabel(session.Round>1?"Denne runden er målt med forrige profil aktiv. A/B sammenligner den med det nye forslaget. Forrige måling er lagret som "+session.ParentMeasurement:"Hver profiltype har sin egen A/B-test. A/B-korreksjonen skjer bare inne i testvinduet. Etter aktivering kan du starte neste runde fra Global profil.",10,false,48));
        }
        static string PhaseDelta(string label,RoundStatistics before,RoundStatistics after){
            return label+": "+RoundStatistics.Delta(RoundStatistics.Value(before.Baseline,0),RoundStatistics.Value(after.Baseline,0),"pp")+" · "+RoundStatistics.Delta(before.Baseline.Count==0?(double?)null:Analysis.Median(before.Baseline.Select(t=>t.FlickMs)),after.Baseline.Count==0?(double?)null:Analysis.Median(after.Baseline.Select(t=>t.FlickMs)),"ms");
        }
        void DriverPage() {
            RefreshDriver();var stack=Stack();content.Controls.Add(stack);
            Add(stack,TextLabel("Runde "+session.Round+" · Bruk profilen globalt",22,true,43));
            Add(stack,ProfileSelector());
            var setupRow=new FlowLayoutPanel{Height=52};
            setupRow.Controls.Add(Button(DriverSetup.Text("Klargjør global bruk","Set up global use"),delegate{OpenDriverSetup(false);},245));
            setupRow.Controls.Add(Button(DriverSetup.Text("Valgfritt: Interception-oppsett","Optional: Interception setup"),delegate{OpenDriverSetup(true);},310));
            Add(stack,setupRow);
            string measured=session.Baseline.Count==0?"Åpne en måling eller kalibrer først.":session.ValidationPassed?"A/B-testen bekrefter forbedring for denne profiltypen.":session.CanTryRawAccel?"Forbedring er ikke bekreftet. Du kan velge å prøve profilen ved å trykke Aktiver med Raw Accel.":session.Notes;
            Add(stack,TextLabel(measured,11,false,48));
            bool curved=session.UsesInterception;
            Add(stack,TextLabel(curved?"Valgfri, eksperimentell korreksjon av retning og buer. Krever en separat Interception-driver og at VectorFit fortsetter å kjøre. Stopp med Ctrl+Alt+F12.":"Følsomhet, retning og rotasjon brukes av Raw Accel på kjernenivå, også med rå museinput i spill. VectorFit kan lukkes. Interception brukes ikke.",11,false,65));
            var buttons=new FlowLayoutPanel{Height=52};
            var activate=Button(curved?"Åpne Interception-valg":activeJson==null?DriverSetup.Text("Klargjør global bruk","Set up global use"):"Aktiver med Raw Accel",delegate{if(curved)OpenGlobal();else if(activeJson==null)OpenDriverSetup(false);else ActivateProfile();},245,true);
            activate.Enabled=curved||activeJson==null||session.CanTryRawAccel;
            buttons.Controls.Add(activate);
            if(!curved)buttons.Controls.Add(Button("Eksporter Raw Accel-profil",ExportProfile,240));
            buttons.Controls.Add(Button("Åpne lagret måling",LoadSession,220));Add(stack,buttons);
            if(!curved){var next=new FlowLayoutPanel{Height=52};next.Controls.Add(Button("Ny runde med aktiv profil",ContinueRound,290));next.Controls.Add(Button("Resultat og A/B",delegate{ShowPage(1);},210));Add(stack,next);}
            if(!curved)AddDetails(stack,"Valg for neste runde",SensitivityChoice(),CircleChoice());
            string driver=activeJson==null?"Raw Accel kunne ikke leses.":Driver.Neutral(activeJson)?"Raw Accel er tilgjengelig og nøytral.":"Raw Accel har en aktiv profil.";
            Add(stack,TextLabel(driver,11,false,40));
            if(!curved&&activeJson!=null&&session.CanTryRawAccel)Add(stack,TextLabel(Driver.MatchesCorrection(activeJson,session.Correction)?DriverSetup.Text("Valgt forslag er aktivt i Raw Accel · lest tilbake og kontrollert. Gjelder også rå museinput; kompatibilitet må vurderes per spill.","Selected proposal is active in Raw Accel · read back and verified. Applies to raw mouse input; check compatibility per game."):DriverSetup.Text("Det valgte forslaget er ikke aktivt i driveren. Trykk Aktiver med Raw Accel for å bruke det.","The selected proposal is not active in the driver. Choose Activate with Raw Accel to apply it."),11,false,48));
            var recovery=new FlowLayoutPanel{Height=52};
            recovery.Controls.Add(Button(DriverSetup.Text("Tilbakestill til nøytral","Reset to neutral"),Neutralize,230));
            recovery.Controls.Add(Button("Gjenopprett profil",Restore,210));
            recovery.Controls.Add(Button("Oppdater status",delegate{ShowPage(2);},190));Add(stack,recovery);
            Add(stack,TextLabel("Nøytraliser slår av Raw Accel-justeringene og lagrer en sikkerhetskopi. Gjenopprett henter tilbake oppsettet før siste endring.",10,false,42));
            Add(stack,TextLabel(curved?"Anti-cheat: FACEIT blokkerer Interception-driveren. Å stoppe VectorFit avinstallerer ikke driveren. Bruk bare der driveren er tillatt. Ingen driver installeres automatisk.":"Denne profilen endrer ikke bevegelsesbuene dynamisk. Slik kurvekorreksjon kan velges separat og krever en egen A/B-test. Ingen generell garanti for godkjenning av anti-cheat.",10,false,65));
        }
        void OpenDriverSetup(bool interception) {
            EnsureFilterStopped();
            using(var setup=new DriverSetupForm(interception))setup.ShowDialog(this);
            ShowPage(2);
        }
        void OpenGlobal() {
            if(!session.UsesInterception)throw new Exception("Velg Interception-profilen først.");
            if(global==null||global.IsDisposed){global=new GlobalForm();global.UseSession(session);global.Show(this);}
            else {if(!global.IsFiltering)global.UseSession(session);global.Activate();}
        }
        bool ActivateProfile(bool showSuccess=true) {
            EnsureFilterStopped();
            CheckRawProfile();RefreshDriver();
            if(activeJson==null)throw new Exception("Raw Accel kunne ikke leses. Profilen er ikke aktivert.\n\n"+driverError);
            string message=(session.ReadyForRawAccel?"A/B-testen bekrefter forbedring.":"Forbedringen er ikke bekreftet.\n"+session.Notes)+"\n\nProfilen gjelder alle mus. Nåværende Raw Accel-oppsett sikkerhetskopieres først. Du kan angre med Gjenopprett profil.\n\n"+(session.ReadyForRawAccel?"Aktivere profilen?":"Vil du likevel prøve profilen?");
            if(UiMessage.Show(this,message,"Aktiver retningsprofil",MessageBoxButtons.YesNo,MessageBoxIcon.Information,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return false;
            SaveSession();string file=Driver.Export(session,RawProfileName(),!session.ReadyForRawAccel);busy=true;UseWaitCursor=true;
            try{Driver.Apply(file,true);if(showSuccess)UiMessage.Show(this,"Profilen er aktiv. Innstillingene er lest tilbake fra driveren og kontrollert.","VectorFit");return true;}
            finally{busy=false;UseWaitCursor=false;ShowPage(2);}
        }
        void CheckRawProfile() {
            if(session.UsesInterception)throw new Exception("Velg Standard · Raw Accel først.");
            if(session.Baseline.Count==0)throw new Exception("Åpne den lagrede målingen din, eller fullfør kalibreringen først.");
            if(!session.CanTryRawAccel)throw new Exception("Kalibreringen er ikke komplett, eller profilen ligger utenfor tillatte grenser.\n"+session.Notes);
        }
        void ExportProfile() {
            CheckRawProfile();
            if(!session.ReadyForRawAccel&&UiMessage.Show(this,"Forbedring er ikke bekreftet.\n"+session.Notes+"\n\nEksportere en prøveprofil likevel?", "Eksporter prøveprofil",MessageBoxButtons.YesNo,MessageBoxIcon.Information,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;
            SaveSession();string file=Driver.Export(session,RawProfileName(),!session.ReadyForRawAccel);
            UiMessage.Show(this,"Raw Accel-profilen er lagret her:\n"+file+"\n\nDen inneholder følsomhet, retning og rotasjon. Ingen innstillinger er aktivert.","Profil eksportert");
        }
        void Neutralize() {
            EnsureFilterStopped();RefreshDriver();
            if(activeJson==null)throw new Exception("Ingen tilgjengelig driver å nøytralisere.");
            if(Driver.Neutral(activeJson)){UiMessage.Show(this,"Oppsettet er allerede nøytralt.");return;}
            string p=Path.Combine(Driver.Data,"neutral.rawaccel.json");Directory.CreateDirectory(Driver.Data);File.WriteAllText(p,Driver.Config(new Correction()).ToString());
            Driver.Apply(p,true);RefreshDriver();
            if(activeJson==null||!Driver.Neutral(activeJson))throw new Exception("Nøytralisering kunne ikke bekreftes. Målingen er stoppet.");
            ShowPage(2);
        }
        void Restore() {
            EnsureFilterStopped();
            string p=Driver.LastBackup();if(p==null)throw new Exception("Ingen sikkerhetskopi er laget av VectorFit ennå.");
            Driver.Apply(p,false);UiMessage.Show(this,"Forrige oppsett er gjenopprettet og kontrollert.");ShowPage(2);
        }
    }
    class Radar:Control {
        readonly Session session;public Radar(Session s){session=s;DoubleBuffered=true;}
        protected override void OnPaint(PaintEventArgs e) {
            var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;float cx=Width/2,cy=Height/2+2,r=Math.Min(Width,Height)*.32f;
            using(var pen=new Pen(Color.FromArgb(65,80,86),1)) {
                g.DrawEllipse(pen,cx-r,cy-r,2*r,2*r);
                for(int d=0;d<8;d++){double a=d*Math.PI/4;g.DrawLine(pen,cx,cy,cx+r*(float)Math.Cos(a),cy+r*(float)Math.Sin(a));}
            }
            var points=new PointF[8];var valid=session.Baseline.Where(t=>t.Valid).ToList();
            using(var font=new Font("Segoe UI",11,FontStyle.Regular,GraphicsUnit.Pixel))for(int d=0;d<8;d++){
                double a=d*Math.PI/4;var ds=valid.Where(t=>t.Direction==d).ToList();double reach=ds.Count>0?Analysis.Median(ds.Select(t=>t.Reach)):1;
                points[d]=new PointF(cx+r*(float)(reach*Math.Cos(a)),cy+r*(float)(reach*Math.Sin(a)));
                string label=L.T(Analysis.Directions[d]);SizeF size=g.MeasureString(label,font);PaintText.Draw(g,label,font,Brushes.LightGray,cx+(r+30)*(float)Math.Cos(a)-size.Width/2,cy+(r+24)*(float)Math.Sin(a)-size.Height/2);
            }
            using(var fill=new SolidBrush(Color.FromArgb(35,109,218,182)))g.FillPolygon(fill,points);
            using(var pen=new Pen(Color.FromArgb(109,218,182),2))g.DrawPolygon(pen,points);
            using(var font=new Font("Segoe UI",11,FontStyle.Regular,GraphicsUnit.Pixel))PaintText.Draw(g,"Klikkposisjon / målavstand. Ringen er 100 %.",font,Brushes.LightGray,10,Height-20);
        }
    }
}
