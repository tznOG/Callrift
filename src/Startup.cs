using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Principal;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml;
using System.Threading.Tasks;
namespace Callrift {
    public static class Startup {
        public static bool OwnsTask(string xml,string executable) {
            var document=new XmlDocument { XmlResolver=null };
            using(var reader=XmlReader.Create(new StringReader(xml),new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null })) document.Load(reader);
            var commands=document.SelectNodes("/*[local-name()='Task']/*[local-name()='Actions']/*[local-name()='Exec']/*[local-name()='Command']");
            return commands.Count==1 && String.Equals(Path.GetFullPath(commands[0].InnerText.Trim().Trim('"')),Path.GetFullPath(executable),StringComparison.OrdinalIgnoreCase);
        }
        public static int CleanupOwned() {
            object service=null,folder=null,task=null;
            try {
                service=Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));
                service.GetType().InvokeMember("Connect",BindingFlags.InvokeMethod,null,service,null);
                folder=service.GetType().InvokeMember("GetFolder",BindingFlags.InvokeMethod,null,service,new object[]{"\\"});
                try { task=folder.GetType().InvokeMember("GetTask",BindingFlags.InvokeMethod,null,folder,new object[]{"DiscordVPN-" + WindowsIdentity.GetCurrent().User.Value}); }
                catch(Exception e) {
                    while(e.InnerException!=null) e=e.InnerException;
                    if(e.HResult==unchecked((int)0x80070002)) return 0;
                    throw;
                }
                string xml=(string)task.GetType().InvokeMember("Xml",BindingFlags.GetProperty,null,task,null);
                if(!OwnsTask(xml,Program.Executable)) return 0;
                Set(false).GetAwaiter().GetResult();
                var settings=Settings.Load(); settings.StartWithWindows=false; settings.Save(); return 0;
            } catch { return 1; }
            finally { foreach(var com in new[]{task,folder,service}) if(com!=null && Marshal.IsComObject(com)) Marshal.FinalReleaseComObject(com); }
        }
        public static async Task Set(bool enabled) {
            var info = new ProcessStartInfo(Program.Executable, "--startup-worker " + (enabled ? "enable" : "disable")) { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden };
            if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)) info.Verb = "runas";
            using (var p = Process.Start(info)) {
                await Task.Run(() => p.WaitForExit()); if (p.ExitCode != 0) throw new Exception("Não foi possível alterar a inicialização do Windows.");
            }
        }
        public static int Worker(bool enable) {
            string sid = WindowsIdentity.GetCurrent().User.Value;
            string task = "DiscordVPN-" + sid;
            string temp = null;
            try {
                string arguments;
                if (enable) {
                    temp = Path.Combine(Path.GetTempPath(), "discordvpn-task-" + Guid.NewGuid().ToString("N") + ".xml");
                    string xml = "<?xml version=\"1.0\" encoding=\"UTF-16\"?><Task version=\"1.4\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\"><RegistrationInfo><Description>Callrift: iniciar e conectar o último perfil ao entrar no Windows.</Description></RegistrationInfo><Triggers><LogonTrigger><Enabled>true</Enabled><UserId>" + sid + "</UserId><Delay>PT15S</Delay></LogonTrigger></Triggers><Principals><Principal id=\"User\"><UserId>" + sid + "</UserId><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal></Principals><Settings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy><DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries><StopIfGoingOnBatteries>false</StopIfGoingOnBatteries><ExecutionTimeLimit>PT0S</ExecutionTimeLimit><StartWhenAvailable>true</StartWhenAvailable></Settings><Actions Context=\"User\"><Exec><Command>" + SecurityElement.Escape(Program.Executable) + "</Command><Arguments>--startup</Arguments><WorkingDirectory>" + SecurityElement.Escape(AppDomain.CurrentDomain.BaseDirectory) + "</WorkingDirectory></Exec></Actions></Task>";
                    File.WriteAllText(temp, xml, System.Text.Encoding.Unicode);
                    arguments = "/Create /TN \"" + task + "\" /XML \"" + temp + "\" /F";
                } else arguments = "/Delete /TN \"" + task + "\" /F";
                using (var p = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "schtasks.exe"), arguments) { UseShellExecute=false, CreateNoWindow=true, RedirectStandardOutput=true, RedirectStandardError=true })) { p.WaitForExit(); return p.ExitCode; }
            } catch { return 1; }
            finally { if (temp != null) try { File.Delete(temp); } catch { } }
        }
    }
}
