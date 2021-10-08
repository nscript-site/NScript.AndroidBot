using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NScript.AndroidBot
{
    public enum control_msg_type
    {
        CONTROL_MSG_TYPE_INJECT_KEYCODE,
        CONTROL_MSG_TYPE_INJECT_TEXT,
        CONTROL_MSG_TYPE_INJECT_TOUCH_EVENT,
        CONTROL_MSG_TYPE_INJECT_SCROLL_EVENT,
        CONTROL_MSG_TYPE_BACK_OR_SCREEN_ON,
        CONTROL_MSG_TYPE_EXPAND_NOTIFICATION_PANEL,
        CONTROL_MSG_TYPE_EXPAND_SETTINGS_PANEL,
        CONTROL_MSG_TYPE_COLLAPSE_PANELS,
        CONTROL_MSG_TYPE_GET_CLIPBOARD,
        CONTROL_MSG_TYPE_SET_CLIPBOARD,
        CONTROL_MSG_TYPE_SET_SCREEN_POWER_MODE,
        CONTROL_MSG_TYPE_ROTATE_DEVICE,
    };

    public enum screen_power_mode
    {
        // see <https://android.googlesource.com/platform/frameworks/base.git/+/pie-release-2/core/java/android/view/SurfaceControl.java#305>
        SCREEN_POWER_MODE_OFF = 0,
        SCREEN_POWER_MODE_NORMAL = 2,
    };

    public struct size
    {
        public UInt16 width;
        public UInt16 height;
    };

    public struct point
    {
        public Int32 x;
        public Int32 y;
    };

    public struct position
    {
        // The video screen size may be different from the real device screen size,
        // so store to which size the absolute position apply, to scale it
        // accordingly.
        public size screen_size;
        public point point;
    };

    public class Labels
    {
        public string[] android_keyevent_action_labels = {
            "down",
            "up",
            "multi",
        };

        public string[] android_motionevent_action_labels = {
            "down",
            "up",
            "move",
            "cancel",
            "outside",
            "ponter-down",
            "pointer-up",
            "hover-move",
            "scroll",
            "hover-enter",
            "hover-exit",
            "btn-press",
            "btn-release"
        };

        public string[] screen_power_mode_labels = {
            "off",
            "doze",
            "normal",
            "doze-suspend",
            "suspend"
        };
    }

    public class Setting
    {
        public const int CONTROL_MSG_MAX_SIZE = (1 << 18); // 256k
        public const int CONTROL_MSG_CLIPBOARD_TEXT_MAX_LENGTH = CONTROL_MSG_MAX_SIZE - 6;
    }

    public unsafe abstract class ControlMsg
    {
        internal static void buffer_write16be(Byte* buf, UInt16 value)
        {
            buf[0] = (byte)(value >> 8);
            buf[1] = (byte)value;
        }

        internal static void buffer_write32be(Byte* buf, UInt32 value)
        {
            buf[0] = (byte)(value >> 24);
            buf[1] = (byte)(value >> 16);
            buf[2] = (byte)(value >> 8);
            buf[3] = (byte)value;
        }

        internal static void buffer_write64be(Byte* buf, UInt64 value)
        {
            buffer_write32be(buf, (byte)(value >> 32));
            buffer_write32be(&buf[4], (UInt32)value);
        }

        internal static void write_position(byte* buf, ref position position)
        {
            buffer_write32be(&buf[0], (uint)position.point.x);
            buffer_write32be(&buf[4], (uint)position.point.y);
            buffer_write16be(&buf[8], position.screen_size.width);
            buffer_write16be(&buf[10], position.screen_size.height);
        }

        // write length (2 bytes) + string (non nul-terminated)
        internal static int write_string(String utf8, int max_len, byte* buf)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(utf8);
            fixed(byte* pBytes = bytes)
            {
                int len = Math.Min(bytes.Length, max_len);
                buffer_write32be(buf, (uint)len);
                Span<byte> dst = new Span<byte>(buf + 4, len);
                Span<byte> source = new Span<byte>(pBytes, len);
                source.CopyTo(dst);
                return 4 + len;
            }
        }

        internal static UInt16 to_fixed_point_16(float f)
        {
            UInt32 u = (UInt16)(f * (2 << 16)); // 2^16
            if (u >= 0xffff)
            {
                u = 0xffff;
            }
            return (UInt16)u;
        }

        public control_msg_type MsgType { get; private set; }
        public ControlMsg(control_msg_type msgType) {
            this.MsgType = msgType;
        }
        public int Serialize(Byte* buf)
        {
            buf[0] = (byte)MsgType;
            return SerializePayloads(buf);
        }

        protected abstract int SerializePayloads(Byte* buf);
    }

    public unsafe class SimpleControlMsg : ControlMsg
    {
        public SimpleControlMsg(control_msg_type msgType) : base(msgType) { }

        protected override unsafe int SerializePayloads(byte* buf)
        {
            return 1;
        }
    }

    public unsafe class InjectKeycodeMsg : ControlMsg
    {
        android_keyevent_action action;
        android_keycode keycode;
        UInt32 repeat;
        android_metastate metastate;

        public InjectKeycodeMsg() : base(control_msg_type.CONTROL_MSG_TYPE_INJECT_KEYCODE) { }

        protected override int SerializePayloads(Byte* buf)
        {
            buf[1] = (byte)action;
            buffer_write32be(&buf[2], (UInt32)keycode);
            buffer_write32be(&buf[6], repeat);
            buffer_write32be(&buf[10], (UInt32)metastate);
            return 14;
        }
    }

    public class InjectTextMsg : ControlMsg
    {
        public string text;

        public InjectTextMsg() : base(control_msg_type.CONTROL_MSG_TYPE_INJECT_TEXT) { }

        protected override unsafe int SerializePayloads(byte* buf)
        {
            const int CONTROL_MSG_INJECT_TEXT_MAX_LENGTH = 300;
            int len =
                write_string(text,
                             CONTROL_MSG_INJECT_TEXT_MAX_LENGTH, &buf[1]);
            return 1 + len;
        }
    }

    public class InjectTouchEventMsg : ControlMsg
    {
        android_motionevent_action action;
        android_motionevent_buttons buttons;
        UInt64 pointer_id;
        position position;
        float pressure = 0;

        public InjectTouchEventMsg() : base(control_msg_type.CONTROL_MSG_TYPE_INJECT_TOUCH_EVENT) { }

        protected override unsafe int SerializePayloads(byte* buf)
        {
            buf[1] = (byte)action;
            buffer_write64be(&buf[2], pointer_id);
            write_position(&buf[10], ref position);
            UInt16 pressureVal = to_fixed_point_16(pressure);
            buffer_write16be(&buf[22], pressureVal);
            buffer_write32be(&buf[24], (uint)buttons);
            return 28;
        }
    }

    public class InjectScrollEventMsg : ControlMsg
    {
        public InjectScrollEventMsg() : base(control_msg_type.CONTROL_MSG_TYPE_INJECT_SCROLL_EVENT) { }

        position position;
        Int32 hscroll;
        Int32 vscroll;

        protected override unsafe int SerializePayloads(byte* buf)
        {
            write_position(&buf[1], ref position);
            buffer_write32be(&buf[13], (UInt32) hscroll);
            buffer_write32be(&buf[17], (UInt32)vscroll);
            return 21;

        }
    }

    public class BackOrScreenOnMsg : ControlMsg
    {
        public BackOrScreenOnMsg() : base(control_msg_type.CONTROL_MSG_TYPE_BACK_OR_SCREEN_ON) { }
        android_keyevent_action action; // action for the BACK key
                                        // screen may only be turned on on ACTION_DOWN

        protected override unsafe int SerializePayloads(byte* buf)
        {
            buf[1] = (byte)action;
            return 2;
        }
    }

    public class SetClipboardMsg : ControlMsg
    {
        public string Text { get; private set; } // owned, to be freed by free()
        public bool IsPaste { get; private set; }
        public SetClipboardMsg(String text, bool paste) : base(control_msg_type.CONTROL_MSG_TYPE_SET_CLIPBOARD) { this.Text = text; this.IsPaste = paste; }

        protected override unsafe int SerializePayloads(byte* buf)
        {
            buf[1] = IsPaste ? (Byte)0x01 : (Byte)0x00;
            int len = write_string(Text,
                                      Setting.CONTROL_MSG_CLIPBOARD_TEXT_MAX_LENGTH,
                                      &buf[2]);
            return 2 + len;
        }
    }

    public class SetScreenPowerModeMsg : ControlMsg
    {
        screen_power_mode mode;
        public SetScreenPowerModeMsg() : base(control_msg_type.CONTROL_MSG_TYPE_SET_SCREEN_POWER_MODE) { }
        protected override unsafe int SerializePayloads(byte* buf)
        {
            buf[1] = (byte)mode;
            return 2;
        }
    }

    public class ExpandNotificationPanelMsg:SimpleControlMsg
    {
        public ExpandNotificationPanelMsg() : base(control_msg_type.CONTROL_MSG_TYPE_EXPAND_NOTIFICATION_PANEL) { }
    }

    public class ExpandSettingPanelMsg : SimpleControlMsg
    {
        public ExpandSettingPanelMsg() : base(control_msg_type.CONTROL_MSG_TYPE_EXPAND_SETTINGS_PANEL) { }
    }

    public class CollapsePanelsMsg : SimpleControlMsg
    {
        public CollapsePanelsMsg() : base(control_msg_type.CONTROL_MSG_TYPE_COLLAPSE_PANELS) { }
    }

    public class GetClipboardMsg : SimpleControlMsg
    {
        public GetClipboardMsg() : base(control_msg_type.CONTROL_MSG_TYPE_GET_CLIPBOARD) { }
    }

    public class RotateDeviceMsg : SimpleControlMsg
    {
        public RotateDeviceMsg() : base(control_msg_type.CONTROL_MSG_TYPE_ROTATE_DEVICE) { }
    }
}
