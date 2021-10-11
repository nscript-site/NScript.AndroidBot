using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NScript.AndroidBot
{
    public enum LockVideoOrientation
    {
        UNLOCKED = -1,
        // lock the current orientation when scrcpy starts
        INITIAL = -2,
        LockVideoOrientation_0 = 0,
        LockVideoOrientation_1,
        LockVideoOrientation_2,
        LockVideoOrientation_3,
    };

    public class BotOptions
    {
        public String Serial;
        public String Crop;
        public String RecordFilename;
        public String WindowTitle;
        public String PushTarget;
        public String RenderDriver;
        public String CodecOptions;
        public String EncoderName;
        public String V4l2Device;
        public LogLevel LogLevel;
        //enum sc_record_format record_format;
        public PortRange PortRange = new PortRange { first = 27183, last = 27199 };
        //struct sc_shortcut_mods shortcut_mods;
        public UInt16 MaxSize = 1024;
        public UInt32 BitRate = 8000000;
        public UInt16 MaxFps;
        public LockVideoOrientation LockVideoOrientation;
        public byte Rotation;
        public Int16 WindowX; // SC_WINDOW_POSITION_UNDEFINED for "auto"
        public Int16 WindowY; // SC_WINDOW_POSITION_UNDEFINED for "auto"
        public UInt16 WindowWidth;
        public UInt16 WindowHeight;
        public UInt32 DisplayId;
        public bool ShowTouches;
        public bool FullScreen;
        public bool AlwaysOnTop;
        public bool Control = true;
        public bool Display;
        public bool TurnScreenOff;
        public bool PreferText;
        public bool WindowBorderless;
        public bool Mipmaps;
        public bool StayAwake;
        public bool ForceAdbForward;
        public bool DisableScreensaver;
        public bool ForwardKeyRepeat;
        public bool ForwardAllClicks;
        public bool LegacyPaste;
        public bool PowerOffOnClose;

        public ServerParams ToServerParams()
        {
            ServerParams sp = new ServerParams();
            sp.serial = this.Serial;
            sp.log_level = this.LogLevel;
            sp.crop = this.Crop;
            sp.port_range = this.PortRange;
            sp.max_size = this.MaxSize;
            sp.bit_rate = this.BitRate;
            sp.max_fps = this.MaxFps;
            sp.lock_video_orientation = (byte)this.LockVideoOrientation;
            sp.control = this.Control;
            sp.display_id = this.DisplayId;
            sp.show_touches = this.ShowTouches;
            sp.stay_awake = this.StayAwake;
            sp.codec_options = this.CodecOptions;
            sp.encoder_name = this.EncoderName;
            sp.force_adb_forward = this.ForceAdbForward;
            sp.power_off_on_close = this.PowerOffOnClose;
            return sp;
        }
    };
}
