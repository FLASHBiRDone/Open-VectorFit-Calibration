using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace VectorFit {
    public class CircleLap {
        public double Radius;
        public bool Clockwise,Complete,Counted;
        public string Reason="";
        public List<Motion> Samples=new List<Motion>();
        public List<Motion> RawSamples;
    }
    public class CircleData {
        // Missing protocol in historical measurements means the original 15-circle test.
        public int Protocol;
        [JsonIgnore] public int Goal {get{return Protocol>=1?60:15;}}
        public bool Counts(CircleLap lap){return Protocol>=2?lap.Counted:Protocol==1?lap.Complete:lap.Reason!="Pause";}
        public List<CircleLap> Laps=new List<CircleLap>();
        public static List<CircleLap> Plan(){return Enumerable.Range(0,60).Select(i=>new CircleLap{Radius=12,Clockwise=i<30}).ToList();}
        public static List<CircleLap> LegacyPlan(bool reverse=false){
            var plan=new List<CircleLap>();double[] sizes={12,8,16};
            for(int block=0;block<3;block++)for(int rep=0;rep<5;rep++)plan.Add(new CircleLap{Radius=sizes[block],Clockwise=(block%2==0)^reverse^(rep>=3)});
            return plan;
        }
    }
    public class CircleShape {
        public bool Valid;
        public double Width,Height,Roundness,Closure,CenterDrift,RadialError,Residual,AxisRatio;
        public double CenterX,CenterY,Major,Minor,Angle;
    }
    public class CircleReport {
        public int Valid,Total;
        public double AxisRatio,Roundness,Closure,CenterDrift,RadialError;
        public bool Stable;
        public string Note="";
    }
    public static class CircleAnalysis {
        static double[] Solve(double[,] a,double[] b){
            int n=b.Length;var m=new double[n,n+1];for(int i=0;i<n;i++){for(int j=0;j<n;j++)m[i,j]=a[i,j];m[i,n]=b[i];}
            for(int col=0;col<n;col++){
                int pivot=col;for(int row=col+1;row<n;row++)if(Math.Abs(m[row,col])>Math.Abs(m[pivot,col]))pivot=row;
                if(Math.Abs(m[pivot,col])<1e-10)return null;
                for(int j=col;j<=n;j++){double v=m[col,j];m[col,j]=m[pivot,j];m[pivot,j]=v;}
                double divisor=m[col,col];for(int j=col;j<=n;j++)m[col,j]/=divisor;
                for(int row=0;row<n;row++)if(row!=col){double factor=m[row,col];for(int j=col;j<=n;j++)m[row,j]-=factor*m[col,j];}
            }return Enumerable.Range(0,n).Select(i=>m[i,n]).ToArray();
        }
        public static CircleShape Measure(CircleLap lap,bool natural=false){
            var result=new CircleShape();if(lap==null||(!natural&&!lap.Complete)||lap.Samples==null||lap.Samples.Count<(natural?8:40)||lap.Radius<=0)return result;
            if(lap.Samples.Any(p=>Double.IsNaN(p.X)||Double.IsInfinity(p.X)||Double.IsNaN(p.Y)||Double.IsInfinity(p.Y)))return result;
            // Resample by angle, so a slow quarter-turn does not dominate the fitted shape.
            double mx=(lap.Samples.Min(p=>p.X)+lap.Samples.Max(p=>p.X))/2,my=(lap.Samples.Min(p=>p.Y)+lap.Samples.Max(p=>p.Y))/2;
            var bins=new List<Motion>[72];for(int i=0;i<bins.Length;i++)bins[i]=new List<Motion>();
            foreach(var p in lap.Samples){double angle=Math.Atan2(p.Y-my,p.X-mx);int bin=(int)Math.Floor((angle+Math.PI)/(2*Math.PI)*72)%72;bins[bin].Add(p);}
            if(bins.Count(bin=>bin.Count>0)<(natural?8:60))return result;
            var points=bins.Where(bin=>bin.Count>0).Select(bin=>new PointD((Analysis.Median(bin.Select(p=>p.X))-mx)/lap.Radius,(Analysis.Median(bin.Select(p=>p.Y))-my)/lap.Radius)).ToList();
            var weights=Enumerable.Repeat(1.0,points.Count).ToArray();double[] coefficients=null;double cx=0,cy=0,k=0,major=0,minor=0,theta=0;
            for(int pass=0;pass<3;pass++){
                var a=new double[5,5];var b=new double[5];
                for(int i=0;i<points.Count;i++){var p=points[i];double[] row={p.X*p.X,p.X*p.Y,p.Y*p.Y,p.X,p.Y};for(int j=0;j<5;j++){b[j]+=weights[i]*row[j];for(int n=0;n<5;n++)a[j,n]+=weights[i]*row[j]*row[n];}}
                coefficients=Solve(a,b);if(coefficients==null)return result;
                double aa=coefficients[0],bb=coefficients[1]/2,cc=coefficients[2],dd=coefficients[3],ee=coefficients[4],det=aa*cc-bb*bb;
                if(aa<=0||cc<=0||det<=1e-8)return result;
                cx=-(cc*dd-bb*ee)/(2*det);cy=-(-bb*dd+aa*ee)/(2*det);k=1+aa*cx*cx+2*bb*cx*cy+cc*cy*cy;
                double discriminant=Math.Sqrt((aa-cc)*(aa-cc)+4*bb*bb),lo=(aa+cc-discriminant)/2,hi=(aa+cc+discriminant)/2;
                if(k<=0||lo<=1e-8)return result;major=Math.Sqrt(k/lo);minor=Math.Sqrt(k/hi);theta=.5*Math.Atan2(2*bb,aa-cc)+Math.PI/2;
                for(int i=0;i<points.Count;i++){double x=points[i].X-cx,y=points[i].Y-cy;double residual=Math.Abs(Math.Sqrt(Math.Max(0,(aa*x*x+2*bb*x*y+cc*y*y)/k))-1);weights[i]=Math.Min(1,.08/Math.Max(1e-9,residual));}
            }
            double cosine=Math.Cos(theta),sine=Math.Sin(theta);
            result.Width=2*lap.Radius*Math.Sqrt(major*major*cosine*cosine+minor*minor*sine*sine);
            result.Height=2*lap.Radius*Math.Sqrt(major*major*sine*sine+minor*minor*cosine*cosine);
            result.AxisRatio=result.Width/result.Height;result.Roundness=major/minor;
            double centerX=mx+cx*lap.Radius,centerY=my+cy*lap.Radius;result.CenterDrift=Math.Sqrt(centerX*centerX+centerY*centerY)/lap.Radius;
            result.CenterX=centerX;result.CenterY=centerY;result.Major=major*lap.Radius;result.Minor=minor*lap.Radius;result.Angle=theta;
            var first=lap.Samples[0];var last=lap.Samples[lap.Samples.Count-1];result.Closure=Math.Sqrt(Math.Pow(last.X-first.X,2)+Math.Pow(last.Y-first.Y,2))/lap.Radius;
            result.RadialError=Analysis.Median(points.Select(p=>Math.Abs(Math.Sqrt(Math.Pow(p.X+mx/lap.Radius,2)+Math.Pow(p.Y+my/lap.Radius,2))-1)));
            result.Residual=Analysis.Median(points.Select(p=>{double x=p.X-cx,y=p.Y-cy;return Math.Abs(Math.Sqrt(Math.Max(0,(coefficients[0]*x*x+coefficients[1]*x*y+coefficients[2]*y*y)/k))-1);}));
            result.Valid=natural?minor>1e-8&&major>0:minor>.25&&major<3&&result.Residual<.18&&result.Closure<.65;
            return result;
        }
        public static CircleLap UndoInput(CircleLap lap,Correction correction){
            if(lap.RawSamples!=null)return new CircleLap{Radius=lap.Radius,Clockwise=lap.Clockwise,Complete=lap.Complete,Reason=lap.Reason,Samples=lap.RawSamples};
            var restored=new CircleLap{Radius=lap.Radius,Clockwise=lap.Clockwise,Complete=lap.Complete,Reason=lap.Reason};if(lap.Samples.Count==0)return restored;
            double x=lap.Samples[0].X,y=lap.Samples[0].Y,px=x,py=y;restored.Samples.Add(new Motion(lap.Samples[0].T,x,y));
            foreach(var p in lap.Samples.Skip(1)){var delta=correction.Inverse(p.X-px,p.Y-py);x+=delta.X;y+=delta.Y;px=p.X;py=p.Y;restored.Samples.Add(new Motion(p.T,x,y));}return restored;
        }
        public static CircleReport Summarize(CircleData data,Correction undo=null){
            if(data!=null&&data.Protocol>=1)return Median(data,undo).Report;
            var report=new CircleReport();if(data==null||data.Laps==null)return report;report.Total=data.Laps.Count;
            var rows=data.Laps.Select(lap=>new{Lap=lap,Shape=Measure(undo==null?lap:UndoInput(lap,undo))}).Where(row=>row.Shape.Valid).ToList();report.Valid=rows.Count;
            if(rows.Count==0){report.Note="Ingen komplette sirkler å sammenligne ennå.";return report;}
            report.AxisRatio=Analysis.Median(rows.Select(r=>r.Shape.AxisRatio));report.Roundness=Analysis.Median(rows.Select(r=>r.Shape.Roundness));report.Closure=Analysis.Median(rows.Select(r=>r.Shape.Closure));report.CenterDrift=Analysis.Median(rows.Select(r=>r.Shape.CenterDrift));report.RadialError=Analysis.Median(rows.Select(r=>r.Shape.RadialError));
            bool sizes=new[]{8.0,12.0,16.0}.All(size=>rows.Count(row=>row.Lap.Radius==size)>=4);
            bool directions=rows.Count(row=>row.Lap.Clockwise)>=5&&rows.Count(row=>!row.Lap.Clockwise)>=5;
            double spread=Analysis.Median(rows.Select(row=>Math.Abs(row.Shape.AxisRatio-report.AxisRatio)));
            double directionalGap=Math.Abs(Analysis.Median(rows.Where(r=>r.Lap.Clockwise).Select(r=>r.Shape.AxisRatio))-Analysis.Median(rows.Where(r=>!r.Lap.Clockwise).Select(r=>r.Shape.AxisRatio)));
            var perSize=rows.GroupBy(r=>r.Lap.Radius).Select(group=>Analysis.Median(group.Select(r=>r.Shape.AxisRatio))).ToArray();
            report.Stable=report.Valid>=12&&sizes&&directions&&spread<.08&&directionalGap<.12&&perSize.Max()-perSize.Min()<.12;
            report.Note=report.Stable?"Sirklene gir et stabilt mål på bredde og høyde.":"Sirklene varierer. De vises i statistikken, men endrer ikke profilforslaget.";return report;
        }
        public static void Contribute(Session session){
            session.CircleNote="";if(session.Circles==null||session.Circles.Laps.Count==0)return;
            if(session.Circles.Protocol>=1){
                var median=Median(session.Circles,session.InputCorrection);var proposed=Propose(session.Circles,session.InputCorrection,session.Correction);
                bool changed=Math.Abs(proposed.Right-session.Correction.Right)>1e-8;
                session.Correction=proposed;session.CircleNote=!median.Report.Stable?median.Report.Note:changed?"Medianformen bidrar til et begrenset profilforslag. A/B må bekrefte bedre sikting.":"Medianformen gir ingen tydelig korreksjon innenfor profilens grenser. Følsomhet og rotasjon vurderes i måltesten.";return;
            }
            var report=Summarize(session.Circles,session.InputCorrection);session.CircleNote=report.Note;
            if(!report.Stable||Math.Abs(Math.Log(report.AxisRatio))<.02)return;
            // A weak prior only: flicks dominate, and the measured A/B target-distance requirement still decides approval.
            var c=session.Correction;double currentRatio=Math.Sqrt(c.Right*c.Left/(c.Down*c.Up));
            double multiplier=Math.Pow((1/report.AxisRatio)/currentRatio,.125);
            multiplier=Analysis.Clamp(multiplier,.98,1.02);
            c.Right=Analysis.Clamp(c.Right*multiplier,.92,1.08);c.Left=Analysis.Clamp(c.Left*multiplier,.92,1.08);
            c.Up=Analysis.Clamp(c.Up/multiplier,.92,1.08);c.Down=Analysis.Clamp(c.Down/multiplier,.92,1.08);
            session.CircleNote="Et stabilt bredde/høyde-avvik bidrar forsiktig til forslaget. A/B må bekrefte at klikkene blir nærmere målet.";
        }

        static PointD Anisotropy(CircleShape shape){double value=Math.Log(shape.Roundness);return new PointD(value*Math.Cos(2*shape.Angle),value*Math.Sin(2*shape.Angle));}
        static double Gap(IEnumerable<CircleShape> a,IEnumerable<CircleShape> b){
            var aa=a.Select(Anisotropy).ToArray();var bb=b.Select(Anisotropy).ToArray();if(aa.Length==0||bb.Length==0)return Double.PositiveInfinity;
            return Math.Sqrt(Math.Pow(Analysis.Median(aa.Select(p=>p.X))-Analysis.Median(bb.Select(p=>p.X)),2)+Math.Pow(Analysis.Median(aa.Select(p=>p.Y))-Analysis.Median(bb.Select(p=>p.Y)),2));
        }
        public static CircleLap TransformLap(CircleLap lap,Correction c){
            var result=new CircleLap{Radius=lap.Radius,Complete=lap.Complete,Clockwise=lap.Clockwise,Reason=lap.Reason};double px=0,py=0,x=0,y=0;
            foreach(var p in lap.Samples){var d=c.Transform(p.X-px,p.Y-py);x+=d.X;y+=d.Y;px=p.X;py=p.Y;result.Samples.Add(new Motion(p.T,x,y));}return result;
        }
        static double[] RadialShape(CircleLap lap,CircleShape shape){
            var bins=Enumerable.Range(0,72).Select(i=>new List<double>()).ToArray();double areaRadius=Math.Sqrt(shape.Major*shape.Minor);
            foreach(var p in lap.Samples){double x=p.X-shape.CenterX,y=p.Y-shape.CenterY;double angle=Math.Atan2(y,x);if(angle<0)angle+=2*Math.PI;int i=(int)Math.Floor(angle/(2*Math.PI)*72)%72;bins[i].Add(Math.Sqrt(x*x+y*y)/areaRadius);}
            if(bins.Count(b=>b.Count>0)<60)return null;
            var values=bins.Select(b=>b.Count==0?Double.NaN:Analysis.Median(b)).ToArray();var filled=(double[])values.Clone();
            for(int i=0;i<72;i++)if(Double.IsNaN(values[i])){int before=1,after=1;while(Double.IsNaN(values[(i-before+72)%72]))before++;while(Double.IsNaN(values[(i+after)%72]))after++;if(before+after>10)return null;filled[i]=(values[(i-before+72)%72]*after+values[(i+after)%72]*before)/(before+after);}
            return filled;
        }
        public static CircleMedian Median(CircleData data,Correction undo=null,Correction apply=null,bool? clockwise=null){
            if(data!=null&&data.Protocol>=2)return NaturalCircles.Median(data,undo,apply,clockwise);
            var result=new CircleMedian();var report=result.Report;if(data==null)return result;
            var rows=new List<Tuple<CircleLap,CircleShape,double[]>>();
            foreach(var original in data.Laps.Where(l=>l.Reason!="Pause"&&(!clockwise.HasValue||l.Clockwise==clockwise.Value))){
                report.Total++;var lap=undo==null?original:UndoInput(original,undo);if(apply!=null)lap=TransformLap(lap,apply);
                var shape=Measure(lap);if(!shape.Valid)continue;var radial=RadialShape(lap,shape);if(radial!=null)rows.Add(Tuple.Create(lap,shape,radial));
            }
            report.Valid=rows.Count;result.Clockwise=rows.Count(r=>r.Item1.Clockwise);result.Counterclockwise=rows.Count-result.Clockwise;
            if(rows.Count==0){report.Note="Ingen komplette sirkler å sammenligne ennå.";return result;}
            for(int i=0;i<72;i++){double angle=(i+.5)*2*Math.PI/72,radius=Analysis.Median(rows.Select(r=>r.Item3[i]));result.Points.Add(new PointD(radius*Math.Cos(angle),radius*Math.Sin(angle)));}
            result.Ellipse=Measure(new CircleLap{Radius=1,Complete=true,Samples=result.Points.Concat(result.Points.Take(1)).Select((p,i)=>new Motion(i*4,p.X,p.Y)).ToList()});
            report.AxisRatio=result.Ellipse.AxisRatio;report.Roundness=result.Ellipse.Roundness;
            report.Closure=Analysis.Median(rows.Select(r=>r.Item2.Closure));report.CenterDrift=Analysis.Median(rows.Select(r=>r.Item2.CenterDrift));report.RadialError=Analysis.Median(rows.Select(r=>r.Item2.RadialError));
            var center=Anisotropy(result.Ellipse);result.Spread=Analysis.Median(rows.Select(r=>{var p=Anisotropy(r.Item2);return Math.Sqrt(Math.Pow(p.X-center.X,2)+Math.Pow(p.Y-center.Y,2));}));
            var cw=rows.Where(r=>r.Item1.Clockwise).Select(r=>r.Item2).ToArray();var ccw=rows.Where(r=>!r.Item1.Clockwise).Select(r=>r.Item2).ToArray();
            result.DirectionGap=Gap(cw,ccw);
            bool steady=new[]{cw,ccw}.All(group=>Gap(group.Take(group.Length/2),group.Skip(group.Length/2))<.08);
            report.Stable=data.Protocol==1&&data.Laps.Count(l=>l.Complete&&l.Clockwise)>=30&&data.Laps.Count(l=>l.Complete&&!l.Clockwise)>=30&&result.Clockwise>=24&&result.Counterclockwise>=24&&rows.All(r=>Math.Abs(r.Item1.Radius-12)<.001)&&result.Ellipse.Valid&&result.Spread<.10&&result.DirectionGap<.08&&steady;
            report.Note=report.Stable?"Medianformen er stabil i begge dreieretninger.":"Medianformen vises, men varierer for mye eller mangler runder. Den endrer ikke profilforslaget.";
            return result;
        }
        public static Correction Propose(CircleData data,Correction input,Correction basis){
            var result=basis.Copy();if(data==null||data.Protocol<1||!Median(data,input).Report.Stable)return result;
            var before=Median(data,input,basis);if(!before.Ellipse.Valid||before.Report.Roundness<1.02)return result;
            // Only the representable horizontal/vertical component changes. A tilted ellipse cannot justify a large rotation of aim.
            double limit=data.Protocol>=3?.02*Median(data,input).TrendStrength:.02;
            double multiplier=Analysis.Clamp(1/Math.Sqrt(before.Report.AxisRatio),1-limit,1+limit);
            result.Right=Analysis.Clamp(result.Right*multiplier,.92,1.08);result.Left=Analysis.Clamp(result.Left*multiplier,.92,1.08);
            result.Up=Analysis.Clamp(result.Up/multiplier,.92,1.08);result.Down=Analysis.Clamp(result.Down/multiplier,.92,1.08);
            var after=Median(data,input,result);
            if(!after.Ellipse.Valid||after.Report.Roundness>before.Report.Roundness-.005)return basis.Copy();return result;
        }
    }
    public class CircleMedian {
        public CircleReport Report=new CircleReport();public CircleShape Ellipse=new CircleShape();public List<PointD> Points=new List<PointD>();
        public int Clockwise,Counterclockwise;public double Spread,DirectionGap,TimeGap;public double TrendStrength=1;
    }
    // Signed angle accumulation prevents backtracking from being counted as extra turns.
    public class CircleTracker {
        public readonly CircleLap Lap;
        double previousAngle,progress,lastSample,previousX,previousY,rawX,rawY;readonly Correction input;readonly bool natural;
        public CircleTracker(CircleLap lap,double x,double y,double time,Correction inputCorrection=null,bool naturalMovement=false){Lap=lap;input=inputCorrection;natural=naturalMovement;previousAngle=Math.Atan2(y,x);lastSample=time;previousX=rawX=x;previousY=rawY=y;Lap.Samples.Add(new Motion(time,x,y));if(input!=null)Lap.RawSamples=new List<Motion>{new Motion(time,x,y)};}
        public bool Add(double x,double y,double time){
            if(input!=null){var movement=input.Inverse(x-previousX,y-previousY);rawX+=movement.X;rawY+=movement.Y;}previousX=x;previousY=y;
            double angle=Math.Atan2(y,x),delta=angle-previousAngle;while(delta>Math.PI)delta-=2*Math.PI;while(delta< -Math.PI)delta+=2*Math.PI;
            if(Math.Sqrt(x*x+y*y)>(natural?1e-7:Lap.Radius*.2)&&Math.Abs(delta)<Math.PI/2)progress+=(Lap.Clockwise?1:-1)*delta;
            previousAngle=angle;bool complete=progress>=2*Math.PI-1e-6;
            if(time-lastSample>=4||complete){Lap.Samples.Add(new Motion(time,x,y));if(input!=null)Lap.RawSamples.Add(new Motion(time,rawX,rawY));lastSample=time;}
            if(complete)Lap.Complete=true;return complete;
        }
        public double Fraction {get{return Analysis.Clamp(progress/(2*Math.PI),0,1);}}
    }
}
