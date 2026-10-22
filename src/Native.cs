using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace VectorFit {
    static class Native {
        [StructLayout(LayoutKind.Sequential)] internal struct RawDevice { public ushort Page,Usage;public uint Flags;public IntPtr Window; }
        [StructLayout(LayoutKind.Sequential)] internal struct DeviceList { public IntPtr Handle;public uint Type; }
        [DllImport("user32.dll",SetLastError=true)] static extern bool RegisterRawInputDevices(RawDevice[] devices,uint count,uint size);
        [DllImport("user32.dll",SetLastError=true)] static extern uint GetRawInputData(IntPtr raw,uint command,IntPtr data,ref uint size,uint headerSize);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern uint GetRawInputDeviceInfo(IntPtr device,uint command,StringBuilder data,ref uint size);
        [DllImport("user32.dll")] static extern uint GetRawInputDeviceList([In,Out] DeviceList[] list,ref uint count,uint size);
        [DllImport("user32.dll")] internal static extern bool SetProcessDPIAware();
        public static void Register(IntPtr hwnd,bool capture) {
            var a=new[]{new RawDevice{Page=1,Usage=2,Flags=capture?0x30u:1u,Window=capture?hwnd:IntPtr.Zero}};
            if(!RegisterRawInputDevices(a,1,(uint)Marshal.SizeOf(typeof(RawDevice))))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        public static string Name(IntPtr handle) {
            uint n=0;GetRawInputDeviceInfo(handle,0x20000007,null,ref n);
            if(n==0||n>4096)return "Ukjent mus";
            var s=new StringBuilder((int)n+1);GetRawInputDeviceInfo(handle,0x20000007,s,ref n);return s.ToString();
        }
        public static int MouseCount() {
            uint n=0;uint size=(uint)Marshal.SizeOf(typeof(DeviceList));
            if(GetRawInputDeviceList(null,ref n,size)==UInt32.MaxValue)return 0;
            var a=new DeviceList[n];GetRawInputDeviceList(a,ref n,size);int count=0;
            foreach(var v in a)if(v.Type==0)count++;return count;
        }
        public static bool Read(IntPtr handle,out IntPtr device,out int dx,out int dy,out ushort buttons,out bool absolute) {
            uint n=0,header=(uint)(IntPtr.Size==8?24:16);device=IntPtr.Zero;dx=dy=0;buttons=0;absolute=false;
            if(GetRawInputData(handle,0x10000003,IntPtr.Zero,ref n,header)==UInt32.MaxValue||n<header+24||n>4096)return false;
            IntPtr p=Marshal.AllocHGlobal((int)n);
            try {
                if(GetRawInputData(handle,0x10000003,p,ref n,header)==UInt32.MaxValue)return false;
                if(Marshal.ReadInt32(p,0)!=0)return false;
                device=Marshal.ReadIntPtr(p,8);int h=(int)header;
                absolute=(Marshal.ReadInt16(p,h)&1)!=0;
                buttons=(ushort)Marshal.ReadInt16(p,h+4);dx=Marshal.ReadInt32(p,h+12);dy=Marshal.ReadInt32(p,h+16);return true;
            } finally { Marshal.FreeHGlobal(p); }
        }
    }
}
