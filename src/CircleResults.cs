using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace VectorFit {
    class CircleMedianChart:Control {
        readonly CircleMedian median,cw,ccw,predicted;readonly bool proposed,natural;readonly int goal,recorded;
        public CircleMedianChart(CircleData data,Correction input=null,Correction candidate=null){
            input=input??new Correction();goal=data.Goal;natural=data.Protocol>=2;recorded=data.Laps.Count(data.Counts);median=CircleAnalysis.Median(data);cw=CircleAnalysis.Median(data,null,null,true);ccw=CircleAnalysis.Median(data,null,null,false);
            var correction=candidate??CircleAnalysis.Propose(data,input,input);proposed=Math.Abs(correction.Right-input.Right)+Math.Abs(correction.Left-input.Left)+Math.Abs(correction.Up-input.Up)+Math.Abs(correction.Down-input.Down)+Math.Abs(correction.Rotation-input.Rotation)>.000001;
            predicted=CircleAnalysis.Median(data,input,correction);DoubleBuffered=true;Height=370;BackColor=Color.FromArgb(28,34,38);
        }
        static void Path(Graphics g,CircleMedian value,Pen pen,float x,float y,float scale){if(value.Points.Count<3)return;g.DrawPolygon(pen,value.Points.Select(p=>new PointF(x+(float)p.X*scale,y+(float)p.Y*scale)).ToArray());}
        static void Ellipse(Graphics g,CircleShape value,Pen pen,float x,float y,float scale){if(!value.Valid)return;var points=Enumerable.Range(0,72).Select(i=>{double a=i*2*Math.PI/72,xx=value.Major*Math.Cos(a),yy=value.Minor*Math.Sin(a);return new PointF(x+(float)(xx*Math.Cos(value.Angle)-yy*Math.Sin(value.Angle))*scale,y+(float)(xx*Math.Sin(value.Angle)+yy*Math.Cos(value.Angle))*scale);}).ToArray();g.DrawPolygon(pen,points);}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            using(var body=new Font("Segoe UI",13,GraphicsUnit.Pixel))using(var title=new Font("Segoe UI",16,FontStyle.Bold,GraphicsUnit.Pixel))using(var ink=new SolidBrush(Color.FromArgb(237,244,242)))using(var muted=new SolidBrush(Color.FromArgb(169,184,188)))
            using(var blue=new Pen(Color.FromArgb(123,173,242),1.5f))using(var orange=new Pen(Color.FromArgb(235,179,112),1.5f))using(var white=new Pen(Color.FromArgb(237,244,242),2.5f))using(var green=new Pen(Color.FromArgb(109,218,182),2.5f))using(var grid=new Pen(Color.FromArgb(94,112,121),1)){
                PaintText.Draw(g,natural?"Registrert · "+recorded+" / "+goal+" runder":"Medianform · "+median.Report.Valid+" / "+goal+" målbare sirkler",title,ink,18,13);
                PaintText.Draw(g,DriverSetup.Text("Med bevegelsesdata · med klokken: ","With movement data · clockwise: ")+cw.Report.Valid+DriverSetup.Text(" · mot klokken: "," · counterclockwise: ")+ccw.Report.Valid,body,muted,18,40);
                float left=Width*.25f,right=Width*.75f,cy=166,scale=65;
                double extent=median.Points.Concat(predicted.Points).Concat(cw.Points).Concat(ccw.Points).Select(p=>Math.Max(Math.Abs(p.X),Math.Abs(p.Y))).DefaultIfEmpty(1).Max();scale=(float)(83/Math.Max(1.15,extent));
                grid.DashStyle=DashStyle.Dash;foreach(float x in new[]{left,right}){g.DrawEllipse(grid,x-scale,cy-scale,scale*2,scale*2);g.DrawLine(grid,x-95,cy,x+95,cy);g.DrawLine(grid,x,cy-90,x,cy+90);}
                PaintText.Draw(g,"Målt median",title,ink,left-58,66);PaintText.Draw(g,proposed?"Beregnet med forslag":"Ingen sirkelendring",title,ink,right-88,66);
                Path(g,cw,blue,left,cy,scale);Path(g,ccw,orange,left,cy,scale);Path(g,median,white,left,cy,scale);
                Path(g,predicted,green,right,cy,scale);
                PaintText.Draw(g,"Lengste/korteste akse: "+(median.Ellipse.Valid?median.Report.Roundness.ToString("F3"):"—"),body,ink,left-95,265);
                PaintText.Draw(g,"Lengste/korteste akse: "+(predicted.Ellipse.Valid?predicted.Report.Roundness.ToString("F3"):"—"),body,ink,right-95,265);
                PaintText.Draw(g,"1,000 = rund · Grå stiplet linje = sirkel · Hvit = median · Blå = med · Oransje = mot",body,muted,new RectangleF(18,294,Width-36,40));
                PaintText.Draw(g,DriverSetup.Text("Alle forsøk teller. Ujevn form tas med; bare manglende bevegelsesdata utelates. Beregnet form er simulert.","Every attempt counts. Irregular shapes are included; only missing movement data is omitted. Predicted shape is simulated."),body,muted,new RectangleF(18,330,Width-36,38));
            }
        }
    }
    class CircleResultForm:Form {
        public CircleResultForm(CircleData data,Correction input){
            Text="TrueTrack | Medianform";ClientSize=new Size(920,600);MinimumSize=new Size(800,600);StartPosition=FormStartPosition.CenterParent;BackColor=Color.FromArgb(19,24,27);ForeColor=Color.FromArgb(237,244,242);Font=new Font("Segoe UI",14,GraphicsUnit.Pixel);AutoScaleMode=AutoScaleMode.None;
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(24),RowCount=4,ColumnCount=1};root.RowStyles.Add(new RowStyle(SizeType.Absolute,42));root.RowStyles.Add(new RowStyle(SizeType.Absolute,370));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,46));Controls.Add(root);
            root.Controls.Add(new Label{Text="Slik ble sirklene dine",Dock=DockStyle.Fill,Font=new Font("Segoe UI",24,FontStyle.Bold,GraphicsUnit.Pixel)},0,0);
            root.Controls.Add(new CircleMedianChart(data,input){Dock=DockStyle.Fill},0,1);
            root.Controls.Add(new Label{Text=CircleAnalysis.Median(data,input).Report.Note+"\nEn skrå ellipse kan bare korrigeres delvis med denne profilen. Ingen global profil er endret.",Dock=DockStyle.Fill,Padding=new Padding(0,10,0,0)},0,2);
            var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill};var next=new Button{Text="Fortsett til måltest",Width=240,Height=40,DialogResult=DialogResult.OK,BackColor=Color.FromArgb(109,218,182),ForeColor=Color.FromArgb(19,24,27),FlatStyle=FlatStyle.Flat};var close=new Button{Text="Lagre og avslutt",Width=200,Height=40,DialogResult=DialogResult.Cancel,FlatStyle=FlatStyle.Flat};buttons.Controls.Add(next);buttons.Controls.Add(close);root.Controls.Add(buttons,0,3);AcceptButton=next;CancelButton=close;L.Controls(this);
        }
    }
    class ActiveProfileDialog:Form {
        public ActiveProfileDialog(bool resumeAfterNeutral,string explanation){
            Text=L.T("Aktiv profil");ClientSize=new Size(650,250);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;BackColor=Color.FromArgb(19,24,27);ForeColor=Color.FromArgb(237,244,242);Font=new Font("Segoe UI",15,GraphicsUnit.Pixel);
            Controls.Add(new Label{Text=explanation+"\n\n"+(resumeAfterNeutral?"Nøytraliser for å fortsette målingen. Det aktive oppsettet sikkerhetskopieres først.":"Nøytraliser for å gå til en ny måling fra nøytralt oppsett. Denne rundens A/B fortsetter ikke."),Location=new Point(24,24),Size=new Size(602,150)});
            var apply=new Button{Text="Nøytraliser profil",Location=new Point(24,190),Size=new Size(260,40),DialogResult=DialogResult.OK,BackColor=Color.FromArgb(109,218,182),ForeColor=Color.FromArgb(19,24,27),FlatStyle=FlatStyle.Flat};
            var cancel=new Button{Text="Avbryt",Location=new Point(298,190),Size=new Size(150,40),DialogResult=DialogResult.Cancel,FlatStyle=FlatStyle.Flat};Controls.Add(apply);Controls.Add(cancel);CancelButton=cancel;AcceptButton=cancel;L.Controls(this);
        }
    }
}

