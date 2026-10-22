using System;
using System.Drawing;
using System.Windows.Forms;

namespace VectorFit {
    public class ProposalRow {
        public string Label;public double Before,After;public bool Degrees;
        public double Change {get{return Degrees?After-Before:100*(After/Before-1);}}
        public string BeforeText {get{return Before.ToString(Degrees?"F2":"F4")+(Degrees?"°":"×");}}
        public string AfterText {get{return After.ToString(Degrees?"F2":"F4")+(Degrees?"°":"×");}}
        public string ChangeText {get{return Math.Abs(Change)<.00001?L.T("Uendret"):Change.ToString("+0.00;-0.00")+(Degrees?"°":" %");}}
    }
    public static class ProposalValues {
        public static ProposalRow[] Rows(Session s){var a=s.InputCorrection;var b=s.Correction;return new[]{
            new ProposalRow{Label="Samlet følsomhet",Before=a.Sensitivity,After=b.Sensitivity},
            new ProposalRow{Label="X · Høyre",Before=a.Right,After=b.Right},new ProposalRow{Label="X · Venstre",Before=a.Left,After=b.Left},
            new ProposalRow{Label="Y · Opp",Before=a.Up,After=b.Up},new ProposalRow{Label="Y · Ned",Before=a.Down,After=b.Down},
            new ProposalRow{Label="Rotasjon",Before=a.Rotation,After=b.Rotation,Degrees=true}};}
    }
    class ProposalView:Control {
        readonly ProposalRow[] rows;
        public ProposalView(Session s){rows=ProposalValues.Rows(s);Height=244;DoubleBuffered=true;BackColor=Color.FromArgb(28,34,38);AccessibleName=L.T("Foreslåtte innstillinger");}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;
            using(var title=new Font("Segoe UI",16,FontStyle.Bold,GraphicsUnit.Pixel))using(var body=new Font("Segoe UI",14,GraphicsUnit.Pixel))using(var small=new Font("Segoe UI",12,GraphicsUnit.Pixel))using(var ink=new SolidBrush(Color.FromArgb(237,244,242)))using(var muted=new SolidBrush(Color.FromArgb(169,184,188)))using(var green=new SolidBrush(Color.FromArgb(109,218,182))){
                PaintText.Draw(g,"Foreslåtte innstillinger",title,ink,18,10);
                float current=Width*.36f,next=Width*.59f,change=Width*.81f;
                PaintText.Draw(g,"Målt med",small,muted,current,39);PaintText.Draw(g,"Nytt forslag",small,muted,next,39);PaintText.Draw(g,"Endring",small,muted,change,39);
                for(int i=0;i<rows.Length;i++){float y=62+i*24;var row=rows[i];PaintText.Draw(g,row.Label,body,ink,18,y);PaintText.Draw(g,row.BeforeText,body,muted,current,y);PaintText.Draw(g,row.AfterText,body,green,next,y);PaintText.Draw(g,row.ChangeText,body,ink,change,y);}
                PaintText.Draw(g,"Aksemultiplikatorene kommer i tillegg til samlet følsomhet. DPI på musen endres ikke.",small,muted,new RectangleF(18,213,Width-36,29));
            }
        }
    }
}
