using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;

namespace Callrift {
    // Only low-priority routes on the new interface. No other adapter, DNS, metric,
    // weak-host setting or pre-existing route is modified.
    public sealed class SecondaryTunnelRoutes : IDisposable {
        readonly List<Route> owned=new List<Route>();
        public int InterfaceIndex { get; private set; }
        public static void CheckAvailable() {
            foreach(var nic in NetworkInterface.GetAllNetworkInterfaces()) {
                if(nic.Name==AppNetworkIsolation.AdapterName || nic.GetIPProperties().UnicastAddresses.Any(a=>a.Address.ToString()==AppNetworkIsolation.IPv4 || a.Address.ToString()==AppNetworkIsolation.IPv6))
                    throw new Exception("Já existe um adaptador ou endereço reservado para o Callrift. Encerre a sessão anterior antes de conectar.");
            }
        }
        public SecondaryTunnelRoutes() {
            NetworkInterface target=null;
            for(int i=0;i<60;i++) {
                target=NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n=>n.Name==AppNetworkIsolation.AdapterName && n.GetIPProperties().UnicastAddresses.Any(a=>a.Address.ToString()==AppNetworkIsolation.IPv4));
                if(target!=null) break; Thread.Sleep(100);
            }
            if(target==null) throw new Exception("O adaptador privado do Discord não foi encontrado.");
            InterfaceIndex=target.GetIPProperties().GetIPv4Properties().Index;
            try { ConfigureHost(2); ConfigureHost(23); Add(2); Add(23); } catch { Dispose(); throw; }
        }
        void ConfigureHost(ushort family) {
            // sing-tun enables IPv4 forwarding even with auto_route=false. Forwarding
            // defeats Windows' strong-host source/interface affinity. Change ONLY the
            // freshly created private adapter, before publishing its secondary routes.
            var row=new InterfaceRow { Family=family,Index=(uint)InterfaceIndex };
            uint error=GetIpInterfaceEntry(ref row);
            if(error!=0) throw new System.ComponentModel.Win32Exception((int)error,"Falha ao consultar a interface privada.");
            row.Forwarding=0; row.WeakHostSend=0; row.WeakHostReceive=0;
            if(family==2) row.SitePrefixLength=0;
            error=SetIpInterfaceEntry(ref row);
            if(error!=0) throw new System.ComponentModel.Win32Exception((int)error,"Falha ao restringir a interface privada.");
            error=GetIpInterfaceEntry(ref row);
            if(error!=0 || row.Forwarding!=0 || row.WeakHostSend!=0 || row.WeakHostReceive!=0 || row.DisableDefaultRoutes!=0)
                throw new Exception("O Windows não confirmou o isolamento da interface privada.");
        }
        void Add(ushort family) {
            var row=new Route(); InitializeIpForwardEntry(ref row);
            row.Index=(uint)InterfaceIndex;
            row.Destination=new Prefix { Address=new SocketAddress { Family=family }, Length=0 };
            // Match sing-tun's Windows gateway calculation. An unspecified next hop
            // makes Windows attempt on-link resolution of arbitrary internet hosts.
            byte[] gateway=System.Net.IPAddress.Parse(family==2?AppNetworkIsolation.IPv4:AppNetworkIsolation.IPv6).GetAddressBytes();
            gateway[gateway.Length-1]++;
            row.NextHop=family==2
                ? new SocketAddress { Family=family,IPv4Address=BitConverter.ToUInt32(gateway,0) }
                : new SocketAddress { Family=family,IPv6A=BitConverter.ToUInt32(gateway,0),IPv6B=BitConverter.ToUInt32(gateway,4),IPv6C=BitConverter.ToUInt32(gateway,8),IPv6D=BitConverter.ToUInt32(gateway,12) };
            row.Metric=AppNetworkIsolation.RouteMetric; row.Protocol=3;
            uint error=CreateIpForwardEntry2(ref row);
            if(error!=0) throw new System.ComponentModel.Win32Exception((int)error,"Não foi possível criar a rota secundária do Discord ("+error+").");
            owned.Add(row);
        }
        public void Dispose() { foreach(var value in owned) { var row=value; DeleteIpForwardEntry2(ref row); } owned.Clear(); }
        [StructLayout(LayoutKind.Explicit,Size=28)] struct SocketAddress { [FieldOffset(0)] public ushort Family; [FieldOffset(4)] public uint IPv4Address; [FieldOffset(8)] public uint IPv6A; [FieldOffset(12)] public uint IPv6B; [FieldOffset(16)] public uint IPv6C; [FieldOffset(20)] public uint IPv6D; }
        [StructLayout(LayoutKind.Sequential)] struct Prefix { public SocketAddress Address; public byte Length; }
        [StructLayout(LayoutKind.Sequential)] struct Route {
            public ulong Luid; public uint Index; public Prefix Destination; public SocketAddress NextHop;
            public byte SitePrefix; public uint ValidLifetime,PreferredLifetime,Metric,Protocol;
            public byte Loopback,Autoconfigure,Publish,Immortal; public uint Age,Origin;
        }
        // MIB_IPINTERFACE_ROW, Windows SDK x64. Unused bytes are retained by the
        // blittable struct so Get/Set preserve every other interface property.
        [StructLayout(LayoutKind.Explicit,Size=168)] struct InterfaceRow {
            [FieldOffset(0)] public ushort Family;
            [FieldOffset(8)] public ulong Luid;
            [FieldOffset(16)] public uint Index;
            [FieldOffset(41)] public byte Forwarding;
            [FieldOffset(42)] public byte WeakHostSend;
            [FieldOffset(43)] public byte WeakHostReceive;
            [FieldOffset(144)] public uint SitePrefixLength;
            [FieldOffset(166)] public byte DisableDefaultRoutes;
        }
        [DllImport("iphlpapi.dll")] static extern uint GetIpInterfaceEntry(ref InterfaceRow row);
        [DllImport("iphlpapi.dll")] static extern uint SetIpInterfaceEntry(ref InterfaceRow row);
        [DllImport("iphlpapi.dll")] static extern void InitializeIpForwardEntry(ref Route row);
        [DllImport("iphlpapi.dll")] static extern uint CreateIpForwardEntry2(ref Route row);
        [DllImport("iphlpapi.dll")] static extern uint DeleteIpForwardEntry2(ref Route row);
    }
}
