using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;
namespace VectorFit {
 public class Sight {
  public int Style=0,Size=7,Gap=2,Thickness=2,ColorIndex=0;
  public static readonly Color[] Colors={Color.White,Color.Cyan,Color.Lime,Color.Magenta,Color.Yellow,Color.Red};
  public static Sight Parse(string json){try{return String.IsNullOrEmpty(json)?new Sight():JsonConvert.DeserializeObject<Sight>(json)??new Sight();}catch{return new Sight();}}
  public static string Preference(){try{return File.ReadAllText(Path.Combine(Driver.Data,"crosshair.json"));}catch{return null;}}
  public static void Draw(Graphics g,float x,float y,string json){
   if(String.IsNullOrEmpty(json)){using(var p=new Pen(Color.White,1.5f)){g.DrawLine(p,x-7,y,x-2,y);g.DrawLine(p,x+2,y,x+7,y);g.DrawLine(p,x,y-7,x,y-2);g.DrawLine(p,x,y+2,x,y+7);}return;}
   var s=Parse(json);float r=(float)Analysis.Clamp(s.Size,3,32),gap=(float)Analysis.Clamp(s.Gap,0,r-1),th=(float)Analysis.Clamp(s.Thickness,1,6);int style=(int)Analysis.Clamp(s.Style,0,5);Color c=Colors[(int)Analysis.Clamp(s.ColorIndex,0,Colors.Length-1)];
   using(var p=new Pen(c,th))using(var b=new SolidBrush(c)){
    if(style==2||style==3||style==4||style==5)g.DrawEllipse(p,x-r,y-r,2*r,2*r);
    if(style==0||style==4){g.DrawLine(p,x-r,y,x-gap,y);g.DrawLine(p,x+gap,y,x+r,y);g.DrawLine(p,x,y-r,x,y-gap);g.DrawLine(p,x,y+gap,x,y+r);}
    if(style==5){float inner=r*.7f;g.DrawLine(p,x-r-3,y,x-inner,y);g.DrawLine(p,x+inner,y,x+r+3,y);g.DrawLine(p,x,y-r-3,x,y-inner);g.DrawLine(p,x,y+inner,x,y+r+3);}
    if(style==1||style==3||style==5)g.FillEllipse(b,x-th,y-th,th*2,th*2);
   }
  }
 }
 class SightEditor:Form {
  public string Result;readonly Sight value;readonly Panel preview=new Panel{Width=440,Height=180,BackColor=Color.FromArgb(19,24,27)};
  string T(string nb,string en){return DriverSetup.Text(nb,en);}
  public SightEditor(){value=Sight.Parse(Sight.Preference());Text=T("TrueTrack · sikte","TrueTrack · crosshair");ClientSize=new Size(490,530);AutoScaleMode=AutoScaleMode.None;Font=new Font("Segoe UI",14,GraphicsUnit.Pixel);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;
   var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(20),FlowDirection=FlowDirection.TopDown,WrapContents=false};Controls.Add(panel);panel.Controls.Add(preview);preview.Paint+=delegate(object o,PaintEventArgs e){Sight.Draw(e.Graphics,preview.Width/2,preview.Height/2,JsonConvert.SerializeObject(value));};
   var styles=new ComboBox{Width=440,DropDownStyle=ComboBoxStyle.DropDownList};styles.Items.AddRange(L.English?new object[]{"Cross","Dot","Ring","Ring + dot","Ring + cross","Ring + ticks + dot"}:new object[]{"Kryss","Prikk","Ring","Ring + prikk","Ring + kryss","Ring + streker + prikk"});styles.SelectedIndex=(int)Analysis.Clamp(value.Style,0,5);styles.SelectedIndexChanged+=delegate{value.Style=styles.SelectedIndex;preview.Invalidate();};panel.Controls.Add(styles);
   var colors=new ComboBox{Width=440,DropDownStyle=ComboBoxStyle.DropDownList};colors.Items.AddRange(L.English?new object[]{"White","Cyan","Green","Magenta","Yellow","Red"}:new object[]{"Hvit","Cyan","Grønn","Magenta","Gul","Rød"});colors.SelectedIndex=(int)Analysis.Clamp(value.ColorIndex,0,5);colors.SelectedIndexChanged+=delegate{value.ColorIndex=colors.SelectedIndex;preview.Invalidate();};panel.Controls.Add(colors);
   Field(panel,T("Størrelse (piksler)","Size (pixels)"),value.Size,3,32,v=>value.Size=v);Field(panel,T("Avstand fra sentrum","Centre gap"),value.Gap,0,20,v=>value.Gap=v);Field(panel,T("Tykkelse","Thickness"),value.Thickness,1,6,v=>value.Thickness=v);
   panel.Controls.Add(new Label{Width=440,Height=48,Text=T("Gjelder ny baseline og fri trening. Pågående sammenligninger beholder sitt sikte.","Applies to a new baseline and free practice. Existing comparisons retain their crosshair.")});
   var save=new Button{Width=210,Height=36,Text=T("Lagre sikte","Save crosshair")};save.Click+=delegate{Result=JsonConvert.SerializeObject(value);Directory.CreateDirectory(Driver.Data);File.WriteAllText(Path.Combine(Driver.Data,"crosshair.json"),Result);DialogResult=DialogResult.OK;Close();};panel.Controls.Add(save);
  }
  void Field(FlowLayoutPanel p,string text,int value,int min,int max,Action<int> change){var row=new FlowLayoutPanel{Width=440,Height=34};row.Controls.Add(new Label{Text=text,Width=280});var n=new NumericUpDown{Minimum=min,Maximum=max,Value=(decimal)Analysis.Clamp(value,min,max),Width=100};n.ValueChanged+=delegate{change((int)n.Value);preview.Invalidate();};row.Controls.Add(n);p.Controls.Add(row);}
 }
}
