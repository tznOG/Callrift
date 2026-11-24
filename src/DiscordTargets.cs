using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace Callrift {
    public static class DiscordTargets {
        public static string[] Discover() {
            var found=new List<string>();
            string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            foreach(string channel in DiscordSession.Channels) {
                string folder=Path.Combine(local,channel);
                if(!Directory.Exists(folder)) continue;
                foreach(string version in Directory.GetDirectories(folder,"app-*")) {
                    string path=Path.Combine(version,channel+".exe");
                    if(File.Exists(path)) found.Add(Path.GetFullPath(path));
                }
            }
            return Validate(found.ToArray());
        }
        public static string[] Validate(string[] paths) {
            if(paths==null || paths.Length==0 || paths.Length>64) throw new Exception("Instale o Discord desktop antes de conectar a VPN.");
            var result=new List<string>();
            foreach(string path in paths) {
                if(String.IsNullOrEmpty(path) || path.Length>1000 || !Path.IsPathRooted(path)) throw new Exception("Caminho do Discord inválido.");
                string full=Path.GetFullPath(path), name=Path.GetFileNameWithoutExtension(full);
                if(!DiscordSession.Channels.Contains(name) || !String.Equals(Path.GetExtension(full),".exe",StringComparison.OrdinalIgnoreCase)) throw new Exception("Somente os executáveis oficiais do Discord podem usar a VPN.");
                if(!File.Exists(full) || !Authenticode.IsDiscord(full)) throw new Exception("A assinatura do executável do Discord não pôde ser validada. Atualize ou reinstale o cliente oficial.");
                result.Add(full);
            }
            return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }
    static class Authenticode {
        public static bool IsDiscord(string path) {
            IntPtr file=IntPtr.Zero; var trust=new TrustData();
            try {
                var info=new FileInfoData { Size=(uint)Marshal.SizeOf(typeof(FileInfoData)),Path=path };
                file=Marshal.AllocHGlobal(Marshal.SizeOf(info)); Marshal.StructureToPtr(info,file,false);
                trust=new TrustData { Size=(uint)Marshal.SizeOf(typeof(TrustData)),UI=2,Choice=1,File=file,StateAction=1,Flags=0x1010 };
                var action=new Guid("00aac56b-cd44-11d0-8cc2-00c04fc295ee");
                if(WinVerifyTrust(new IntPtr(-1),ref action,ref trust)!=0) return false;
                using(var certificate=new X509Certificate2(X509Certificate.CreateFromSignedFile(path)))
                    return certificate.GetNameInfo(X509NameType.SimpleName,false)=="Discord Inc.";
            } catch { return false; }
            finally {
                if(trust.State!=IntPtr.Zero) { var action=new Guid("00aac56b-cd44-11d0-8cc2-00c04fc295ee"); trust.StateAction=2; WinVerifyTrust(new IntPtr(-1),ref action,ref trust); }
                if(file!=IntPtr.Zero) { Marshal.DestroyStructure(file,typeof(FileInfoData)); Marshal.FreeHGlobal(file); }
            }
        }
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct FileInfoData { public uint Size; [MarshalAs(UnmanagedType.LPWStr)] public string Path; public IntPtr Handle,Subject; }
        [StructLayout(LayoutKind.Sequential)] struct TrustData { public uint Size; public IntPtr Policy,Sip; public uint UI,Revocation,Choice; public IntPtr File; public uint StateAction; public IntPtr State,Url; public uint Flags,Context; public IntPtr Signature; }
        [DllImport("wintrust.dll",ExactSpelling=true,PreserveSig=true)] static extern int WinVerifyTrust(IntPtr window,ref Guid action,ref TrustData data);
    }
}
