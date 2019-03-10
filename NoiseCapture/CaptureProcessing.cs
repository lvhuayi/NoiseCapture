using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
//后期添加
using System.Runtime.InteropServices;
using System.IO;
using System.Threading;
using Microsoft.DirectX;
using Microsoft.DirectX.DirectSound;

namespace NoiseCapture
{
    class CaptureProcessing
    {
        #region 成员数据
        private Capture CapDevNoise = null;                         // 音频捕捉设备  
        private CaptureBuffer RecBuffer = null;                     // 缓冲区对象  
        private WaveFormat WaveRecFormat;                           // 录音格式  

        private int NextCaptureOffset = 0;                          // 该次录音缓冲区的起始点  
        private int SampleCount = 0;                                // 录制的样本数目  

        private Notify MsgNotify = null;                            // 消息通知对象  
        private const int NotifyNum = 16;                           // 通知的个数  
        private int NotifySize = 0;                                 // 每次通知大小  
        private int BufferSize = 0;                                 // 缓冲队列大小  
        private Thread NotifyThread = null;                         // 处理缓冲区消息的线程  
        private AutoResetEvent NotificationEvent = null;            // 通知事件  
        
        public short[] NoiseTimeData = new short[11025];            // 用于显示分析的声音数据
        public string FileSaveInfo = string.Empty;                  // 文件保存路径  
        private FileStream WaveData = null;                         // 文件流  
        private BinaryWriter FileWriter = null;                     // 写文件 
        private bool MarkFileSave = false;                          // 声音文件保存标记
        #endregion

