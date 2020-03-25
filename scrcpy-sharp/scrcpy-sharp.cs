using Fleck;
using NLog;
using SharpAdbClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SharpScrcpy
{
  public class UsbDeviceEventArgs
  {
    public UsbDeviceEventArgs(DeviceData device)
    {
      this.device = device;
    }
    public String GetSerial()
    {
      return device.Serial;
    }
    public bool IsConnected()
    {
      return device.State == DeviceState.Online;
    }
    private DeviceData device {
      get;
      set;
    }
    public Size screen_size {
      get;
      set;
    }
    public double display_zoom_rate {
      get;
      set;
    }
  };
  public struct QueCmd
  {
    public enum QCType
    {
      QC_IMAGE_DATA,
      QC_CLINET_OPEN,
      QC_CLINET_CLOSE
    }
    public QCType cmd_type
    {
      set;
      get;
    }
    public object data
    {
      set;
      get;
    }
  }

  struct DeviceScreenInfo {
    public Size screen_size {
      get;
      set;
    }
  }

  public class ScrcpySrvSharp : QueueHelper<QueCmd>
  {
    public event EventHandler<UsbDeviceEventArgs> OnDeviceConnected;
    public event EventHandler<UsbDeviceEventArgs> OnDeviceDisconnected;
    public event EventHandler<Image> OnImageArrived;
    Logger log_ = LogManager.GetCurrentClassLogger();
    WebSocketServer websocket_server_;
    List<IWebSocketConnection> client_sockets = new List<IWebSocketConnection>();

    AdbServer adb_server_;
    string adb_path_;
    string adb_folder_path_ ;
    string adb_svr_path_;
    public int Display_height { get; set; } = 800;
    static ScrcpySrvSharp()
    {
      scrcpy_sharp_ = new ScrcpySrvSharp();
      scrcpy_sharp_.adb_folder_path_ = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
      scrcpy_sharp_.adb_folder_path_ += @"\adb\";
      scrcpy_sharp_.adb_path_ = scrcpy_sharp_.adb_folder_path_+ @"adb.exe";
      scrcpy_sharp_.adb_svr_path_ = scrcpy_sharp_.adb_folder_path_ + @"scrcpy-server";
    }
    private static ScrcpySrvSharp scrcpy_sharp_ = null;
    public static ScrcpySrvSharp Get()
    {
      return scrcpy_sharp_;
    }

    public bool RunServer(int websocketport  = 0)
    {
      adb_server_ = new AdbServer();
      try
      {
        var ret = adb_server_.StartServer(adb_path_, true);
        if (ret != StartServerResult.Started)
        {
          adb_server_.RestartServer();
        }
        var monitor = new DeviceMonitor(new AdbSocket(new IPEndPoint(IPAddress.Loopback, AdbClient.AdbServerPort)));
        monitor.DeviceConnected += this.OnDeviceConnectedNotify;
        monitor.DeviceDisconnected += this.OnDeviceDisconnectedNotify;
        monitor.DeviceChanged += this.OnDeviceChangedNotify;
        monitor.Start();
      }
      catch (Exception e)
      {
        log_.Error($"RunServer {e.ToString()}");
        adb_server_ = null;
        return false;
      }
      finally
      {

      }
      RunWebSocket(websocketport);
      return true;
    }

    public void Close()
    {

      CloseScrcpy(-1);
      websocket_server_.Dispose();
      Close();
    }

    public String ExcuteAdbCmd(String cmd, int timeout = 5000)
    {
        return ExcuteCmd($"\"{adb_path_}\" {cmd}", timeout);
 
    }

    public String ExcuteAdbShellCmd(String cmd, String dev = null, int timeout = 5000)
    {
      if (null != dev && 0 != dev.Length)
      {
        return ExcuteCmd($"\"{adb_path_}\" -s {dev} shell \" {cmd}\"", timeout);
      }
      else
      {
        return ExcuteCmd($"\"{adb_path_}\" shell \" {cmd}\"", timeout);
      }
    }

    private void TryNotifyDeviceConnected(DeviceData dev)
    {
      if (dev.State != DeviceState.Online)
      {
        return;
      }
      Task<int> task = new Task<int>(() =>
      {
        var ret = ExcuteAdbShellCmd("dumpsys window displays |head -n 3", dev.Serial);
        log_.Debug($"OnDeviceChanged {ret}");
        Regex regex = new Regex(@"init=(\d+)x(\d+)");
        var m = regex.Match(ret);
        if(m.Success)
        {
          Size sz = new Size();
          sz.Width = Convert.ToInt32(m.Groups[1].Value);
          sz.Height = Convert.ToInt32(m.Groups[2].Value);
          double rate = sz.Height * 1.0 / Display_height;
          var e2 = new UsbDeviceEventArgs(dev);
          e2.screen_size = sz;
          e2.display_zoom_rate = rate;
          OnDeviceConnected?.Invoke(this, e2);
        }

        return 1;
      }) ;
      task.Start();
    }
    private void OnDeviceConnectedNotify(object sender, DeviceDataEventArgs e)
    {
      log_.Debug($"OnDeviceConnected {e.Device.Name} {e.Device.State}");
      TryNotifyDeviceConnected(e.Device);
     // OnDeviceConnected?.Invoke(this, new UsbDeviceEventArgs(e.Device));
    }

    private void OnDeviceDisconnectedNotify(object sender, DeviceDataEventArgs e)
    {
      log_.Debug($"OnDeviceDisconnected {e.Device.Name}");
      OnDeviceDisconnected?.Invoke(this, new UsbDeviceEventArgs(e.Device));
    }
    private void OnDeviceChangedNotify(object sender, DeviceDataEventArgs e)
    {
      log_.Debug($"OnDeviceChanged {e.Device.Name} {e.Device.State}");
      TryNotifyDeviceConnected(e.Device);
     // OnDeviceChanged?.Invoke(this, new UsbDeviceEventArgs(e.Device));
    }

    private string ExcuteCmd(string cmd, int time_out = 1000)
    {
      cmd = cmd.Trim() + "&exit";
      Process p = new Process();
      p.StartInfo.FileName = "cmd.exe";
      p.StartInfo.UseShellExecute = false;
      p.StartInfo.RedirectStandardInput = true;
      p.StartInfo.RedirectStandardOutput = true;
      p.StartInfo.RedirectStandardError = true;
      p.StartInfo.CreateNoWindow = true;
      p.Start();
      p.StandardInput.WriteLine(cmd);
      p.StandardInput.AutoFlush = true;
      StreamReader reader = p.StandardOutput;
      StreamReader error = p.StandardError;
      string output = reader.ReadToEnd() + error.ReadToEnd();

      int index = output.IndexOf(cmd) + cmd.Length;
      output = output.Substring(index, output.Length - index);
      p.WaitForExit(time_out);
      p.Close();
      return output.Trim();
    }

    bool RunWebSocket(int port)
    {
      if(0 == port)
      {
        return false;
      }
      websocket_server_ = new WebSocketServer($"ws://0.0.0.0:{port}");
      websocket_server_.Start(OnWebSocketAction);
      return true;
    }

    IntPtr[] GetCommand(String cmdLine)
    {
      var m = cmdLine.Split(' ');
      IntPtr[] cmd = new IntPtr[m.Length + 1];
      cmd[0] = Marshal.StringToHGlobalAnsi($"\"{System.Reflection.Assembly.GetExecutingAssembly().Location}\"");
      for (int i = 0; i < m.Length; ++i)
      {
        cmd[i + 1] = Marshal.StringToHGlobalAnsi(m[i]);
      }
      return cmd;
    }

    public void StartScrcpyServer(UsbDeviceEventArgs dev)
    {
      Task<int> task = new Task<int>(() =>
      {
        try
        {
          OnVideoDataArrived cb = OnMirrorImageArrive;
          RegistVideoCB(cb);
          GC.KeepAlive(cb);
          OnScrcpyLog cb2 = OnScrcpyLog1;
          RegistScrcpyLogCB(cb2);
          GC.KeepAlive(cb);
          SetADBFolderPath(Marshal.StringToHGlobalAnsi(adb_path_)
                           , Marshal.StringToHGlobalAnsi(adb_svr_path_));
          IntPtr[] sbs = GetCommand($"-s {dev.GetSerial()} -m {Display_height} -b 2M");
          var ret = RunScrcpy(sbs.Length, sbs);
         
          return ret;
        }
        catch(Exception e)
        {
          log_.Error($"StartScrcpyServer {e.ToString()}");
        }
        finally
        {
          OnDeviceDisconnected?.Invoke(this, dev);
          if (!adb_server_.GetStatus().IsRunning)
          {
            adb_server_.RestartServer();
          }
        }
        return -1;
      });
      task.Start();
    }

    unsafe int OnMirrorImageArrive(IntPtr buff, int num)
    {
      byte[] buff2 = new byte[num];
      Marshal.Copy(buff, buff2, 0, num);
      AddAndRun(QueCmd.QCType.QC_IMAGE_DATA, buff2);
      return 1;
    }
    unsafe int OnScrcpyLog1(int category, int priority, IntPtr message)
    {
      log_.Debug($"ScrcpyLog=> {Marshal.PtrToStringAnsi(message)}");
      return 1;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate int OnVideoDataArrived(IntPtr buff, int num);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate int OnScrcpyLog(int category, int priority, IntPtr message);

    [DllImport(@".\scrcpy-vs-dll.dll", EntryPoint = "RunScrcpy", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = false, CallingConvention = CallingConvention.StdCall)]
    extern static unsafe int RunScrcpy(int n, IntPtr[] str);

    [DllImport(@".\scrcpy-vs-dll.dll", EntryPoint = "RegistVideoCB", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = false, CallingConvention = CallingConvention.StdCall)]
    extern static unsafe int RegistVideoCB(OnVideoDataArrived funcVideoDataCB);

    [DllImport(@".\scrcpy-vs-dll.dll", EntryPoint = "CloseScrcpy", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = false, CallingConvention = CallingConvention.StdCall)]
    extern static unsafe void CloseScrcpy(int wait_exist);

    [DllImport(@".\scrcpy-vs-dll.dll", EntryPoint = "SetADBFolderPath", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = false, CallingConvention = CallingConvention.StdCall)]
    extern static unsafe void SetADBFolderPath(IntPtr adb_path, IntPtr srv_path);

    [DllImport(@".\scrcpy-vs-dll.dll", EntryPoint = "RegistScrcpyLogCB", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = false, CallingConvention = CallingConvention.StdCall)]
    extern static unsafe void RegistScrcpyLogCB(OnScrcpyLog funcScrcpyLogCB);

    void OnWebSocketAction(IWebSocketConnection socket)
    {
      socket.OnOpen = () =>
      {
        AddAndRun(QueCmd.QCType.QC_CLINET_OPEN, socket);
      };
      socket.OnClose = () =>
      {
        AddAndRun(QueCmd.QCType.QC_CLINET_CLOSE, socket);
      };
      socket.OnMessage = message =>
      {
        log_.Debug("OnWebSocketAction OnMessage");
        //allSockets.ToList().ForEach(s => s.Send("Echo: " + message));
      };

    }

    void AddAndRun(QueCmd.QCType type, Object obj)
    {
      QueCmd cmd = new QueCmd();
      cmd.cmd_type = type;
      cmd.data = obj;
      Add(cmd);
      Resume();
    }


    protected override void Execute(QueCmd cmd)
    {
      switch (cmd.cmd_type)
      {
      case QueCmd.QCType.QC_IMAGE_DATA:
      {
        var buff = cmd.data as byte[];
        MemoryStream buf = new MemoryStream(buff);
        Image image = Image.FromStream(buf, true);
        OnImageArrived?.Invoke(this,image);
        string data2 = "data:image/jpg;base64," + Convert.ToBase64String(buff);
        client_sockets.ForEach(s => s.Send(data2));
      }
      break;
      case QueCmd.QCType.QC_CLINET_OPEN:
      {
        log_.Debug("WebSocket Open");
        client_sockets.Add(cmd.data as IWebSocketConnection);
      }
      break;
      case QueCmd.QCType.QC_CLINET_CLOSE:
      {
        log_.Debug("WebSocket Close");
        client_sockets.Remove(cmd.data as IWebSocketConnection);
      }
      break;
      default:
        break;
      }
    }
  }
}
