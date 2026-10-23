using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
namespace VectorFit {
 public class PracticeSample { public double T,X,Y,TargetX,TargetY; public bool OnTarget; }
 public class PracticeResult {
  public string Id=Guid.NewGuid().ToString("N"),Utc=DateTime.UtcNow.ToString("o"),Mode,Input,Device="";
  public int Protocol=1,Seconds,Hits,Clicks,Attempts;public bool Complete;public Setup Setup;
  public double Elapsed,OnTargetMs,ObservedMs,ErrorIntegral;public List<double> HitTimes=new List<double>();
  public List<AimClick> ShotEvents=new List<AimClick>();
  public List<PracticeSample> Samples=new List<PracticeSample>();
  public double Accuracy {get{return Clicks==0?0:100.0*Hits/Clicks;}}
  public double Coverage {get{return Elapsed<=0?0:ObservedMs/Elapsed;}}
  public double Tracking {get{return ObservedMs<=0?0:100*OnTargetMs/ObservedMs;}}
  public double HitsPerMinute {get{return Elapsed<=0?0:Hits*60000/Elapsed;}}
  public bool Comparable(PracticeResult b){return Complete&&b.Complete&&Coverage>=.95&&b.Coverage>=.95&&Protocol==b.Protocol&&Mode==b.Mode&&Seconds==b.Seconds&&Input==b.Input&&Device==b.Device&&JToken.DeepEquals(JObject.FromObject(Setup),JObject.FromObject(b.Setup));}
 }
 static class Practice {
  public static string Text(string nb,string en){return DriverSetup.Text(nb,en);}
  public static string Name(string mode){return mode=="tracking"?Text("Tracking · følg målet","Tracking · follow the target"):mode=="switch"?Text("Målbytte","Target switching"):mode=="precision"?Text("Presisjon","Precision"):"Flick";}
  public static PointD Target(double ms){double t=ms/1000;return new PointD(18*Math.Sin(t*.67),9*Math.Sin(t*1.13));}
  public static bool Inside(PointD target,double x,double y,double radius){return AimGeometry.Offset(target.X,target.Y,x,y).Length<=Math.Tan(radius*Math.PI/180);}
  public static readonly PointD[] Spots={new PointD(-14,-7),new PointD(14,-7),new PointD(14,7),new PointD(-14,7)};
  public static void Integrate(PracticeResult r,double dt,bool on,double error){if(dt<=0||dt>50)return;r.ObservedMs+=dt;if(on)r.OnTargetMs+=dt;r.ErrorIntegral+=error*dt;}
  public static string Folder {get{return Path.Combine(Driver.Data,"practice");}}
  public static void Save(PracticeResult r){Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,r.Id+".json"),JObject.FromObject(r).ToString());}
  public static List<PracticeResult> Load(){var list=new List<PracticeResult>();if(!Directory.Exists(Folder))return list;foreach(var file in Directory.GetFiles(Folder,"*.json").OrderByDescending(File.GetLastWriteTimeUtc).Take(1000))try{if(new FileInfo(file).Length<20*1024*1024){var r=JObject.Parse(File.ReadAllText(file)).ToObject<PracticeResult>();if(r!=null&&r.Setup!=null&&r.Samples!=null&&r.HitTimes!=null&&r.ShotEvents!=null&&r.Utc!=null&&r.Utc.Length>=10&&r.Samples.All(v=>v!=null))list.Add(r);}}catch{}return list.OrderByDescending(r=>r.Utc).ToList();}
  public static string Summary(PracticeResult r){return Name(r.Mode)+" · "+(r.Complete?Text("Fullført","Complete"):Text("Avbrutt","Interrupted"))+"\n"+(r.Mode=="tracking"?Text("Tid på målet ↑: ","Time on target ↑: ")+r.Tracking.ToString("F1")+" %":Text("Treff/min ↑: ","Hits/min ↑: ")+r.HitsPerMinute.ToString("F1")+" · "+Text("Treffprosent ↑: ","Hit accuracy ↑: ")+r.Accuracy.ToString("F1")+" % · "+Text("Tid til treff ↓: ","Successful hit time ↓: ")+(r.HitTimes.Count==0?"—":Analysis.Median(r.HitTimes).ToString("F0")+" ms"))+"\n"+Text("Registrert tid: ","Observed time: ")+(100*r.Coverage).ToString("F1")+" %";}
 }
 class PracticeForm:Form {
  public readonly PracticeResult Result; readonly Timer timer=new Timer();readonly Stopwatch clock=new Stopwatch();readonly Random random=new Random(1729);
  readonly Font body=new Font("Segoe UI",17,GraphicsUnit.Pixel),heading=new Font("Segoe UI",26,FontStyle.Bold,GraphicsUnit.Pixel);
  bool captured,running,ready=true,paused;IntPtr device;double x,y,last,targetStart,nextOnset;int active;PointD target=new PointD(14,0);
  double Radius {get{return Result.Mode=="tracking"?1.5:Result.Mode=="precision"?.45:1;}}
  public PracticeForm(Setup setup,string mode,int seconds,string input){
   Result=new PracticeResult{Setup=JObject.FromObject(setup).ToObject<Setup>(),Mode=mode,Seconds=seconds,Input=input};Result.Setup.Arena=true;Result.Setup.Crosshair=Sight.Preference();
   DoubleBuffered=true;AutoScaleMode=AutoScaleMode.None;KeyPreview=true;FormBorderStyle=FormBorderStyle.None;WindowState=FormWindowState.Maximized;BackColor=Color.FromArgb(19,24,27);
   timer.Interval=8;timer.Tick+=delegate{Step();};
   Shown+=delegate{CaptureInput();timer.Start();};Deactivate+=delegate{if(captured){clock.Stop();running=false;paused=true;Release();Invalidate();}};
   KeyDown+=delegate(object o,KeyEventArgs e){if(e.KeyCode==Keys.Escape)Close();if(e.KeyCode==Keys.Space&&paused){paused=false;ready=true;CaptureInput();Invalidate();}};
   FormClosed+=delegate{clock.Stop();timer.Stop();Release();timer.Dispose();body.Dispose();heading.Dispose();Result.Elapsed=Math.Min(clock.Elapsed.TotalMilliseconds,Result.Seconds*1000.0);};
  }
  void CaptureInput(){try{Native.Register(Handle,true);captured=true;Cursor.Hide();Cursor.Clip=RectangleToScreen(ClientRectangle);}catch(Exception e){UiMessage.Show(e.Message,"TrueTrack");Close();}}
  void Release(){if(!captured)return;captured=false;Cursor.Clip=Rectangle.Empty;Cursor.Show();try{Native.Register(Handle,false);}catch{}}
  protected override void WndProc(ref Message m){if(m.Msg==0xFF&&captured){IntPtr d;int dx,dy;ushort buttons;bool absolute;if(Native.Read(m.LParam,out d,out dx,out dy,out buttons,out absolute)&&!absolute){if(device==IntPtr.Zero&&(buttons&1)!=0){device=d;Result.Device=Native.Name(d);}if(device==d){Step();if(running){x+=dx*Result.Setup.DegreesPerCount;y=Analysis.Clamp(y+dy*Result.Setup.DegreesPerCount,-85,85);}if((buttons&1)!=0)ClickRaw();}}}base.WndProc(ref m);}
  void ClickRaw(){if(ready){ready=false;running=true;clock.Start();last=clock.Elapsed.TotalMilliseconds;if(Result.Samples.Count==0)Next();return;}if(!running||Result.Mode=="tracking"||clock.Elapsed.TotalMilliseconds<nextOnset)return;
   Result.Clicks++;bool hit=Practice.Inside(target,x,y,Radius);Result.ShotEvents.Add(new AimClick(clock.Elapsed.TotalMilliseconds,x,y,hit));if(hit){Result.Hits++;Result.HitTimes.Add(clock.Elapsed.TotalMilliseconds-targetStart);}
   if(hit||Result.Mode=="flick"){Result.Attempts++;Next();}Invalidate();
  }
  void Next(){double now=clock.Elapsed.TotalMilliseconds;
   if(Result.Mode=="switch"){active=(active+1+random.Next(3))%4;target=Practice.Spots[active];nextOnset=now;}
   else if(Result.Mode!="tracking"){double a=random.Next(8)*Math.PI/4;double distance=random.Next(2)==0?8:14;target=new PointD(x+Math.Cos(a)*distance,Analysis.Clamp(y+Math.Sin(a)*distance,-60,60));nextOnset=now+250;}
   targetStart=nextOnset;
  }
  void Step(){if(!running)return;double now=Math.Min(clock.Elapsed.TotalMilliseconds,Result.Seconds*1000.0),dt=now-last;
   if(dt<=0)return;last=now;Result.Elapsed=now;
   if(Result.Mode=="tracking")target=Practice.Target(now);
   var offset=AimGeometry.Offset(target.X,target.Y,x,y);bool on=Practice.Inside(target,x,y,Radius);
   Practice.Integrate(Result,dt,on,Math.Atan(offset.Length)*180/Math.PI);
   if(Result.Samples.Count==0||now-Result.Samples.Last().T>=16)Result.Samples.Add(new PracticeSample{T=now,X=x,Y=y,TargetX=target.X,TargetY=target.Y,OnTarget=on});
   if(Result.Mode=="precision"&&now-targetStart>=3000){Result.Attempts++;Next();}
   if(now>=Result.Seconds*1000){Result.Complete=true;running=false;clock.Stop();Close();return;}Invalidate();
  }
  PointF Project(PointD p){var q=AimGeometry.Offset(p.X,p.Y,x,y);double f=ClientSize.Width/(2*Math.Tan(Result.Setup.Hfov*Math.PI/360));return new PointF((float)(ClientSize.Width/2+f*q.X),(float)(ClientSize.Height/2+f*q.Y));}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;ArenaScene.Draw(g,ClientSize,Result.Setup.Hfov,x,y);
   float f=(float)(ClientSize.Width/(2*Math.Tan(Result.Setup.Hfov*Math.PI/360))),r=f*(float)Math.Tan(Radius*Math.PI/180);
   if(Result.Mode=="switch")for(int i=0;i<4;i++){var p=Project(Practice.Spots[i]);using(var pen=new Pen(Color.FromArgb(90,120,123),2))g.DrawEllipse(pen,p.X-r,p.Y-r,r*2,r*2);}
   if(clock.Elapsed.TotalMilliseconds>=nextOnset){var p=Project(target);using(var b=new SolidBrush(Color.FromArgb(109,218,182)))g.FillEllipse(b,p.X-r,p.Y-r,r*2,r*2);}
   int cx=ClientSize.Width/2,cy=ClientSize.Height/2;Sight.Draw(g,cx,cy,Result.Setup.Crosshair);
   using(var b=new SolidBrush(Color.FromArgb(19,24,27))){g.FillRectangle(b,0,0,ClientSize.Width,90);g.FillRectangle(b,0,ClientSize.Height-80,ClientSize.Width,80);}
   g.DrawString("TRUETRACK / "+Practice.Name(Result.Mode),heading,Brushes.White,28,16);
   g.DrawString(Practice.Text("Fri trening · endrer ingen profil","Free practice · no profile changes"),body,Brushes.LightGray,28,51);
   g.DrawString(Math.Max(0,Result.Seconds-Result.Elapsed/1000).ToString("F0")+" s",heading,Brushes.White,ClientSize.Width-100,24);
   string live=Result.Mode=="tracking"?Practice.Text("Tid på mål ↑: ","Time on target ↑: ")+Result.Tracking.ToString("F1")+" %":Practice.Text("Treff/min ↑: ","Hits/min ↑: ")+Result.HitsPerMinute.ToString("F1")+" · "+Practice.Text("Treffprosent ↑: ","Accuracy ↑: ")+Result.Accuracy.ToString("F1")+" %";
   g.DrawString(live,body,Brushes.White,28,ClientSize.Height-70);
   g.DrawString("Esc · "+Practice.Text("Avslutt","Exit")+"   |   Alt+Tab · "+Practice.Text("Pause","Pause"),body,Brushes.LightGray,28,ClientSize.Height-40);
   if(ready||paused){using(var b=new SolidBrush(Color.FromArgb(245,19,24,27)))g.FillRectangle(b,50,cy-120,ClientSize.Width-100,240);
    string instruction=Result.Mode=="tracking"?Practice.Text("Hold siktet på det bevegelige målet. Ikke klikk.","Keep the crosshair on the moving target. No clicking."):Result.Mode=="switch"?Practice.Text("Treff det grønne målet, og bytt til neste grønne mål.","Hit the green target, then switch to the next green target."):Result.Mode=="flick"?Practice.Text("Klikk én gang per mål, også ved bom.","Click once per target, including misses."):Practice.Text("Treff raskt med kontroll. Bomklikk nullstiller ikke tiden.","Hit quickly with control. Misses do not reset the clock.");
    using(var sf=new StringFormat{Alignment=StringAlignment.Center})g.DrawString(paused?Practice.Text("Pause · trykk mellomrom for å fortsette","Paused · press Space to resume"):instruction+"\n\n"+Practice.Text("Sentrer musen på underlaget. Klikk for å starte.","Center your mouse on the pad. Click to start."),body,Brushes.White,new RectangleF(70,cy-75,ClientSize.Width-140,180),sf);
   }
  }
#if DEVELOPMENT
  public void Preview(){WindowState=FormWindowState.Normal;Size=new Size(1000,740);ready=false;target=Practice.Target(1200);}
#endif
 }
}