namespace VectorFit {
    class MeasuredCircleComparison:Control {
        readonly CircleMedian before,after;readonly CircleData a,b;
        public MeasuredCircleComparison(CircleData baseline,CircleData current){a=baseline;b=current;before=CircleAnalysis.Median(a);after=CircleAnalysis.Median(b);Height=330;DoubleBuffered=true;BackColor=Color.FromArgb(28,34,38);}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            using(var font=new Font("Segoe UI",13,GraphicsUnit.Pixel))using(var blue=new Pen(Color.FromArgb(123,173,242),2))using(var green=new Pen(Color.FromArgb(109,218,182),2))using(var grid=new Pen(Color.FromArgb(80,95,104),1)){
                var all=before.Points.Concat(after.Points).ToList();double extent=all.Select(p=>Math.Max(Math.Abs(p.X),Math.Abs(p.Y))).DefaultIfEmpty(1).Max();float scale=(float)(90/Math.Max(1.15,extent)),x=Width/2,y=145;
                grid.DashStyle=DashStyle.Dash;g.DrawEllipse(grid,x-scale,y-scale,2*scale,2*scale);
                Action<CircleMedian,Pen> draw=(m,p)=>{if(m.Points.Count>2)g.DrawPolygon(p,m.Points.Select(q=>new PointF(x+(float)q.X*scale,y+(float)q.Y*scale)).ToArray());};draw(before,blue);draw(after,green);
                g.DrawString(DriverSetup.Text("Faktisk målte medianformer · ingen simulering","Measured median shapes · no simulation"),font,Brushes.White,16,14);
                g.DrawString(DriverSetup.Text("Blå: første baseline · grønn: målt forslag","Blue: first baseline · green: measured proposal"),font,Brushes.White,16,250);
                g.DrawString(DriverSetup.Text("Akseforhold (1 = rund): ","Axis ratio (1 = round): ")+(before.Ellipse.Valid?before.Report.Roundness.ToString("F3"):"—")+" → "+(after.Ellipse.Valid?after.Report.Roundness.ToString("F3"):"—"),font,Brushes.White,16,275);
                g.DrawString(DriverSetup.Text("Sentrert og størrelsesnormalisert. Rundere form beviser ikke bedre sikting.","Centered and size-normalized. A rounder shape does not prove better aim."),font,Brushes.LightGray,16,300);
            }
        }
    }
}
