using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;

namespace Callrift {
    // WFP classifies the executable in the kernel, before any traffic reaches the TUN.
    // This session is dynamic: Windows removes every filter if its owning worker exits.
    public sealed class AppNetworkIsolation : IDisposable {
        public const string AdapterName = "Callrift Discord";
        public const string IPv4 = "198.18.83.1";
        public const string IPv6 = "fd7a:ca11:81f7::1";
        public const int RouteMetric = 60000;
        IntPtr engine;
        readonly Guid sublayer = Guid.NewGuid();
        public int RedirectFilterCount { get; private set; }
        static readonly Guid AppId = new Guid("d78e1e87-8644-4ea5-9437-d809ecefc971");
        static readonly Guid LocalAddress = new Guid("d9ee00de-c1ef-4617-bfe3-ffd8f5a08957");
        static readonly Guid Protocol = new Guid("3971ef2b-623e-4f9a-8cb1-6e79b806b9a7");
        static readonly Guid[] Layers = {
            new Guid("c6e63c8c-b784-4562-aa7d-0a67cfcaf9a3"), // ALE_CONNECT_REDIRECT_V4
            new Guid("66978cad-c704-42ac-86ac-7c1a231bd253"), // ALE_BIND_REDIRECT_V4
            new Guid("587e54a7-8046-42ba-a0aa-b716250fc7fd"), // ALE_CONNECT_REDIRECT_V6
            new Guid("bef02c9c-606b-4536-8c26-1c2fc7b631d4")  // ALE_BIND_REDIRECT_V6
        };
        static readonly Guid[] Callouts = {
            new Guid("3c5a284f-af01-51fa-4361-6c6c50424144"),
            new Guid("10636af3-50d6-4f53-acb7-d5af33217fca"),
            new Guid("3c5a284f-af01-51fa-4361-6c6c50424145"),
            new Guid("10636af3-50d6-4f53-acb7-d5af33217faa")
        };
        static readonly Guid[] AuthLayers = {
            new Guid("c38d57d1-05a7-4c33-904f-7fbceee60e82"),
            new Guid("4a72393b-319f-44bc-84c3-ba54dcb3b6b4")
        };

        public AppNetworkIsolation(string[] executables) {
            if (IntPtr.Size != 8) throw new PlatformNotSupportedException("O isolamento requer Windows x64.");
            if (executables == null || executables.Length == 0 || executables.Any(String.IsNullOrWhiteSpace)) throw new ArgumentException("Nenhum executável foi selecionado.");
            // A separate test harness uses this API with its own executables. The elevated app
            // worker only calls it with paths independently discovered and verified as Discord.
            try {
                var session = new Session { Key=Guid.NewGuid(), Display=new Display("Callrift: isolamento do Discord"), Flags=1 };
                Check(FwpmEngineOpen0(null,10,IntPtr.Zero,ref session,out engine),"abrir sessão WFP");
                Check(FwpmTransactionBegin0(engine,0),"iniciar transação WFP");
                var layer = new Sublayer { Key=sublayer, Display=new Display("Callrift: somente aplicativos selecionados"), Weight=10001 };
                Check(FwpmSubLayerAdd0(engine,ref layer,IntPtr.Zero),"criar camada WFP");
                using (var memory = new NativeMemory()) {
                    Guid v4 = AddContext(IPAddress.Parse(IPv4),memory), v6 = AddContext(IPAddress.Parse(IPv6),memory);
                    for (int i=0;i<Layers.Length;i++) {
                        var callout = new Callout { Key=Callouts[i], Display=new Display("Callrift: redirecionamento por aplicativo"), Flags=0x20000, Layer=Layers[i] };
                        uint id;
                        Check(FwpmCalloutAdd0(engine,ref callout,IntPtr.Zero,out id),"registrar callout; confira se outra VPN Proton está ativa");
                        IntPtr existing;
                        Check(FwpmCalloutGetByKey0(engine,ref callout.Key,out existing),"verificar driver WFP");
                        try { if ((((Callout)Marshal.PtrToStructure(existing,typeof(Callout))).Flags & 0x40000)==0) throw new Exception("O driver de isolamento não está carregado. A VPN não foi ativada."); }
                        finally { FwpmFreeMemory0(ref existing); }
                    }
                    var appConditions = new List<Condition>();
                    foreach (string path in executables.Distinct(StringComparer.OrdinalIgnoreCase)) {
                        IntPtr blob;
                        Check(FwpmGetAppIdFromFileName0(Path.GetFullPath(path),out blob),"identificar executável no Windows");
                        try {
                            var original = (Blob)Marshal.PtrToStructure(blob,typeof(Blob));
                            byte[] bytes = new byte[original.Size]; Marshal.Copy(original.Data,bytes,0,bytes.Length);
                            var app = new Condition { Key=AppId, Value=new Value { Type=12, Pointer=memory.Struct(new Blob { Size=(uint)bytes.Length, Data=memory.Bytes(bytes) }) } };
                            appConditions.Add(app);
                            for (int i=0;i<Layers.Length;i++) {
                                // TCP connect and UDP bind; no rule for another executable or protocol.
                                var transport = new Condition { Key=Protocol, Value=new Value { Type=1, Number=(ulong)(i%2==0?6:17) } };
                                AddFilter(Layers[i],0x5003,Callouts[i],i<2?v4:v6,15,new[]{app,transport},memory);
                                RedirectFilterCount++;
                            }
                        } finally { FwpmFreeMemory0(ref blob); }
                    }
                    // Never let this secondary interface become a VPN fallback for other apps.
                    // Soft permits only skip our own lower-weight block; other firewall providers
                    // still get their normal decision. No CLEAR_ACTION_RIGHT is used.
                    for (int family=0;family<2;family++) {
                        byte[] bytes=IPAddress.Parse(family==0?IPv4:IPv6).GetAddressBytes();
                        var address=new Condition { Key=LocalAddress, Value=family==0
                            ? new Value { Type=3, Number=((ulong)bytes[0]<<24)|((ulong)bytes[1]<<16)|((ulong)bytes[2]<<8)|bytes[3] }
                            : new Value { Type=11, Pointer=memory.Bytes(bytes) } };
                        foreach(var app in appConditions) AddFilter(AuthLayers[family],0x1002,Guid.Empty,Guid.Empty,15,new[]{address,app},memory);
                        AddFilter(AuthLayers[family],0x1001,Guid.Empty,Guid.Empty,0,new[]{address},memory);
                    }
                }
                Check(FwpmTransactionCommit0(engine),"ativar regras de isolamento");
            } catch { Dispose(); throw; }
        }

