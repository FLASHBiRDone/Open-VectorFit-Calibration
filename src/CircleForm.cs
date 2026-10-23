using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VectorFit {
    class CircleForm:Form {
        readonly Setup setup;readonly Correction input;readonly List<CircleLap> plan;readonly Stopwatch clock=Stopwatch.StartNew();readonly Timer timer=new Timer();
        readonly Font title=new Font("Segoe UI",24,FontStyle.Bold,GraphicsUnit.Pixel),body=new Font("Segoe UI",15,GraphicsUnit.Pixel),small=new Font("Segoe UI",13,GraphicsUnit.Pixel);
        public readonly CircleData Data=new CircleData{Protocol=3};public bool Completed;public string DeviceName="";
        double introShown,introExit;
        CircleTracker tracker;int index;double x,y,deadline,started;string phase="ready",feedback="",transition="";bool captured;IntPtr device=IntPtr.Zero;
        public CircleForm(Setup value,Correction inputCorrection=null){
            setup=value;input=inputCorrection??new Correction();plan=CircleData.Plan();x=plan[0].Radius;
            Text=L.T("VectorFit | Sirkeltest");BackColor=Color.FromArgb(17,21,24);DoubleBuffered=true;KeyPreview=true;AutoScaleMode=AutoScaleMode.None;WindowState=FormWindowState.Maximized;FormBorderStyle=FormBorderStyle.None;
            timer.Interval=16;timer.Tick+=delegate{
                double now=clock.Elapsed.TotalMilliseconds;
                if(phase=="intro-out"&&now-introExit>=320)BeginNext(true);
                if(phase=="countdown"&&now>=deadline)BeginDrawing();
                if(phase=="drawing"&&now-started>20000){tracker.Lap.Reason="Tiden gikk ut";FinishLap();}
                Invalidate();
            };
            Shown+=delegate{introShown=clock.Elapsed.TotalMilliseconds;CaptureInput();timer.Start();};Deactivate+=delegate{if(captured)Pause();};
            FormClosing+=delegate{if(tracker!=null){tracker.Lap.Reason="Avsluttet";Data.Laps.Add(tracker.Lap);tracker=null;}};
            KeyDown+=delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Escape){Close();e.Handled=true;}if(e.KeyCode==Keys.Space&&phase=="paused"){CaptureInput();BeginNext(true);e.Handled=true;}};
        }
        void CaptureInput(){try{Native.Register(Handle,true);Cursor.Clip=RectangleToScreen(ClientRectangle);Cursor.Hide();captured=true;}catch(Exception e){UiMessage.Show(this,e.Message);Close();}}
        void ReleaseInput(){if(!captured)return;captured=false;Cursor.Clip=Rectangle.Empty;Cursor.Show();try{Native.Register(Handle,false);}catch{}}
        void Pause(){ReleaseInput();if(tracker!=null&&phase=="drawing"){tracker.Lap.Reason="Pause";Data.Laps.Add(tracker.Lap);}tracker=null;phase="paused";Invalidate();}
        void BeginNext(bool reset){
            if(index>=plan.Count){Completed=true;Close();return;}
            if(reset){x=plan[index].Radius;y=0;phase="countdown";deadline=clock.Elapsed.TotalMilliseconds+(index==0?1500:2500);}
            else BeginDrawing();
        }
        void BeginDrawing(){transition="";var item=plan[index];tracker=new CircleTracker(new CircleLap{Radius=item.Radius,Clockwise=item.Clockwise},x,y,clock.Elapsed.TotalMilliseconds,input,true);started=clock.Elapsed.TotalMilliseconds;phase="drawing";}
        void FinishLap(){
            var previous=plan[index];tracker.Lap.Counted=true;Data.Laps.Add(tracker.Lap);tracker=null;feedback="";index++;
            bool reset=index>=plan.Count||previous.Radius!=plan[index].Radius||previous.Clockwise!=plan[index].Clockwise;
            transition=index<plan.Count&&previous.Clockwise!=plan[index].Clockwise?"Bytter dreieretning":index<plan.Count&&previous.Radius!=plan[index].Radius?"Bytter størrelse":"";
            BeginNext(reset);
        }
        protected override void WndProc(ref Message message){
            if(message.Msg==0xFF&&captured){IntPtr handle;int dx,dy;ushort buttons;bool absolute;
                if(Native.Read(message.LParam,out handle,out dx,out dy,out buttons,out absolute)&&!absolute){
                    if(device==IntPtr.Zero&&(buttons&1)!=0){string name=Native.Name(handle);if(setup.Device!=""&&setup.Device!=name){feedback="Bruk samme mus og tilkobling som i kalibreringen.";}else{device=handle;DeviceName=name;}}
                    if(device!=IntPtr.Zero&&device==handle){
                        if(phase=="drawing"&&(dx!=0||dy!=0)){x+=dx*setup.DegreesPerCount;y+=dy*setup.DegreesPerCount;if(tracker.Add(x,y,clock.Elapsed.TotalMilliseconds))FinishLap();}
                        if((buttons&1)!=0&&phase=="ready"){introExit=clock.Elapsed.TotalMilliseconds;phase="intro-out";}
                    }
                }
            }base.WndProc(ref message);
        }
        double PixelsPerDegree {get{return Math.Max(1,Math.Min(ClientSize.Width/(2*Math.Tan(setup.Hfov*Math.PI/360))*Math.PI/180,(ClientSize.Height-260)/36.0));}}
        protected override void OnPaint(PaintEventArgs e){
            base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;int cx=ClientSize.Width/2,cy=ClientSize.Height/2+15;
            var item=plan[Math.Min(index,plan.Count-1)];double scale=PixelsPerDegree;string size="Naturlig bevegelse · alle runder teller";
            using(var ink=new SolidBrush(Color.FromArgb(237,244,242)))using(var muted=new SolidBrush(Color.FromArgb(169,184,188)))using(var green=new SolidBrush(Color.FromArgb(109,218,182)))using(var dot=new SolidBrush(Color.FromArgb(123,173,242))){
                PaintText.Draw(g,"Sirkeltest · "+index+" / 60",title,ink,32,24);
                PaintText.Draw(g,(item.Clockwise?"Med klokken":"Mot klokken")+" · "+(index%30)+" / 30 · "+size,body,green,32,62);
                PaintText.Draw(g,"Gjør naturlige rundbevegelser rundt midten. Ikke prøv å tegne pent eller rette opp formen.",small,muted,32,91);
                DrawDirectionArrow(g,cx,cy,item.Clockwise);
                if(phase=="countdown"){
                    string countdown=(transition==""?"Gjør deg klar":transition)+" · "+Math.Max(1,(int)Math.Ceiling((deadline-clock.Elapsed.TotalMilliseconds)/1000.0));
                    var sizeText=g.MeasureString(L.T(countdown),body);PaintText.Draw(g,countdown,body,ink,cx-sizeText.Width/2,cy+48);
                }
                float pointerX=(float)(cx+x*scale),pointerY=(float)(cy+y*scale);using(var pen=new Pen(Color.FromArgb(237,244,242),1.5f)){g.DrawLine(pen,pointerX-9,pointerY,pointerX+9,pointerY);g.DrawLine(pen,pointerX,pointerY-9,pointerX,pointerY+9);}
                string instruction=phase=="ready"?DriverSetup.Text("Ingen krav til fine eller lukkede sirkler. Alle forsøk teller. Klikk for å starte.","No need for neat or closed circles. Every attempt counts. Click to start."):phase=="paused"?"Pause. Trykk mellomrom for å fortsette.":phase=="countdown"?"Gjør deg klar. Finn en behagelig bevegelse.":"La formen falle naturlig. Det er ingen punkter du må treffe.";
                if(phase!="ready"&&phase!="intro-out")PaintText.Draw(g,instruction,body,ink,32,Height-112);
                if(feedback!="")PaintText.Draw(g,feedback,small,muted,32,Height-137);
                PaintText.Draw(g,"Datainnhenting → analyse → neste fase",small,muted,32,Height-90);
                ProgressStrip.DrawBar(g,new RectangleF(32,Height-65,Width-64,32),(index+(tracker==null?0:tracker.Fraction))/60.0,index+" / 60");
                PaintText.Draw(g,"Esc avslutter  ·  Alt+Tab setter på pause",small,muted,32,Height-28);
                if(phase=="ready"||phase=="intro-out")DrawIntro(g);
            }
        }
        void DrawIntro(Graphics g){
            double now=clock.Elapsed.TotalMilliseconds;
            float a=(float)Analysis.Clamp(phase=="intro-out"?1-(now-introExit)/320:(now-introShown)/650,0,1);a=a*a*(3-2*a);
            float w=Math.Min(1000,ClientSize.Width-80),left=(ClientSize.Width-w)/2,top=ClientSize.Height/2f-210;
            using(var shade=new SolidBrush(Color.FromArgb((int)(a*250),17,21,24)))g.FillRectangle(shade,left-20,top-30,w+40,440);
            using(var heading=new Font("Segoe UI",36,FontStyle.Bold,GraphicsUnit.Pixel))using(var text=new Font("Segoe UI",24,GraphicsUnit.Pixel))
            using(var ink=new SolidBrush(Color.FromArgb((int)(255*a),237,244,242)))using(var accent=new SolidBrush(Color.FromArgb((int)(255*a),109,218,182)))
            using(var sf=new StringFormat{Alignment=StringAlignment.Center}){
                g.DrawString(DriverSetup.Text("Finn din naturlige bevegelse","Discover your natural movement"),heading,ink,new RectangleF(left,top,w,65),sf);
                g.DrawString(DriverSetup.Text("Gjør 30 runder med klokken, deretter 30 mot klokken.\nPilen viser retningen. Vi tar en pause når retningen byttes.","Make 30 clockwise turns, then 30 counterclockwise.\nFollow the arrow. There is a pause when direction changes."),text,ink,new RectangleF(left,top+90,w,95),sf);
                g.DrawString(DriverSetup.Text("Ikke prøv å tegne perfekte sirkler eller rette opp formen.\nBeveg deg naturlig. Alle forsøk teller.","Do not try to draw perfect circles or correct their shape.\nMove naturally. Every attempt counts."),text,ink,new RectangleF(left,top+195,w,80),sf);
                g.DrawString(DriverSetup.Text("Sentrer musen på underlaget og finn en behagelig håndstilling.","Center your mouse on the pad and find a comfortable hand position."),text,ink,new RectangleF(left,top+285,w,65),sf);
                g.DrawString(DriverSetup.Text("Klikk når du er klar","Click when you are ready"),text,accent,new RectangleF(left,top+365,w,40),sf);
            }
        }
        internal static void DrawDirectionArrow(Graphics g,float cx,float cy,bool clockwise){
            const float radius=27;float start=clockwise?-135:-45,sweep=clockwise?270:-270;
            using(var pen=new Pen(Color.FromArgb(109,218,182),3)){pen.StartCap=LineCap.Round;g.DrawArc(pen,cx-radius,cy-radius,2*radius,2*radius,start,sweep);}
            double angle=(start+sweep)*Math.PI/180,sign=clockwise?1:-1;var tip=new PointF(cx+radius*(float)Math.Cos(angle),cy+radius*(float)Math.Sin(angle));
            float tx=(float)(-Math.Sin(angle)*sign),ty=(float)(Math.Cos(angle)*sign);
            var triangle=new[]{tip,new PointF(tip.X-tx*12-ty*6,tip.Y-ty*12+tx*6),new PointF(tip.X-tx*12+ty*6,tip.Y-ty*12-tx*6)};
            using(var brush=new SolidBrush(Color.FromArgb(109,218,182)))g.FillPolygon(brush,triangle);
        }
        protected override void Dispose(bool disposing){if(disposing){timer.Stop();ReleaseInput();timer.Dispose();title.Dispose();body.Dispose();small.Dispose();}base.Dispose(disposing);}
#if DEVELOPMENT
        public void PreviewIntro(){WindowState=FormWindowState.Normal;Size=new Size(1000,740);phase="ready";introShown=-1000;}
        public void Preview(){WindowState=FormWindowState.Normal;Size=new Size(1280,800);index=3;x=0;y=plan[index].Radius;phase="drawing";}
        public void PreviewTransition(bool clockwise){Preview();index=clockwise?0:30;plan[index].Clockwise=clockwise;x=plan[index].Radius;y=0;transition=clockwise?"Gjør deg klar":"Bytter dreieretning";phase="countdown";deadline=clock.Elapsed.TotalMilliseconds+2500;}
#endif
    }
}
