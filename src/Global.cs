using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace VectorFit {
    public static class InterceptionApi {
        [StructLayout(LayoutKind.Sequential)] public struct Stroke {public ushort State,Flags;public short Wheel;public int X,Y;public uint Information;}
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int Predicate(int device);
                const string Dll="interception.dll";
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr LoadLibraryEx(string path,IntPtr file,uint flags);
        static InterceptionApi(){if(LoadLibraryEx(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"runtime","optional","Interception","interception.dll"),IntPtr.Zero,8)==IntPtr.Zero)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"Valgfritt Interception-bibliotek mangler. Raw Accel-profilen fungerer uten dette.");}
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern IntPtr interception_create_context();
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern void interception_destroy_context(IntPtr context);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern void interception_set_filter(IntPtr context,Predicate predicate,ushort filter);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern ushort interception_get_filter(IntPtr context,int device);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern int interception_wait_with_timeout(IntPtr context,uint ms);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern int interception_receive(IntPtr context,int device,out Stroke stroke,uint count);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern int interception_send(IntPtr context,int device,ref Stroke stroke,uint count);
        [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] public static extern uint interception_get_hardware_id(IntPtr context,int device,[Out] byte[] buffer,uint size);
        public static string Hardware(IntPtr context,int device) {var bytes=new byte[2048];uint size=interception_get_hardware_id(context,device,bytes,(uint)bytes.Length);return size==0?"":Encoding.Unicode.GetString(bytes,0,(int)Math.Min(size,(uint)bytes.Length)).TrimEnd('\0').Replace('\0',' ');}
    }
    // Exactly the same floating-point curve and directional transforms as the trainer.
    public class MouseTransform {
        readonly CurveEngine curve;readonly Correction correction;readonly double degrees;double remainderX,remainderY;
        public MouseTransform(Session s){curve=new CurveEngine(s.ActiveCurve);correction=s.Correction;degrees=s.Setup.DegreesPerCount;}
        public void Reset(){curve.Reset();remainderX=remainderY=0;}
        public void Apply(ref InterceptionApi.Stroke stroke,double time,bool active) {
            if(!active||(stroke.Flags&1)!=0){Reset();return;}
            if(stroke.X==0&&stroke.Y==0)return;
            var bent=curve.Transform(stroke.X*degrees,stroke.Y*degrees,time);var output=correction.Transform(bent.X,bent.Y);
            double x=output.X/degrees+remainderX,y=output.Y/degrees+remainderY;
            int ix=(int)Math.Round(x),iy=(int)Math.Round(y);remainderX=x-ix;remainderY=y-iy;stroke.X=ix;stroke.Y=iy;
        }
    }
    class GlobalForm:Form {


        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
        readonly ComboBox devices=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=650};
        readonly Label status=new Label{AutoSize=false,Width=650,Height=100,Text="Valgfri Interception-profil. Last inn en testet kurveprofil og velg musen."};
        readonly Button start=new Button{Text="Aktiver globalt",Width=210,Height=40};
        Session session;volatile bool stop=true;Thread worker;public bool IsFiltering {get{return !stop||(worker!=null&&worker.IsAlive);}}
        class Device {public int Id;public string Hardware;public override string ToString(){return L.T("Mus ")+Id+": "+Hardware;}}
        public GlobalForm() {
            Text="VectorFit | Valgfri Interception-profil";ClientSize=new System.Drawing.Size(710,505);
            var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(20),FlowDirection=FlowDirection.TopDown,WrapContents=false};Controls.Add(panel);
            panel.Controls.Add(new Label{Width=650,Height=65,Text="Profilen gjelder den valgte musen i Windows og alle programmer.\nStopp med Ctrl+Alt+F12 eller Stopp-knappen. Raw Accel må være nøytral.\nEksperimentell kurvekorreksjon. Krever Interception-driveren og omstart."});
            panel.Controls.Add(new Label{Width=650,Height=50,Text="FACEIT blokkerer Interception-driveren. Å stoppe filteret avinstallerer ikke driveren.\nBruk bare der Interception er tillatt. Dette vinduet installerer ingen driver."});
            panel.Controls.Add(MakeButton("Åpne måling",LoadMeasurement));
            panel.Controls.Add(MakeButton("Finn mus",RefreshDevices));panel.Controls.Add(devices);panel.Controls.Add(start);
            var stopButton=new Button{Text="Stopp",Width=210,Height=35};stopButton.Click+=delegate{stop=true;L.SetText(status,"Stopper …");};panel.Controls.Add(stopButton);panel.Controls.Add(status);
            start.Click+=delegate{Guard(StartFilter);};FormClosing+=delegate{stop=true;if(worker!=null)worker.Join(1000);};
            L.Controls(this);
        }
        Button MakeButton(string title,Action action){var b=new Button{Text=title,Width=210,Height=35};b.Click+=delegate{Guard(action);};return b;}
        void Guard(Action a){try{if(IsFiltering)throw new Exception("Stopp korreksjonen før du endrer oppsettet.");a();}catch(Exception e){UiMessage.Show(this,e.Message,"VectorFit");}}
        void LoadMeasurement(){using(var d=new OpenFileDialog{Filter=L.T("VectorFit-måling|*.vectorfit.json"),InitialDirectory=Driver.Data})if(d.ShowDialog()==DialogResult.OK){
            if(new FileInfo(d.FileName).Length>50*1024*1024)throw new Exception("Målefilen er for stor.");
            var s=Session.FromJson(File.ReadAllText(d.FileName));s.SelectMode(ProfileMode.Interception);
            if(!s.ReadyForInterception)throw new Exception("Åpne målingen i VectorFit, velg Interception og fullfør A/B-testen for denne profiltypen først.");
            session=s;L.SetText(status,"Måling lastet: "+s.Setup.Mouse+", "+s.Setup.Dpi+" DPI. Kurveområder: "+s.Curve.SupportedBins);
        }}
        public void UseSession(Session value) {
            if(IsFiltering)return;session=null;
            if(value==null||!value.ReadyForInterception){L.SetText(status,"Målingen er ikke klar for Interception. Velg Interception-profilen og fullfør dens A/B-test som bekrefter forbedring.");return;}
            session=value;L.SetText(status,"Valgt måling: "+value.Setup.Mouse+", "+value.Setup.Dpi+" DPI. Velg musen med Finn mus.");
        }
        void RefreshDevices(){devices.Items.Clear();IntPtr context=InterceptionApi.interception_create_context();if(context==IntPtr.Zero)throw new Exception("Interception-driveren er ikke tilgjengelig. Kurvekorreksjonen kan ikke aktiveres før driveren er tilgjengelig.");
            try{for(int i=11;i<=20;i++){string id=InterceptionApi.Hardware(context,i);if(id!="")devices.Items.Add(new Device{Id=i,Hardware=id});}}finally{InterceptionApi.interception_destroy_context(context);}
            if(devices.Items.Count==1)devices.SelectedIndex=0;
        }
        void StartFilter(){
            if(worker!=null&&worker.IsAlive)throw new Exception("Den forrige kjøringen avsluttes fortsatt.");
            if(session==null||devices.SelectedItem==null)throw new Exception("Velg måling og mus først.");
            if(!session.ReadyForInterception)throw new Exception("Profiltypen er endret eller Interception-testen er ikke bestått. Velg en testet Interception-profil.");
            string error;string raw=Driver.TryRead(out error);if((raw==null&&!Driver.KnownAbsent(error))||!Driver.Neutral(raw))throw new Exception("Raw Accel må være tilgjengelig og nøytral, eller ikke installert. Åpne VectorFit for å nøytralisere profilen.");
            var device=(Device)devices.SelectedItem;var transform=new MouseTransform(session);stop=false;start.Enabled=false;
            L.SetText(status,"Aktiverer profilen globalt. Ctrl+Alt+F12 stopper.");
            worker=new Thread(delegate(){RunFilter(device,transform);}){IsBackground=true,Name="VectorFit mouse filter"};worker.Start();
        }
        void RunFilter(Device selected,MouseTransform transform){
            IntPtr context=IntPtr.Zero;string result="Korreksjonen er stoppet.";
            InterceptionApi.Predicate predicate=d=>d==selected.Id?1:0;
            try{
                context=InterceptionApi.interception_create_context();if(context==IntPtr.Zero)throw new Exception("Kunne ikke åpne Interception-driveren.");
                if(InterceptionApi.Hardware(context,selected.Id)!=selected.Hardware)throw new Exception("Musen er endret. Finn og velg musen på nytt.");
                InterceptionApi.interception_set_filter(context,predicate,0xFFFF);
                if(InterceptionApi.interception_get_filter(context,selected.Id)!=0xFFFF)throw new Exception("Musefilteret kunne ikke aktiveres.");
                var clock=Stopwatch.StartNew();
                BeginInvoke((Action)delegate{if(!stop)L.SetText(status,"Aktiv globalt: den valgte musen korrigeres i Windows og spill. Ctrl+Alt+F12 stopper.");});
                while(!stop){
                    if((GetAsyncKeyState(0x11)&0x8000)!=0&&(GetAsyncKeyState(0x12)&0x8000)!=0&&(GetAsyncKeyState(0x7B)&0x8000)!=0){stop=true;break;}
                    int device=InterceptionApi.interception_wait_with_timeout(context,10);if(device==0)continue;
                    InterceptionApi.Stroke stroke;if(InterceptionApi.interception_receive(context,device,out stroke,1)!=1)continue;
                    var original=stroke;
                    try{transform.Apply(ref stroke,clock.Elapsed.TotalMilliseconds,!stop);}catch{stroke=original;stop=true;result="Korreksjonen stoppet ved en beregningsfeil.";}
                    if(InterceptionApi.interception_send(context,device,ref stroke,1)!=1)throw new Exception("Driveren kunne ikke sende museinput videre. Filteret stoppes.");
                }
            }catch(Exception e){result=e.Message;}
            finally{
                if(context!=IntPtr.Zero){InterceptionApi.interception_set_filter(context,predicate,0);InterceptionApi.interception_destroy_context(context);}GC.KeepAlive(predicate);stop=true;
                if(!IsDisposed&&IsHandleCreated)try{BeginInvoke((Action)delegate{start.Enabled=true;L.SetText(status,result);});}catch(InvalidOperationException){}
            }
        }
    }
}
