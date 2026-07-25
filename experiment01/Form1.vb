Imports System.Diagnostics
Imports System.IO
Imports System.Text.Json
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports System.Drawing
Imports System.Linq

Public Class Form1
    Private pbCanvas As PictureBox
    Private btnStart As Button
    Private btnAutoStart As Button
    Private btnAutoStartShuffle As Button
    Private lblStatus As Label
    Private txtLog As TextBox

    Private numAgents As NumericUpDown
    Private numTasks As NumericUpDown
    Private numWA As NumericUpDown
    Private numWB As NumericUpDown
    Private numTrials As NumericUpDown

    Private pythonProcess As Process
    Private tasks As New List(Of TaskData)
    Private agents As New List(Of AgentData)
    Private isRunning As Boolean = False

    Private isAutoMode As Boolean = False
    Private isShuffleMode As Boolean = False
    Private rand As New Random()
    Private currentTrial As Integer = 0
    Private totalTrials As Integer = 0
    Private trialResults As New List(Of Integer)

    Public Class TaskData
        Public Property id As Integer
        Public Property x As Double
        Public Property y As Double
    End Class

    Public Class AgentData
        Public Property id As Integer
        Public Property x As Double
        Public Property y As Double
        Public Property target As Integer
    End Class

    Public Sub New()
        MyBase.New()
        Try
            InitializeComponent()
        Catch ex As Exception
        End Try
    End Sub

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Try
            Me.Text = "研究1: MASシミュレーション (ベースライン測定)"
            Me.Size = New Size(900, 600)
            Me.StartPosition = FormStartPosition.CenterScreen

            pbCanvas = New PictureBox() With {
                .Size = New Size(500, 500),
                .Location = New Point(20, 20),
                .BorderStyle = BorderStyle.FixedSingle,
                .BackColor = Color.White
            }
            AddHandler pbCanvas.Paint, AddressOf PbCanvas_Paint

            Dim panel As New Panel() With {
                .Location = New Point(540, 20),
                .Size = New Size(320, 500)
            }

            Dim createInput = Function(lblText As String, yPos As Integer, defVal As Decimal, minVal As Decimal, maxVal As Decimal, decPlaces As Integer) As NumericUpDown
                                  Dim lbl As New Label() With {.Text = lblText, .Location = New Point(10, yPos + 3), .AutoSize = True}
                                  Dim num As New NumericUpDown() With {
                                      .Location = New Point(150, yPos),
                                      .Size = New Size(100, 25),
                                      .Minimum = minVal, .Maximum = maxVal,
                                      .Value = defVal, .DecimalPlaces = decPlaces
                                  }
                                  panel.Controls.Add(lbl)
                                  panel.Controls.Add(num)
                                  Return num
                              End Function

            numAgents = createInput("エージェント数:", 10, 50, 10, 10000, 0)
            numTasks = createInput("タスク数:", 40, 5, 1, 50, 0)
            numWA = createInput("タ効重(Wa):", 70, 1D, 0D, 10D, 1)
            numWB = createInput("倫制重(Wb):", 100, 5D, 0D, 10D, 1)
            numTrials = createInput("自動試行回数:", 130, 10, 1, 1000, 0)

            btnStart = New Button() With {
                .Text = "1回実行 (描画あり)",
                .Location = New Point(10, 170),
                .Size = New Size(140, 40)
            }
            AddHandler btnStart.Click, AddressOf BtnStart_Click

            btnAutoStart = New Button() With {
                .Text = "自動連続実行",
                .Location = New Point(160, 170),
                .Size = New Size(140, 40),
                .BackColor = Color.LightGreen
            }
            AddHandler btnAutoStart.Click, AddressOf BtnAutoStart_Click

            btnAutoStartShuffle = New Button() With {
                .Text = "シャッフル自動実行 (Wa, Wbをランダム化)",
                .Location = New Point(10, 220),
                .Size = New Size(290, 40),
                .BackColor = Color.LightSkyBlue
            }
            AddHandler btnAutoStartShuffle.Click, AddressOf BtnAutoStartShuffle_Click

            txtLog = New TextBox() With {
                .Location = New Point(10, 270),
                .Size = New Size(290, 220),
                .Multiline = True,
                .ScrollBars = ScrollBars.Vertical,
                .ReadOnly = True
            }

            lblStatus = New Label() With {
                .Text = "待機中...",
                .Location = New Point(20, 530),
                .AutoSize = True,
                .Font = New Font("Meiryo", 10)
            }

            panel.Controls.Add(btnStart)
            panel.Controls.Add(btnAutoStart)
            panel.Controls.Add(btnAutoStartShuffle)
            panel.Controls.Add(txtLog)

            Me.Controls.Add(pbCanvas)
            Me.Controls.Add(panel)
            Me.Controls.Add(lblStatus)

        Catch ex As Exception
            MessageBox.Show("画面の初期化中にエラーが発生しました。" & vbCrLf & ex.Message & vbCrLf & ex.StackTrace, "起動エラー", MessageBoxButtons.OK, MessageBoxIcon.Error)
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

    Private Async Sub BtnStart_Click(sender As Object, e As EventArgs)
        If isRunning Then Return
        isAutoMode = False
        isShuffleMode = False
        Await RunSimulationAsync()
    End Sub

    Private Async Sub BtnAutoStart_Click(sender As Object, e As EventArgs)
        If isRunning Then Return
        isAutoMode = True
        isShuffleMode = False
        totalTrials = CInt(numTrials.Value)
        currentTrial = 0
        trialResults.Clear()

        AddLog($"=== 自動実行開始: 全 {totalTrials} 回 ===")
        AddLog($"設定: Agent={numAgents.Value}, Task={numTasks.Value}, Wa={numWA.Value}, Wb={numWB.Value}")

        btnStart.Enabled = False
        btnAutoStart.Enabled = False
        btnAutoStartShuffle.Enabled = False

        Await RunNextTrial()
    End Sub

    Private Async Sub BtnAutoStartShuffle_Click(sender As Object, e As EventArgs)
        If isRunning Then Return
        isAutoMode = True
        isShuffleMode = True
        totalTrials = CInt(numTrials.Value)
        currentTrial = 0
        trialResults.Clear()

        AddLog($"=== シャッフル自動実行開始: 全 {totalTrials} 回 ===")
        AddLog($"固定設定: Agent={numAgents.Value}, Task={numTasks.Value}")

        btnStart.Enabled = False
        btnAutoStart.Enabled = False
        btnAutoStartShuffle.Enabled = False

        Await RunNextTrial()
    End Sub

    Private Async Function RunNextTrial() As Task
        currentTrial += 1
        If currentTrial > totalTrials Then
            Dim avgSteps = If(trialResults.Count > 0, trialResults.Average(), 0)
            AddLog($"=== 完了 ===")
            AddLog($"平均収束ステップ数: {avgSteps:F2}")
            AddLog($"----------------------------")
            lblStatus.Text = "自動実行完了"
            btnStart.Enabled = True
            btnAutoStart.Enabled = True
            btnAutoStartShuffle.Enabled = True
            isRunning = False
            Return
        End If

        If isShuffleMode Then
            Dim rangeWa As Double = CDbl(numWA.Maximum - numWA.Minimum)
            Dim rangeWb As Double = CDbl(numWB.Maximum - numWB.Minimum)
            numWA.Value = CDec(Math.Round(CDbl(numWA.Minimum) + rand.NextDouble() * rangeWa, 1))
            numWB.Value = CDec(Math.Round(CDbl(numWB.Minimum) + rand.NextDouble() * rangeWb, 1))
            AddLog($"[試行 {currentTrial}] Wa={numWA.Value}, Wb={numWB.Value}")
        End If

        lblStatus.Text = $"自動実行中... ({currentTrial}/{totalTrials})"
        Await RunSimulationAsync()

        If isAutoMode Then
            Await RunNextTrial()
        End If
    End Function

    Private Async Function RunSimulationAsync() As Task
        isRunning = True
        tasks.Clear()
        agents.Clear()

        Dim pythonScriptPath As String = Path.Combine(Application.StartupPath, "simulation.py")
        Dim startInfo As New ProcessStartInfo()
        startInfo.FileName = "python"
        startInfo.Arguments = $"""{pythonScriptPath}"" --agents {numAgents.Value} --tasks {numTasks.Value} --wa {numWA.Value} --wb {numWB.Value}"
        startInfo.UseShellExecute = False
        startInfo.RedirectStandardOutput = True
        startInfo.CreateNoWindow = True
        startInfo.WorkingDirectory = Application.StartupPath

        Try
            pythonProcess = Process.Start(startInfo)
            Using reader As StreamReader = pythonProcess.StandardOutput
                While Not reader.EndOfStream
                    Dim jsonStr As String = Await reader.ReadLineAsync()
                    If String.IsNullOrEmpty(jsonStr) Then Continue While

                    ProcessJsonData(jsonStr)

                    If Not isAutoMode Then
                        pbCanvas.Invalidate()
                        Await Task.Delay(10)
                    End If
                End While
            End Using
            pythonProcess.WaitForExit()
        Catch ex As Exception
            MessageBox.Show("Pythonの実行エラー: " & ex.Message, "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error)
            isAutoMode = False
        Finally
            If Not isAutoMode Then
                isRunning = False
                lblStatus.Text = "完了"
            End If
            pbCanvas.Invalidate()
        End Try
    End Function

    Private Sub ProcessJsonData(jsonStr As String)
        Try
            Using doc As JsonDocument = JsonDocument.Parse(jsonStr)
                Dim root = doc.RootElement
                Dim msgType = root.GetProperty("type").GetString()

                If msgType = "init" Then
                    For Each t In root.GetProperty("tasks").EnumerateArray()
                        tasks.Add(New TaskData With {.id = t.GetProperty("id").GetInt32(), .x = t.GetProperty("x").GetDouble(), .y = t.GetProperty("y").GetDouble()})
                    Next
                ElseIf msgType = "update" Then
                    Dim stepNum = root.GetProperty("step").GetInt32()
                    If Not isAutoMode Then
                        Me.Invoke(Sub() lblStatus.Text = $"実行中... ステップ: {stepNum}")
                    End If
                    agents.Clear()
                    For Each a In root.GetProperty("agents").EnumerateArray()
                        agents.Add(New AgentData With {.id = a.GetProperty("id").GetInt32(), .x = a.GetProperty("x").GetDouble(), .y = a.GetProperty("y").GetDouble(), .target = a.GetProperty("target").GetInt32()})
                    Next
                ElseIf msgType = "end" Then
                    Dim finalStep = root.GetProperty("step").GetInt32()
                    Dim reason = root.GetProperty("reason").GetString()

                    If isAutoMode Then
                        trialResults.Add(finalStep)
                        If isShuffleMode Then
                            AddLog($"  -> 収束ステップ {finalStep} ({reason})")
                        Else
                            AddLog($"試行 {currentTrial}: 収束ステップ {finalStep} ({reason})")
                        End If
                    Else
                        Me.Invoke(Sub() lblStatus.Text = $"終了 ({reason}) - かかったステップ数: {finalStep}")
                    End If
                End If
            End Using
        Catch ex As Exception
            Debug.WriteLine(ex.Message)
        End Try
    End Sub

    Private Sub PbCanvas_Paint(sender As Object, e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias

        Dim taskBrush As New SolidBrush(Color.Red)
        For Each t In tasks
            g.FillRectangle(taskBrush, CSng(t.x) - 10, CSng(t.y) - 10, 20, 20)
            g.DrawString($"T{t.id}", Me.Font, Brushes.Black, CSng(t.x) + 12, CSng(t.y) - 10)
        Next
        taskBrush.Dispose()

        Dim agentBrush As New SolidBrush(Color.Blue)
        For Each a In agents
            g.FillEllipse(agentBrush, CSng(a.x) - 5, CSng(a.y) - 5, 10, 10)
            Dim targetTask = tasks.FirstOrDefault(Function(t) t.id = a.target)
            If targetTask IsNot Nothing Then
                g.DrawLine(Pens.LightGray, CSng(a.x), CSng(a.y), CSng(targetTask.x), CSng(targetTask.y))
            End If
        Next
        agentBrush.Dispose()
    End Sub
End Class