        Guid AddContext(IPAddress address,NativeMemory memory) {
            // Proton CONNECT_REDIRECT_DATA: ADDRESS_FAMILY at 0, aligned address union at 4.
            byte[] data=new byte[20]; data[0]=(byte)(address.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork?2:23);
            var ip=address.GetAddressBytes(); Array.Copy(ip,0,data,4,ip.Length);
            var context=new Context { Key=Guid.NewGuid(), Display=new Display("Callrift: endereço privado do adaptador"), Type=8,
                Data=memory.Struct(new Blob { Size=(uint)data.Length,Data=memory.Bytes(data) }) };
            ulong id; Check(FwpmProviderContextAdd0(engine,ref context,IntPtr.Zero,out id),"definir destino do isolamento"); return context.Key;
        }
        void AddFilter(Guid layer,uint action,Guid callout,Guid context,byte weight,Condition[] conditions,NativeMemory memory) {
            var filter=new Filter { Key=Guid.NewGuid(), Display=new Display("Callrift: isolamento por executável"), Layer=layer, Sublayer=sublayer,
                Weight=new Value { Type=1, Number=weight }, Count=(uint)conditions.Length, Conditions=memory.Array(conditions),
                Action=new ActionData { Type=action, Key=callout }, Context=context, Flags=context==Guid.Empty?0U:4U };
            ulong id; Check(FwpmFilterAdd0(engine,ref filter,IntPtr.Zero,out id),"aplicar filtro WFP");
        }
        public void Dispose() { if(engine!=IntPtr.Zero) { FwpmEngineClose0(engine); engine=IntPtr.Zero; } }
        static void Check(uint error,string operation) { if(error!=0) throw new Exception("Não foi possível "+operation+" (0x"+error.ToString("X8")+")."); }

