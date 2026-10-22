using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VectorFit {
    public static class Driver {
        public static string Root=AppDomain.CurrentDomain.BaseDirectory;
        public static string Data=Path.Combine(Root,"profiles");
        public static string Run(string command,string file) {
            var start=new ProcessStartInfo(Path.Combine(Root,"runtime","VectorFit.Bridge.exe"),command+(file==null?"":" \""+file+"\"")) {
                WorkingDirectory=Path.Combine(Root,"runtime"),UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,
                StandardOutputEncoding=System.Text.Encoding.UTF8,StandardErrorEncoding=System.Text.Encoding.UTF8
            };
            using(var p=Process.Start(start)) {
                var output=p.StandardOutput.ReadToEndAsync();var error=p.StandardError.ReadToEndAsync();
                if(!p.WaitForExit(12000)) {p.Kill();throw new Exception("Driveren svarte ikke innen 12 sekunder.");}
                if(p.ExitCode!=0)throw new Exception(error.Result.Trim());return output.Result;
            }
        }
        public static JObject Config(Correction c) {
            if(!c.Safe())throw new Exception("Profilen ligger utenfor tillatte grenser.");
            var obj=JObject.Parse(File.ReadAllText(Path.Combine(Root,"runtime","rawaccel-default.json")));
            var p=(JObject)obj["profiles"][0];p["name"]="VectorFit";
            p["Output DPI"]=1000*c.Right*c.Sensitivity;
            p["Y/X output DPI ratio (vertical sens multiplier)"]=c.Down/c.Right;
            p["L/R output DPI ratio (left sens multiplier)"]=c.Left/c.Right;
            p["U/D output DPI ratio (up sens multiplier)"]=c.Up/c.Down;
            p["Degrees of rotation"]=c.Rotation;
            return obj;
        }
        public static string TryRead(out string error) { try {error=null;return Run("read",null);}catch(Exception e){error=e.Message;return null;} }
        public static bool Neutral(string json) {
            if(json==null)return true;
            var a=JObject.Parse(json);
            // For a controlled baseline, require neutral gains and zero normalization for every configured device.
            var dc=(JObject)a["defaultDeviceConfig"];
            if((int)dc["DPI (normalizes input speed unit: counts/ms -> in/s)"]!=0)return false;
            foreach(JObject d in (JArray)a["devices"])if((int)d["config"]["DPI (normalizes input speed unit: counts/ms -> in/s)"]!=0)return false;
            foreach(JObject p in (JArray)a["profiles"]) {
                if(Math.Abs((double)p["Output DPI"]-1000)>1e-7||Math.Abs((double)p["Y/X output DPI ratio (vertical sens multiplier)"]-1)>1e-7||
                   Math.Abs((double)p["L/R output DPI ratio (left sens multiplier)"]-1)>1e-7||Math.Abs((double)p["U/D output DPI ratio (up sens multiplier)"]-1)>1e-7||
                   (double)p["Degrees of rotation"]!=0||(double)p["Degrees of angle snapping"]!=0||(double)p["Input Speed Cap"]!=0||
                   (string)p["Whole or horizontal accel parameters"]["mode"]!="noaccel"||(string)p["Vertical accel parameters"]["mode"]!="noaccel")return false;
                foreach(JProperty q in ((JObject)p["Input speed calculation parameters"]).Properties())if(q.Name.StartsWith("Time in ms")&&(double)q.Value!=0)return false;
            }return true;
        }
        static bool Equivalent(JToken a,JToken b) {
            if(a==null||b==null)return a==b;
            bool na=a.Type==JTokenType.Integer||a.Type==JTokenType.Float,nb=b.Type==JTokenType.Integer||b.Type==JTokenType.Float;
            if(na&&nb)return Math.Abs((double)a-(double)b)<1e-7;
            if(a.Type!=b.Type)return false;
            if(a is JObject){var x=(JObject)a;var y=(JObject)b;return x.Count==y.Count&&x.Properties().All(p=>y[p.Name]!=null&&Equivalent(p.Value,y[p.Name]));}
            if(a is JArray){var x=(JArray)a;var y=(JArray)b;return x.Count==y.Count&&x.Select((v,i)=>Equivalent(v,y[i])).All(v=>v);}
            return JToken.DeepEquals(a,b);
        }
        public static bool MatchesCorrection(string json,Correction correction) {
            if(correction==null||!correction.Safe())return false;
            var expected=Config(correction);
            if(json==null)return Neutral(expected.ToString());
            var actual=JObject.Parse(json);
            if(!(actual["profiles"] is JArray)||((JArray)actual["profiles"]).Count!=1)return false;
            actual["profiles"][0]["name"]=expected["profiles"][0]["name"];
            return Equivalent(actual,expected);
        }
        public static string Export(Session session,string name,bool allowUnvalidated=false) {
            if(!session.CanTryRawAccel||(!session.ReadyForRawAccel&&!allowUnvalidated))throw new Exception("Profilen må være beregnet og innenfor grensene. En profil uten bekreftet forbedring krever et uttrykkelig valg før eksport eller aktivering.");
            Directory.CreateDirectory(Data);string path=Path.Combine(Data,name+".rawaccel.json");
            File.WriteAllText(path,Config(session.Correction).ToString());Run("validate",path);return path;
        }
        public static string Backup(string json) {
            Directory.CreateDirectory(Data);string p=Path.Combine(Data,"backup-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".json");
            File.WriteAllText(p,json);File.WriteAllText(Path.Combine(Data,"last-backup.txt"),Path.GetFileName(p));return p;
        }
        public static string LastBackup() {
            string p=Path.Combine(Data,"last-backup.txt");if(!File.Exists(p))return null;
            string name=File.ReadAllText(p).Trim();if(name!=Path.GetFileName(name))throw new Exception("Ugyldig sikkerhetskopi.");
            string path=Path.Combine(Data,name);return File.Exists(path)?path:null;
        }
        public static void Apply(string path,bool backup) {
            string before=Run("read",null);if(backup)Backup(before);
            try {Run("apply",path);}catch(Exception e) {
                // Best effort rollback, with the exact previous device settings, not merely a neutral default.
                string recovery=Path.Combine(Data,"recovery-before-failed-apply.json");File.WriteAllText(recovery,before);
                try {Run("apply",recovery);}catch(Exception r){throw new Exception(e.Message+"\nGjenoppretting feilet også: "+r.Message+"\nSikkerhetskopi: "+recovery);}
                throw new Exception(e.Message+"\nTidligere oppsett er gjenopprettet.");
            }
        }
        public static string Fingerprint(string json) {
            if(json==null)return "no-driver";
            using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(System.Text.Encoding.UTF8.GetBytes(JObject.Parse(json).ToString(Formatting.None))));
        }
        public static bool KnownAbsent(string error) {
            return error!=null&&error.IndexOf("Raw Accel is not installed",StringComparison.OrdinalIgnoreCase)>=0;
        }
    }
}