        #region 对外操作函数
        /// <summary>
        /// 构造函数 NoiseProcessing
        /// </summary>
        public CaptureProcessing()
        {
            // 设定录音格式
            WaveRecFormat = SetWaveFormat();
        }
        //===============================================================================================
        /// <summary>
        /// 创建录音PCM格式，此处使用16bit,44.1kHz,Mono的录音格式
        /// </summary>
        /// <returns>WaveFormat结构体</returns>
        private WaveFormat SetWaveFormat()
        {
            WaveFormat WaveRec = new WaveFormat();
            WaveRec.FormatTag = WaveFormatTag.Pcm;   // PCM音频类型
            WaveRec.SamplesPerSecond = 44100;        // 采样率(单位：Hz)典型值：11025、22050、44100Hz
            WaveRec.BitsPerSample = 16;              // 采样位数：8、16
            WaveRec.Channels = 1;                    // 声道：Mono
            WaveRec.BlockAlign = (short)(WaveRec.Channels * (WaveRec.BitsPerSample / 8));
            WaveRec.AverageBytesPerSecond = WaveRec.BlockAlign * WaveRec.SamplesPerSecond;
            return WaveRec;
        }
        //===============================================================================================
        /// <summary>  
        /// 初始化录音设备,此处使用主录音设备  
        /// </summary>  
        /// <returns>调用成功返回true,否则返回false</returns>  
        public bool InitCaptureDevice()
        {
            // 获取默认音频捕捉设备
            CaptureDevicesCollection devices = new CaptureDevicesCollection();  // 枚举音频捕捉设备
            Guid DeviceGuid = Guid.Empty;                                       // 音频捕捉设备的ID
            if (devices.Count > 0)
                DeviceGuid = devices[0].DriverGuid;
            else
            {
                MessageBox.Show("系统中无音频采集设备");
                return false;
            }

            // 用指定的捕捉设备创建Capture对象
            try
            {
                CapDevNoise = new Capture(DeviceGuid);
            }
            catch (DirectXException e)
            {
                MessageBox.Show(e.ToString());
                return false;
            }
            // 创建声音采集缓冲区
            CreateNoiseBuffer();
            // 建立通知消息,当缓冲区满时进行处理
            InitNotifications();
            return true;
        }
        //===============================================================================================
        /// <summary>
        /// 创建录音使用的缓冲区
        /// </summary>
        public void CreateNoiseBuffer()
        {
            // 缓冲区的描述对象
            CaptureBufferDescription NoiseBufferDescription = new CaptureBufferDescription();
            if (null != MsgNotify)
            {
                MsgNotify.Dispose();
                MsgNotify = null;
            }
            if (null != RecBuffer)
            {
                RecBuffer.Dispose();
                RecBuffer = null;
            }
            // 设定通知大小,默认为0.25s采集时长数据
            //NotifySize = (1024 > WaveRecFormat.AverageBytesPerSecond / 4) ? 1024 : (WaveRecFormat.AverageBytesPerSecond / 4);
            NotifySize = WaveRecFormat.AverageBytesPerSecond / 4;
            NotifySize -= NotifySize % WaveRecFormat.BlockAlign;
            // 设定缓冲区大小（44.1kHz, 16Bits, 0.25s, = 352.8kB）
            BufferSize = NotifySize * NotifyNum;
            // 创建缓冲区描述           
            NoiseBufferDescription.BufferBytes = BufferSize;
            NoiseBufferDescription.Format = WaveRecFormat;           // 录音格式
            // 创建缓冲区
            RecBuffer = new CaptureBuffer(NoiseBufferDescription, CapDevNoise);
            NextCaptureOffset = 0;
        }
        //===============================================================================================
        /// <summary>
        /// 初始化通知事件,将原缓冲区分成16个缓冲队列,在每个缓冲队列的结束点设定通知点.
        /// </summary>
        /// <returns>是否成功</returns>
        public bool InitNotifications()
        {
            if (null == RecBuffer)
            {
                MessageBox.Show("未创建录音缓冲区");
                return false;
            }
            // 创建一个通知事件,当缓冲队列满了就激发该事件.
            NotificationEvent = new AutoResetEvent(false);
            // 创建一个线程管理缓冲区事件
            if (null == NotifyThread)
            {
                NotifyThread = new Thread(WaitThread);
                NotifyThread.Start();
            }
            // 设定通知的位置
            BufferPositionNotify[] PositionNotify = new BufferPositionNotify[NotifyNum];
            for (int i = 0; i < NotifyNum; i++)
            {
                PositionNotify[i].Offset = (NotifySize * i) + NotifySize - 1;
                PositionNotify[i].EventNotifyHandle = NotificationEvent.SafeWaitHandle.DangerousGetHandle();
            }
            MsgNotify = new Notify(RecBuffer);
            MsgNotify.SetNotificationPositions(PositionNotify, NotifyNum);
            return true;
        }
        //===============================================================================================
        /// <summary>
        /// 接收缓冲区满消息的处理线程
        /// </summary>
        private void WaitThread()
        {
            while (true)
            {
                // 等待缓冲区的通知消息
                NotificationEvent.WaitOne(Timeout.Infinite, true);
                // 从缓冲区取数据分析
                CapturedNoiseProc();

            }
        }
        //===============================================================================================
        /// <summary>
        /// 从缓冲区取数据
        /// </summary>
        public void CapturedNoiseProc()
        {
            byte[] CaptureNoiseData = null;                      //用于保存采集声音数据
            int ReadPos = 0;
            int CapturePos = 0;
            int LockSize = 0;
            RecBuffer.GetCurrentPosition(out CapturePos, out ReadPos);
            LockSize = ReadPos - NextCaptureOffset;
            if (LockSize < 0)
                LockSize += BufferSize;
            // 对齐缓冲区边界,实际上由于开始设定完整,这个操作是多余的.
            LockSize -= (LockSize % NotifySize);
            if (0 == LockSize)
                return;
            // 读取缓冲区内的数据
            CaptureNoiseData = (byte[])RecBuffer.Read(NextCaptureOffset, typeof(byte), LockFlag.None, LockSize);     
            //将缓冲区数据存入时域显示数组
            NoiseTimeData = (short[])RecBuffer.Read(NextCaptureOffset, typeof(short), LockFlag.None, LockSize/2);
            /*//等价代码
            for (uint count = 0; count <= (CaptureNoiseData.Length/2-1); count++)
            {
                NoiseTimeData[count] = (Int16)(CaptureNoiseData[2 * count + 1] << 8);
                NoiseTimeData[count] += CaptureNoiseData[2*count];
            }
            */
            if (MarkFileSave == true)
            {
                // 保存声音为Wave文件
                FileWriter.Write(CaptureNoiseData, 0, CaptureNoiseData.Length);
            }          
            // 更新已经录制的数据长度.
            SampleCount += CaptureNoiseData.Length;
            // 移动录制数据的起始点,通知消息只负责指示产生消息的位置,并不记录上次录制的位置
            NextCaptureOffset += CaptureNoiseData.Length;
            NextCaptureOffset %= BufferSize; // Circular buffer
        }
        //===============================================================================================
        /// <summary>
        /// 声音采集
        /// </summary>
        public void CaptureStart()
        {          
            //启动数据记录存入缓冲区
            RecBuffer.Start(true);
        }
        /// <summary>
        /// 声音采集保存
        /// </summary>
        public void CaptureSaveStart()
        {
            // 创建声音保存文件
            CreateWaveFile(FileSaveInfo);           
            //启动数据记录存入缓冲区
            RecBuffer.Start(true);
            //保存文件标记置位
            MarkFileSave = true;
        }
        /// <summary>
        /// 停止声音采集
        /// </summary>
        public void CaptureStop()
        {
            // 关闭通知消息
            if (null != NotificationEvent)
                NotificationEvent.Set();
            // 停止录音
            RecBuffer.Stop();
        }
        /// <summary>
        /// 停止声音采集保存
        /// </summary>
        public void CaptureSaveStop()
        {
            // 关闭通知消息
            if (null != NotificationEvent)
                NotificationEvent.Set();
            // 停止录音
            RecBuffer.Stop();
            // 写入缓冲区最后的数据
            CapturedNoiseProc();
            // 回写长度信息
            FileWriter.Seek(4, SeekOrigin.Begin);
            FileWriter.Write((int)(SampleCount + 36));   // 写文件长度
            FileWriter.Seek(40, SeekOrigin.Begin);
            FileWriter.Write(SampleCount);                // 写数据长度
            FileWriter.Close();
            WaveData.Close();
            FileWriter = null;
            WaveData = null;
            //保存文件标记复位
            MarkFileSave = false;
        }
        //===============================================================================================
        //===============================================================================================
        /// <summary>
        /// 创建需保存的Wave文件,写入必要文件头
        /// </summary>
        public void CreateWaveFile(string strFileName)
        {
            WaveData = new FileStream(strFileName, FileMode.CreateNew);
            FileWriter = new BinaryWriter(WaveData);
            /**************************************************************************
               Here is where the file will be created. A
               wave file is a RIFF file, which has chunks
               of data that describe what the file contains.
               A wave RIFF file is put together like this:
               The 12 byte RIFF chunk is constructed like this:
                Bytes 0 - 3 :  'R' 'I' 'F' 'F'
                Bytes 4 - 7 :  Length of file, minus the first 8 bytes of the RIFF description.
                     (4 bytes for "WAVE" + 24 bytes for format chunk length + bytes for data chunk description + actual sample data size.)
                Bytes 8 - 11: 'W' 'A' 'V' 'E'
                The 24 byte FORMAT chunk is constructed like this:
                Bytes 0 - 3 : 'f' 'm' 't' ' '
                Bytes 4 - 7 : The format chunk length. This is always 16.
                Bytes 8 - 9 : File padding. Always 1.
                Bytes 10- 11: Number of channels. Either 1 for mono,  or 2 for stereo.
                Bytes 12- 15: Sample rate.
                Bytes 16- 19: Number of bytes per second.
                Bytes 20- 21: Bytes per sample. 1 for 8 bit mono, 2 for 8 bit stereo or bit mono, 4 for 16 bit stereo.
                Bytes 22- 23: Number of bits per sample.
                The DATA chunk is constructed like this:
                Bytes 0 - 3 : 'd' 'a' 't' 'a'
                Bytes 4 - 7 : Length of data, in bytes.
                Bytes 8 -: Actual sample data.
              ***************************************************************************/
            char[] ChunkRiff = { 'R', 'I', 'F', 'F' };
            char[] ChunkType = { 'W', 'A', 'V', 'E' };
            char[] ChunkFmt = { 'f', 'm', 't', ' ' };
            char[] ChunkData = { 'd', 'a', 't', 'a' };
            short shPad = 1;                // File padding
            int nFormatChunkLength = 0x10;  // Format chunk length.
            int nLength = 0;                // File length, minus first 8 bytes of RIFF description. This will be filled in later.
            short shBytesPerSample = 0;     // Bytes per sample.
            // 一个样本点的字节数目
            if (8 == WaveRecFormat.BitsPerSample && 1 == WaveRecFormat.Channels)
                shBytesPerSample = 1;
            else if ((8 == WaveRecFormat.BitsPerSample && 2 == WaveRecFormat.Channels) || (16 == WaveRecFormat.BitsPerSample && 1 == WaveRecFormat.Channels))
                shBytesPerSample = 2;
            else if (16 == WaveRecFormat.BitsPerSample && 2 == WaveRecFormat.Channels)
                shBytesPerSample = 4;
            // RIFF 块
            FileWriter.Write(ChunkRiff);
            FileWriter.Write(nLength);
            FileWriter.Write(ChunkType);
            // WAVE块
            FileWriter.Write(ChunkFmt);
            FileWriter.Write(nFormatChunkLength);
            FileWriter.Write(shPad);
            FileWriter.Write(WaveRecFormat.Channels);
            FileWriter.Write(WaveRecFormat.SamplesPerSecond);
            FileWriter.Write(WaveRecFormat.AverageBytesPerSecond);
            FileWriter.Write(shBytesPerSample);
            FileWriter.Write(WaveRecFormat.BitsPerSample);
            // 数据块
            FileWriter.Write(ChunkData);
            FileWriter.Write((int)0);   // The sample length will be written in later.
        }
        #endregion
    }//class CaptureProcessing
}//namespace NoiseCapture
