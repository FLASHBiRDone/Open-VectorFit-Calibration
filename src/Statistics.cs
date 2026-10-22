using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace VectorFit {
    class CalibrationSteps:Control {
        public CalibrationSteps(){DoubleBuffered=true;Height=135;BackColor=Color.FromArgb(28,34,38);AccessibleName="Sirkeltest → kartlegging → A/B → resultat";}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;
            using(var heading=new Font("Segoe UI",16,FontStyle.Bold,GraphicsUnit.Pixel))using(var body=new Font("Segoe UI",13,GraphicsUnit.Pixel))using(var ink=new SolidBrush(Color.FromArgb(237,244,242)))using(var muted=new SolidBrush(Color.FromArgb(169,184,188)))using(var accent=new SolidBrush(Color.FromArgb(109,218,182))){
                string[] names={"Fase 1 · Baseline","Fase 2 · Ny profil","Fase 3 og videre"};
                string[] lines={"Naturlige bevegelser.\nAnalysen gir første forslag.","Bruk første korrigering.\nSamle nye målinger.","Juster fra siste måling.\nFølg fart og presisjon."};
                int column=Width/3;for(int i=0;i<3;i++){int x=i*column+18;PaintText.Draw(g,(i+1).ToString("00"),body,accent,x,14);PaintText.Draw(g,names[i],heading,ink,x,38);PaintText.Draw(g,lines[i],body,muted,new RectangleF(x,69,column-28,60));}
            }
        }
    }
    class ProgressStrip:Control {
        public int Value,Maximum=144;
        public ProgressStrip(){DoubleBuffered=true;AccessibleRole=AccessibleRole.ProgressBar;}
        protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Color.FromArgb(45,57,62));using(var fill=new SolidBrush(Color.FromArgb(109,218,182)))e.Graphics.FillRectangle(fill,0,0,Width*(float)Math.Min(Maximum,Value)/Maximum,Height);}
    }
    public class CalibrationProgress {
        public int Round=1,Warmup,Measured,Compared,PrecisionMeasured,PrecisionCompared;
        public bool IncludePrecision;
        public int MeasurementGoal=80,ComparisonGoal=64,Circles,CircleGoal=15;
        public bool IncludeWarmup=true,IncludeBaseline=true,IncludeComparison=true;
        public string Stage="";
        public int Total {get{return (Circles>0?CircleGoal:0)+(IncludeWarmup?8:0)+(IncludeBaseline?MeasurementGoal:0)+(IncludeComparison?ComparisonGoal:0)+(IncludePrecision?((IncludeBaseline?16:0)+(IncludeComparison?32:0)):0);}}
        public int Done {get{return Math.Min(CircleGoal,Circles)+(IncludeWarmup?Math.Min(8,Warmup):0)+(IncludeBaseline?Math.Min(MeasurementGoal,Measured):0)+(IncludeComparison?Math.Min(ComparisonGoal,Compared):0)+(IncludePrecision?Math.Min(16,PrecisionMeasured)+Math.Min(32,PrecisionCompared):0);}}
        public double Fraction {get{return Total==0?0:(double)Done/Total;}}
        public string Label {get{return L.T("Fase ")+Round+"  ·  "+L.T(Stage)+"  ·  "+
            (Circles>0?L.T("Sirkler ")+Math.Min(CircleGoal,Circles)+"/"+CircleGoal+"   →   ":"")+
            (IncludeWarmup?L.T("Øvelse ")+Math.Min(8,Warmup)+"/8   →   ":"")+
            (IncludeBaseline?L.T("Måling ")+Math.Min(MeasurementGoal,Measured)+"/"+MeasurementGoal:"")+(IncludeComparison?"   →   "+L.T("A/B ")+Math.Min(ComparisonGoal,Compared)+"/"+ComparisonGoal:"")+(IncludePrecision?" · "+L.T("Presisjon ")+(PrecisionMeasured+PrecisionCompared)+"/"+((IncludeBaseline?16:0)+(IncludeComparison?32:0)):"");}}
        public static int Clicks(IEnumerable<Trial> trials){return trials.Count(t=>t.Rejection!="Tiden gikk ut");}
        public static CalibrationProgress FromSession(Session s){return new CalibrationProgress{IncludePrecision=s.DualTask,PrecisionMeasured=s.PrecisionBaseline.Count,PrecisionCompared=s.PrecisionValidation.Count,Round=s.Round,IncludeWarmup=false,IncludeComparison=!(s.GuidedCalibration&&s.Round==1&&s.Validation.Count==0),MeasurementGoal=s.BaselineGoal,CircleGoal=s.Circles.Goal,Circles=Math.Min(s.Circles.Goal,s.Circles.Laps.Count(s.Circles.Counts)),
            Measured=s.DualTask?s.Baseline.Count:Clicks(s.Baseline),Compared=s.AdaptiveValidation?s.Validation.Count:Clicks(s.Validation),ComparisonGoal=s.ComparisonGoal,Stage=s.ValidationPassed?"Forbedring bekreftet":s.FitReady?"Analyse ferdig · velg neste fase":s.Round==1?"Baseline · datainnhenting":"Datainnhenting med faseprofil"};}
    }
    public class RoundStatistics {
        public Session Session;
        public List<Trial> Baseline,A,B;
        public RoundStatistics(Session s){Session=s;Baseline=s.Baseline.Where(t=>t.Valid).ToList();A=s.Validation.Where(t=>(s.AdaptiveValidation||t.Valid)&&!t.Corrected).ToList();B=s.Validation.Where(t=>(s.AdaptiveValidation||t.Valid)&&t.Corrected).ToList();}
        public static double? Value(List<Trial> trials,int metric) {
            if(trials.Count==0)return null;
            if(metric==0)return 100*Analysis.Median(trials.Select(t=>t.Valid?t.ClickError:Math.Max(1,t.ClickError)));
            if(metric==1)return Analysis.Median(trials.Select(t=>t.TotalMs));
            if(Enumerable.Range(0,8).Any(d=>!trials.Any(t=>t.Direction==d)))return null;
            return 100*Analysis.DirectionBias(trials);
        }
        public static string Delta(double? before,double? after,string unit) {
            if(!before.HasValue||!after.HasValue)return L.T("Ikke nok data");
            double difference=after.Value-before.Value;
            string relative=before.Value>1e-9?" ("+(difference/before.Value*100).ToString("+0.0;-0.0;0.0")+" %)":"";
            return difference.ToString("+0.0;-0.0;0.0")+" "+L.T(unit)+relative;
        }
    }
    public static class History {
        public static bool SameSetup(Session a,Session b){return a.Setup.Mouse==b.Setup.Mouse&&a.Setup.Dpi==b.Setup.Dpi&&a.Setup.Cm360==b.Setup.Cm360&&a.Setup.Hfov==b.Setup.Hfov&&a.Setup.Device==b.Setup.Device;}
        public static List<RoundStatistics> Load(Session current,string folder,out string note) {
            note="";var result=new List<RoundStatistics>{new RoundStatistics(current)};var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var s=current;
            while(!String.IsNullOrEmpty(s.ParentMeasurement)&&result.Count<100) {
                string name=s.ParentMeasurement;
                if(name!=Path.GetFileName(name)||!seen.Add(name)){note="Historikken har en ugyldig lenke.";break;}
                try {
                    string path=Path.Combine(folder,name);if(!File.Exists(path)||new FileInfo(path).Length>50*1024*1024){note="En tidligere målefil mangler eller er for stor.";break;}
                    var previous=Session.FromJson(File.ReadAllText(path));
                    if(!SameSetup(current,previous)||previous.Round!=s.Round-1){note="Historikken stopper ved et annet oppsett eller brudd i rundene.";break;}
                    previous.SelectMode(current.Mode);result.Add(new RoundStatistics(previous));s=previous;
                }catch {note="En tidligere målefil kunne ikke leses.";break;}
            }
            result.Reverse();return result;
        }
    }
    class MovementChart:Control {
        readonly RoundStatistics stats;
        public MovementChart(Session session){stats=new RoundStatistics(session);DoubleBuffered=true;Height=308;BackColor=Color.FromArgb(28,34,38);}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;
            using(var body=new Font("Segoe UI",12,GraphicsUnit.Pixel))using(var bold=new Font("Segoe UI",14,FontStyle.Bold,GraphicsUnit.Pixel))using(var ink=new SolidBrush(Color.FromArgb(237,244,242)))using(var muted=new SolidBrush(Color.FromArgb(169,184,188))){
                PaintText.Draw(g,"Treff og kontroll gjennom bevegelsen",bold,ink,18,14);
                string[] names={"Treff ved klikk","Treff etter overskyting","Overskyting før klikk","Etterkorrigering","Tid til første målpassering","Tid etter første bevegelse","Nye passeringer av målet"};
                var groups=new[]{stats.Baseline,stats.A,stats.B};string[] headings={"Baseline","A","B"};float start=Width*.40f,column=(Width-start-18)/3;
                for(int row=0;row<names.Length;row++)PaintText.Draw(g,names[row],body,muted,18,65+row*27);
                for(int i=0;i<3;i++){
                    var trials=groups[i];var quality=trials.Select(Trajectory.Measure).Where(q=>q.Available).ToList();float x=start+i*column;PaintText.Draw(g,headings[i],bold,ink,x,40);
                    string[] values=new[]{trials.Count>0&&trials.All(t=>t.Hit.HasValue)?trials.Count(t=>t.Hit==true)+" / "+trials.Count:"—",quality.Any(q=>q.TargetKnown)?quality.Count(q=>q.HitAfterOvershoot).ToString():"—",quality.Count>0?(100*Analysis.Median(quality.Select(q=>q.Overshoot))).ToString("F1")+" %":"—",quality.Count>0?(100*Analysis.Median(quality.Select(q=>q.CorrectionDistance))).ToString("F1")+" %":"—",quality.Any(q=>q.FirstEntryMs.HasValue)?Analysis.Median(quality.Where(q=>q.FirstEntryMs.HasValue).Select(q=>q.FirstEntryMs.Value)).ToString("F0")+" ms":"—",quality.Count>0?Analysis.Median(quality.Select(q=>q.CorrectionMs)).ToString("F0")+" ms":"—",quality.Any(q=>q.TargetKnown)?Analysis.Median(quality.Where(q=>q.TargetKnown).Select(q=>(double)q.Reentries)).ToString("F1"):"—"};
                    for(int row=0;row<values.Length;row++)PaintText.Draw(g,values[row],body,ink,x,65+row*27);
                }
                PaintText.Draw(g,"Bevegelsesmål er medianer. Avstander er prosent av målavstanden. Treff etter retur teller fortsatt som treff.",body,muted,new RectangleF(18,261,Width-36,42));
            }
        }
    }
    class ComparisonChart:Control {
        readonly RoundStatistics stats;
        static readonly Color ink=Color.FromArgb(237,244,242),muted=Color.FromArgb(169,184,188),baseline=Color.FromArgb(119,137,153),aColor=Color.FromArgb(123,173,242),bColor=Color.FromArgb(109,218,182);
        public ComparisonChart(Session session){stats=new RoundStatistics(session);DoubleBuffered=true;Height=236;BackColor=Color.FromArgb(28,34,38);}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            using(var font=new Font("Segoe UI",12,GraphicsUnit.Pixel))using(var bold=new Font("Segoe UI",14,FontStyle.Bold,GraphicsUnit.Pixel))using(var white=new SolidBrush(ink))using(var gray=new SolidBrush(muted)) {
                string[] titles={"Klikkavvik","Tid til klikk","Retningsspredning"},units={"pp","ms","pp"};
                int col=Width/3;
                for(int m=0;m<3;m++){
                    int x=m*col+18;g.DrawString(L.T(titles[m]),bold,white,x,15);
                    var values=new[]{RoundStatistics.Value(stats.Baseline,m),RoundStatistics.Value(stats.A,m),RoundStatistics.Value(stats.B,m)};
                    double max=Math.Max(1,values.Where(v=>v.HasValue).Select(v=>v.Value).DefaultIfEmpty(1).Max());
                    string[] labels={"Baseline","A","B"};Color[] colors={baseline,aColor,bColor};
                    for(int i=0;i<3;i++){float y=47+i*41;string number=values[i].HasValue?values[i].Value.ToString(m==1?"F0":"F1")+(m==1?" ms":" %"):"—";
                        g.DrawString(labels[i],font,gray,x,y);g.DrawString(number,font,white,x+col-103,y);
                        using(var fill=new SolidBrush(colors[i]))if(values[i].HasValue)g.FillRectangle(fill,x,y+21,(float)((col-38)*values[i].Value/max),5);
                    }
                    g.DrawString("B − A: "+RoundStatistics.Delta(values[1],values[2],units[m]),font,white,new RectangleF(x,181,col-30,45));
                }
            }
        }
    }
    class HistoryChart:Control {
        readonly List<RoundStatistics> rounds;readonly int metric;
        public HistoryChart(List<RoundStatistics> data,int selectedMetric){rounds=data;metric=selectedMetric;DoubleBuffered=true;Height=242;BackColor=Color.FromArgb(28,34,38);}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            using(var font=new Font("Segoe UI",12,GraphicsUnit.Pixel))using(var bold=new Font("Segoe UI",14,FontStyle.Bold,GraphicsUnit.Pixel))using(var gray=new SolidBrush(Color.FromArgb(169,184,188)))using(var ink=new SolidBrush(Color.FromArgb(237,244,242))) {
                string title=metric==0?"Klikkavvik gjennom rundene":"Tid til klikk gjennom rundene";g.DrawString(L.T(title),bold,ink,18,14);
                var measured=rounds.Select(r=>RoundStatistics.Value(r.Baseline,metric)).ToArray();var candidate=rounds.Select(r=>RoundStatistics.Value(r.B,metric)).ToArray();
                double max=Math.Max(1,measured.Concat(candidate).Where(v=>v.HasValue).Select(v=>v.Value).DefaultIfEmpty(1).Max()*1.25);
                float left=54,right=Width-45,top=58,bottom=180;
                Func<int,float> x=i=>rounds.Count==1?(left+right)/2:left+(right-left)*i/(rounds.Count-1);
                Func<double,float> y=v=>bottom-(float)(v/max)*(bottom-top);
                using(var grid=new Pen(Color.FromArgb(57,67,73)))for(int i=0;i<=2;i++){float yy=top+(bottom-top)*i/2;g.DrawLine(grid,left,yy,right,yy);g.DrawString((max*(1-i/2.0)).ToString(metric==1?"F0":"F1"),font,gray,8,yy-8);}
                if(measured.Length>0&&measured[0].HasValue)using(var line=new Pen(Color.FromArgb(130,169,184,188))){line.DashStyle=DashStyle.Dash;g.DrawLine(line,left,y(measured[0].Value),right,y(measured[0].Value));}
                for(int series=0;series<2;series++){
                    var values=series==0?measured:candidate;Color color=series==0?Color.FromArgb(123,173,242):Color.FromArgb(109,218,182);
                    using(var pen=new Pen(color,2))using(var brush=new SolidBrush(color))for(int i=0;i<values.Length;i++){
                        if(!values[i].HasValue)continue;float xx=x(i),yy=y(values[i].Value);
                        if(i>0&&values[i-1].HasValue)g.DrawLine(pen,x(i-1),y(values[i-1].Value),xx,yy);
                        g.FillEllipse(brush,xx-4,yy-4,8,8);
                        if(rounds.Count<=10)g.DrawString(values[i].Value.ToString(metric==1?"F0":"F1"),font,brush,xx-12,yy+(series==0?-20:8));
                    }
                }
                for(int i=0;i<rounds.Count;i++)if(rounds.Count<=12||i==0||i==rounds.Count-1||i%Math.Max(1,rounds.Count/8)==0)g.DrawString(L.T("Runde ")+rounds[i].Session.Round,font,gray,x(i)-22,189);
                g.DrawString(L.T("Blå: baseline per runde   ·   Grønn: B-forslag   ·   Stiplet: første baseline   ·   Lavere er bedre"),font,gray,18,218);
            }
        }
    }
}
