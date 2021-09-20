using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NScript.AndroidBot
{
    public class AdbUtils
    {
        /// <summary>
        /// convenience function to wait for a successful process execution
        /// automatically log process errors with the provided process name
        /// </summary>
        /// <param name="proc"></param>
        /// <param name="name"></param>
        /// <param name="close"></param>
        /// <returns></returns>
        public static bool process_check_success(ProcessSession proc, String name, bool close)
        {
            bool result = proc.Run();
            if (result == close) proc.OnMsg?.Invoke( $"[{name}] ok" );
            return result;
        }

        public static String get_adb_command()
        {
            return "adb";
        }

        public static ProcessSession adb_execute(String serial, String[] adb_cmd)
        {
            List<String> args = new List<string>();
            args.Add(get_adb_command());
            if (serial != null)
            {
                args.Add("-s");
                args.Add(serial);
            }
            args.AddRange(adb_cmd);

            ProcessSession process = new ProcessSession(args.ToArray());
            return process;
        }

        /// <summary>
        /// serialize argv to string "[arg1], [arg2], [arg3]"
        /// </summary>
        /// <param name="cmds"></param>
        /// <returns></returns>
        public static String argv_to_string(String[] cmds)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var item in cmds)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append('[').Append(item).Append(']');
            }
            return sb.ToString();
        }

        public static ProcessSession adb_forward(String serial, UInt16 local_port, String device_socket_name)
        {
            String[] cmds = { "forward", $"tcp:{local_port}", $"localabstract:{device_socket_name}" };
            return adb_execute(serial, cmds);
        }

        public static ProcessSession adb_forward_remove(String serial, UInt16 local_port)
        {
            String[] cmds = { "forward", "--remove", $"tcp:{local_port}"};
            return adb_execute(serial, cmds);
        }

        public static ProcessSession adb_reverse(String serial, String device_socket_name, UInt16 local_port)
        {
            String[] cmds = { "reverse", $"localabstract:{device_socket_name}", $"tcp:{local_port}" };
            return adb_execute(serial, cmds);
        }

        public static ProcessSession adb_reverse_remove(String serial, String device_socket_name)
        {
            String[] cmds = { "reverse", "--remove", $"localabstract:{device_socket_name}" };
            return adb_execute(serial, cmds);
        }

        /// <summary>
        /// Windows will parse the string, so the paths must be quoted
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static String EncodePath(String path)
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                return "\"" + path + "\"";
            else
                return path;
        }

        public static ProcessSession adb_push(String serial, String local, String remote)
        {
            String[] cmds = { "push", EncodePath(local), EncodePath(remote) };
            return adb_execute(serial, cmds);
        }

        public static ProcessSession adb_install(String serial, String local)
        {
            String[] cmds = { "install", "-r", EncodePath(local) };
            return adb_execute(serial, cmds);
        }

        public static String log_level_to_server_string(LogLevel level)
        {
            return level.ToString().ToLower();
        }
    }
}
