Imports System.Diagnostics
Imports System.IO
Imports System.Text.Json
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports System.Windows.Forms.DataVisualization.Charting
Imports System.Drawing
Imports System.Linq
Imports System.Collections.Generic

Public Class Form1
    Inherits Form

    Private chartUtility As Chart
    Private btnStart As Button
    Private btnAutoStart As Button
    Private lblStatus As Label
    Private txtLog As TextBox

    Private numN As NumericUpDown
    Private numM As NumericUpDown
    Private numK As NumericUpDown
    Private numTrials As NumericUpDown

    Private isRunning As Boolean = False
    Private isAutoMode As Boolean = False
    Private currentTrial As Integer = 0
    Private totalTrials As Integer = 0

    Private trialSteps As New List(Of Integer)()
    Private trialRatios As New List(Of Double)()
    Private currentOutputDir As String = ""

    Public Sub New()
        MyBase.New()
        Try
            InitializeComponent()
        Catch ex As Exception
        End Try
    End Sub

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Try
            Me.Text = "研究2: 純粋な最適応答ダイナミクス (制約排除モデル)"
            Me.Size = New Size(900, 600)
            Me.StartPosition = FormStartPosition.CenterScreen
            Me.BackColor = Color.White

            chartUtility = New Chart() With {
                .Size = New Size(500, 500),
                .Location = New Point(20, 20),
                .BorderlineDashStyle = ChartDashStyle.Solid,
                .BorderlineColor = Color.Gray,
                .BackColor = Color.White
            }
            Dim chartArea As New ChartArea("MainArea")
            chartArea.AxisX.Title = "タイムステップ (t)"
            chartArea.AxisX.MajorGrid.LineColor = Color.LightGray
            chartArea.AxisY.Title = "大域的総効用 U_total"
            chartArea.AxisY.MajorGrid.LineColor = Color.LightGray
            chartUtility.ChartAreas.Add(chartArea)
            Dim legend As New Legend() With {.Docking = Docking.Top}
            chartUtility.Legends.Add(legend)

            Dim panel As New Panel() With {
                .Location = New Point(540, 20),
                .Size = New Size(320, 500)
            }

            Dim createInput = Function(lblText As String, yPos As Integer, defVal As Decimal, minVal As Decimal, maxVal As Decimal, decPlaces As Integer) As NumericUpDown
                                  Dim lbl As New Label() With {.Text = lblText, .Location = New Point(10, yPos + 3), .AutoSize = True}
                                  Dim num As New NumericUpDown() With {
                                      .Location = New Point(160, yPos),
                                      .Size = New Size(100, 25),
                                      .Minimum = minVal, .Maximum = maxVal,
                                      .Value = defVal, .DecimalPlaces = decPlaces
                                  }
                                  panel.Controls.Add(lbl)
                                  panel.Controls.Add(num)
                                  Return num
                              End Function

            numN = createInput("エージェント数(N):", 10, 200, 10, 20000, 0)
            numM = createInput("グループ数(M):", 40, 20, 2, 1000, 0)
            numK = createInput("スキル次元(K):", 70, 3, 1, 10, 0)
            numTrials = createInput("自動試行回数:", 100, 10, 1, 10000, 0)

            btnStart = New Button() With {
                .Text = "1回実行 (描画あり)",
                .Location = New Point(10, 140),
                .Size = New Size(140, 40)
            }
            AddHandler btnStart.Click, AddressOf BtnStart_Click

            btnAutoStart = New Button() With {
                .Text = "自動連続実行",
                .Location = New Point(160, 140),
                .Size = New Size(140, 40),
                .BackColor = Color.LightGreen
            }
            AddHandler btnAutoStart.Click, AddressOf BtnAutoStart_Click

            txtLog = New TextBox() With {
                .Location = New Point(10, 190),
                .Size = New Size(290, 300),
                .Multiline = True,
                .ScrollBars = ScrollBars.Vertical,
                .ReadOnly = True,
                .BackColor = Color.White
            }

            lblStatus = New Label() With {
                .Text = "待機中...",
                .Location = New Point(20, 530),
                .AutoSize = True,
                .Font = New Font("Meiryo", 10)
            }

            panel.Controls.Add(btnStart)
            panel.Controls.Add(btnAutoStart)
            panel.Controls.Add(txtLog)

            Me.Controls.Add(chartUtility)
            Me.Controls.Add(panel)
            Me.Controls.Add(lblStatus)

        Catch ex As Exception
            MessageBox.Show("画面の初期化中にエラーが発生しました。" & vbCrLf & ex.Message, "起動エラー", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Application.Exit()
        End Try
    End Sub

    Private Sub AddLog(msg As String)
        Me.Invoke(Sub()
                      txtLog.AppendText(msg & vbCrLf)
                      txtLog.SelectionStart = txtLog.Text.Length
                      txtLog.ScrollToCaret()
                  End Sub)
    End Sub

    Private Sub CreateOutputDirectory(prefix As String)
        Dim baseDir As String = Path.Combine(Application.StartupPath, "Results")
        currentOutputDir = Path.Combine(baseDir, $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}")
        If Not Directory.Exists(currentOutputDir) Then
            Directory.CreateDirectory(currentOutputDir)
        End If
        AddLog($"[保存先] {currentOutputDir}")
    End Sub

    Private Async Sub BtnStart_Click(sender As Object, e As EventArgs)
        If isRunning Then Return
        isAutoMode = False

        CreateOutputDirectory("Single")
        Await RunSimulationAsync()
    End Sub

    Private Async Sub BtnAutoStart_Click(sender As Object, e As EventArgs)
        If isRunning Then Return
        isAutoMode = True
        totalTrials = CInt(numTrials.Value)
        currentTrial = 0
        trialSteps.Clear()
        trialRatios.Clear()

        CreateOutputDirectory("Auto")

        AddLog($"=== 自動実行開始: 全 {totalTrials} 回 ===")
        AddLog($"設定: N={numN.Value}, M={numM.Value}, K={numK.Value}")

        btnStart.Enabled = False
        btnAutoStart.Enabled = False

        Await RunNextTrial()
    End Sub

    Private Async Function RunNextTrial() As Task
        currentTrial += 1
        If currentTrial > totalTrials Then
            Dim avgSteps = If(trialSteps.Count > 0, trialSteps.Average(), 0)
            Dim avgRatio = If(trialRatios.Count > 0, trialRatios.Average(), 0)
            AddLog($"=== 完了 ===")
            AddLog($"平均収束ステップ数: {avgSteps:F2}")
            AddLog($"平均近似比(MAS/SA): {avgRatio:F4}")
            AddLog($"----------------------------")
            lblStatus.Text = "自動実行完了"
            btnStart.Enabled = True
            btnAutoStart.Enabled = True
            isRunning = False
            Return
        End If

        lblStatus.Text = $"自動実行中... ({currentTrial}/{totalTrials})"
        Await RunSimulationAsync()

        If isAutoMode Then
            Await RunNextTrial()
        End If
    End Function

    Private Async Function RunSimulationAsync() As Task
        isRunning = True

        Dim n = CInt(numN.Value)
        Dim m = CInt(numM.Value)
        Dim k = CInt(numK.Value)

        Dim pythonScriptPath As String = Path.Combine(Application.StartupPath, "simulation_core.py")
        Dim startInfo As New ProcessStartInfo()
        startInfo.FileName = "python"
        startInfo.Arguments = $"""{pythonScriptPath}"" {n} {m} {k}"
        startInfo.UseShellExecute = False
        startInfo.RedirectStandardOutput = True
        startInfo.RedirectStandardError = True
        startInfo.CreateNoWindow = True
        startInfo.WorkingDirectory = Application.StartupPath

        Try
            Dim jsonResult As String = ""
            Using process As Process = Process.Start(startInfo)
                Using reader As StreamReader = process.StandardOutput
                    jsonResult = Await reader.ReadToEndAsync()
                End Using
                process.WaitForExit()

                If process.ExitCode <> 0 Then
                    Using errReader As StreamReader = process.StandardError
                        Throw New Exception(Await errReader.ReadToEndAsync())
                    End Using
                End If
            End Using

            ProcessJsonData(jsonResult)

        Catch ex As Exception
            AddLog($"Python実行エラー: {ex.Message}")
            isAutoMode = False
            btnStart.Enabled = True
            btnAutoStart.Enabled = True
        Finally
            If Not isAutoMode Then
                isRunning = False
                lblStatus.Text = "完了"
            End If
        End Try
    End Function

    Private Sub ProcessJsonData(jsonStr As String)
        Try
            Dim doc As JsonDocument = JsonDocument.Parse(jsonStr)
            Dim root = doc.RootElement

            Dim masSteps = root.GetProperty("MAS_Steps").GetInt32()
            Dim ratio = root.GetProperty("ApproximationRatio").GetDouble()
            Dim c_j = root.GetProperty("Capacity_C_j").GetInt32()

            Dim masHistory = root.GetProperty("MAS_History").EnumerateArray().Select(Function(x) x.GetDouble()).ToList()
            Dim saHistory = root.GetProperty("SA_History").EnumerateArray().Select(Function(x) x.GetDouble()).ToList()

            If isAutoMode Then
                trialSteps.Add(masSteps)
                trialRatios.Add(ratio)
                AddLog($"試行 {currentTrial}: 収束 {masSteps} steps, 近似比 {ratio:F4}")
            Else
                AddLog($"[1回実行] 定員制約 C_j = {c_j}")
                AddLog($"収束ステップ数: {masSteps}")
                AddLog($"近似比 (MAS/SA): {ratio:F4}")
                AddLog($"----------------------------")
            End If

            DrawAndSaveChart(masHistory, saHistory, jsonStr)

        Catch ex As Exception
            AddLog($"結果解析エラー: {ex.Message}")
        End Try
    End Sub

    Private Sub DrawAndSaveChart(masHistory As List(Of Double), saHistory As List(Of Double), originalJson As String)
        Me.Invoke(Sub()
                      chartUtility.Series.Clear()

                      Dim seriesMAS As New Series("MAS (Best Response)")
                      seriesMAS.ChartType = SeriesChartType.Line
                      seriesMAS.BorderWidth = 3
                      seriesMAS.Color = Color.RoyalBlue
                      For i As Integer = 0 To masHistory.Count - 1
                          seriesMAS.Points.AddXY(i, masHistory(i))
                      Next

                      Dim seriesSA As New Series("SA (Upper Bound)")
                      seriesSA.ChartType = SeriesChartType.Line
                      seriesSA.BorderWidth = 2
                      seriesSA.Color = Color.Crimson
                      seriesSA.BorderDashStyle = ChartDashStyle.Dash
                      For i As Integer = 0 To saHistory.Count - 1
                          seriesSA.Points.AddXY(i, saHistory(i))
                      Next

                      chartUtility.Series.Add(seriesMAS)
                      chartUtility.Series.Add(seriesSA)

                      Dim allValues = masHistory.Concat(saHistory).ToList()
                      If allValues.Count > 0 Then
                          chartUtility.ChartAreas("MainArea").AxisY.Minimum = Math.Floor(allValues.Min() * 1.05)
                          chartUtility.ChartAreas("MainArea").AxisY.Maximum = 0
                      End If

                      chartUtility.Update()

                      If Not String.IsNullOrEmpty(currentOutputDir) AndAlso Directory.Exists(currentOutputDir) Then
                          Dim timestamp As String = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")

                          Dim imagePath As String = Path.Combine(currentOutputDir, $"graph_{timestamp}.png")
                          chartUtility.SaveImage(imagePath, ChartImageFormat.Png)

                          Dim jsonPath As String = Path.Combine(currentOutputDir, $"data_{timestamp}.json")
                          File.WriteAllText(jsonPath, originalJson)
                      End If
                  End Sub)
    End Sub
End Class
