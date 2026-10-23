using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace VectorFit {
    static class Program {
        [STAThread] static int Main(string[] args) {
            try {
#if DEVELOPMENT
                if(args.Length>2&&args[0]=="--audit")return Tests.Audit(args[1],args[2]);
                if(args.Length>0&&args[0]=="--selftest")return Tests.Run(args.Length>1?args[1]:Path.Combine(Driver.Root,"test-results.txt"));
#endif
#if !DEVELOPMENT
                UserStorage.Initialize();
#endif
                L.Load();
                Native.SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
#if DEVELOPMENT
                if(Array.IndexOf(args,"en")>=0)L.English=true;
                if(args.Length>2&&args[0]=="--render-map") {var session=Session.FromJson(File.ReadAllText(args[2]));using(var map=new DirectionMap(session)){map.Width=950;using(var bmp=new Bitmap(map.Width,map.Height)){map.DrawToBitmap(bmp,new Rectangle(Point.Empty,bmp.Size));bmp.Save(args[1]);}}return 0;}

                if(args.Length>1&&args[0]=="--render-game-setup"){
                    using(var f=new GameSetupForm(new Setup(),delegate{})){f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.ShowInTaskbar=false;f.Show();Application.DoEvents();using(var bmp=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bmp,new Rectangle(Point.Empty,bmp.Size));bmp.Save(args[1]);}}return 0;
                }
                if(args.Length>1&&args[0]=="--render-driver-setup"){
                    using(var f=new DriverSetupForm(Array.IndexOf(args,"interception")>=0)){f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.ShowInTaskbar=false;f.Show();Application.DoEvents();
                        using(var bmp=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bmp,new Rectangle(Point.Empty,bmp.Size));bmp.Save(args[1]);}
                    }return 0;
                }                if(args.Length>2&&args[0]=="--render-circle-result") {
                    var session=Session.FromJson(File.ReadAllText(args[2]));using(var f=new CircleResultForm(session.Circles,session.InputCorrection)){f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.ShowInTaskbar=false;f.Show();Application.DoEvents();using(var bmp=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bmp,new Rectangle(Point.Empty,bmp.Size));bmp.Save(args[1]);}f.Close();}return 0;
                }
                if(args.Length>1&&args[0]=="--render-warning") {
                    using(var f=new ActiveProfileDialog(true,"Den aktive Raw Accel-profilen stemmer ikke med oppsettet denne målingen krever.")){f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.ShowInTaskbar=false;f.Show();Application.DoEvents();using(var bmp=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bmp,new Rectangle(Point.Empty,bmp.Size));bmp.Save(args[1]);}f.Close();}return 0;
                }
                if(args.Length>1&&args[0]=="--render-circle") {
                    using(var f=new CircleForm(new Setup())){if(Array.IndexOf(args,"cw")>=0||Array.IndexOf(args,"ccw")>=0)f.PreviewTransition(Array.IndexOf(args,"cw")>=0);else f.Preview();using(var bmp=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bmp,new Rectangle(Point.Empty,bmp.Size));bmp.Save(args[1]);}}return 0;
                }
                if(args.Length>1&&args[0]=="--render-aim") {
                    using(var f=new AimForm(new Session{PrecisionFirst=true,SmallGainProtocol=true,Setup=new Setup{Arena=Array.IndexOf(args,"arena")>=0}},false,true)){if(Array.IndexOf(args,"precision")>=0||Array.IndexOf(args,"instructions")>=0)f.PreviewInstructions(Array.IndexOf(args,"precision")>=0);else f.PreviewRings();if(Array.IndexOf(args,"turned")>=0)f.PreviewCamera(25,12);if(Array.IndexOf(args,"small")>=0)f.Size=new Size(1000,740);using(var bmp=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bmp,new Rectangle(Point.Empty,bmp.Size));bmp.Save(args[1]);}}return 0;
                }
                if(args.Length>1&&args[0]=="--render") {
                    using(var f=new MainForm()){
                        f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.ShowInTaskbar=false;f.Show();Application.DoEvents();
                        if(args.Length>2){f.PreviewPage(Int32.Parse(args[2]),args.Length>3?args[3]:null,args.Length>4&&args[4]=="interception");Application.DoEvents();}
                        if(Array.IndexOf(args,"details")>=0){f.PreviewDetails();Application.DoEvents();}
                        if(Array.IndexOf(args,"bottom")>=0){f.PreviewScroll(650);Application.DoEvents();}
                        if(Array.IndexOf(args,"movement")>=0){f.PreviewScroll(440);Application.DoEvents();}
                        if(Array.IndexOf(args,"small")>=0){f.Size=f.MinimumSize;Application.DoEvents();}
                        if(Array.IndexOf(args,"layout")>=0)File.WriteAllText(args[1]+".txt",f.PreviewLayout());
                        using(var bmp=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bmp,new Rectangle(Point.Empty,bmp.Size));bmp.Save(args[1]);}
                        f.Close();
                    }return 0;
                }
#endif
                Application.ThreadException+=delegate(object sender,System.Threading.ThreadExceptionEventArgs e){UiMessage.Show(e.Exception.Message,"TrueTrack");};
                Application.Run(new MainForm());return 0;
            }catch(Exception e){
                if(args.Length>0){File.WriteAllText(Path.Combine(Driver.Root,"startup-error.txt"),e.ToString());return 1;}
                UiMessage.Show("TrueTrack kunne ikke starte.\n\n"+e.Message+"\n\nPakk ut hele mappen før du åpner TrueTrack.exe.","TrueTrack");return 1;
            }
        }
    }
}


