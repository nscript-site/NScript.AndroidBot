using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NScript.AndroidBot
{
    public struct PortRange
    {
        public UInt16 first;
        public UInt16 last;
    };

    public enum LogLevel
    {
        VERBOSE,
        DEBUG,
        INFO,
        WARN,
        ERROR,
    };

    public class ServerParams
    {
        public String serial;
        public LogLevel log_level;
        public String crop;
        public String codec_options;
        public String encoder_name;
        public PortRange port_range = new PortRange { first = 27183, last = 27199 };
        public UInt16 max_size;
        public UInt32 bit_rate = 8000000;
        public UInt16 max_fps;
        public Byte lock_video_orientation;
        public bool control;
        public UInt32 display_id;
        public bool show_touches;
        public bool stay_awake;
        public bool force_adb_forward;
        public bool power_off_on_close;
    }

    public class Server
    {
        public const String SOCKET_NAME = "scrcpy";
        public const String SERVER_FILENAME = "scrcpy-server";
        public const String DEVICE_SERVER_PATH = "/data/local/tmp/scrcpy-server.jar";
        public const String DEFAULT_SERVER_PATH = "/share/scrcpy/" + SERVER_FILENAME;
        public const String SCRCPY_VERSION = "1.18";
        const UInt32 IPV4_LOCALHOST = 0x7F000001;
        const int DEVICE_NAME_FIELD_LENGTH = 64;

        public String Serial { get; set; }
        public ProcessSession process;
        public Task wait_server_thread;
        public bool server_socket_closed;
        public Mutex mutex;
        public Object syncRoot = new object();
        public Object process_terminated_cond;
        public bool process_terminated;
        public Socket server_socket; // only used if !tunnel_forward
        public Socket video_socket;
        public Socket control_socket;
        public UInt16 local_port; // selected from port_range
        public bool tunnel_enabled;
        public bool tunnel_forward; // use "adb forward" instead of "adb reverse"

        public String DeviceName { get; private set; }
        public System.Drawing.Size FrameSize { get; private set; }

        public Action<String> OnMsg { get; set; }

        public string get_server_path()
        {
            return SERVER_FILENAME;
        }

        public bool push_server()
        {
            String server_path = get_server_path();
            if (server_path == null)
            {
                return false;
            }

            if (!is_regular_file(server_path))
            {
                LOGE($"'{server_path}' does not exist or is not a regular file\n");
                return false;
            }

            ProcessSession process = AdbUtils.adb_push(Serial, server_path, DEVICE_SERVER_PATH);
            process.OnMsg = process.OnErr = this.OnMsg;
            return AdbUtils.process_check_success(process, "adb push", true);
        }

        public bool enable_tunnel_reverse(String serial, UInt16 local_port)
        {
            ProcessSession process = AdbUtils.adb_reverse(serial, SOCKET_NAME, local_port);
            process.OnMsg = process.OnErr = this.OnMsg;
            return AdbUtils.process_check_success(process, "adb reverse", true);
        }

        public bool disable_tunnel_reverse(String serial)
        {
            ProcessSession process = AdbUtils.adb_reverse_remove(serial, SOCKET_NAME);
            process.OnMsg = process.OnErr = this.OnMsg;
            return AdbUtils.process_check_success(process, "adb reverse --remove", true);
        }

        public bool enable_tunnel_forward(String serial, UInt16 local_port)
        {
            ProcessSession process = AdbUtils.adb_forward(serial, local_port, SOCKET_NAME);
            process.OnMsg = process.OnErr = this.OnMsg;
            return AdbUtils.process_check_success(process, "adb forward", true);
        }

        public bool disable_tunnel_forward(String serial, UInt16 local_port)
        {
            ProcessSession process = AdbUtils.adb_forward_remove(serial, local_port);
            process.OnMsg = process.OnErr = this.OnMsg;
            return AdbUtils.process_check_success(process, "adb forward --remove", true);
        }

        public bool disable_tunnel()
        {
            if (this.tunnel_forward)
            {
                return disable_tunnel_forward(this.Serial, this.local_port);
            }
            return disable_tunnel_reverse(this.Serial);
        }

        static Socket listen_on_port(UInt16 port)
        {
            return NetUtils.net_listen(IPAddress.Parse("127.0.0.1"), port, 2);
        }

        public bool is_regular_file(String filePath)
        {
            return true;
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

        public bool enable_tunnel_reverse_any_port(PortRange port_range)
        {
            UInt16 port = port_range.first;
            for (; ; )
            {
                if (!enable_tunnel_reverse(this.Serial, port))
                {
                    // the command itself failed, it will fail on any port
                    return false;
                }

                // At the application level, the device part is "the server" because it
                // serves video stream and control. However, at the network level, the
                // client listens and the server connects to the client. That way, the
                // client can listen before starting the server app, so there is no
                // need to try to connect until the server socket is listening on the
                // device.
                this.server_socket = listen_on_port(port);
                if(this.server_socket != null)
                {
                    this.local_port = port;
                    return true;
                }

                // failure, disable tunnel and try another port
                if (!disable_tunnel_reverse(this.Serial))
                {
                    LOGW($"Could not remove reverse tunnel on port {port}");
                }

                // check before incrementing to avoid overflow on port 65535
                if (port < port_range.last)
                {
                    LOGW($"Could not listen on port {port}, retrying on {port + 1}");
                    port++;
                    continue;
                }

                if (port_range.first == port_range.last)
                {
                    LOGE($"Could not listen on port {port_range.first}");
                }
                else
                {
                    LOGE($"Could not listen on any port in range {port_range.first}:{port_range.last}");
                }
                return false;
            }
        }

        public bool enable_tunnel_forward_any_port(PortRange port_range)
        {
            this.tunnel_forward = true;
            UInt16 port = port_range.first;
            for (;;)
            {
                if (enable_tunnel_forward(this.Serial, port))
                {
                    // success
                    this.local_port = port;
                    return true;
                }

                if (port < port_range.last)
                {
                    LOGW($"Could not forward port {port}, retrying on {port + 1}");
                    port++;
                    continue;
                }

                if (port_range.first == port_range.last)
                {
                    LOGE($"Could not forward port {port_range.first}");
                }
                else
                {
                    LOGE($"Could not forward any port in range {port_range.first}:{port_range.last}");
                }
                return false;
            }
        }

        public bool enable_tunnel_any_port(PortRange port_range, bool force_adb_forward)
        {
            if (!force_adb_forward)
            {
                // Attempt to use "adb reverse"
                if (enable_tunnel_reverse_any_port(port_range))
                {
                    return true;
                }

                // if "adb reverse" does not work (e.g. over "adb connect"), it
                // fallbacks to "adb forward", so the app socket is the client

                LOGW("'adb reverse' failed, fallback to 'adb forward'");
            }

            return enable_tunnel_forward_any_port(port_range);
        }

        public ProcessSession execute_server(ServerParams serverParams)
        {
            String[] cmd = {
        "shell",
        "CLASSPATH=" + DEVICE_SERVER_PATH,
        "app_process",
//#ifdef SERVER_DEBUGGER
//# define SERVER_DEBUGGER_PORT "5005"
//# ifdef SERVER_DEBUGGER_METHOD_NEW
//        /* Android 9 and above */
//        "-XjdwpProvider:internal -XjdwpOptions:transport=dt_socket,suspend=y,server=y,address="
//# else
//        /* Android 8 and below */
//        "-agentlib:jdwp=transport=dt_socket,suspend=y,server=y,address="
//# endif
//            SERVER_DEBUGGER_PORT,
//#endif
        "/", // unused
        "com.genymobile.scrcpy.Server",
        SCRCPY_VERSION,
        AdbUtils.log_level_to_server_string(serverParams.log_level),
        serverParams.max_size.ToString(),
        serverParams.bit_rate.ToString(),
        serverParams.max_fps.ToString(),
        serverParams.lock_video_orientation.ToString(),
        this.tunnel_forward ? "true" : "false",
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
            //# ifdef SERVER_DEBUGGER
            //        LOGI("Server debugger waiting for a client on device port "
            //         SERVER_DEBUGGER_PORT "...");
            //    // From the computer, run
            //    //     adb forward tcp:5005 tcp:5005
            //    // Then, from Android Studio: Run > Debug > Edit configurations...
            //    // On the left, click on '+', "Remote", with:
            //    //     Host: localhost
            //    //     Port: 5005
            //    // Then click on "Debug"
            //#endif

            return AdbUtils.adb_execute(Serial, cmd);
        }

        public Socket connect_and_read_byte(UInt16 port)
        {
            Socket socket = NetUtils.net_connect(IPV4_LOCALHOST, port);
            byte[] bytes = new byte[1];
            // the connection may succeed even if the server behind the "adb tunnel"
            // is not listening, so read one byte to detect a working connection
            if (NetUtils.net_recv(socket, new Span<byte>(bytes)) != 1)
            {
                socket.Close();
                // the server is not listening yet behind the adb tunnel
                return null;
            }
            return socket;
        }

        public Socket connect_to_server(UInt16 port, UInt32 attempts, UInt32 delay /*ms*/)
        {
            do
            {
                LOGD($"Remaining connection attempts: {attempts}");
                Socket socket = connect_and_read_byte(port);
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

        static void close_socket(Socket socket)
        {
            if (socket == null) return;
            NetUtils.net_shutdown(socket, SocketShutdown.Both);
            socket.Close();
        }

        int run_wait_server()
        {
            this.process.Run();
            this.process_terminated = true;
            if (this.server_socket != null)
            {
                this.server_socket.Close();
            }

            //process_wait(server->process, false); // ignore exit code

            //sc_mutex_lock(&server->mutex);
            //server->process_terminated = true;
            //sc_cond_signal(&server->process_terminated_cond);
            //sc_mutex_unlock(&server->mutex);

            //// no need for synchronization, server_socket is initialized before this
            //// thread was created
            //if (server->server_socket != INVALID_SOCKET
            //        && !atomic_flag_test_and_set(&server->server_socket_closed))
            //{
            //    // On Linux, accept() is unblocked by shutdown(), but on Windows, it is
            //    // unblocked by closesocket(). Therefore, call both (close_socket()).
            //    close_socket(server->server_socket);
            //}
            //LOGD("Server terminated");
            return 0;
        }

        public bool Start(ServerParams serverParams)
        {
            this.Serial = serverParams.serial;

            if (!this.push_server())
            {
                return false;
            }

            if (!this.enable_tunnel_any_port(serverParams.port_range,
                                            serverParams.force_adb_forward))
            {
                return false;
            }

            // server will connect to our server socket
            this.process = this.execute_server(serverParams);
            this.process.OnMsg = this.process.OnErr = this.OnMsg;
            if (this.process == null) goto error;

            // If the server process dies before connecting to the server socket, then
            // the client will be stuck forever on accept(). To avoid the problem, we
            // must be able to wake up the accept() call when the server dies. To keep
            // things simple and multiplatform, just spawn a new thread waiting for the
            // server process and calling shutdown()/close() on the server socket if
            // necessary to wake up any accept() blocking call.

            this.wait_server_thread = new Task(() => run_wait_server());
            this.wait_server_thread.Start();

            //if (!ok)
            //{
            //    process_terminate(server->process);
            //    process_wait(server->process, true); // ignore exit code
            //    goto error;
            //}

            this.tunnel_enabled = true;

            return true;

        error:
            if (!this.tunnel_forward)
            {
                this.server_socket_closed = true;
                this.server_socket.Close();
            }
            this.disable_tunnel();

            return false;
        }

        public (string,int,int) ReadDeviceInfo(Socket socket)
        {
            byte[] buf = new byte[DEVICE_NAME_FIELD_LENGTH + 4];
            if (NetUtils.net_recv_all(socket, buf) < DEVICE_NAME_FIELD_LENGTH + 4)
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

        public void Connect()  // server_connect_to()  
        {
            if (!this.tunnel_forward)
            {
                this.video_socket = NetUtils.net_accept(this.server_socket);

                this.control_socket = NetUtils.net_accept(this.server_socket);
                if (this.server_socket_closed == false)
                {
                    this.server_socket_closed = true;
                    this.server_socket.Close();
                }
            }
            else
            {
                UInt32 attempts = 100;
                UInt32 delay = 100; // ms
                this.video_socket = this.connect_to_server(local_port, attempts, delay);
                // we know that the device is listening, we don't need several attempts
                this.control_socket = NetUtils.net_connect(IPV4_LOCALHOST, this.local_port);
            }

            // we don't need the adb tunnel anymore
            this.disable_tunnel(); // ignore failure
            this.tunnel_enabled = false;

            // The sockets will be closed on stop if device_read_info() fails
            (string deviceName, int width, int height) = ReadDeviceInfo(this.video_socket);
            this.DeviceName = deviceName;
            this.FrameSize = new System.Drawing.Size(width, height);
        }

        public void server_stop()
        {
            if (this.server_socket != null && this.server_socket_closed == false)
            {
                this.server_socket_closed = true;
                this.server_socket.Close();
            }

            if (this.video_socket != null)
            {
                this.video_socket.Close();
            }

            if (this.control_socket != null)
            {
                this.control_socket.Close();
            }

            if (this.tunnel_enabled)
            {
                // ignore failure
                this.disable_tunnel();
            }

            this.process.Kill();
        }
    }
}
