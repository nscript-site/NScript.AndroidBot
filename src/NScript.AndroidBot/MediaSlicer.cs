using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;

namespace NScript.AndroidBot
{
    using Geb.Image;
    using Geb.Media.IO;

    public class MediaSlicer
    {
        private Object SyncRoot = new object();
        private MediaWriter MediaWriter;

        public double FrameRate { get; set; }

        private String _baseDir = "./slicer";

        private DateTime Start = DateTime.Now;

        /// <summary>
        /// 切片的最大长度，单位是秒
        /// </summary>
        public double MaxDuration { get; set; } = 600;

        public bool Enable { get; set; }

        private List<Tuple<double, String>> History = new ();

        public Tuple<double, String>[] GetHistory()
        {
            lock(SyncRoot)
            {
                return History.ToArray();
            }
        }

        public String BaseDir
        {
            get { return _baseDir; }
            set
            {
                if(_baseDir != value)
                {
                    DirectoryInfo dirInfo = new DirectoryInfo(value);
                    if (dirInfo.Exists == false) dirInfo.Create();

                    lock(SyncRoot)
                    {
                        _baseDir = value;

                        if (MediaWriter != null)
                        {
                            MediaWriter.Close();
                            MediaWriter = null;
                        }
                    }
                }
            }
        }

        private String PrevFileName = String.Empty;

        public void Receive(ImageBgr24 image) 
        {
            if (Enable == false) return;

            lock(SyncRoot)
            {
                DateTime now = DateTime.Now;
                if(MediaWriter == null || (now - Start).TotalSeconds > MaxDuration)
                {
                    if (MediaWriter != null)
                    {
                        History.Add(new Tuple<double, string>(MaxDuration, PrevFileName));
                        
                        while (History.Count > 5)
                            History.RemoveAt(0);

                        MediaWriter.Close();
                    }

                    DirectoryInfo dirInfo = new DirectoryInfo(_baseDir);
                    if (dirInfo.Exists == false) dirInfo.Create();
                    FileInfo file = new FileInfo(Path.Combine(dirInfo.FullName, now.ToFileTimeUtc() + ".mp4"));
                    PrevFileName = file.Name;
                    MediaWriter = new MediaWriter(file.FullName, image.Width, image.Height, FrameRate);
                    Start = DateTime.Now;
                    lastAudioOffset = 0;
                }

                MediaWriter.WriteFrame(image);
            }
        }

        private double lastAudioOffset = 0;

        public unsafe void Receive(Byte[] audioData)
        {
            if (Enable == false) return;
            double duration = (audioData.Length * 1000) / (2.8*4*44100);

            lock (SyncRoot)
            {
                if (MediaWriter != null)
                {
                    MediaWriter.WriteAudio(audioData, audioData.Length, 0);
                    lastAudioOffset += duration;
                }
            }
        }
    }
}
