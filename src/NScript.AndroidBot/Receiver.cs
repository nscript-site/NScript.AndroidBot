using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;

namespace NScript.AndroidBot
{
    public enum device_msg_type
    {
        DEVICE_MSG_TYPE_CLIPBOARD,
    };

    public class device_msg
    {
        public device_msg_type type;
        public string text;
    }

    public class Receiver
    {
        public Socket control_socket;

        public Receiver(Socket socket)
        {
            this.control_socket = socket;
        }

        static unsafe int device_msg_deserialize(byte* buf, int len,
               device_msg msg)
        {
            //if (len < 5)
            //{
            //    // at least type + empty string length
            //    return 0; // not available
            //}

            //msg.type = (device_msg_type)buf[0];
            //switch (msg.type)
            //{
            //    case device_msg_type.DEVICE_MSG_TYPE_CLIPBOARD:
            //        {
            //            size_t clipboard_len = buffer_read32be(&buf[1]);
            //            if (clipboard_len > len - 5)
            //            {
            //                return 0; // not available
            //            }
            //            char* text = malloc(clipboard_len + 1);
            //            if (!text)
            //            {
            //                LOGW("Could not allocate text for clipboard");
            //                return -1;
            //            }
            //            if (clipboard_len)
            //            {
            //                memcpy(text, &buf[5], clipboard_len);
            //            }
            //            text[clipboard_len] = '\0';

            //            msg->clipboard.text = text;
            //            return 5 + clipboard_len;
            //        }
            //    default:
            //        LOGW("Unknown device message type: %d", (int)msg->type);
            //        return -1; // error, we cannot recover
            //}
            throw new NotImplementedException();
        }

        public static void process_msg(device_msg msg)
        {
            switch (msg.type)
            {
                case device_msg_type.DEVICE_MSG_TYPE_CLIPBOARD:
                    break;
            }
        }

        static unsafe int process_msgs(byte* buf, int len)
        {
            int head = 0;
            for (; ; )
            {
                device_msg msg = new device_msg();
                int r = device_msg_deserialize(&buf[head], len - head, msg);
                if (r == -1)
                {
                    return -1;
                }
                if (r == 0)
                {
                    return head;
                }

                process_msg(msg);

                head += r;
                if (head == len)
                {
                    return head;
                }
            }
        }
    }
}