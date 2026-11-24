using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Threading;

namespace Callrift {
    // Unmodified, signed Proton AG WFP callout driver, GPL-3.0-or-later.
    // Pinned source/binary: ProtonVPN/win-app a4e78deae2e123ee0ac1c48cc69a66647c5228da.
    // Only our own demand-start service is managed. Never stop an existing Proton service.
    public sealed partial class IsolationDriver : IDisposable {
        public const string Sha256="ba788ffdc1bd95dd4aa0eccb1b43a5e512eae09fb2d8d623e7266bde65d79e00";
        public const string ServiceName="CallriftDiscordIsolation";
        IntPtr service,manager;
        bool verifiedService;
        Mutex ownership;
        bool owns;
        public static void VerifyFile(string path) {
            using(var input=File.OpenRead(path)) using(var hash=SHA256.Create()) {
                if(BitConverter.ToString(hash.ComputeHash(input)).Replace("-","").ToLowerInvariant()!=Sha256) throw new Exception("A integridade do driver de isolamento não confere.");
            }
        }
        public IsolationDriver(string source) {
            VerifyFile(source);
            try {
                ownership=new Mutex(false,"Global\\CallriftIsolationDriverOwnership");
                try { owns=ownership.WaitOne(0); } catch(AbandonedMutexException) { owns=true; }
                if(!owns) throw new Exception("Outra sessão do Callrift já controla o driver de isolamento.");
                // Program Files prevents a normal user from replacing the privileged service payload.
                string directory=Path.GetDirectoryName(InstalledPath);
                string driver=InstalledPath;
                Directory.CreateDirectory(directory);
                for(string p=directory;p!=Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);p=Path.GetDirectoryName(p))
                    if((File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0) throw new Exception("A pasta do driver não pode ser um link de diretório.");
                var acl=new DirectorySecurity(); acl.SetAccessRuleProtection(true,false);
                foreach(var sid in new[]{WellKnownSidType.BuiltinAdministratorsSid,WellKnownSidType.LocalSystemSid})
                    acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid,null),FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
                acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid,null),FileSystemRights.ReadAndExecute,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
                new DirectoryInfo(directory).SetAccessControl(acl);
                if(!File.Exists(driver)) File.Copy(source,driver,false);
                if((File.GetAttributes(driver)&FileAttributes.ReparsePoint)!=0) throw new Exception("O arquivo do driver não pode ser um link.");
                VerifyFile(driver);
                manager=OpenSCManager(null,null,3); Require(manager!=IntPtr.Zero,"abrir gerenciador de serviços");
                service=CreateService(manager,ServiceName,"Callrift: isolamento do Discord",0xF01FF,1,3,1,driver,null,IntPtr.Zero,null,null,null);
                if(service==IntPtr.Zero) {
                    int error=Marshal.GetLastWin32Error(); if(error!=1073) throw new Win32Exception(error,"Não foi possível registrar o driver do Callrift.");
                    service=OpenService(manager,ServiceName,0xF01FF); Require(service!=IntPtr.Zero,"abrir driver do Callrift");
                    VerifyService(service);
                }
                verifiedService=true;
                ServiceStatus status; Require(QueryServiceStatus(service,out status),"verificar estado do driver");
                // A crashed worker leaves its demand-start service running, but Windows
                // removes that worker's dynamic filters. Recover only our verified service,
                // after acquiring exclusive ownership; never touch another Proton service.
                if(status.State==4) return;
                if(status.State==3) {
                    for(int i=0;i<50 && status.State==3;i++) { Thread.Sleep(100); Require(QueryServiceStatus(service,out status),"aguardar encerramento do driver"); }
                }
                if(status.State!=1) throw new Exception("O driver de isolamento ainda está mudando de estado. Aguarde e tente novamente.");
                Require(StartService(service,0,IntPtr.Zero),"iniciar driver assinado do Callrift");
                for(int i=0;i<50;i++) { Require(QueryServiceStatus(service,out status),"aguardar driver"); if(status.State==4) return; if(status.State==1) throw new Win32Exception((int)status.ExitCode,"O Windows recusou o driver de isolamento."); Thread.Sleep(100); }
                throw new Exception("O driver de isolamento não iniciou a tempo.");
            } catch { Dispose(); throw; }
        }
        public void Dispose() {
            if(service!=IntPtr.Zero) {
                bool removed=false;
                try { if(verifiedService) { StopAndDeleteService(service); removed=true; } }
                catch { ConnectionJournal.Record("driver_cleanup_failed"); }
                CloseServiceHandle(service); service=IntPtr.Zero;
                if(removed) try { RemoveInstalledFile(); } catch { ConnectionJournal.Record("driver_file_cleanup_failed"); }
            }
            if(manager!=IntPtr.Zero) { CloseServiceHandle(manager); manager=IntPtr.Zero; }
            if(ownership!=null) { if(owns) ownership.ReleaseMutex(); ownership.Dispose(); ownership=null; owns=false; }
        }
        static void Require(bool ok,string action) { if(!ok) throw new Win32Exception(Marshal.GetLastWin32Error(),"Não foi possível "+action+"."); }
        [StructLayout(LayoutKind.Sequential)] struct ServiceStatus { public uint Type,State,Accepted,ExitCode,SpecificExitCode,Checkpoint,WaitHint; }
        [StructLayout(LayoutKind.Sequential)] struct ServiceConfig { public uint Type,Start,Error; public IntPtr BinaryPath,Group; public uint Tag; public IntPtr Dependencies,Account,Display; }
        [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr OpenSCManager(string machine,string database,uint access);
        [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr CreateService(IntPtr manager,string name,string display,uint access,uint type,uint start,uint error,string path,string group,IntPtr tag,string dependencies,string account,string password);
        [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr OpenService(IntPtr manager,string name,uint access);
        [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool QueryServiceConfig(IntPtr service,IntPtr config,uint size,out uint needed);
        [DllImport("advapi32.dll",SetLastError=true)] static extern bool QueryServiceStatus(IntPtr service,out ServiceStatus status);
        [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool StartService(IntPtr service,uint count,IntPtr args);
        [DllImport("advapi32.dll",SetLastError=true)] static extern bool ControlService(IntPtr service,uint control,out ServiceStatus status);
        [DllImport("advapi32.dll")] static extern bool CloseServiceHandle(IntPtr handle);
    }
}
