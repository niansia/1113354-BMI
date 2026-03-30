using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace _1113354_陳冠瑋_BMI
{
    public partial class Form1 : Form
    {
        private const double UnderweightThreshold = 18.5;
        private const double NormalThreshold = 24;
        private const double OverweightThreshold = 27;
        private const double Obese1Threshold = 30;
        private const double Obese2Threshold = 35;
        private const double DistributionMin = 10;
        private const double DistributionMax = 40;
        private const int SidePanelPreferredWidth = 430;

        private bool isDarkTheme;
        private double animatedBmi;
        private int animatedGauge;
        private double targetBmi;
        private int targetGauge;
        private double latestHeightMeter;
        private readonly List<double> trendBmis = new List<double>();
        private readonly List<DateTime> trendTimes = new List<DateTime>();

        public Form1()
        {
            InitializeComponent();
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
            {
                return;
            }

            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            ApplyModernStyle();
            EnableDoubleBuffer(panelHeader);
            EnableDoubleBuffer(panelDistribution);
            EnableDoubleBuffer(panelTrend);
            EnableDoubleBuffer(panelBmiRing);
            ApplyTheme(false);
            ResetResultDisplay();
            Resize += Form1_Resize;
            btnRun.MouseEnter += BtnRun_MouseEnter;
            btnRun.MouseLeave += BtnRun_MouseLeave;
            btnClear.MouseEnter += BtnClear_MouseEnter;
            btnClear.MouseLeave += BtnClear_MouseLeave;
            panelDistribution.Resize += PanelDistribution_Resize;
            splitContainerMain.SplitterMoved += splitContainerMain_SplitterMoved;
            Shown += Form1_Shown;
            Paint += Form1_Paint;
            clockTimer.Start();
            clockTimer_Tick(this, EventArgs.Empty);
            splitContainerMain.Panel2Collapsed = false;
            ConfigureSplitLayout();
            LayoutHistoryPanel();
            txtHeight.Focus();
        }

        private void EnableDoubleBuffer(Control control)
        {
            if (control == null)
            {
                return;
            }

            var property = typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            if (property != null)
            {
                property.SetValue(control, true, null);
            }
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            ConfigureSplitLayout();
            LayoutHistoryPanel();
        }

        private void splitContainerMain_SplitterMoved(object sender, SplitterEventArgs e)
        {
            ConfigureSplitLayout();
            LayoutHistoryPanel();
        }

        private void ConfigureSplitLayout()
        {
            int totalWidth = splitContainerMain.Width;
            if (totalWidth <= 0)
            {
                return;
            }

            int target = totalWidth - SidePanelPreferredWidth - splitContainerMain.SplitterWidth;
            int min = splitContainerMain.Panel1MinSize;
            int max = totalWidth - splitContainerMain.Panel2MinSize - splitContainerMain.SplitterWidth;
            if (max < min)
            {
                return;
            }

            int distance = Math.Max(min, Math.Min(max, target));
            if (splitContainerMain.SplitterDistance != distance)
            {
                splitContainerMain.SplitterDistance = distance;
            }
        }

        private void LayoutHistoryPanel()
        {
            int width = groupBoxHistory.ClientSize.Width;
            int height = groupBoxHistory.ClientSize.Height;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            const int margin = 14;
            const int top = 34;
            const int spacing = 10;
            const int buttonHeight = 38;
            const int buttonGap = 8;

            double trendRange = trendBmis.Count > 1 ? MaxTrendValue() - MinTrendValue() : 0d;
            double normalizedRange = Math.Min(1d, trendRange / 8d);
            int dynamicExtra = (int)Math.Round(normalizedRange * 70d);
            int baseTrendHeight = Math.Max(130, (int)Math.Round(height * 0.28));
            int maxTrendHeight = Math.Max(130, height - margin * 2 - spacing * 2 - buttonHeight * 2 - buttonGap - 100);
            int trendHeight = Math.Min(maxTrendHeight, baseTrendHeight + dynamicExtra);
            panelTrend.SetBounds(margin, top, width - margin * 2, trendHeight);

            int buttonsTop = height - margin - (buttonHeight * 2 + buttonGap);
            int listTop = panelTrend.Bottom + spacing;
            int listHeight = Math.Max(100, buttonsTop - spacing - listTop);
            listBoxHistory.SetBounds(margin, listTop, width - margin * 2, listHeight);

            btnExportReport.SetBounds(margin, buttonsTop, width - margin * 2, buttonHeight);
            btnCopySummary.SetBounds(margin, buttonsTop + buttonHeight + buttonGap, width - margin * 2, buttonHeight);

            panelTrend.Visible = true;
            listBoxHistory.Visible = true;
            btnExportReport.Visible = true;
            btnCopySummary.Visible = true;
        }

        private void Form1_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            var rect = ClientRectangle;
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            Color c1 = isDarkTheme ? Color.FromArgb(21, 27, 36) : Color.FromArgb(245, 249, 255);
            Color c2 = isDarkTheme ? Color.FromArgb(31, 36, 48) : Color.FromArgb(233, 241, 252);
            using (var brush = new LinearGradientBrush(rect, c1, c2, 95f))
            {
                g.FillRectangle(brush, rect);
            }
        }

        private void btnRun_Click(object sender, EventArgs e)
        {
            if (!TryReadInputs(out double heightMeter, out double weight))
            {
                return;
            }

            double bmi = weight / (heightMeter * heightMeter);
            UpdateResult(bmi, heightMeter);
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            txtHeight.Clear();
            txtWeight.Clear();
            errorProvider1.Clear();
            animationTimer.Stop();
            ResetResultDisplay();
            txtHeight.Focus();
        }

        private void btnTheme_Click(object sender, EventArgs e)
        {
            ApplyTheme(!isDarkTheme);
        }

        private void btnCopySummary_Click(object sender, EventArgs e)
        {
            if (lblResult.Text == "--")
            {
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("BMI 報告");
            sb.AppendLine($"BMI：{lblResult.Text}");
            sb.AppendLine($"判定：{lblCategory.Text}");
            sb.AppendLine($"百分位：{lblPercentile.Text}");
            sb.AppendLine($"風險：{lblRisk.Text}");
            sb.AppendLine(lblHealthyRange.Text);
            sb.AppendLine($"建議：{lblAdvice.Text}");
            sb.AppendLine(lblDeltaToNormal.Text);
            sb.AppendLine(lblTargetWeight.Text);
            Clipboard.SetText(sb.ToString());
            lblHint.Text = "已複製 BMI 報告到剪貼簿";
        }

        private void btnExportReport_Click(object sender, EventArgs e)
        {
            if (trendBmis.Count == 0)
            {
                lblHint.Text = "目前沒有可匯出的資料";
                return;
            }

            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "CSV 檔案 (*.csv)|*.csv|文字檔案 (*.txt)|*.txt";
                dialog.FileName = "BMI_Report_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                var sb = new StringBuilder();
                sb.AppendLine("Time,BMI,Category");
                for (int i = 0; i < trendBmis.Count; i++)
                {
                    string time = trendTimes[i].ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                    string bmi = trendBmis[i].ToString("F2", CultureInfo.InvariantCulture);
                    string category = GetBmiCategory(trendBmis[i]);
                    sb.AppendLine(time + "," + bmi + "," + category);
                }

                File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
                lblHint.Text = "報告已匯出：" + Path.GetFileName(dialog.FileName);
            }
        }

        private void txtInput_TextChanged(object sender, EventArgs e)
        {
            var input = sender as TextBox;
            if (input == null)
            {
                return;
            }

            errorProvider1.SetError(input, string.Empty);
            if (string.IsNullOrWhiteSpace(txtHeight.Text) || string.IsNullOrWhiteSpace(txtWeight.Text))
            {
                ResetResultDisplay();
            }
        }

        private void txtInput_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (char.IsControl(e.KeyChar) || char.IsDigit(e.KeyChar))
            {
                return;
            }

            var input = sender as TextBox;
            if (input != null && e.KeyChar == '.' && !input.Text.Contains("."))
            {
                return;
            }

            e.Handled = true;
        }

        private bool TryReadInputs(out double heightMeter, out double weight)
        {
            heightMeter = 0;
            weight = 0;
            errorProvider1.Clear();

            if (!double.TryParse(txtHeight.Text, out double heightCm) || heightCm <= 0)
            {
                errorProvider1.SetError(txtHeight, "請輸入大於 0 的身高");
                txtHeight.Focus();
                lblCategory.Text = "請修正輸入資料";
                lblCategory.ForeColor = Color.Firebrick;
                lblRisk.Text = "等待輸入";
                return false;
            }

            if (!double.TryParse(txtWeight.Text, out double parsedWeight) || parsedWeight <= 0)
            {
                errorProvider1.SetError(txtWeight, "請輸入大於 0 的體重");
                txtWeight.Focus();
                lblCategory.Text = "請修正輸入資料";
                lblCategory.ForeColor = Color.Firebrick;
                lblRisk.Text = "等待輸入";
                return false;
            }

            heightMeter = heightCm / 100d;
            weight = parsedWeight;
            return true;
        }

        private void UpdateResult(double bmi, double heightMeter)
        {
            latestHeightMeter = heightMeter;
            targetBmi = bmi;
            targetGauge = Math.Max(progressBarBmi.Minimum, Math.Min(progressBarBmi.Maximum, (int)Math.Round(bmi * 10)));
            animationTimer.Start();
            lblCategory.Text = GetBmiCategory(bmi);
            lblCategory.ForeColor = GetCategoryColor(bmi);
            lblHealthyRange.Text = GetHealthyWeightRangeText(heightMeter);
            lblSubtitle.Text = $"目前狀態：{lblCategory.Text}";
            lblCategory.BackColor = GetCategoryBackgroundColor(bmi);
            lblPercentile.Text = GetPercentileText(bmi);
            lblRisk.Text = GetRiskLevelText(bmi);
            lblAdvice.Text = GetAdviceText(bmi);
            lblDeltaToNormal.Text = GetDeltaToNormalText(bmi, heightMeter);
            lblTargetWeight.Text = GetTargetWeightText(heightMeter);
            lblGaugeInfo.Text = "進度條說明：目前 BMI " + bmi.ToString("F1", CultureInfo.InvariantCulture) + "，對應區間「" + GetBmiCategory(bmi) + "」";
            lblDistributionInfo.Text = "色帶說明：藍=過輕、綠=正常、橘=過重、紅=肥胖，指示線會標示目前位置";
            UpdateDistributionMarker(bmi);
            AddTrendPoint(bmi);
            LayoutHistoryPanel();
            AddHistoryItem(bmi);
            panelDistribution.Invalidate();
            panelBmiRing.Invalidate();
            panelTrend.Invalidate();
        }

        private void ResetResultDisplay()
        {
            latestHeightMeter = 0;
            animatedBmi = 0;
            animatedGauge = progressBarBmi.Minimum;
            targetBmi = 0;
            targetGauge = progressBarBmi.Minimum;
            lblResult.Text = "--";
            lblCategory.Text = "請先輸入身高與體重";
            lblCategory.ForeColor = Color.DimGray;
            lblCategory.BackColor = Color.FromArgb(240, 247, 255);
            lblHealthyRange.Text = "正常體重範圍：--";
            lblSubtitle.Text = "輸入資料後立即獲得視覺化分析";
            lblPercentile.Text = "超越比例：--";
            lblRisk.Text = "風險等級：--";
            lblAdvice.Text = "建議：--";
            lblDeltaToNormal.Text = "距離正常區間：--";
            lblTargetWeight.Text = "目標體重：--";
            lblGaugeInfo.Text = "進度條說明：用來顯示 BMI 在 10~40 的位置";
            lblDistributionInfo.Text = "色帶說明：藍=過輕、綠=正常、橘=過重、紅=肥胖";
            progressBarBmi.Value = progressBarBmi.Minimum;
            UpdateDistributionMarker(DistributionMin);
            lblHint.Text = "提示：按 Enter 計算、按 Esc 清除，輸入只接受數字與小數點";
            panelDistribution.Invalidate();
            panelBmiRing.Invalidate();
            panelTrend.Invalidate();
        }

        private void AddTrendPoint(double bmi)
        {
            trendBmis.Add(bmi);
            trendTimes.Add(DateTime.Now);
            while (trendBmis.Count > 30)
            {
                trendBmis.RemoveAt(0);
                trendTimes.RemoveAt(0);
            }
        }

        private string GetBmiCategory(double bmi)
        {
            if (bmi < UnderweightThreshold)
            {
                return "過輕";
            }

            if (bmi < NormalThreshold)
            {
                return "正常";
            }

            if (bmi < OverweightThreshold)
            {
                return "過重";
            }

            if (bmi < Obese1Threshold)
            {
                return "輕度肥胖";
            }

            if (bmi < Obese2Threshold)
            {
                return "中度肥胖";
            }

            return "重度肥胖";
        }

        private Color GetCategoryColor(double bmi)
        {
            if (bmi < UnderweightThreshold)
            {
                return Color.DodgerBlue;
            }

            if (bmi < NormalThreshold)
            {
                return Color.ForestGreen;
            }

            if (bmi < OverweightThreshold)
            {
                return Color.DarkOrange;
            }

            return Color.Firebrick;
        }

        private string GetHealthyWeightRangeText(double heightMeter)
        {
            double min = UnderweightThreshold * heightMeter * heightMeter;
            double max = NormalThreshold * heightMeter * heightMeter;
            return $"正常體重範圍：{min:F1} kg ~ {max:F1} kg";
        }

        private Color GetCategoryBackgroundColor(double bmi)
        {
            if (bmi < UnderweightThreshold)
            {
                return Color.FromArgb(236, 246, 255);
            }

            if (bmi < NormalThreshold)
            {
                return Color.FromArgb(235, 252, 239);
            }

            if (bmi < OverweightThreshold)
            {
                return Color.FromArgb(255, 245, 232);
            }

            return Color.FromArgb(255, 236, 236);
        }

        private string GetPercentileText(double bmi)
        {
            double percentile = 100d / (1d + Math.Exp(-(bmi - 23d) / 2.8d));
            return $"超越約 {percentile:F1}% 成人 BMI";
        }

        private string GetRiskLevelText(double bmi)
        {
            if (bmi < UnderweightThreshold)
            {
                return "風險等級：低體重風險";
            }

            if (bmi < NormalThreshold)
            {
                return "風險等級：理想範圍";
            }

            if (bmi < OverweightThreshold)
            {
                return "風險等級：代謝風險上升";
            }

            if (bmi < Obese1Threshold)
            {
                return "風險等級：中度健康風險";
            }

            if (bmi < Obese2Threshold)
            {
                return "風險等級：高健康風險";
            }

            return "風險等級：極高健康風險";
        }

        private string GetAdviceText(double bmi)
        {
            if (bmi < UnderweightThreshold)
            {
                return "建議：提升蛋白質與阻力訓練，穩定增重";
            }

            if (bmi < NormalThreshold)
            {
                return "建議：維持目前作息與運動，持續追蹤";
            }

            if (bmi < OverweightThreshold)
            {
                return "建議：每週 150 分鐘有氧並控制精緻糖";
            }

            return "建議：先從飲食赤字與規律運動逐步下降";
        }

        private string GetDeltaToNormalText(double bmi, double heightMeter)
        {
            double currentWeight = bmi * heightMeter * heightMeter;
            double minWeight = UnderweightThreshold * heightMeter * heightMeter;
            double maxWeight = NormalThreshold * heightMeter * heightMeter;

            if (bmi < UnderweightThreshold)
            {
                return $"距離正常區間：需增加 {minWeight - currentWeight:F1} kg";
            }

            if (bmi < NormalThreshold)
            {
                return "距離正常區間：已在理想範圍內";
            }

            return $"距離正常區間：需減少 {currentWeight - maxWeight:F1} kg";
        }

        private string GetTargetWeightText(double heightMeter)
        {
            double target = ((UnderweightThreshold + NormalThreshold) / 2d) * heightMeter * heightMeter;
            return $"目標體重：約 {target:F1} kg";
        }

        private void UpdateDistributionMarker(double bmi)
        {
            double normalized = (Math.Max(DistributionMin, Math.Min(DistributionMax, bmi)) - DistributionMin) / (DistributionMax - DistributionMin);
            int x = (int)Math.Round(normalized * (panelDistribution.Width - panelMarker.Width));
            panelMarker.Left = Math.Max(0, Math.Min(panelDistribution.Width - panelMarker.Width, x));
        }

        private void AddHistoryItem(double bmi)
        {
            string item = string.Format(CultureInfo.InvariantCulture, "{0:HH:mm:ss}  BMI {1:F2}  {2}", DateTime.Now, bmi, GetBmiCategory(bmi));
            listBoxHistory.Items.Insert(0, item);
            while (listBoxHistory.Items.Count > 12)
            {
                listBoxHistory.Items.RemoveAt(listBoxHistory.Items.Count - 1);
            }
        }

        private void ApplyModernStyle()
        {
            ApplyRoundedRegion(btnRun, 18);
            ApplyRoundedRegion(btnClear, 16);
            ApplyRoundedRegion(btnTheme, 16);
            ApplyRoundedRegion(btnCopySummary, 16);
            ApplyRoundedRegion(btnExportReport, 16);
            ApplyRoundedRegion(lblResult, 12);
            ApplyRoundedRegion(lblCategory, 12);
            ApplyRoundedRegion(progressBarBmi, 10);
            ApplyRoundedRegion(panelHeader, 22);
            ApplyRoundedRegion(panelDistribution, 12);
            ApplyRoundedRegion(panelTrend, 14);
        }

        private void clockTimer_Tick(object sender, EventArgs e)
        {
            lblDateTime.Text = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss ddd", CultureInfo.GetCultureInfo("zh-TW"));
        }

        private void headerPulseTimer_Tick(object sender, EventArgs e)
        {
            panelHeader.Invalidate();
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            ApplyModernStyle();
            ConfigureSplitLayout();
            LayoutHistoryPanel();
            UpdateDistributionMarker(targetBmi > 0 ? targetBmi : DistributionMin);
            panelBmiRing.Invalidate();
            panelDistribution.Invalidate();
            panelTrend.Invalidate();
        }

        private void PanelDistribution_Resize(object sender, EventArgs e)
        {
            UpdateDistributionMarker(targetBmi > 0 ? targetBmi : DistributionMin);
            panelDistribution.Invalidate();
        }

        private void panelTrend_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(panelTrend.BackColor);

            int headerHeight = 24;
            Rectangle chartArea = new Rectangle(12, headerHeight + 8, panelTrend.Width - 24, panelTrend.Height - headerHeight - 16);
            if (chartArea.Width <= 10 || chartArea.Height <= 10)
            {
                return;
            }

            using (var titleFont = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (var titleBrush = new SolidBrush(isDarkTheme ? Color.FromArgb(220, 232, 249) : Color.FromArgb(59, 83, 113)))
            {
                g.DrawString("Trend", titleFont, titleBrush, 14, 7);
            }

            if (trendBmis.Count == 0)
            {
                using (var font = new Font("微軟正黑體", 9f, FontStyle.Bold))
                using (var brush = new SolidBrush(isDarkTheme ? Color.FromArgb(179, 196, 220) : Color.FromArgb(104, 126, 153)))
                {
                    SizeF textSize = g.MeasureString("尚無資料", font);
                    float textX = chartArea.Left + (chartArea.Width - textSize.Width) / 2f;
                    float textY = chartArea.Top + (chartArea.Height - textSize.Height) / 2f;
                    g.DrawString("尚無資料", font, brush, textX, textY);
                }

                using (var borderPen = new Pen(isDarkTheme ? Color.FromArgb(66, 76, 94) : Color.FromArgb(216, 226, 239)))
                {
                    g.DrawRectangle(borderPen, chartArea);
                }

                return;
            }

            double minValue = MinTrendValue();
            double maxValue = MaxTrendValue();
            double range = maxValue - minValue;
            double padding = range < 0.01 ? 1d : Math.Max(0.6d, range * 0.18d);
            double min = minValue - padding;
            double max = maxValue + padding;
            if (Math.Abs(max - min) < 0.001)
            {
                max = min + 1;
            }

            using (var gridPen = new Pen(isDarkTheme ? Color.FromArgb(66, 76, 94) : Color.FromArgb(216, 226, 239)))
            {
                g.DrawRectangle(gridPen, chartArea);
                for (int i = 1; i <= 3; i++)
                {
                    int y = chartArea.Top + (chartArea.Height * i / 4);
                    g.DrawLine(gridPen, chartArea.Left, y, chartArea.Right, y);
                }
            }

            RectangleF plotArea = new RectangleF(chartArea.Left + 5f, chartArea.Top + 7f, chartArea.Width - 10f, chartArea.Height - 14f);
            if (plotArea.Width <= 4f || plotArea.Height <= 4f)
            {
                return;
            }

            PointF[] points = new PointF[trendBmis.Count];
            for (int i = 0; i < trendBmis.Count; i++)
            {
                float x = plotArea.Left + (plotArea.Width * i / (float)Math.Max(1, trendBmis.Count - 1));
                float y = plotArea.Bottom - (float)((trendBmis[i] - min) / (max - min) * plotArea.Height);
                points[i] = new PointF(x, y);
            }

            using (var fillBrush = new SolidBrush(isDarkTheme ? Color.FromArgb(28, 99, 182, 255) : Color.FromArgb(34, 35, 131, 219)))
            {
                if (points.Length > 1)
                {
                    PointF[] areaPoints = new PointF[points.Length + 2];
                    for (int i = 0; i < points.Length; i++)
                    {
                        areaPoints[i] = points[i];
                    }

                    areaPoints[points.Length] = new PointF(points[points.Length - 1].X, plotArea.Bottom);
                    areaPoints[points.Length + 1] = new PointF(points[0].X, plotArea.Bottom);
                    g.FillPolygon(fillBrush, areaPoints);
                }
            }

            using (var linePen = new Pen(isDarkTheme ? Color.FromArgb(99, 182, 255) : Color.FromArgb(35, 131, 219), 2.2f))
            {
                if (points.Length > 1)
                {
                    g.DrawLines(linePen, points);
                }
            }

            using (var pointBrush = new SolidBrush(isDarkTheme ? Color.FromArgb(166, 216, 255) : Color.FromArgb(35, 131, 219)))
            {
                for (int i = 0; i < points.Length; i++)
                {
                    g.FillEllipse(pointBrush, points[i].X - 2.6f, points[i].Y - 2.6f, 5.2f, 5.2f);
                }
            }

            PointF latest = points[points.Length - 1];
            using (var markerBrush = new SolidBrush(isDarkTheme ? Color.FromArgb(120, 207, 141) : Color.FromArgb(22, 166, 91)))
            {
                g.FillEllipse(markerBrush, latest.X - 4.5f, latest.Y - 4.5f, 9, 9);
            }

            using (var font = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (var brush = new SolidBrush(isDarkTheme ? Color.FromArgb(220, 232, 249) : Color.FromArgb(59, 83, 113)))
            {
                string latestText = trendBmis[trendBmis.Count - 1].ToString("F2", CultureInfo.InvariantCulture);
                SizeF latestSize = g.MeasureString(latestText, font);
                g.DrawString(latestText, font, brush, chartArea.Right - latestSize.Width, 7);
            }
        }

        private double MinTrendValue()
        {
            double min = trendBmis[0];
            for (int i = 1; i < trendBmis.Count; i++)
            {
                if (trendBmis[i] < min)
                {
                    min = trendBmis[i];
                }
            }
            return min;
        }

        private double MaxTrendValue()
        {
            double max = trendBmis[0];
            for (int i = 1; i < trendBmis.Count; i++)
            {
                if (trendBmis[i] > max)
                {
                    max = trendBmis[i];
                }
            }
            return max;
        }

        private void panelHeader_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = panelHeader.ClientRectangle;

            Color c1 = isDarkTheme ? Color.FromArgb(27, 64, 113) : Color.FromArgb(23, 108, 196);
            Color c2 = isDarkTheme ? Color.FromArgb(18, 44, 81) : Color.FromArgb(17, 89, 164);
            using (var bg = new LinearGradientBrush(rect, c1, c2, 25f))
            {
                g.FillRectangle(bg, rect);
            }

            using (var glow = new SolidBrush(Color.FromArgb(28, Color.White)))
            {
                g.FillEllipse(glow, rect.Width - 360, -140, 520, 260);
                g.FillEllipse(glow, -220, -120, 420, 220);
            }

            using (var stroke = new Pen(isDarkTheme ? Color.FromArgb(62, 95, 140) : Color.FromArgb(71, 135, 205), 1.2f))
            {
                g.DrawRectangle(stroke, 0, 0, rect.Width - 1, rect.Height - 1);
            }
        }

        private void panelDistribution_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (var blue = new SolidBrush(Color.FromArgb(72, 161, 255)))
            using (var green = new SolidBrush(Color.FromArgb(86, 200, 114)))
            using (var orange = new SolidBrush(Color.FromArgb(247, 167, 73)))
            using (var red = new SolidBrush(Color.FromArgb(237, 104, 104)))
            {
                int w = panelDistribution.Width;
                int h = panelDistribution.Height;
                int x1 = (int)Math.Round((UnderweightThreshold - DistributionMin) / (DistributionMax - DistributionMin) * w);
                int x2 = (int)Math.Round((NormalThreshold - DistributionMin) / (DistributionMax - DistributionMin) * w);
                int x3 = (int)Math.Round((OverweightThreshold - DistributionMin) / (DistributionMax - DistributionMin) * w);

                g.FillRectangle(blue, 0, 0, Math.Max(1, x1), h);
                g.FillRectangle(green, Math.Max(0, x1), 0, Math.Max(1, x2 - x1), h);
                g.FillRectangle(orange, Math.Max(0, x2), 0, Math.Max(1, x3 - x2), h);
                g.FillRectangle(red, Math.Max(0, x3), 0, Math.Max(1, w - x3), h);
            }

            using (var borderPen = new Pen(isDarkTheme ? Color.FromArgb(90, 101, 119) : Color.FromArgb(197, 210, 226)))
            {
                g.DrawRectangle(borderPen, 0, 0, panelDistribution.Width - 1, panelDistribution.Height - 1);
            }
        }

        private void panelBmiRing_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(panelBmiRing.BackColor);

            int size = Math.Min(panelBmiRing.Width, panelBmiRing.Height) - 24;
            if (size <= 40)
            {
                return;
            }

            int x = (panelBmiRing.Width - size) / 2;
            int y = (panelBmiRing.Height - size) / 2;
            var ring = new Rectangle(x, y, size, size);

            DrawRingSegment(g, ring, DistributionMin, UnderweightThreshold, Color.FromArgb(72, 161, 255));
            DrawRingSegment(g, ring, UnderweightThreshold, NormalThreshold, Color.FromArgb(86, 200, 114));
            DrawRingSegment(g, ring, NormalThreshold, OverweightThreshold, Color.FromArgb(247, 167, 73));
            DrawRingSegment(g, ring, OverweightThreshold, DistributionMax, Color.FromArgb(237, 104, 104));

            double value = targetBmi > 0 ? animatedBmi : DistributionMin;
            float angle = ValueToAngle(value);
            Point center = new Point(ring.Left + ring.Width / 2, ring.Top + ring.Height / 2);
            int radius = ring.Width / 2;
            double rad = (angle - 90) * Math.PI / 180.0;
            Point needle = new Point(
                center.X + (int)((radius - 6) * Math.Cos(rad)),
                center.Y + (int)((radius - 6) * Math.Sin(rad)));

            using (var pen = new Pen(isDarkTheme ? Color.WhiteSmoke : Color.FromArgb(40, 58, 81), 2.5f))
            using (var dot = new SolidBrush(isDarkTheme ? Color.WhiteSmoke : Color.FromArgb(40, 58, 81)))
            {
                g.DrawLine(pen, center, needle);
                g.FillEllipse(dot, center.X - 5, center.Y - 5, 10, 10);
            }

            string valueText = targetBmi > 0 ? $"{targetBmi:F1}" : "--";
            using (var fontMain = new Font("Segoe UI", 15f, FontStyle.Bold))
            using (var fontSub = new Font("微軟正黑體", 8.5f, FontStyle.Bold))
            using (var brushText = new SolidBrush(isDarkTheme ? Color.WhiteSmoke : Color.FromArgb(51, 73, 101)))
            {
                SizeF mainSize = g.MeasureString(valueText, fontMain);
                g.DrawString(valueText, fontMain, brushText, center.X - mainSize.Width / 2f, center.Y - mainSize.Height * 0.85f);
                g.DrawString("BMI", fontSub, brushText, center.X - 13, center.Y + 12);
            }
        }

        private void DrawRingSegment(Graphics g, Rectangle ring, double from, double to, Color color)
        {
            using (var pen = new Pen(color, 13f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                float start = ValueToAngle(from) - 90f;
                float sweep = ValueToAngle(to) - ValueToAngle(from);
                g.DrawArc(pen, ring, start, sweep);
            }
        }

        private float ValueToAngle(double value)
        {
            double clamped = Math.Max(DistributionMin, Math.Min(DistributionMax, value));
            double normalized = (clamped - DistributionMin) / (DistributionMax - DistributionMin);
            return (float)(normalized * 360d);
        }

        private void ApplyRoundedRegion(Control control, int radius)
        {
            if (control.Width <= 0 || control.Height <= 0)
            {
                return;
            }

            using (GraphicsPath path = new GraphicsPath())
            {
                int diameter = radius * 2;
                path.AddArc(0, 0, diameter, diameter, 180, 90);
                path.AddArc(control.Width - diameter, 0, diameter, diameter, 270, 90);
                path.AddArc(control.Width - diameter, control.Height - diameter, diameter, diameter, 0, 90);
                path.AddArc(0, control.Height - diameter, diameter, diameter, 90, 90);
                path.CloseFigure();
                control.Region = new Region(path);
            }
        }

        private void BtnRun_MouseEnter(object sender, EventArgs e)
        {
            btnRun.BackColor = Color.FromArgb(38, 122, 198);
        }

        private void BtnRun_MouseLeave(object sender, EventArgs e)
        {
            btnRun.BackColor = isDarkTheme ? Color.FromArgb(67, 146, 223) : Color.FromArgb(27, 111, 186);
        }

        private void BtnClear_MouseEnter(object sender, EventArgs e)
        {
            btnClear.BackColor = isDarkTheme ? Color.FromArgb(66, 75, 92) : Color.FromArgb(224, 236, 248);
        }

        private void BtnClear_MouseLeave(object sender, EventArgs e)
        {
            btnClear.BackColor = isDarkTheme ? Color.FromArgb(53, 61, 76) : Color.FromArgb(239, 245, 250);
        }

        private void animationTimer_Tick(object sender, EventArgs e)
        {
            bool bmiDone = Math.Abs(animatedBmi - targetBmi) < 0.01;
            if (!bmiDone)
            {
                animatedBmi += (targetBmi - animatedBmi) * 0.24;
            }
            else
            {
                animatedBmi = targetBmi;
            }

            if (animatedGauge != targetGauge)
            {
                animatedGauge += Math.Sign(targetGauge - animatedGauge) * Math.Max(1, Math.Abs(targetGauge - animatedGauge) / 5);
            }
            else
            {
                animatedGauge = targetGauge;
            }

            lblResult.Text = targetBmi <= 0 ? "--" : $"{animatedBmi:F2}";
            progressBarBmi.Value = Math.Max(progressBarBmi.Minimum, Math.Min(progressBarBmi.Maximum, animatedGauge));
            panelBmiRing.Invalidate();

            if (Math.Abs(animatedBmi - targetBmi) < 0.02 && animatedGauge == targetGauge)
            {
                lblResult.Text = targetBmi <= 0 ? "--" : $"{targetBmi:F2}";
                progressBarBmi.Value = targetGauge;
                animationTimer.Stop();
            }
        }

        private void ApplyTheme(bool dark)
        {
            isDarkTheme = dark;

            if (dark)
            {
                BackColor = Color.FromArgb(22, 27, 36);
                panelHeader.BackColor = Color.FromArgb(31, 61, 105);
                lblTitle.ForeColor = Color.White;
                lblSubtitle.ForeColor = Color.FromArgb(189, 214, 247);
                lblDateTime.ForeColor = Color.FromArgb(203, 224, 250);
                groupBox1.BackColor = Color.FromArgb(31, 38, 50);
                groupBox2.BackColor = Color.FromArgb(31, 38, 50);
                groupBoxHistory.BackColor = Color.FromArgb(31, 38, 50);
                groupBox1.ForeColor = Color.FromArgb(231, 238, 248);
                groupBox2.ForeColor = Color.FromArgb(231, 238, 248);
                groupBoxHistory.ForeColor = Color.FromArgb(231, 238, 248);
                txtHeight.BackColor = Color.FromArgb(45, 53, 67);
                txtWeight.BackColor = Color.FromArgb(45, 53, 67);
                txtHeight.ForeColor = Color.WhiteSmoke;
                txtWeight.ForeColor = Color.WhiteSmoke;
                btnRun.BackColor = Color.FromArgb(67, 146, 223);
                btnClear.BackColor = Color.FromArgb(53, 61, 76);
                btnClear.ForeColor = Color.WhiteSmoke;
                btnTheme.BackColor = Color.FromArgb(50, 58, 72);
                btnTheme.ForeColor = Color.WhiteSmoke;
                btnCopySummary.BackColor = Color.FromArgb(50, 58, 72);
                btnCopySummary.ForeColor = Color.WhiteSmoke;
                btnExportReport.BackColor = Color.FromArgb(50, 58, 72);
                btnExportReport.ForeColor = Color.WhiteSmoke;
                panelBmiRing.BackColor = Color.FromArgb(37, 46, 60);
                panelDistribution.BackColor = Color.FromArgb(56, 64, 80);
                panelTrend.BackColor = Color.FromArgb(44, 53, 67);
                lblHint.ForeColor = Color.FromArgb(166, 179, 199);
                listBoxHistory.BackColor = Color.FromArgb(38, 46, 58);
                listBoxHistory.ForeColor = Color.FromArgb(226, 236, 248);
            }
            else
            {
                BackColor = Color.FromArgb(245, 249, 255);
                panelHeader.BackColor = Color.FromArgb(19, 93, 169);
                lblTitle.ForeColor = Color.White;
                lblSubtitle.ForeColor = Color.FromArgb(224, 241, 255);
                lblDateTime.ForeColor = Color.FromArgb(224, 241, 255);
                groupBox1.BackColor = Color.White;
                groupBox2.BackColor = Color.White;
                groupBoxHistory.BackColor = Color.White;
                groupBox1.ForeColor = Color.FromArgb(56, 73, 95);
                groupBox2.ForeColor = Color.FromArgb(56, 73, 95);
                groupBoxHistory.ForeColor = Color.FromArgb(56, 73, 95);
                txtHeight.BackColor = Color.FromArgb(248, 252, 255);
                txtWeight.BackColor = Color.FromArgb(248, 252, 255);
                txtHeight.ForeColor = Color.FromArgb(44, 56, 74);
                txtWeight.ForeColor = Color.FromArgb(44, 56, 74);
                btnRun.BackColor = Color.FromArgb(27, 111, 186);
                btnClear.BackColor = Color.FromArgb(239, 245, 250);
                btnClear.ForeColor = Color.FromArgb(41, 64, 92);
                btnTheme.BackColor = Color.FromArgb(234, 242, 252);
                btnTheme.ForeColor = Color.FromArgb(41, 64, 92);
                btnCopySummary.BackColor = Color.FromArgb(234, 242, 252);
                btnCopySummary.ForeColor = Color.FromArgb(41, 64, 92);
                btnExportReport.BackColor = Color.FromArgb(234, 242, 252);
                btnExportReport.ForeColor = Color.FromArgb(41, 64, 92);
                panelBmiRing.BackColor = Color.White;
                panelDistribution.BackColor = Color.FromArgb(230, 238, 248);
                panelTrend.BackColor = Color.FromArgb(241, 247, 255);
                lblHint.ForeColor = Color.DimGray;
                listBoxHistory.BackColor = Color.FromArgb(246, 251, 255);
                listBoxHistory.ForeColor = Color.FromArgb(55, 74, 98);
            }

            btnTheme.Text = isDarkTheme ? "淺色模式" : "深色模式";
            ApplyModernStyle();
            panelBmiRing.Invalidate();
            panelDistribution.Invalidate();
            panelTrend.Invalidate();
            panelHeader.Invalidate();
            Invalidate();
        }
    }
}
