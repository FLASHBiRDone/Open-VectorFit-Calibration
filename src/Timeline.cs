using System;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
namespace VectorFit {
 public class OutcomeTimeline:Control {
  Session s;public OutcomeTimeline(Session value){s=value;Height=285;DoubleBuffered=true;BackColor=Color.FromArgb(28,34,38);}
  public static double? Rate(System.Collections.Generic.IEnumerable<Trial> trials){var ts=trials.ToList();return ts.Count==0||ts.Any(t=>!t.Hit.HasValue)?null:(double?)ts.Count(t=>t.Hit==true)*100/ts.Count;}
  public static string HitChange(System.Collections.Generic.IEnumerable<Trial> a,System.Collections.Generic.IEnumerable<Trial> b){var x=Rate(a);var y=Rate(b);if(!x.HasValue||!y.HasValue)return "—";double change=y.Value-x.Value;return x.Value.ToString("F1")+" % → "+y.Value.ToString("F1")+" %   ("+change.ToString("+0.0;-0.0;0.0")+DriverSetup.Text(" prosentpoeng)"," percentage points)");}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var a=s.PrecisionValidation.Where(t=>!t.Corrected).ToList();var b=s.PrecisionValidation.Where(t=>t.Corrected).ToList();var g=e.Graphics;
   using(var title=new Font("Segoe UI",18,FontStyle.Bold,GraphicsUnit.Pixel))using(var font=new Font("Segoe UI",14,GraphicsUnit.Pixel))using(var ink=new SolidBrush(Color.WhiteSmoke))using(var grey=new SolidBrush(Color.Silver)){
    g.DrawString(DriverSetup.Text("Tid til treff · Baseline og ny profil","Time to hit · Baseline and new profile"),title,ink,16,12);
    if(a.Count==0||b.Count==0){g.DrawString(DriverSetup.Text("Fullfør A/B for å sammenligne.","Complete A/B to compare."),font,grey,16,52);return;}
    double av=Analysis.Median(a.Select(PrecisionCheck.Time)),bv=Analysis.Median(b.Select(PrecisionCheck.Time)),scale=Math.Max(1,Math.Max(av,bv));float width=Math.Max(100,Width-290);
    for(int i=0;i<2;i++){double value=i==0?av:bv;int y=58+i*48;g.DrawString(i==0?"Baseline":DriverSetup.Text("Ny profil","New profile"),font,ink,16,y+8);using(var fill=new SolidBrush(i==0?Color.FromArgb(105,125,145):bv<av?Color.FromArgb(80,190,145):bv>av?Color.FromArgb(225,110,105):Color.FromArgb(105,125,145)))g.FillRectangle(fill,135,y,width*(float)(value/scale),32);g.DrawString(value.ToString("F0")+" ms",font,ink,145+width,y+8);}
    g.DrawString(Math.Abs(bv-av).ToString("F0")+DriverSetup.Text(bv<av?" ms raskere":bv>av?" ms tregere":" ms forskjell",bv<av?" ms faster":bv>av?" ms slower":" ms difference")+DriverSetup.Text(" · median, inkludert bom og tidsavbrudd"," · median, including misses and timeouts"),font,ink,16,154);
    var fa=s.Validation.Where(t=>!t.Corrected);var fb=s.Validation.Where(t=>t.Corrected);
    g.DrawString(DriverSetup.Text("Presisjon: mål truffet ","Precision: targets completed ")+HitChange(a,b),font,ink,16,184);
    g.DrawString(DriverSetup.Text("Flick: bullseye ","Flick: bullseye ")+HitChange(fa,fb),font,ink,16,210);
    g.DrawString(DriverSetup.Text("Presisjon kan inkludere flere klikk; flick teller første klikk. Foreløpig ved ufullført test.\nFargene viser forskjell, ikke bekreftet forbedring. Samme tidsskala for begge profiler.","Precision may include multiple clicks; flick counts the first click. Partial tests are provisional.\nColors show differences, not confirmed benefit. Both profiles use the same time scale."),font,grey,new RectangleF(16,240,Width-32,44));
   }
  }
 }
}
