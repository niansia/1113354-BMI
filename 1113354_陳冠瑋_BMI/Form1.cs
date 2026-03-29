using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
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

        private bool isDarkTheme;
        private double animatedBmi;
        private int animatedGauge;
        private double targetBmi;
        private int targetGauge;

        public Form1()
        {
            InitializeComponent();
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            ApplyModernStyle();
            ApplyTheme(false);
            ResetResultDisplay();
            Resize += Form1_Resize;
            btnRun.MouseEnter += BtnRun_MouseEnter;
            btnRun.MouseLeave += BtnRun_MouseLeave;
            btnClear.MouseEnter += BtnClear_MouseEnter;
            btnClear.MouseLeave += BtnClear_MouseLeave;
            panelDistribution.Resize += PanelDistribution_Resize;
            txtHeight.Focus();
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
            Clipboard.SetText(sb.ToString());
            lblHint.Text = "已複製 BMI 報告到剪貼簿";
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
            UpdateDistributionMarker(bmi);
            AddHistoryItem(bmi);
        }

        private void ResetResultDisplay()
        {
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
            progressBarBmi.Value = progressBarBmi.Minimum;
            UpdateDistributionMarker(DistributionMin);
            lblHint.Text = "提示：按 Enter 計算、按 Esc 清除，輸入只接受數字與小數點";
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
            ApplyRoundedRegion(lblResult, 12);
            ApplyRoundedRegion(lblCategory, 12);
            ApplyRoundedRegion(progressBarBmi, 10);
            ApplyRoundedRegion(panelHeader, 22);
            ApplyRoundedRegion(panelDistribution, 12);
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            ApplyModernStyle();
            UpdateDistributionMarker(targetBmi > 0 ? targetBmi : DistributionMin);
        }

        private void PanelDistribution_Resize(object sender, EventArgs e)
        {
            UpdateDistributionMarker(targetBmi > 0 ? targetBmi : DistributionMin);
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
                panelDistribution.BackColor = Color.FromArgb(56, 64, 80);
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
                panelDistribution.BackColor = Color.FromArgb(230, 238, 248);
                lblHint.ForeColor = Color.DimGray;
                listBoxHistory.BackColor = Color.FromArgb(246, 251, 255);
                listBoxHistory.ForeColor = Color.FromArgb(55, 74, 98);
            }

            btnTheme.Text = isDarkTheme ? "淺色模式" : "深色模式";
            ApplyModernStyle();
        }
    }
}
