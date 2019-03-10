/*Reference:
 * 
 * https://www.cnblogs.com/MyDevelopNotes/p/5494570.html
 * https://www.cnblogs.com/xielong/p/5710294.html
 * https://ai.baidu.com/forum/topic/show/492634
 * 
*/
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
using System.Windows.Forms.DataVisualization.Charting;


namespace NoiseCapture
{
    public partial class Form1 : Form
    {
        #region 成员数据
        //传感器标定数据
        double Calibration_N = 1.0000;          //噪声传感器，dB/Qstep

        bool Status_SoundCard_Ready;            //声卡准备状态
        bool EnableFileSave = false;            //保存Wave音频文件使能
        bool StartStop = false;                 //按钮运行状态指示，Start：True；Stop：False；

        double NoiseRMS_dB = 0;                 //一显示帧（250ms）噪音幅值；
        
        #endregion
        //建立CaptureProcessing实例
        private CaptureProcessing CaptureData = new CaptureProcessing();
        //建立频域数据分析Complex实例
        private Complex AnalysisData = new Complex();

        #region 对外操作函数
        //===============================================================================================
        public Form1()
        {
            InitializeComponent();
        }
        //===============================================================================================
        private void Form1_Load(object sender, EventArgs e)
        {
            Status_SoundCard_Ready = CaptureData.InitCaptureDevice();
            //this.ControlBox = false;              //不显示最大、最小、关闭按钮
            if (Status_SoundCard_Ready == true)
            {
                this.toolStripStatusLabel1.Text = "计算机声卡准备正常";
                this.toolStripStatusLabel2.Text = "未设置数据保存";
            }
            else
            {
                this.toolStripStatusLabel1.ForeColor = Color.Red;
                this.toolStripStatusLabel1.Text = "计算机声卡准备异常";
                this.toolStripStatusLabel2.Text = "未设置数据保存";
                this.button1.Enabled = false;
                this.button2.Enabled = false;
            }
        }
        //===============================================================================================
        /// <summary>
        /// 保存采集声音文件设置
        /// </summary>
        private void button1_Click(object sender, EventArgs e)
        {
            SaveFileDialog SetSaveFile = new SaveFileDialog();
            SetSaveFile.Filter = "Wave文件(*.wav)|*.wav";
            SetSaveFile.Title = "Save an Sound File";
            SetSaveFile.RestoreDirectory = true;
            SetSaveFile.ShowDialog();  
            // If the file name is not an empty string open it for saving.  
            if (SetSaveFile.FileName != string.Empty)
            {
                CaptureData.FileSaveInfo = SetSaveFile.FileName;
                EnableFileSave = true;
                this.toolStripStatusLabel2.Text = "Wave数据保存" + CaptureData.FileSaveInfo;
            }
        }
        //===============================================================================================
        /// <summary>
        /// 声音采集处理与停止
        /// </summary>
        private void button2_Click(object sender, EventArgs e)
        {            
            InitChart();                       //初始化时域、频域波形显示控件
            if (StartStop == false)//启动声音数据采集
            {
                this.toolStripStatusLabel1.Text = "计算机声卡采集中";
                if (EnableFileSave == true)//保存所采集的声音数据
                {
                    CaptureData.CaptureSaveStart();                    
                }
                else//不保存所采集的声音数据
                {
                    CaptureData.CaptureStart();
                }
                this.button2.ForeColor = Color.Red;
                this.button1.Enabled = false;
                this.button3.Enabled = false;
                this.timer1.Start();
                StartStop = true;
            }
            else if (StartStop == true)//停止声音数据采集
            {
                this.toolStripStatusLabel1.Text = "计算机声卡准备正常";
                if (EnableFileSave == true)//保存所采集的声音数据
                {
                    CaptureData.CaptureSaveStop();
                    EnableFileSave = false;
                }
                else//不保存所采集的声音数据
                {
                    CaptureData.CaptureStop();
                }              
                this.button2.ForeColor = Color.Blue;
                this.timer1.Stop();
                this.button1.Enabled = true;
                this.button3.Enabled = true;
                StartStop = false;
            }
        }
        //===============================================================================================
        /// <summary>
        /// 退出程序
        /// </summary>
        private void button3_Click(object sender, EventArgs e)
        {
            CaptureData = null;
            AnalysisData = null;
            System.Environment.Exit(0);
            this.Close();       //退出程序
        }
        //===============================================================================================
        /// <summary>
        /// 初始化时域、频域波形显示控件
        /// </summary>
        private void InitChart()
        {
            //定义图表区域
            this.chart_T.ChartAreas.Clear();
            ChartArea ChartArea_T = new ChartArea("T1");
            this.chart_T.ChartAreas.Add(ChartArea_T);
            this.chart_F.ChartAreas.Clear();
            ChartArea ChartArea_F = new ChartArea("F1");
            this.chart_F.ChartAreas.Add(ChartArea_F);
            //定义存储和显示点的容器
            this.chart_T.Series.Clear();
            Series series_chart_T_1 = new Series("ScT1");
            series_chart_T_1.ChartArea = "T1";
            this.chart_T.Series.Add(series_chart_T_1);
            this.chart_F.Series.Clear();
            Series series_chart_F_1 = new Series("ScF1");
            series_chart_F_1.ChartArea = "F1";
            this.chart_F.Series.Add(series_chart_F_1);
            //设置图表显示样式
            //this.chart_T.ChartAreas[0].AxisY.Minimum = -32768;
            //this.chart_T.ChartAreas[0].AxisY.Maximum = 32767;
            this.chart_T.ChartAreas[0].AxisY.IsStartedFromZero = false;
            this.chart_T.ChartAreas[0].AxisX.Minimum = 0;
            this.chart_T.ChartAreas[0].AxisX.Maximum = 11025;
            this.chart_T.ChartAreas[0].AxisX.Interval = 1100;
            this.chart_F.ChartAreas[0].AxisY.IsStartedFromZero = false;
            this.chart_F.ChartAreas[0].AxisX.Minimum = 0;
            this.chart_F.ChartAreas[0].AxisX.Maximum = 4096;
            this.chart_F.ChartAreas[0].AxisX.Interval = 400;
           
            //不显示坐标刻度值
            this.chart_T.ChartAreas[0].AxisX.LabelStyle.Enabled = false;
            //this.chart_T.ChartAreas[0].AxisY.LabelStyle.Enabled = false;
            //this.chart_F.ChartAreas[0].AxisX.LabelStyle.Enabled = false;
            //this.chart_F.ChartAreas[0].AxisY.LabelStyle.Enabled = false;
            //设置坐标轴刻度值字号
            //this.chart_T.ChartAreas[0].AxisX.LabelStyle.Font = new Font("Trebuchet MS", 7);
            this.chart_T.ChartAreas[0].AxisY.LabelStyle.Font = new Font("Trebuchet MS", 7);
            this.chart_T.ChartAreas[0].AxisX.Title = "Time";
            this.chart_T.ChartAreas[0].AxisX.TitleFont = new Font("Trebuchet MS", 7);
            //this.chart_T.ChartAreas[0].AxisY.Title = "Amplitude";
            //this.chart_T.ChartAreas[0].AxisY.TitleFont = new Font("Trebuchet MS", 7);
            this.chart_T.ChartAreas[0].AxisX.MajorGrid.LineColor = System.Drawing.Color.Silver;
            this.chart_T.ChartAreas[0].AxisY.MajorGrid.LineColor = System.Drawing.Color.Silver;
            this.chart_F.ChartAreas[0].AxisX.LabelStyle.Font = new Font("Trebuchet MS", 7);
            this.chart_F.ChartAreas[0].AxisY.LabelStyle.Font = new Font("Trebuchet MS", 7);
            this.chart_F.ChartAreas[0].AxisX.Title = "Frequency(Hz)";
            this.chart_F.ChartAreas[0].AxisX.TitleFont = new Font("Trebuchet MS", 7);
            //this.chart_F.ChartAreas[0].AxisY.Title = "Amplitude";
            //this.chart_F.ChartAreas[0].AxisY.TitleFont = new Font("Trebuchet MS", 7);
            this.chart_F.ChartAreas[0].AxisX.MajorGrid.LineColor = System.Drawing.Color.Silver;
            this.chart_F.ChartAreas[0].AxisY.MajorGrid.LineColor = System.Drawing.Color.Silver;
            //设置图表坐标轴样式
            /*
            CustomLabel Label_chart_F = new CustomLabel();
            for (uint i = 0; i < 11; i++)
            {
                Label_chart_F.Text = i.ToString();
                Label_chart_F.ToPosition = i;
                this.chart_F.ChartAreas[0].AxisX.CustomLabels.Add(Label_chart_F);
            }
            */
            //设置图表显示样式
            this.chart_T.Series[0].Color = Color.Red;
            this.chart_F.Series[0].Color = Color.Blue;
            this.chart_T.Series[0].ChartType = SeriesChartType.FastLine;
            this.chart_F.Series[0].ChartType = SeriesChartType.Column;

            this.chart_T.Titles.Clear();
            this.chart_F.Titles.Clear();
            this.chart_T.Series[0].Points.Clear();
            this.chart_F.Series[0].Points.Clear();         
        }
        //===============================================================================================
        /// <summary>
        /// 定时器事件
        /// </summary>
        private void timer1_Tick(object sender, EventArgs e)
        {
            this.chart_T.Series[0].Points.Clear();
            this.chart_F.Series[0].Points.Clear();
            //显示时域声音图像
            for (int i = 0; i < CaptureData.NoiseTimeData.Length; i++)
            {
                NoiseRMS_dB += Math.Pow(CaptureData.NoiseTimeData[i], 2);
                this.chart_T.Series[0].Points.AddXY((i), CaptureData.NoiseTimeData[i]);
            }
            NoiseRMS_dB = NoiseRMS_dB / CaptureData.NoiseTimeData.Length * Calibration_N;                //计算噪音RMS值
            this.textBox_dB.Text = Convert.ToString(Math.Round(Math.Sqrt(NoiseRMS_dB), 2));
            NoiseRMS_dB = 0;
            //频域分析数据准备
            for (int count = 0; count < 8192; count++)
            {
                AnalysisData.NoiseAnalysisDataReal[count] = CaptureData.NoiseTimeData[count + 1410];
                AnalysisData.NoiseAnalysisDataImag[count] = 0;
            }
            //计算频域信息
            AnalysisData.FFT(1, 13, AnalysisData.NoiseAnalysisDataReal, AnalysisData.NoiseAnalysisDataImag);
            //显示频域声音图像
            for (int i = 0; i < AnalysisData.NoiseAnalysisDataReal.Length / 2; i++)
            {
                this.chart_F.Series[0].Points.AddXY((i), Math.Sqrt(Math.Pow(AnalysisData.NoiseAnalysisDataReal[i], 2.0) + Math.Pow(AnalysisData.NoiseAnalysisDataImag[i], 2.0)));
            }
        }

        #endregion
    }//public partial class Form1 : Form
}//namespace NoiseCapture
