using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
namespace VectorFit {
 partial class MainForm {
  void PracticePage(){var stack=Stack();content.Controls.Add(stack);
   Add(stack,TextLabel(Practice.Text("Tren med profilen din","Practice with your profile"),22,true,42));
   Add(stack,TextLabel(Practice.Text("Ingen nye korreksjoner beregnes. Treningen bruker input som Windows leverer. Aktiv global profil legges ikke på en gang til. Velg DPI, cm/360 og FOV under Kalibrer først.","No corrections are fitted. Practice uses the input delivered by Windows; an active global profile is not applied twice. Set DPI, cm/360 and FOV under Calibrate first."),11,false,60));
   var duration=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=100};duration.Items.AddRange(new object[]{30,60,120});duration.SelectedIndex=1;
   var config=new FlowLayoutPanel{Height=40};config.Controls.Add(new Label{Text=Practice.Text("Sekunder per øvelse","Seconds per exercise"),Width=200,Height=30});config.Controls.Add(duration);Add(stack,config);
   var buttons=new FlowLayoutPanel{Height=54};foreach(string mode in new[]{"flick","precision","tracking","switch"}){string chosen=mode;buttons.Controls.Add(Button(Practice.Name(mode),delegate{RunPractice(new[]{chosen},(int)duration.SelectedItem);},200));}Add(stack,buttons);
   var routine=new CheckedListBox{Height=88,CheckOnClick=true,BackColor=panel,ForeColor=ink,BorderStyle=BorderStyle.None};foreach(var mode in new[]{"flick","precision","tracking","switch"})routine.Items.Add(Practice.Name(mode),true);
   string routineFile=Path.Combine(Driver.Data,"practice-routine.json");
   try{if(File.Exists(routineFile)){var saved=JObject.Parse(File.ReadAllText(routineFile));int seconds=(int)saved["Seconds"];if(new[]{30,60,120}.Contains(seconds))duration.SelectedItem=seconds;var modes=saved["Modes"].ToObject<string[]>();for(int i=0;i<4;i++)routine.SetItemChecked(i,modes.Contains(new[]{"flick","precision","tracking","switch"}[i]));}}catch{}
   AddDetails(stack,Practice.Text("Min treningsrutine","My practice routine"),routine,TextLabel(Practice.Text("Velg øvelsene. Rutinen lagres når du starter. Hver del venter på et klikk, slik at du kan ta pause.","Choose exercises. Your routine is saved when started. Each part waits for a click, so you can rest between exercises."),11,false,45));
   Add(stack,Button(Practice.Text("Start min rutine","Start my routine"),delegate{var modes=routine.CheckedIndices.Cast<int>().Select(i=>new[]{"flick","precision","tracking","switch"}[i]).ToArray();if(modes.Length==0)throw new Exception(Practice.Text("Velg minst én øvelse.","Choose at least one exercise."));File.WriteAllText(routineFile,JObject.FromObject(new{Seconds=(int)duration.SelectedItem,Modes=modes}).ToString());RunPractice(modes,(int)duration.SelectedItem);},260,true));
   var records=Practice.Load();Add(stack,TextLabel(Practice.Text("Treningshistorikk","Practice history"),18,true,38));
   if(records.Count==0){Add(stack,TextLabel(Practice.Text("Din første økt vil vises her. Trening holdes separat fra kalibrering.","Your first session will appear here. Practice is kept separate from calibration."),11,false,45));return;}
   var list=new ListBox{Height=150,BackColor=panel,ForeColor=ink,BorderStyle=BorderStyle.None};foreach(var r in records)list.Items.Add(r.Utc.Substring(0,Math.Min(16,r.Utc.Length)).Replace('T',' ')+" UTC · "+Practice.Name(r.Mode)+" · "+r.Seconds+" s"+(r.Complete?"":Practice.Text(" · avbrutt"," · interrupted")));Add(stack,list);
   var summary=TextLabel("",11,false,100);Add(stack,summary);var chart=new PracticeHistoryChart();Add(stack,chart);
   list.SelectedIndexChanged+=delegate{if(list.SelectedIndex<0)return;var r=records[list.SelectedIndex];summary.Text=Practice.Summary(r);chart.Set(records.Where(v=>r.Comparable(v)).OrderBy(v=>v.Utc).ToList());};list.SelectedIndex=0;
   Add(stack,Button(Practice.Text("Spill av valgt økt","Replay selected session"),delegate{if(list.SelectedIndex>=0)using(var replay=new ReplayForm(records[list.SelectedIndex]))replay.ShowDialog(this);},300));
  }
  void RunPractice(string[] modes,int seconds){
   EnsureFilterStopped();RefreshDriver();string before=Driver.Fingerprint(activeJson);bool known=activeJson!=null||Driver.KnownAbsent(driverError);
   var summaries=new List<string>();
   foreach(string mode in modes){using(var form=new PracticeForm(draft,mode,seconds,known?before:"unknown-"+Guid.NewGuid().ToString("N"))){form.ShowDialog(this);var r=form.Result;if(r.Elapsed<=0)break;
     string err;string after=Driver.TryRead(out err);if(Driver.Fingerprint(after)!=before||(after==null&&!Driver.KnownAbsent(err)))r.Input="changed-or-unknown-"+r.Id;
     Practice.Save(r);summaries.Add(Practice.Summary(r));if(!r.Complete)break;}}
   ShowPage(5);if(summaries.Count>0)UiMessage.Show(this,String.Join("\n\n",summaries),Practice.Text("Treningsresultat","Practice results"));
  }
 }
 class PracticeHistoryChart:Control {
  List<PracticeResult> rows=new List<PracticeResult>();public PracticeHistoryChart(){Height=230;DoubleBuffered=true;BackColor=Color.FromArgb(28,34,38);}
  public void Set(List<PracticeResult> values){rows=values.Skip(Math.Max(0,values.Count-20)).ToList();Invalidate();}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;using(var f=new Font("Segoe UI",13,GraphicsUnit.Pixel)){
   g.DrawString(Practice.Text("Samme oppsett · siste 20 fullførte økter · høyere er bedre","Same setup · last 20 completed sessions · higher is better"),f,Brushes.White,15,12);
   if(rows.Count==0){g.DrawString(Practice.Text("Ingen sammenlignbare økter med nok registrert tid.","No comparable sessions with sufficient observed time."),f,Brushes.LightGray,15,50);return;}
   bool tracking=rows[0].Mode=="tracking";double max=tracking?100:Math.Max(1,rows.Max(r=>r.HitsPerMinute)*1.1);float width=(Width-60f)/rows.Count;
   for(int i=0;i<rows.Count;i++){double value=tracking?rows[i].Tracking:rows[i].HitsPerMinute;float h=(float)(value/max*115);using(var b=new SolidBrush(Color.FromArgb(109,218,182)))g.FillRectangle(b,30+i*width,170-h,Math.Max(2,width-8),h);g.DrawString(value.ToString("F1"),f,Brushes.White,30+i*width,150-h);if(rows.Count<=7)g.DrawString(rows[i].Utc.Substring(5,5),f,Brushes.LightGray,30+i*width,179);}
   g.DrawString(tracking?Practice.Text("Tid på mål (%) · utvikling viser også øvelse og dagsform","Time on target (%) · trends also reflect practice and daily variation"):Practice.Text("Treff per minutt · les også treffprosent og tid til treff ovenfor","Hits per minute · also read accuracy and time to hit above"),f,Brushes.LightGray,15,207);
  }}
 }
 class ReplayForm:Form {
  readonly List<PracticeSample> samples=new List<PracticeSample>();readonly List<AimClick> clicks=new List<AimClick>();readonly List<Trial> trials=new List<Trial>();
  readonly Timer timer=new Timer();readonly TrackBar seek=new TrackBar{Dock=DockStyle.Bottom,Minimum=0,Maximum=1000,TickStyle=TickStyle.None};readonly ComboBox selection=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=360};
  readonly ComboBox speed=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=80};readonly Label info=new Label{Dock=DockStyle.Top,Height=62,ForeColor=Color.White};readonly ReplayCanvas canvas=new ReplayCanvas();bool playing;double time,end;readonly Stopwatch clock=Stopwatch.StartNew();double last;
  public ReplayForm(Session s):this(){foreach(var group in new[]{s.Baseline,s.PrecisionBaseline,s.Validation,s.PrecisionValidation})foreach(var t in group){trials.Add(t);selection.Items.Add((group==s.Baseline?"Baseline flick":group==s.PrecisionBaseline?"Baseline precision":group==s.Validation?"A/B flick":"A/B precision")+" · "+(t.Corrected?"B":"A")+" · "+(t.Hit==true?Practice.Text("Treff","Hit"):Practice.Text("Bom / tidsgrense","Miss / timeout"))+" · "+t.TotalMs.ToString("F0")+" ms");}
   selection.SelectedIndexChanged+=delegate{LoadTrial(trials[selection.SelectedIndex]);};if(trials.Count>0)selection.SelectedIndex=0;else info.Text=Practice.Text("Ingen lagrede forsøk.","No recorded trials.");
  }
  public ReplayForm(PracticeResult r):this(){selection.Visible=false;samples.AddRange(r.Samples);clicks.AddRange(r.ShotEvents);end=r.Elapsed;info.Text=Practice.Name(r.Mode)+" · "+Practice.Text("Registrert siktebane, ikke video","Recorded aim path, not video");canvas.Tracking=true;canvas.Radius=r.Mode=="tracking"?1.5:r.Mode=="precision"?.45:1;UpdateFrame();}
  ReplayForm(){Text=Practice.Text("TrueTrack · avspilling","TrueTrack · replay");Size=new Size(1100,780);MinimumSize=new Size(800,600);AutoScaleMode=AutoScaleMode.None;BackColor=Color.FromArgb(19,24,27);Font=new Font("Segoe UI",14,GraphicsUnit.Pixel);StartPosition=FormStartPosition.CenterParent;
   var toolbar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=45};var play=new Button{Text=Practice.Text("Spill / pause","Play / pause"),Width=150,Height=34,ForeColor=Color.White,BackColor=Color.FromArgb(40,52,56),FlatStyle=FlatStyle.Flat};play.Click+=delegate{if(time>=end)time=0;playing=!playing;last=clock.Elapsed.TotalMilliseconds;};speed.Items.AddRange(new object[]{"0.25×","0.5×","1×"});speed.SelectedIndex=2;
   toolbar.Controls.Add(selection);toolbar.Controls.Add(play);toolbar.Controls.Add(speed);Controls.Add(canvas);Controls.Add(info);Controls.Add(toolbar);Controls.Add(seek);canvas.Dock=DockStyle.Fill;
   seek.Scroll+=delegate{time=end*seek.Value/1000;UpdateFrame();};timer.Interval=16;timer.Tick+=delegate{double now=clock.Elapsed.TotalMilliseconds;if(playing){time=Math.Min(end,time+(now-last)*new[]{.25,.5,1}[speed.SelectedIndex]);if(time>=end)playing=false;UpdateFrame();}last=now;};timer.Start();FormClosed+=delegate{timer.Stop();timer.Dispose();};
  }
