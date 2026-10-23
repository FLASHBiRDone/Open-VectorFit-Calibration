using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace VectorFit {
    public static class DriverSetup {
        public const string RawUrl="https://github.com/RawAccelOfficial/rawaccel/releases/download/v1.7.1/RawAccel_v1.7.1.zip";
        public const string RawHash="770fe3ae0919ca3c4d412f58c985eb27f5434decad809f7e8206de4e8852eec4";
        public const string InterceptionUrl="https://github.com/oblitum/Interception/releases/download/v1.0.1/Interception.zip";
        public const string InterceptionHash="ad038963d6413055765128b0b931f6e765147c9916dba79e65d872b261f9af10";
        public static string Text(string nb,string en){return L.English?en:nb;}
        public static bool Verify(string file,string expected){
            using(var stream=File.OpenRead(file))using(var sha=SHA256.Create())
                return String.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-",""),expected,StringComparison.OrdinalIgnoreCase);
        }
        public static bool Registered(bool interception){
            using(var key=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\"+(interception?"mouse":"rawaccel")))return key!=null;
        }
        public static string Extract(string zip,string destination,bool interception){
            if(!Verify(zip,interception?InterceptionHash:RawHash))throw new Exception(Text("Kontrollsummen stemmer ikke. Ingen installasjon er startet.","Checksum mismatch. No installation was started."));
            // Fresh private directory; never overwrite a previous downloaded package.
            Directory.CreateDirectory(destination);
            ZipFile.ExtractToDirectory(zip,destination);
            string exe=Path.Combine(destination,interception?@"Interception\command line installer\install-interception.exe":@"RawAccel\installer.exe");
            if(!File.Exists(exe))throw new IOException("Official installer not found.");
            return exe;
        }
        public static async Task<string> Download(bool interception){
            ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
            string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VectorFit","driver-setup",Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);string zip=Path.Combine(folder,"official.zip");
            string bundled=Path.Combine(Driver.Root,"runtime","installers","RawAccel_v1.7.1.zip");
            if(!interception&&File.Exists(bundled)){
                File.Copy(bundled,zip);
                return Extract(zip,Path.Combine(folder,"package"),false);
            }            using(var client=new WebClient()){client.Headers["User-Agent"]="Open-VectorFit-Calibration";await client.DownloadFileTaskAsync(new Uri(interception?InterceptionUrl:RawUrl),zip);}
            return Extract(zip,Path.Combine(folder,"package"),interception);
        }
        public static ProcessStartInfo StartInfo(string exe,bool interception,bool uninstall){
            return new ProcessStartInfo(exe,interception?(uninstall?"/uninstall":"/install"):""){
                WorkingDirectory=Path.GetDirectoryName(exe),UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Normal};
        }
    }
    class DriverSetupForm:Form {
        readonly bool interception;
        readonly Label status=new Label{AutoSize=false,Width=650,Height=75};
        readonly Button install=new Button{Width=310,Height=42};
        readonly Button remove=new Button{Width=310,Height=38};
        readonly CheckBox acknowledge=new CheckBox{Width=650,Height=55};
        bool working;
        bool rawReady, rawRegistered;
        static string T(string n,string e){return DriverSetup.Text(n,e);}
        public DriverSetupForm(bool optional){
            interception=optional;Text=optional?"Interception · "+T("valgfritt oppsett","optional setup"):"Raw Accel · "+T("global bruk","global use");
            ClientSize=new System.Drawing.Size(700,650);StartPosition=FormStartPosition.CenterParent;
            var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(20),FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true};Controls.Add(panel);
            panel.Controls.Add(new Label{Width=650,Height=145,Text=optional?
                T("Eksperimentell kurvekorreksjon. VectorFit må kjøre, og Raw Accel må være nøytral. Ctrl+Alt+F12 stopper korreksjonen, men avinstallerer ikke driveren. Anti-cheat kan blokkere også en installert driver som ikke brukes.",
                  "Experimental curve correction. VectorFit must stay running and Raw Accel must be neutral. Ctrl+Alt+F12 stops correction but does not uninstall the driver. Anti-cheat may block even an installed driver that is not in use."):
                T("Alt du trenger følger med TrueTrack. Du trenger ikke laste ned eller konfigurere Raw Accel selv.\n1. Trykk Installer driver og godkjenn Windows-spørsmålet.\n2. Start Windows på nytt ved behov, og åpne TrueTrack igjen.\n3. Åpne målingen og trykk Aktiver med Raw Accel.",
                  "Everything you need is included with TrueTrack. No separate Raw Accel download or manual configuration is needed.\n1. Click Install driver and approve the Windows prompt.\n2. Restart Windows if needed, then reopen TrueTrack.\n3. Open your measurement and click Activate with Raw Accel.")});
            panel.Controls.Add(new Label{Width=650,Height=100,Text=optional?
                T("Interception har egen lisens: oppstrøms oppgir LGPL og særvilkår for ikke-kommersiell distribusjon, samt egne kommersielle lisenser. VectorFits MIT-lisens dekker ikke Interception. Du må avklare vilkårene for din bruk. Vi tilbyr ingen anti-cheat-garanti.",
                  "Interception has its own license: upstream specifies LGPL and conditions for non-commercial distribution, plus separate commercial licenses. VectorFit's MIT license does not cover Interception. Check the terms for your use. We offer no anti-cheat guarantee."):
                T("Raw Accel 1.7.1 følger med og kontrolleres med SHA-256. Mangler pakken, hentes den fra offisiell GitHub. MIT-lisensen beholdes. Administratorgodkjenning og omstart kan kreves. Ingen profil aktiveres ved installasjon. Ingen garanti for alle spill.",
                  "Raw Accel 1.7.1 is bundled and checked with SHA-256. If missing, it is downloaded from official GitHub. Its MIT license is retained. Administrator approval and a restart may be required. Installation does not activate a profile. Compatibility with every game is not guaranteed.")});
            var link=new LinkLabel{Width=650,Height=30,Text=T("Les lisens og offisiell dokumentasjon","Read license and official documentation")};
            link.LinkClicked+=delegate{Process.Start(new ProcessStartInfo(optional?"https://github.com/oblitum/Interception#license":"https://github.com/RawAccelOfficial/rawaccel"){UseShellExecute=true});};panel.Controls.Add(link);
            acknowledge.Text=T("Jeg har lest informasjonen og vil selv velge installasjon.","I have read the information and want to choose installation myself.");
            acknowledge.CheckedChanged+=delegate{UpdateButtons();};if(optional)panel.Controls.Add(acknowledge);
            install.Text=optional?T("Last ned og installer","Download and install"):T("Installer driver · følger med","Install driver · included");install.Click+=async delegate{await Run(false);};panel.Controls.Add(install);
            remove.Text=T("Avinstaller Interception-driveren","Uninstall the Interception driver");remove.Click+=async delegate{await Run(true);};if(optional)panel.Controls.Add(remove);
            var refresh=new Button{Width=310,Height=35,Text=T("Kontroller status etter omstart","Check status after restart")};refresh.Click+=delegate{if(!working)RefreshStatus();};panel.Controls.Add(refresh);panel.Controls.Add(status);
            var done=new Button{Width=310,Height=35,Text=T("Tilbake til profilen","Back to profile")};done.Click+=delegate{if(!working)Close();};panel.Controls.Add(done);
            FormClosing+=delegate(object sender,FormClosingEventArgs e){if(working){e.Cancel=true;status.Text=T("Vent til oppsettet er ferdig. Ingen automatisk omstart.","Wait for setup to finish. There is no automatic restart.");}};
            RefreshStatus();
        }
        void UpdateButtons(){install.Enabled=!working&&(interception?acknowledge.Checked:!rawReady&&!rawRegistered);remove.Enabled=!working&&acknowledge.Checked;}
        void RefreshStatus(){
            try{
                if(interception)status.Text=DriverSetup.Registered(true)?T("Driverregistrering funnet. Bruk Finn mus etter omstart for å kontrollere tilgang.","Driver registration found. Use Find mouse after restarting to check access."):T("Ingen driverregistrering funnet.","No driver registration found.");
                else {string error;string json=Driver.TryRead(out error);rawReady=json!=null;rawRegistered=DriverSetup.Registered(false);status.Text=json!=null?T("Driveren svarer. Lukk oppsettet og aktiver ønsket profil.","Driver responds. Close setup and activate your chosen profile."):rawRegistered?T("Driveren er registrert, men svarer ikke. Start Windows på nytt; ikke installer på nytt først.","Driver is registered but not responding. Restart Windows before attempting reinstallation."):T("Driveren er ikke tilgjengelig. Ingen profil er aktivert av oppsettet.","Driver is unavailable. Setup has not activated a profile.");}
            }catch(Exception e){status.Text=T("Status kunne ikke bekreftes: ","Status could not be verified: ")+e.Message;}
            UpdateButtons();
        }
        async Task Run(bool uninstall){
            if(working||(interception&&!acknowledge.Checked)||(!interception&&(rawReady||rawRegistered)))return;
            if(MessageBox.Show(this,T("Dette starter offisiell driverinstallasjon med administratorgodkjenning. Omstart kan kreves. Fortsette?","This starts the official driver installer with administrator approval. A restart may be required. Continue?"),Text,MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;
            working=true;UpdateButtons();
            try{
                status.Text=T("Klargjør og kontrollerer offisiell pakke …","Preparing and verifying the official package …");
                string exe=await DriverSetup.Download(interception);
                if(interception&&!uninstall){
                    string root=Directory.GetParent(Path.GetDirectoryName(exe)).FullName;
                    string destination=Path.Combine(Driver.Root,"runtime","optional","Interception");Directory.CreateDirectory(destination);
                    File.Copy(Path.Combine(root,@"library\x64\interception.dll"),Path.Combine(destination,"interception.dll"),true);
                }
                status.Text=T("Fullfør det offisielle oppsettsvinduet. Godkjenn Windows-spørsmålet hvis du vil fortsette.","Complete the official setup window. Approve the Windows prompt if you want to continue.");
                int code=await Task.Run(delegate{using(var process=Process.Start(DriverSetup.StartInfo(exe,interception,uninstall))){process.WaitForExit();return process.ExitCode;}});
                status.Text=T("Oppsettet er avsluttet. Avslutningskode: ","Setup exited. Exit code: ")+code+T(". Les meldingen fra installasjonsprogrammet. Start Windows på nytt og kontroller status. Ingen profil er aktivert.",". Read the installer's message. Restart Windows and check status. No profile was activated.");
            }catch(System.ComponentModel.Win32Exception e){status.Text=e.NativeErrorCode==1223?T("Administratorgodkjenningen ble avbrutt. Ingen installasjon er bekreftet.","Administrator approval was cancelled. Installation is not confirmed."):e.Message;}
            catch(Exception e){status.Text=T("Oppsettet kunne ikke fullføres: ","Setup could not complete: ")+e.Message;}
            finally{working=false;if(!interception){string error;rawReady=Driver.TryRead(out error)!=null;rawRegistered=DriverSetup.Registered(false);}UpdateButtons();}
        }
    }
}