        // ABI definitions from the Windows SDK, fwpmtypes.h/fwptypes.h (x64).
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct Display {
            [MarshalAs(UnmanagedType.LPWStr)] public string Name;
            [MarshalAs(UnmanagedType.LPWStr)] public string Description;
            public Display(string name) { Name=name; Description=null; }
        }
        [StructLayout(LayoutKind.Sequential)] struct Blob { public uint Size; public IntPtr Data; }
        [StructLayout(LayoutKind.Sequential)] struct Session { public Guid Key; public Display Display; public uint Flags,Timeout,Process; public IntPtr Sid,Username; public int KernelMode; }
        [StructLayout(LayoutKind.Sequential)] struct Sublayer { public Guid Key; public Display Display; public uint Flags; public IntPtr Provider; public Blob Data; public ushort Weight; }
        [StructLayout(LayoutKind.Sequential)] struct Callout { public Guid Key; public Display Display; public uint Flags; public IntPtr Provider; public Blob Data; public Guid Layer; public uint Id; }
        [StructLayout(LayoutKind.Sequential)] struct Context { public Guid Key; public Display Display; public uint Flags; public IntPtr Provider; public Blob ProviderData; public uint Type; public IntPtr Data; public ulong Id; }
        [StructLayout(LayoutKind.Explicit,Size=16)] struct Value { [FieldOffset(0)] public uint Type; [FieldOffset(8)] public ulong Number; [FieldOffset(8)] public IntPtr Pointer; }
        [StructLayout(LayoutKind.Sequential)] struct Condition { public Guid Key; public uint Match; public Value Value; }
        [StructLayout(LayoutKind.Sequential)] struct ActionData { public uint Type; public Guid Key; }
        [StructLayout(LayoutKind.Sequential)] struct Filter {
            public Guid Key; public Display Display; public uint Flags; public IntPtr Provider; public Blob Data;
            public Guid Layer,Sublayer; public Value Weight; public uint Count; public IntPtr Conditions; public ActionData Action;
            // The native union also contains UINT64 and therefore has 8-byte alignment.
            public uint Padding; public Guid Context; public IntPtr Reserved; public ulong Id; public Value EffectiveWeight;
        }
        sealed class NativeMemory : IDisposable {
            readonly List<IntPtr> blocks=new List<IntPtr>();
            public IntPtr Alloc(int length) { var p=Marshal.AllocHGlobal(length); blocks.Add(p); return p; }
            public IntPtr Bytes(byte[] bytes) { var p=Alloc(bytes.Length); Marshal.Copy(bytes,0,p,bytes.Length); return p; }
            public IntPtr Struct<T>(T value) { var p=Alloc(Marshal.SizeOf(typeof(T))); Marshal.StructureToPtr(value,p,false); return p; }
            public IntPtr Array<T>(T[] values) { int size=Marshal.SizeOf(typeof(T)); var p=Alloc(size*values.Length); for(int i=0;i<values.Length;i++) Marshal.StructureToPtr(values[i],IntPtr.Add(p,i*size),false); return p; }
            public void Dispose() { foreach(var p in blocks) Marshal.FreeHGlobal(p); }
        }
        [DllImport("fwpuclnt.dll",CharSet=CharSet.Unicode)] static extern uint FwpmEngineOpen0(string server,uint auth,IntPtr identity,ref Session session,out IntPtr engine);
        [DllImport("fwpuclnt.dll")] static extern uint FwpmEngineClose0(IntPtr engine);
        [DllImport("fwpuclnt.dll")] static extern uint FwpmTransactionBegin0(IntPtr engine,uint flags);
        [DllImport("fwpuclnt.dll")] static extern uint FwpmTransactionCommit0(IntPtr engine);
        [DllImport("fwpuclnt.dll")] static extern uint FwpmSubLayerAdd0(IntPtr engine,ref Sublayer layer,IntPtr security);
        [DllImport("fwpuclnt.dll")] static extern uint FwpmCalloutAdd0(IntPtr engine,ref Callout callout,IntPtr security,out uint id);
        [DllImport("fwpuclnt.dll")] static extern uint FwpmCalloutGetByKey0(IntPtr engine,ref Guid key,out IntPtr callout);
        [DllImport("fwpuclnt.dll")] static extern uint FwpmProviderContextAdd0(IntPtr engine,ref Context context,IntPtr security,out ulong id);
        [DllImport("fwpuclnt.dll")] static extern uint FwpmFilterAdd0(IntPtr engine,ref Filter filter,IntPtr security,out ulong id);
        [DllImport("fwpuclnt.dll",CharSet=CharSet.Unicode)] static extern uint FwpmGetAppIdFromFileName0(string path,out IntPtr blob);
        [DllImport("fwpuclnt.dll")] static extern void FwpmFreeMemory0(ref IntPtr memory);
    }
}
