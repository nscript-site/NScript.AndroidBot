using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NScript.AndroidBot
{
    enum sc_lock_video_orientation
    {
        SC_LOCK_VIDEO_ORIENTATION_UNLOCKED = -1,
        // lock the current orientation when scrcpy starts
        SC_LOCK_VIDEO_ORIENTATION_INITIAL = -2,
        SC_LOCK_VIDEO_ORIENTATION_0 = 0,
        SC_LOCK_VIDEO_ORIENTATION_1,
        SC_LOCK_VIDEO_ORIENTATION_2,
        SC_LOCK_VIDEO_ORIENTATION_3,
    };

    public class BotOptions
    {
        String serial;
        String crop;
        String record_filename;
        String window_title;
        String push_target;
        String render_driver;
        String codec_options;
        String encoder_name;
        String v4l2_device;
        LogLevel log_level;
        //enum sc_record_format record_format;
        ScPortRange port_range = new ScPortRange { first = 27183, last = 27199 };
        //struct sc_shortcut_mods shortcut_mods;
        UInt16 max_size = 1024;
        UInt32 bit_rate = 8000000;
        UInt16 max_fps;
        sc_lock_video_orientation lock_video_orientation;
        byte rotation;
        Int16 window_x; // SC_WINDOW_POSITION_UNDEFINED for "auto"
        Int16 window_y; // SC_WINDOW_POSITION_UNDEFINED for "auto"
        UInt16 window_width;
        UInt16 window_height;
        UInt32 display_id;
        bool show_touches;
        bool fullscreen;
        bool always_on_top;
        bool control = true;
        bool display;
        bool turn_screen_off;
        bool prefer_text;
        bool window_borderless;
        bool mipmaps;
        bool stay_awake;
        bool force_adb_forward;
        bool disable_screensaver;
        bool forward_key_repeat;
        bool forward_all_clicks;
        bool legacy_paste;
        bool power_off_on_close;

        public ServerParams ToServerParams()
        {
            ServerParams sp = new ServerParams();
            sp.serial = this.serial;
            sp.log_level = this.log_level;
            sp.crop = this.crop;
            sp.port_range = this.port_range;
            sp.max_size = this.max_size;
            sp.bit_rate = this.bit_rate;
            sp.max_fps = this.max_fps;
            sp.lock_video_orientation = (byte)this.lock_video_orientation;
            sp.control = this.control;
            sp.display_id = this.display_id;
            sp.show_touches = this.show_touches;
            sp.stay_awake = this.stay_awake;
            sp.codec_options = this.codec_options;
            sp.encoder_name = this.encoder_name;
            sp.force_adb_forward = this.force_adb_forward;
            sp.power_off_on_close = this.power_off_on_close;
            return sp;
        }
    };
}
