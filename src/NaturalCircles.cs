using System;
using System.Collections.Generic;
using System.Linq;

namespace VectorFit {
    // Every recorded path with actual finite movement contributes. No roundness, closure or ellipse-residual filter.
    public static class NaturalCircles {
        static double Distance(PointD a,PointD b){return Math.Sqrt(Math.Pow(a.X-b.X,2)+Math.Pow(a.Y-b.Y,2));}
        static PointD Vector(CircleShape s){return new PointD(Math.Log(s.Roundness)*Math.Cos(2*s.Angle),Math.Log(s.Roundness)*Math.Sin(2*s.Angle));}
        static double Gap(IEnumerable<CircleShape> a,IEnumerable<CircleShape> b){var x=a.Select(Vector).ToArray();var y=b.Select(Vector).ToArray();if(x.Length==0||y.Length==0)return Double.PositiveInfinity;return Distance(new PointD(Analysis.Median(x.Select(p=>p.X)),Analysis.Median(x.Select(p=>p.Y))),new PointD(Analysis.Median(y.Select(p=>p.X)),Analysis.Median(y.Select(p=>p.Y))));}
        public static List<PointD> Normalize(CircleLap lap){
            if(lap.Samples==null||lap.Samples.Count<2||lap.Samples.Any(p=>Double.IsNaN(p.X)||Double.IsInfinity(p.X)||Double.IsNaN(p.Y)||Double.IsInfinity(p.Y)))return null;
            var samples=lap.Samples.Select(p=>new PointD(p.X,p.Y)).ToList();if(!lap.Clockwise)samples.Reverse();
            double cx=(samples.Min(p=>p.X)+samples.Max(p=>p.X))/2,cy=(samples.Min(p=>p.Y)+samples.Max(p=>p.Y))/2;
            var lengths=new double[samples.Count];for(int i=1;i<samples.Count;i++)lengths[i]=lengths[i-1]+Distance(samples[i],samples[i-1]);double total=lengths.Last();if(total<1e-8)return null;
            var points=new List<PointD>();int at=1;
            for(int i=0;i<72;i++){double wanted=total*i/71;while(at<lengths.Length-1&&lengths[at]<wanted)at++;double t=(wanted-lengths[at-1])/Math.Max(1e-12,lengths[at]-lengths[at-1]);points.Add(new PointD(samples[at-1].X+(samples[at].X-samples[at-1].X)*t-cx,samples[at-1].Y+(samples[at].Y-samples[at-1].Y)*t-cy));}
            double scale=Math.Sqrt(points.Average(p=>p.X*p.X+p.Y*p.Y));if(scale<1e-8)return null;
            // Align phase, never rotate the measured shape. Preserve its orientation relative to the mouse axes.
            int start=Enumerable.Range(0,72).OrderBy(i=>Math.Abs(Math.Atan2(points[i].Y,points[i].X))).First();
            return Enumerable.Range(0,72).Select(i=>new PointD(points[(start+i)%72].X/scale,points[(start+i)%72].Y/scale)).ToList();
        }
        static CircleShape Shape(List<PointD> points){return CircleAnalysis.Measure(new CircleLap{Radius=1,Complete=true,Samples=points.Select((p,i)=>new Motion(i*4,p.X,p.Y)).ToList()},true);}
        public static CircleMedian Median(CircleData data,Correction undo,Correction apply,bool? clockwise){
            var result=new CircleMedian();var report=result.Report;
            var rows=new List<Tuple<CircleLap,List<PointD>,CircleShape>>();
            foreach(var original in data.Laps.Where(l=>data.Counts(l)&&(!clockwise.HasValue||l.Clockwise==clockwise.Value))){
                report.Total++;var lap=undo==null?original:CircleAnalysis.UndoInput(original,undo);if(apply!=null)lap=CircleAnalysis.TransformLap(lap,apply);
                var points=Normalize(lap);if(points!=null)rows.Add(Tuple.Create(lap,points,Shape(points)));
            }
            report.Valid=rows.Count;result.Clockwise=rows.Count(r=>r.Item1.Clockwise);result.Counterclockwise=rows.Count-result.Clockwise;
            if(rows.Count==0){report.Note="Alle runder er registrert. Ingen bevegelsesdata å beregne form fra.";return result;}
            for(int i=0;i<72;i++)result.Points.Add(new PointD(Analysis.Median(rows.Select(r=>r.Item2[i].X)),Analysis.Median(rows.Select(r=>r.Item2[i].Y))));
            result.Ellipse=Shape(result.Points);report.AxisRatio=result.Ellipse.AxisRatio;report.Roundness=result.Ellipse.Roundness;
            // Reliability governs the proposed correction, never whether the user's round counts or appears in the median.
            bool geometry=result.Ellipse.Valid;
            if(geometry){
                var shaped=rows.Where(r=>r.Item3.Valid).ToList();
                var center=Vector(result.Ellipse);result.Spread=Analysis.Median(shaped.Select(r=>Distance(Vector(r.Item3),center)));
                var cw=shaped.Where(r=>r.Item1.Clockwise).Select(r=>r.Item3).ToArray();var ccw=shaped.Where(r=>!r.Item1.Clockwise).Select(r=>r.Item3).ToArray();result.DirectionGap=Gap(cw,ccw);
                result.TimeGap=new[]{cw,ccw}.Max(g=>Gap(g.Take(g.Length/2),g.Skip(g.Length/2)));bool steady=result.TimeGap<.08;
                report.Stable=data.Laps.Count(l=>data.Counts(l)&&l.Clockwise)==30&&data.Laps.Count(l=>data.Counts(l)&&!l.Clockwise)==30&&cw.Length>=24&&ccw.Length>=24&&result.Spread<.10&&result.DirectionGap<.08&&steady;
            }
            if(data.Protocol>=3){
                // Typical anisotropy drives the proposal; ordinary lap-to-lap scatter reduces its strength.
                var cwShapes=rows.Where(r=>r.Item1.Clockwise&&r.Item3.Valid).Select(r=>r.Item3).ToList();
                var ccwShapes=rows.Where(r=>!r.Item1.Clockwise&&r.Item3.Valid).Select(r=>r.Item3).ToList();
                double cwBias=Analysis.Median(cwShapes.Select(v=>Math.Log(v.AxisRatio))),ccwBias=Analysis.Median(ccwShapes.Select(v=>Math.Log(v.AxisRatio)));
                bool opposed=cwBias*ccwBias<0&&Math.Min(Math.Abs(cwBias),Math.Abs(ccwBias))>.02;
                report.Stable=geometry&&data.Laps.Count(l=>data.Counts(l)&&l.Clockwise)==30&&data.Laps.Count(l=>data.Counts(l)&&!l.Clockwise)==30&&cwShapes.Count>=24&&ccwShapes.Count>=24&&!opposed&&result.Spread<.55&&result.DirectionGap<.25&&result.TimeGap<.35;
                result.TrendStrength=Analysis.Clamp(1-Math.Max(0,result.Spread-.10)/.60,.25,1);
                report.Note=report.Stable?"En felles ellipsetendens brukes til et forsiktig akseforslag. Vanlig variasjon reduserer styrken, men fjerner ikke tendensen.":opposed?"Dreieretningene peker mot motsatte akseendringer. Ingen felles sirkelkorrigering foreslås.":"Formen vises, men svært store avvik, endret tendens eller for lite formdata hindrer et felles akseforslag.";
                return result;
            }
            report.Note=report.Stable?"Alle runder teller. Den naturlige formen gir et stabilt grunnlag for et prøveforslag.":"Alle runder teller og formen vises. Ingen entydig sirkelkorreksjon; måltesten kan fortsatt foreslå akseendringer.";
            return result;
        }
    }
}
