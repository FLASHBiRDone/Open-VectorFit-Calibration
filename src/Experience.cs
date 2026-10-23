using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Net;
using System.Diagnostics;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
namespace VectorFit {
 public static class UserStorage {
  public static void Merge(string source,string destination){
   Directory.CreateDirectory(destination);if(!Directory.Exists(source)||Path.GetFullPath(source)==Path.GetFullPath(destination))return;
   foreach(string file in Directory.GetFiles(source)){
    string target=Path.Combine(destination,Path.GetFileName(file));
    if(!File.Exists(target))File.Copy(file,target);
    else if(!File.ReadAllBytes(file).SequenceEqual(File.ReadAllBytes(target))){
     string conflicts=Path.Combine(destination,"import-conflicts");Directory.CreateDirectory(conflicts);
     using(var hash=SHA256.Create()){string suffix=BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(file))).Replace("-","").Substring(0,16);string copy=Path.Combine(conflicts,Path.GetFileName(file)+"."+suffix);if(!File.Exists(copy))File.Copy(file,copy);}
    }
   }
  }
  public static void Initialize(){string target=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TrueTrack","profiles");Merge(Path.Combine(Driver.Root,"profiles"),target);Driver.Data=target;}
 }
 public class DirectionMetric {
  public int N,Hits;public double Time;public double? Correction,Reach;
  public static DirectionMetric From(IEnumerable<Trial> trials,bool precision){var ts=trials.ToList();var q=ts.Select(Trajectory.Measure).ToList();return new DirectionMetric{N=ts.Count,Hits=ts.Count(t=>t.Hit==true),Time=ts.Count==0?0:ts.Average(t=>precision?PrecisionCheck.Time(t):t.TotalMs),Correction=q.Count>0&&q.All(x=>x.Available)?(double?)q.Average(x=>100*x.CorrectionDistance):null,Reach=q.Count>0&&q.All(x=>x.PrimaryDetected)?(double?)q.Average(x=>100*x.PrimaryReach):null};}
 }
 public class DirectionMap:Control {
  Session s;public DirectionMap(Session session){s=session;Height=590;DoubleBuffered=true;BackColor=Color.FromArgb(28,34,38);AccessibleName="Direction map: A previous profile, B proposal";}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
   using(var font=new Font("Segoe UI",12,GraphicsUnit.Pixel))using(var title=new Font("Segoe UI",17,FontStyle.Bold,GraphicsUnit.Pixel))using(var white=new SolidBrush(Color.WhiteSmoke))using(var grey=new SolidBrush(Color.Silver))using(var pen=new Pen(Color.FromArgb(70,85,90))){
    g.DrawString(DriverSetup.Text("Retningskart · A → B","Direction map · A → B"),title,white,16,12);
    g.DrawString(DriverSetup.Text("Tid og etterkorrigering: lavere er bedre. Treff: høyere er bedre.","Time and corrective travel: lower is better. Hits: higher is better."),font,grey,16,40);
    string[] dirs={"→","↘","↓","↙","←","↖","↑","↗"};
    for(int d=0;d<8;d++){double angle=d*Math.PI/4;float cx=Width/2f,cy=290;float x=cx+(float)Math.Cos(angle)*(Width/2f-130),y=cy+(float)Math.Sin(angle)*175;g.DrawLine(pen,cx,cy,x,y);
     var pa=DirectionMetric.From(s.PrecisionValidation.Where(t=>t.Direction==d&&!t.Corrected),true);var pb=DirectionMetric.From(s.PrecisionValidation.Where(t=>t.Direction==d&&t.Corrected),true);
     var fa=DirectionMetric.From(s.Validation.Where(t=>t.Direction==d&&!t.Corrected),false);var fb=DirectionMetric.From(s.Validation.Where(t=>t.Direction==d&&t.Corrected),false);
     var box=new RectangleF(x-108,y-57,216,114);using(var fill=new SolidBrush(Color.FromArgb(35,43,48)))g.FillRectangle(fill,box);
     string text=dirs[d]+"   A / B · n="+pa.N+"/"+pb.N+"\n";
     text+=pa.N==0||pb.N==0?DriverSetup.Text("Ingen A/B-data ennå","No A/B data yet"):DriverSetup.Text("Presisjon ","Precision ")+pa.Time.ToString("F0")+" → "+pb.Time.ToString("F0")+" ms\n"+DriverSetup.Text("Korr. ","Correct. ")+Value(pa.Correction)+" → "+Value(pb.Correction)+" %\n"+DriverSetup.Text("Treff ","Hits ")+pa.Hits+"/"+pa.N+" → "+pb.Hits+"/"+pb.N;
     text+="\n"+DriverSetup.Text("Første flick ","First flick ")+Value(fa.Reach)+" → "+Value(fb.Reach)+" %";
     g.DrawString(text,font,white,new RectangleF(box.X+7,box.Y+5,202,106));
    }
    g.DrawString(DriverSetup.Text("Første flick = rekkevidde relativt til målavstanden; 100 % er ikke automatisk best.\nSmå grupper: beskrivende tall, ikke bevis på forbedring. Ingen data vises som —.","First flick = reach relative to target distance; 100% is not automatically best.\nSmall groups: descriptive results, not proof of improvement. Missing data: —."),font,grey,new RectangleF(16,545,Width-32,44));
   }
  }
  static string Value(double? v){return v.HasValue?v.Value.ToString("F1"):"—";}
 }
 public static class AppUpdates {
  public const string Version="0.19.0";
  static bool checking;
  public static bool Enabled {get{return File.Exists(Path.Combine(Driver.Data,"check-updates.txt"));}set{var p=Path.Combine(Driver.Data,"check-updates.txt");if(value)File.WriteAllText(p,"yes");else if(File.Exists(p))File.Delete(p);}}
  public static bool Newer(string tag){System.Version v;return System.Version.TryParse((tag??"").TrimStart('v'),out v)&&v>new System.Version(Version);}
  static WebClient Client(){ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;var c=new WebClient();c.Headers["User-Agent"]="TrueTrack/"+Version;return c;}
  public static async void Check(Form owner,bool quiet){
   if(checking)return;checking=true;
   try{JObject release;using(var c=Client()){var array=JArray.Parse(await c.DownloadStringTaskAsync("https://api.github.com/repos/FLASHBiRDone/TrueTrack/releases"));release=array.OfType<JObject>().FirstOrDefault(x=>(bool?)x["draft"]!=true&&Newer((string)x["tag_name"]));}
    if(release==null){if(!quiet)MessageBox.Show(owner,DriverSetup.Text("Du har nyeste tilgjengelige versjon.","You have the latest available version."));return;}
    if(owner.IsDisposed)return;
    if(MessageBox.Show(owner,DriverSetup.Text("Ny versjon tilgjengelig: ","New version available: ")+(string)release["tag_name"]+DriverSetup.Text("\nLast ned og installer? Appen lukkes. Målinger og profiler beholdes. Dette inkluderer eksperimentelle utgaver.","\nDownload and install? The app will close. Measurements and profiles are preserved. This includes experimental releases."),"TrueTrack",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
    owner.Enabled=false;
    string folder=Path.Combine(Path.GetTempPath(),"TrueTrack-update-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
    string zip=Path.Combine(folder,"TrueTrack-Windows.zip");string checksum;
    using(var c=Client()){checksum=await c.DownloadStringTaskAsync(Asset(release,"TrueTrack-SHA256SUMS.txt"));await c.DownloadFileTaskAsync(Asset(release,"TrueTrack-Windows.zip"),zip);}
    string expected=checksum.Split(new[]{' ','\t','\r','\n'},StringSplitOptions.RemoveEmptyEntries)[0];
    using(var hash=SHA256.Create())using(var stream=File.OpenRead(zip)){if(!String.Equals(expected,BitConverter.ToString(hash.ComputeHash(stream)).Replace("-",""),StringComparison.OrdinalIgnoreCase))throw new Exception("Update checksum mismatch. Nothing installed.");}
    string script=Path.Combine(folder,"Update-TrueTrack.ps1");File.Copy(Path.Combine(Driver.Root,"Update-TrueTrack.ps1"),script);
    Process.Start(new ProcessStartInfo("powershell.exe","-NoProfile -ExecutionPolicy Bypass -File \""+script+"\" -Zip \""+zip+"\" -Destination \""+Driver.Root.TrimEnd('\\')+"\" -ParentId "+Process.GetCurrentProcess().Id){UseShellExecute=false,CreateNoWindow=true});owner.Close();
   }catch(Exception ex){if(!owner.IsDisposed)MessageBox.Show(owner,DriverSetup.Text("Oppdateringen ble ikke installert. ","The update was not installed. ")+ex.Message);}
   finally{checking=false;if(!owner.IsDisposed)owner.Enabled=true;}
  }
  static string Asset(JObject r,string name){var a=((JArray)r["assets"]).OfType<JObject>().Single(x=>(string)x["name"]==name);string u=(string)a["browser_download_url"];if(!u.StartsWith("https://github.com/FLASHBiRDone/TrueTrack/releases/download/",StringComparison.Ordinal))throw new Exception("Unexpected update source");return u;}
 }
}
