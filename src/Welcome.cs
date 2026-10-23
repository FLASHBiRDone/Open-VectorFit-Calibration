using System;
using System.Drawing;
using System.Windows.Forms;
namespace VectorFit {
 class WelcomeForm:Form {
  readonly Timer fade=new Timer();bool leaving;
  public WelcomeForm(){Text="TrueTrack";ClientSize=new Size(720,570);MinimumSize=Size;MaximumSize=Size;StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(17,21,24);ForeColor=Color.White;AutoScaleMode=AutoScaleMode.None;FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;Opacity=0;
   var title=new Label{Text="Welcome to TrueTrack",Font=new Font("Segoe UI",34,FontStyle.Bold,GraphicsUnit.Pixel),TextAlign=ContentAlignment.MiddleCenter,Bounds=new Rectangle(30,70,660,65)};Controls.Add(title);
   Controls.Add(new Label{Text=DriverSetup.Text("Finn din naturlige bevegelse. Test det som fungerer for deg.","Discover your movement. Test what works for you."),Font=new Font("Segoe UI",18,GraphicsUnit.Pixel),ForeColor=Color.FromArgb(169,184,188),TextAlign=ContentAlignment.MiddleCenter,Bounds=new Rectangle(40,145,640,60)});
   var preview=new Panel{Bounds=new Rectangle(270,220,180,100),BackColor=Color.FromArgb(28,34,38)};preview.Paint+=delegate(object o,PaintEventArgs e){Sight.Draw(e.Graphics,90,50,Sight.Preference());};Controls.Add(preview);
   var choose=new Button{Text=DriverSetup.Text("Velg ditt foretrukne sikte","Choose your preferred crosshair"),Font=new Font("Segoe UI",18,GraphicsUnit.Pixel),FlatStyle=FlatStyle.Flat,ForeColor=ForeColor,BackColor=BackColor,Bounds=new Rectangle(170,335,380,44)};choose.Click+=delegate{using(var editor=new SightEditor())editor.ShowDialog(this);preview.Invalidate();};Controls.Add(choose);
   Controls.Add(new Label{Text=DriverSetup.Text("Valgfritt · du kan endre siktet senere under Kalibrer.","Optional · change it later under Calibrate."),Font=new Font("Segoe UI",15,GraphicsUnit.Pixel),ForeColor=Color.FromArgb(169,184,188),TextAlign=ContentAlignment.MiddleCenter,Bounds=new Rectangle(40,393,640,35)});
   var go=new Button{Text=DriverSetup.Text("Kom i gang","Get started"),Font=new Font("Segoe UI",18,GraphicsUnit.Pixel),FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(109,218,182),ForeColor=BackColor,Bounds=new Rectangle(240,460,240,48)};go.FlatAppearance.BorderSize=0;go.Click+=delegate{leaving=true;go.Enabled=false;};Controls.Add(go);AcceptButton=go;
   fade.Interval=16;fade.Tick+=delegate{Opacity=Math.Max(0,Math.Min(1,Opacity+(leaving?-.065:.045)));if(leaving&&Opacity<=0){DialogResult=DialogResult.OK;Close();}};Shown+=delegate{fade.Start();};
  }
  public void Preview(){Opacity=1;fade.Stop();}
  protected override void Dispose(bool disposing){if(disposing)fade.Dispose();base.Dispose(disposing);}
 }
}