#if DEVELOPMENT
  public void Preview(){time=end*.8;UpdateFrame();}
#endif
  void LoadTrial(Trial t){playing=false;time=0;end=t.TotalMs;samples.Clear();clicks.Clear();samples.AddRange(t.Samples.Select(m=>new PracticeSample{T=m.T,X=m.X,Y=m.Y,TargetX=t.TargetX,TargetY=t.TargetY}));clicks.AddRange(t.Clicks);canvas.Tracking=false;canvas.Radius=t.HitRadiusDegrees>0?t.HitRadiusDegrees:.8;
   var q=Trajectory.Measure(t);info.Text=samples.Count<2?Practice.Text("Bevegelsesdata mangler for dette forsøket.","Movement data are missing for this trial."):Practice.Text("Tid til klikk: ","Time to click: ")+t.TotalMs.ToString("F0")+" ms · "+(q.Available?Practice.Text("Etterkorrigering: ","Corrective travel: ")+(100*q.CorrectionDistance).ToString("F1")+" %":"")+"\n"+Practice.Text("Hvitt: siktebane · grønt: mål/treff · rødt: bomklikk","White: aim path · green: target/hit · red: missed click");UpdateFrame();
  }
  void UpdateFrame(){seek.Value=end<=0?0:(int)Analysis.Clamp(time/end*1000,0,1000);canvas.Samples=samples;canvas.Clicks=clicks;canvas.Time=time;canvas.Invalidate();}
 }
 class ReplayCanvas:Control {
  public List<PracticeSample> Samples=new List<PracticeSample>();public List<AimClick> Clicks=new List<AimClick>();public double Time,Radius=.8;public bool Tracking;
  public ReplayCanvas(){DoubleBuffered=true;BackColor=Color.FromArgb(28,34,38);}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(Samples.Count==0)return;var g=e.Graphics;g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
   double minX=Samples.Min(p=>Math.Min(p.X,p.TargetX))-3,maxX=Samples.Max(p=>Math.Max(p.X,p.TargetX))+3,minY=Samples.Min(p=>Math.Min(p.Y,p.TargetY))-3,maxY=Samples.Max(p=>Math.Max(p.Y,p.TargetY))+3;
   double scale=Math.Min((Width-80)/(maxX-minX),(Height-80)/(maxY-minY));Func<double,double,PointF> project=(x,y)=>new PointF((float)(Width/2+(x-(minX+maxX)/2)*scale),(float)(Height/2+(y-(minY+maxY)/2)*scale));
   var shown=Samples.Where(p=>p.T<=Time).ToList();if(shown.Count==0)shown.Add(Samples[0]);var current=shown.Last();var target=project(current.TargetX,current.TargetY);float radius=(float)(Radius*scale);
   using(var p=new Pen(Color.FromArgb(109,218,182),2))g.DrawEllipse(p,target.X-radius,target.Y-radius,radius*2,radius*2);
   if(shown.Count>1)g.DrawLines(Pens.LightGray,shown.Select(p=>project(p.X,p.Y)).ToArray());
   foreach(var c in Clicks.Where(c=>c.T<=Time)){var p=project(c.X,c.Y);g.FillEllipse(c.Hit?Brushes.MediumAquamarine:Brushes.IndianRed,p.X-4,p.Y-4,8,8);}
   var at=project(current.X,current.Y);g.FillEllipse(Brushes.White,at.X-4,at.Y-4,8,8);
   using(var f=new Font("Segoe UI",12,GraphicsUnit.Pixel))g.DrawString(Time.ToString("F0")+" ms · "+Practice.Text("vinkelkart (grader)","angular map (degrees)"),f,Brushes.White,15,15);
  }
 }
}
