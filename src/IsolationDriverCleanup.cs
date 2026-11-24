using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using Microsoft.Win32;

namespace Callrift {
    public sealed partial class IsolationDriver {
        public static string InstalledPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Callrift Network","1.0.5.0","ProtonVPN.CalloutDriver.sys"); } }
        public static bool MatchesService(uint type,uint start,string registered) {
            if(registered!=null && registered.StartsWith(@"\??\",StringComparison.Ordinal)) registered=registered.Substring(4);
            return type==1 && start==3 && String.Equals(registered,InstalledPath,StringComparison.OrdinalIgnoreCase);
        }
        static void VerifyService(IntPtr handle) {
            uint needed; QueryServiceConfig(handle,IntPtr.Zero,0,out needed);
            if(needed<Marshal.SizeOf(typeof(ServiceConfig)) || needed>65536) throw new Exception("Configuração do serviço de isolamento inválida.");
            IntPtr config=Marshal.AllocHGlobal((int)needed);
            try {
                Require(QueryServiceConfig(handle,config,needed,out needed),"verificar serviço do driver");
                var info=(ServiceConfig)Marshal.PtrToStructure(config,typeof(ServiceConfig));
                if(!MatchesService(info.Type,info.Start,Marshal.PtrToStringUni(info.BinaryPath))) throw new Exception("Existe um serviço de isolamento com configuração diferente. Nada foi substituído.");
            } finally { Marshal.FreeHGlobal(config); }
        }
        static void StopAndDeleteService(IntPtr handle) {
            VerifyService(handle);
            ServiceStatus status; Require(QueryServiceStatus(handle,out status),"consultar driver");
            if(status.State==4) Require(ControlService(handle,1,out status),"encerrar driver");
            for(int i=0;i<50 && status.State!=1;i++) { Thread.Sleep(100); Require(QueryServiceStatus(handle,out status),"aguardar encerramento do driver"); }
            if(status.State!=1) throw new Exception("O driver ainda está em uso. Encerre o Callrift e tente novamente.");
            Require(DeleteService(handle),"remover serviço do driver");
        }
        static void VerifyInstalledFile() {
            string root=Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            for(string path=InstalledPath;path!=root;path=Path.GetDirectoryName(path))
                if((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0) throw new Exception("A limpeza não aceita links no caminho do driver.");
            if(File.Exists(InstalledPath)) VerifyFile(InstalledPath);
        }
        static void RemoveInstalledFile() {
            VerifyInstalledFile();
            if(File.Exists(InstalledPath)) File.Delete(InstalledPath);
            string version=Path.GetDirectoryName(InstalledPath),parent=Path.GetDirectoryName(version);
            foreach(string path in new[]{version,parent}) if(Directory.Exists(path) && Directory.GetFileSystemEntries(path).Length==0) Directory.Delete(path,false);
        }
        static bool Acquire(Mutex mutex) { try { return mutex.WaitOne(0); } catch(AbandonedMutexException) { return true; } }
        static bool ActiveSession() {
            try {
                using(var mutex=Mutex.OpenExisting("Global\\CallriftDiscordNetworkSession",MutexRights.Synchronize|MutexRights.Modify)) {
                    if(!Acquire(mutex)) return true;
                    mutex.ReleaseMutex(); return false;
                }
            } catch(WaitHandleCannotBeOpenedException) { return false; }
            catch(UnauthorizedAccessException) { return true; }
        }
        public static int CleanupUnused() {
            try {
                // Another installed/portable copy may still own the shared component.
                // It removes its own service on normal disconnect; never interrupt it.
                if(ActiveSession()) return 0;
                bool present=File.Exists(InstalledPath);
                using(var key=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\"+ServiceName)) present|=key!=null;
                if(!present) return 0;
                if(new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)) return CleanupWorker();
                using(var process=Process.Start(new ProcessStartInfo(Program.Executable,"--network-cleanup-worker") { UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden })) {
                    process.WaitForExit(); return process.ExitCode;
                }
            } catch { ConnectionJournal.Record("network_cleanup_failed"); return 1; }
        }
        public static int CleanupWorker() {
            if(!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)) return 1;
            using(var session=new Mutex(false,"Global\\CallriftDiscordNetworkSession")) {
                if(!Acquire(session)) return 0;
                try {
                    using(var driver=new Mutex(false,"Global\\CallriftIsolationDriverOwnership")) {
                        if(!Acquire(driver)) return 0;
                        try {
                            SecondaryTunnelRoutes.CheckAvailable();
                            VerifyInstalledFile();
                            IntPtr manager=OpenSCManager(null,null,1); Require(manager!=IntPtr.Zero,"consultar serviços");
                            try {
                                IntPtr service=OpenService(manager,ServiceName,0x10025); // QUERY_CONFIG | QUERY_STATUS | STOP | DELETE
                                if(service==IntPtr.Zero) { if(Marshal.GetLastWin32Error()!=1060) throw new Win32Exception(Marshal.GetLastWin32Error()); }
                                else try { StopAndDeleteService(service); } finally { CloseServiceHandle(service); }
                            } finally { CloseServiceHandle(manager); }
                            RemoveInstalledFile(); ConnectionJournal.Record("network_component_removed"); return 0;
                        } finally { driver.ReleaseMutex(); }
                    }
                } catch { ConnectionJournal.Record("network_cleanup_failed"); return 1; }
                finally { session.ReleaseMutex(); }
            }
        }
        [DllImport("advapi32.dll",SetLastError=true)] static extern bool DeleteService(IntPtr service);
    }
}
