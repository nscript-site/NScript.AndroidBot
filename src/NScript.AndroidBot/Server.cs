using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NScript.AndroidBot
{
    public class Server
    {
        private static HashSet<UInt16> PortsUsed { get; set; } = new HashSet<ushort>();

        public static bool IsPortUsed(UInt16 port)
        {
            lock (PortsUsed)
                return PortsUsed.Contains(port);
        }

        public static void SetPortUsed(UInt16 port)
        {
            lock (PortsUsed)
                PortsUsed.Add(port);
        }

        public static void SetPortUnused(UInt16 port)
        {
            lock (PortsUsed)
                PortsUsed.Remove(port);
        }

        public const String SCRCPY_SOCKET_NAME = "scrcpy";
        public const String SCRCPY_SERVER_FILENAME = "scrcpy-server";
        public const String SCRCPY_DEVICE_SERVER_PATH = "/data/local/tmp/scrcpy-server.jar";
        public const String DEFAULT_SERVER_PATH = "/share/scrcpy/" + SCRCPY_SERVER_FILENAME;
        public const String SCRCPY_VERSION = "1.18";

        public const String SNDCPY_SOCKET_NAME = "sndcpy";
        public const String SNDCPY_LOCAL_PATH = "./tools/sndcpy.apk";
        public const String SNDCPY_DEVICE_SERVER_PATH = "/data/local/tmp/sndcpy.adk";

        static readonly IPAddress IPV4_LOCALHOST = IPAddress.Parse("127.0.0.1");
        const int DEVICE_NAME_FIELD_LENGTH = 64;

        public String Serial { get; set; }
        public Object syncRoot = new object();

        public ProcessSession ScrcpyServer;
        public Socket VideoSocket;
        public Socket ControlSocket;
        public UInt16 LocalScrcpyServerPort; // selected from port_range

        public ProcessSession SndcpyServer;
        public Socket AudioSocket;
        public UInt16 LocalSndcpyServerPort = 28200;

        public String DeviceName { get; private set; }

        public System.Drawing.Size FrameSize { get; private set; }

        public Action<String> OnMsg { get; set; }

        public Action OnVideoSocketConnected { get; set; }
        public Action OnAudioSocketConnected { get; set; }
        public Action OnControlSocketConnected { get; set; }

        private AtxAgentServer AtxAgent { get; set; }

        public string GetScrcpyServerDeviceFileName()
        {
            return SCRCPY_SERVER_FILENAME;
        }

        public bool PushScrcpyServerToDevice()
        {
            String deviceFilePath = GetScrcpyServerDeviceFileName();
            if (deviceFilePath == null)
            {
                return false;
            }

            ProcessSession process = AdbUtils.adb_push(Serial, deviceFilePath, SCRCPY_DEVICE_SERVER_PATH);
            process.OnMsg = process.OnErr = this.OnMsg;
            return AdbUtils.process_check_success(process, "adb push", true);
        }

        public bool DisableTunnelForward(String serial, UInt16 local_port)
        {
            SetPortUnused(local_port);
            ProcessSession process = AdbUtils.adb_forward_remove(serial, local_port);
            process.OnMsg = process.OnErr = this.OnMsg;
            return AdbUtils.process_check_success(process, "adb forward --remove", true);
        }

        public bool EnableTunnelForward(String serial, UInt16 localPort, String remoteSocketName)
        {
            if (IsPortUsed(localPort)) return false;
            ProcessSession process = AdbUtils.adb_forward(serial, localPort, remoteSocketName);
            process.OnMsg = process.OnErr = this.OnMsg;
            bool rtn = AdbUtils.process_check_success(process, "adb forward", true);
            SetPortUsed(localPort);
            return rtn;
        }

        public bool EnableTunnelForward(String serial, UInt16 localPort, UInt16 remotePort)
        {
            if (IsPortUsed(localPort)) return false;
            ProcessSession process = AdbUtils.adb_forward(serial, localPort, remotePort);
            process.OnMsg = process.OnErr = this.OnMsg;
            bool rtn = AdbUtils.process_check_success(process, "adb forward", true);
            SetPortUsed(localPort);
            return rtn;
        }

        public void LOGE(String msg)
        {
            OnMsg?.Invoke(msg);
        }

        public void LOGW(String msg)
        {
            OnMsg?.Invoke(msg);
        }

        public void LOGD(String msg)
        {
            OnMsg?.Invoke(msg);
        }

        /// <summary>
        /// 将 UI 界面 dump 出来。结果为 xml 布局文件。
        /// </summary>
        /// <returns></returns>
        public String DumpUI()
        {
            String ui = null;
            try
            {
                ui = this.AtxAgent.DumpUI();
            }
            catch(WebException ex)
            {
                // 再重联一次
                this.ConnectAtxAgent();
                ui = this.AtxAgent.DumpUI();
            }
            return ui;
        }

        internal static bool IsConnected(Socket socket)
        {
            if (socket == null) return false;
            if (socket.Poll(-1, SelectMode.SelectError) == false) return true;
            //if (socket.Poll(-1, SelectMode.SelectRead) || socket.Available > 0) return true;
            else return false;
        }

        private void RunDaemon()
        {
            Task.Run(() => {
                while(true)
                {
                    System.Threading.Thread.Sleep(1000);

                    bool isVideoSocketConnected = IsConnected(VideoSocket);
                    bool isControlSocketConnected = IsConnected(ControlSocket);
                    bool isAudioSocketConnected = IsConnected(AudioSocket);

                    if(isVideoSocketConnected == false || isControlSocketConnected == false)
                    {
                        OnMsg?.Invoke($"[{Serial}][Daemon]: VideoSocket-{isVideoSocketConnected},ControlSocket-{isControlSocketConnected}");
                        ConnectScrcpy();
                    }

                    if(isAudioSocketConnected == false)
                    {
                        OnMsg?.Invoke($"[{Serial}][Daemon]: AudioSocket-{isAudioSocketConnected},ControlSocket-{isControlSocketConnected}");
                        ConnectSndcpy();
                    }
                }
            });
        }

        public void CreateAtxAgentServer(UInt16 portStart)
        {
            for(UInt16 port = portStart; port < 62300; port ++)
            {
                if (EnableTunnelForward(this.Serial, port, 7912))  // Remote Port: 7912
                {
                    AtxAgent = new AtxAgentServer(port);
                    return;
                }
            }

            LOGE($"CreateAtxAgentServer failed, could not forward UIAutomator port");
        }

        public bool EnableScrcpyForward(UInt16 portStart)
        {
            for (UInt16 port = portStart; port < 62300; port++)
            {
                if (EnableTunnelForward(this.Serial, port, SCRCPY_SOCKET_NAME))
                {
                    this.LocalScrcpyServerPort = port;
                    return true;
                }
            }

            LOGE($"Could not forward Scrcpy port");
            return false;
        }

        public ProcessSession RunScrcpyServer(ServerParams serverParams)
        {
            String[] cmd = {
                "shell",
                "CLASSPATH=" + SCRCPY_DEVICE_SERVER_PATH,
                "app_process",
                "/", // unused
                "com.genymobile.scrcpy.Server",
                SCRCPY_VERSION,
                AdbUtils.log_level_to_server_string(serverParams.log_level),
                serverParams.max_size.ToString(),
                serverParams.bit_rate.ToString(),
                serverParams.max_fps.ToString(),
                serverParams.lock_video_orientation.ToString(),
                "true", // true - forward, false - reverse 
                serverParams.crop != null ? serverParams.crop : "-",
                "true", // always send frame meta (packet boundaries + timestamp)
                serverParams.control? "true" : "false",
                serverParams.display_id.ToString(),
                serverParams.show_touches ? "true" : "false",
                serverParams.stay_awake ? "true" : "false",
                serverParams.codec_options != null ? serverParams.codec_options : "-",
                serverParams.encoder_name != null ? serverParams.encoder_name : "-",
                serverParams.power_off_on_close ? "true" : "false",
            };

            return AdbUtils.adb_execute(Serial, cmd);
        }

        public Socket ConnectAndReadByte(UInt16 port)
        {
            Socket socket = NetUtils.Connect(IPV4_LOCALHOST, port);
            byte[] bytes = new byte[1];
            // the connection may succeed even if the server behind the "adb tunnel"
            // is not listening, so read one byte to detect a working connection
            if (socket.Receive(new Span<byte>(bytes)) != 1)
            {
                socket.Close();
                // the server is not listening yet behind the adb tunnel
                return null;
            }
            return socket;
        }

        public Socket ConnectToServer(UInt16 port, UInt32 attempts, UInt32 delay /*ms*/)
        {
            do
            {
                LOGD($"Remaining connection attempts: {attempts}");
                Socket socket = ConnectAndReadByte(port);
                if (socket != null)
                {
                    // it worked!
                    return socket;
                }

                if (attempts > 0)
                {
                    Thread.Sleep((int)delay);
                }
            } while (--attempts > 0);
            return null;
        }

        private ServerParams StartServerParams;

        public bool Start(ServerParams serverParams)
        {
            StartServerParams = serverParams;
            this.Serial = serverParams.serial;

            ConnectScrcpy();
            ConnectSndcpy();
            ConnectAtxAgent();
            RunDaemon();

            return true;
        }

        private void ConnectAtxAgent()
        {
            // 检查是否安装 AtxAgent
            if (this.IsAtxAgentInstalled() == false)
            {
                this.InstallAtxAgent();
            }

            if (this.AtxAgent != null)
            {
                this.DisableTunnelForward(this.Serial, this.AtxAgent.Port);
            }
            
            this.CreateAtxAgentServer(this.StartServerParams.port_start);

            if (this.AtxAgent.IsRunning() == false)
            {
                StopAtxAgent();
                StartAtxAgent();

                if (this.AtxAgent.WaitRunning(5000) == false)
                {
                    this.InstallAtxAgent();
                }

                this.AtxAgent.WaitRunning(30000);
            }
        }

        private void ConnectScrcpy()
        {
            lock (this)
            {
                this.StopScrcpySockets();
                this.PushScrcpyServerToDevice();

                this.EnableScrcpyForward(StartServerParams.port_start);

                this.ScrcpyServer = this.RunScrcpyServer(StartServerParams);
                this.ScrcpyServer.OnMsg = this.ScrcpyServer.OnErr = this.OnMsg;
                this.ScrcpyServer.RunAtBackground();

                try
                {
                    ConnectScrcpySockets();
                }
                catch(Exception ex)
                {
                    this.PushScrcpyServerToDevice();
                    this.EnableScrcpyForward(StartServerParams.port_start);

                    this.ScrcpyServer = this.RunScrcpyServer(StartServerParams);
                    this.ScrcpyServer.OnMsg = this.ScrcpyServer.OnErr = this.OnMsg;
                    this.ScrcpyServer.RunAtBackground();
                    ConnectScrcpySockets();
                }
            }
        }

        private void ConnectSndcpy()
        {
            lock (this)
            {
                this.StopSndcpySockets();

                this.InstallSndcpy();
                this.EnableSndcpyForward(StartServerParams.audio_port_start);

                this.SndcpyServer = this.RunSndcpyServer(StartServerParams);
                this.SndcpyServer.OnMsg = this.SndcpyServer.OnErr = this.OnMsg;
                this.SndcpyServer.RunAtBackground();

                try
                {
                    ConnectSndcpySockets();
                }
                catch (Exception ex)
                {
                    this.InstallSndcpy();
                    this.EnableSndcpyForward(StartServerParams.audio_port_start);

                    this.SndcpyServer = this.RunSndcpyServer(StartServerParams);
                    this.SndcpyServer.OnMsg = this.SndcpyServer.OnErr = this.OnMsg;
                    this.SndcpyServer.RunAtBackground();
                    ConnectSndcpySockets();
                }
            }
        }

        #region Scrcpy Methods

        private void ConnectScrcpySockets()  // server_connect_to()  
        {
            UInt32 attempts = 1000;
            UInt32 delay = 100; // ms
            this.VideoSocket = this.ConnectToServer(LocalScrcpyServerPort, attempts, delay);
            this.ControlSocket = NetUtils.Connect(IPV4_LOCALHOST, this.LocalScrcpyServerPort);

            if (this.VideoSocket != null)
            {
                // The sockets will be closed on stop if device_read_info() fails
                (string deviceName, int width, int height) = ReadDeviceInfo(this.VideoSocket);
                this.DeviceName = deviceName;
                this.FrameSize = new System.Drawing.Size(width, height);

                OnVideoSocketConnected?.Invoke();
            }

            if (this.ControlSocket != null)
            {
                OnControlSocketConnected?.Invoke();
            }
        }

        public void StopScrcpySockets()
        {
            //this.VideoSocket?.Close();

            //this.ControlSocket?.Close();

            this.DisableTunnelForward(Serial, LocalScrcpyServerPort);

            this.ScrcpyServer?.Kill();
        }

        public (string, int, int) ReadDeviceInfo(Socket socket)
        {
            byte[] buf = new byte[DEVICE_NAME_FIELD_LENGTH + 4];
            if (NetUtils.RecvAll(socket, buf) < DEVICE_NAME_FIELD_LENGTH + 4)
            {
                throw new BotException("Could not retrieve device information");
            }

            int byteCount = 0;
            for (int i = 0; i < buf.Length; i++)
            {
                if (buf[i] == '\0')
                {
                    byteCount = i;
                    break;
                }
            }

            String deviceName = System.Text.Encoding.ASCII.GetString(buf, 0, byteCount);
            int width = (buf[DEVICE_NAME_FIELD_LENGTH] << 8)
                    | buf[DEVICE_NAME_FIELD_LENGTH + 1];
            int height = (buf[DEVICE_NAME_FIELD_LENGTH + 2] << 8)
                    | buf[DEVICE_NAME_FIELD_LENGTH + 3];
            return (deviceName, width, height);
        }

        #endregion

        #region AtxAgent Methods

        static String AtxListenAddr = "127.0.0.1:7912";
        string AtxAgentPath = "/data/local/tmp/atx-agent";

        internal bool IsAtxAgentInstalled()
        {
            String rtn = AdbUtils.RunShell(Serial, "du", "-h", AtxAgentPath);
            return rtn.StartsWith("du:") == false;
        }

        internal void InstallAtxAgent()
        {
            this.OnMsg?.Invoke($"[{Serial}] install atx agent");

            InstallUIAutomatorApks();

            StopAtxAgent();

            this.OnMsg?.Invoke($"[{Serial}] adb shell pm install -r -t /data/local/tmp/atx-agent");
            AdbUtils.RunShell(Serial, "pm", "install", "-r", "-t", Push("./tools/atx-agent"));

            StartAtxAgent();
        }

        internal void InstallUIAutomatorApks()
        {
            this.OnMsg?.Invoke($"[{Serial}] adb shell pm uninstall com.github.uiautomator");
            this.OnMsg?.Invoke($"[{Serial}] adb shell pm uninstall com.github.uiautomator.test");
            this.OnMsg?.Invoke($"[{Serial}] adb shell pm install -r -t /data/local/tmp/app-uiautomator.apk");
            this.OnMsg?.Invoke($"[{Serial}] adb shell pm install -r -t /data/local/tmp/app-uiautomator-test.apk");

            AdbUtils.RunShell(Serial, "pm", "uninstall", "com.github.uiautomator");
            AdbUtils.RunShell(Serial, "pm", "uninstall", "com.github.uiautomator.test");

            AdbUtils.RunShell(Serial, "pm", "install", "-r", "-t", Push("./tools/app-uiautomator.apk", "0644"));
            AdbUtils.RunShell(Serial, "pm", "install", "-r", "-t", Push("./tools/app-uiautomator-test.apk", "0644"));
        }

        internal void StopAtxAgent()
        {
            this.OnMsg?.Invoke($"[{Serial}] adb shell {AtxAgentPath} server --stop");
            AdbUtils.RunShell(Serial, AtxAgentPath, "server", "--stop");
        }

        internal void StartAtxAgent()
        {
            GrantAtxPermissions();
            // adb shell /data/local/tmp/atx-agent server --nouia -d --addr 127.0.0.1:7912
            this.OnMsg?.Invoke($"[{Serial}] adb shell {AtxAgentPath} server --nouia -d --addr {AtxListenAddr}");
            AdbUtils.RunShell(Serial, AtxAgentPath, "server", "--nouia", "-d", "--addr", AtxListenAddr);
        }

        internal void GrantAtxPermissions()
        {
            String[] permissions =
                {
                "android.permission.SYSTEM_ALERT_WINDOW",
                "android.permission.ACCESS_FINE_LOCATION",
                "android.permission.READ_PHONE_STATE"
                };

            foreach (var permission in permissions)
            {
                this.OnMsg?.Invoke($"[{Serial}] adb shell pm grant com.github.uiautomator {permission}");
                AdbUtils.RunShell(Serial, "pm", "grant", "com.github.uiautomator", permission);
            }
        }

        #endregion

        #region

        public void StopSndcpySockets()
        {

            this.DisableTunnelForward(Serial, LocalSndcpyServerPort);

            this.SndcpyServer?.Kill();
        }

        public bool EnableSndcpyForward(UInt16 portStart = 28200)
        {
            for (UInt16 port = portStart; port < 62300; port++)
            {
                if (EnableTunnelForward(this.Serial, port, SNDCPY_SOCKET_NAME))
                {
                    this.LocalSndcpyServerPort = port;
                    return true;
                }
            }

            LOGE($"Could not forward sndcpy port");
            return false;
        }

        public ProcessSession RunSndcpyServer(ServerParams serverParams)
        {
            String[] cmd = {
                "shell",
                "am",
                "start",
                "com.rom1v.sndcpy/.MainActivity"
            };

            return AdbUtils.adb_execute(Serial, cmd);
        }

        private void ConnectSndcpySockets()  // server_connect_to()  
        {
            UInt32 attempts = 1000;
            UInt32 delay = 100; // ms
            this.AudioSocket = this.ConnectToServer(LocalSndcpyServerPort, attempts, delay);

            if (this.AudioSocket != null)
            {
                OnAudioSocketConnected?.Invoke();
            }
        }

        internal void InstallSndcpy()
        {
            this.OnMsg?.Invoke($"[{Serial}] install sndcpy");
            this.OnMsg?.Invoke($"[{Serial}] adb shell pm uninstall com.rom1v.sndcpy");
            AdbUtils.RunShell(Serial, "uninstall", "com.rom1v.sndcpy");
            AdbUtils.RunShell(Serial, "install", "-t", "-g", Push("./tools/sndcpy.apk", "0644"));
            AdbUtils.RunShell(Serial, "appops", "set", "com.rom1v.sndcpy", "PROJECT_MEDIA","allow");
        }

        #endregion


        internal String Push(String filePath, String permission = "0755", String dest = null)
        {
            System.IO.FileInfo fileInfo = new System.IO.FileInfo(filePath);
            if (dest == null) dest = "/data/local/tmp/" + fileInfo.Name;
            this.OnMsg?.Invoke($"[{Serial}] adb push {filePath} {dest}");
            this.OnMsg?.Invoke($"[{Serial}] adb shell chmod {permission} {dest}");
            AdbUtils.Run(AdbUtils.adb_push(Serial, filePath, dest));
            AdbUtils.RunShell(Serial, "chmod", permission, dest);
            return dest;
        }
    }
}
