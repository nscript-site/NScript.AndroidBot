using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NScript.AndroidBot
{
    public enum ControlMsgType
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

    public enum ScreenPowerMode
    {
        // see <https://android.googlesource.com/platform/frameworks/base.git/+/pie-release-2/core/java/android/view/SurfaceControl.java#305>
        SCREEN_POWER_MODE_OFF = 0,
        SCREEN_POWER_MODE_NORMAL = 2,
    };

    public struct Size
    {
        public UInt16 Width;
        public UInt16 Height;
    };

    public struct Point
    {
        public Int32 X;
        public Int32 Y;
    };

    public struct Position
    {
        // The video screen size may be different from the real device screen size,
        // so store to which size the absolute position apply, to scale it
        // accordingly.
        public Size ScreenSize;
        public Point Point;
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

        internal static void write_position(byte* buf, ref Position position)
        {
            buffer_write32be(&buf[0], (uint)position.Point.X);
            buffer_write32be(&buf[4], (uint)position.Point.Y);
            buffer_write16be(&buf[8], position.ScreenSize.Width);
            buffer_write16be(&buf[10], position.ScreenSize.Height);
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

        public ControlMsgType MsgType { get; private set; }
        public ControlMsg(ControlMsgType msgType) {
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
        public SimpleControlMsg(ControlMsgType msgType) : base(msgType) { }

        protected override unsafe int SerializePayloads(byte* buf)
        {
            return 1;
        }
    }

    public unsafe class InjectKeycodeMsg : ControlMsg
    {
        public AndroidKeyeventAction Action;
        public AndroidKeycode Keycode;
        public UInt32 Repeat;
        public AndroidMetastate Metastate;

        public InjectKeycodeMsg() : base(ControlMsgType.CONTROL_MSG_TYPE_INJECT_KEYCODE) { }

        protected override int SerializePayloads(Byte* buf)
        {
            buf[1] = (byte)Action;
            buffer_write32be(&buf[2], (UInt32)Keycode);
            buffer_write32be(&buf[6], Repeat);
            buffer_write32be(&buf[10], (UInt32)Metastate);
            return 14;
        }
    }

    public class InjectTextMsg : ControlMsg
    {
        public string Text;

        public InjectTextMsg() : base(ControlMsgType.CONTROL_MSG_TYPE_INJECT_TEXT) { }

        protected override unsafe int SerializePayloads(byte* buf)
        {
            const int CONTROL_MSG_INJECT_TEXT_MAX_LENGTH = 300;
            int len =
                write_string(Text,
                             CONTROL_MSG_INJECT_TEXT_MAX_LENGTH, &buf[1]);
            return 1 + len;
        }
    }

    public class InjectTouchEventMsg : ControlMsg
    {
        public AndroidMotionEventAction Action;
        public AndroidMotionEventButtons Buttons;
        public UInt64 PointerId;
        public Position Position;
        public float Pressure = 0;

        public InjectTouchEventMsg() : base(ControlMsgType.CONTROL_MSG_TYPE_INJECT_TOUCH_EVENT) { }

        protected override unsafe int SerializePayloads(byte* buf)
        {
            buf[1] = (byte)Action;
            buffer_write64be(&buf[2], PointerId);
            write_position(&buf[10], ref Position);
            UInt16 pressureVal = to_fixed_point_16(Pressure);
            buffer_write16be(&buf[22], pressureVal);
            buffer_write32be(&buf[24], (uint)Buttons);
            return 28;
        }
    }

    public class InjectScrollEventMsg : ControlMsg
    {
        public InjectScrollEventMsg() : base(ControlMsgType.CONTROL_MSG_TYPE_INJECT_SCROLL_EVENT) { }

        public Position Position;
        public Int32 HScroll;
        public Int32 VScroll;

        protected override unsafe int SerializePayloads(byte* buf)
        {
            write_position(&buf[1], ref Position);
            buffer_write32be(&buf[13], (UInt32) HScroll);
            buffer_write32be(&buf[17], (UInt32)VScroll);
            return 21;
        }
    }

    public class BackOrScreenOnMsg : ControlMsg
    {
        public BackOrScreenOnMsg() : base(ControlMsgType.CONTROL_MSG_TYPE_BACK_OR_SCREEN_ON) { }
        public AndroidKeyeventAction Action; // action for the BACK key
                                        // screen may only be turned on on ACTION_DOWN
        protected override unsafe int SerializePayloads(byte* buf)
        {
            buf[1] = (byte)Action;
            return 2;
        }
    }

    public class SetClipboardMsg : ControlMsg
    {
        public string Text { get; private set; } // owned, to be freed by free()
        public bool IsPaste { get; private set; }
        public SetClipboardMsg(String text, bool paste) : base(ControlMsgType.CONTROL_MSG_TYPE_SET_CLIPBOARD) { this.Text = text; this.IsPaste = paste; }

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
        public ScreenPowerMode Mode;
        public SetScreenPowerModeMsg() : base(ControlMsgType.CONTROL_MSG_TYPE_SET_SCREEN_POWER_MODE) { }
        protected override unsafe int SerializePayloads(byte* buf)
        {
            buf[1] = (byte)Mode;
            return 2;
        }
    }

    public class ExpandNotificationPanelMsg:SimpleControlMsg
    {
        public ExpandNotificationPanelMsg() : base(ControlMsgType.CONTROL_MSG_TYPE_EXPAND_NOTIFICATION_PANEL) { }
    }

    public class ExpandSettingPanelMsg : SimpleControlMsg
    {
        public ExpandSettingPanelMsg() : base(ControlMsgType.CONTROL_MSG_TYPE_EXPAND_SETTINGS_PANEL) { }
    }

    public class CollapsePanelsMsg : SimpleControlMsg
    {
        public CollapsePanelsMsg() : base(ControlMsgType.CONTROL_MSG_TYPE_COLLAPSE_PANELS) { }
    }

    public class GetClipboardMsg : SimpleControlMsg
    {
        public GetClipboardMsg() : base(ControlMsgType.CONTROL_MSG_TYPE_GET_CLIPBOARD) { }
    }

    public class RotateDeviceMsg : SimpleControlMsg
    {
        public RotateDeviceMsg() : base(ControlMsgType.CONTROL_MSG_TYPE_ROTATE_DEVICE) { }
    }
}
