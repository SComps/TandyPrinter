Imports System.IO
Imports System.Net
Imports System.Net.Sockets
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks
Imports PdfSharp
Imports PdfSharp.Drawing
Imports PdfSharp.Fonts
Imports PdfSharp.Pdf

Module Program
    Private Const LinesPerPage As Integer = 65
    Private lines As New List(Of String)()
    Private buffer As New StringBuilder()
    Private lineCount As Integer = 0
    Private forcePageBreakRequested As Boolean = False
    Private cts As New CancellationTokenSource()
    Private currentPdfPageLines As New List(Of String)()
    Private pdfPageNumber As Integer = 0

    Private listenIp As IPAddress = IPAddress.Loopback
    Private listenPort As Integer = 1234

    Sub Main(args As String())
        ' Parse command line args for IP and Port
        If args.Length >= 1 Then
            If Not IPAddress.TryParse(args(0), listenIp) Then
                Console.WriteLine("Invalid IP address, defaulting to 127.0.0.1")
                listenIp = IPAddress.Loopback
            End If
        End If
        If args.Length >= 2 Then
            If Not Integer.TryParse(args(1), listenPort) Then
                Console.WriteLine("Invalid port, defaulting to 1234")
                listenPort = 1234
            End If
        End If

        Console.WriteLine($"Listening on {listenIp}:{listenPort}")

        ' Set font resolver to load font from file
        GlobalFontSettings.FontResolver = New FileFontResolver(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fonts", "lineprinter.ttf"))

        AddHandler Console.CancelKeyPress, AddressOf Console_CancelKeyPress
        Console.WriteLine("Press Ctrl+C to exit. Press F1 to force a page break.")

        Dim keyTask = Task.Run(AddressOf KeyListenerLoop)
        MainAsync(cts.Token).GetAwaiter().GetResult()
        cts.Cancel()
        keyTask.Wait()
        Console.WriteLine("Application exiting.")
    End Sub

    Private Sub Console_CancelKeyPress(sender As Object, e As ConsoleCancelEventArgs)
        Console.WriteLine("Ctrl+C pressed, shutting down...")
        e.Cancel = True
        cts.Cancel()
    End Sub

    Private Sub KeyListenerLoop()
        While Not cts.Token.IsCancellationRequested
            If Console.KeyAvailable Then
                Dim key = Console.ReadKey(intercept:=True)
                If key.Key = ConsoleKey.F1 Then
                    forcePageBreakRequested = True
                End If
            Else
                Thread.Sleep(50)
            End If
        End While
    End Sub

    Async Function MainAsync(token As CancellationToken) As Task
        Try
            Dim listener As New TcpListener(listenIp, listenPort)
            listener.Start()
            Console.WriteLine("Waiting for connections...")

            While Not token.IsCancellationRequested
                Using client As TcpClient = Await listener.AcceptTcpClientAsync().WaitAsync(token)
                    Console.WriteLine("Client connected: " & client.Client.RemoteEndPoint.ToString())

                    Using stream As NetworkStream = client.GetStream()
                        buffer.Clear()
                        lines.Clear()
                        currentPdfPageLines.Clear()
                        lineCount = 0
                        pdfPageNumber = 0
                        forcePageBreakRequested = False

                        Dim readBuffer(1024) As Byte

                        While Not token.IsCancellationRequested
                            Dim bytesReadTask = stream.ReadAsync(readBuffer, 0, readBuffer.Length, token)
                            Dim bytesRead As Integer

                            Try
                                bytesRead = Await bytesReadTask
                            Catch ex As OperationCanceledException
                                Exit While
                            End Try

                            If bytesRead = 0 Then
                                Console.WriteLine("Client disconnected.")
                                Await MaybePrintPageBreakAsync()
                                Await SavePdfPageAsync()
                                Exit While
                            End If

                            Dim receivedText As String = Encoding.UTF8.GetString(readBuffer, 0, bytesRead)
                            Dim i As Integer = 0

                            While i < receivedText.Length
                                Dim ch As Char = receivedText(i)

                                If ch = vbCr OrElse ch = vbLf Then
                                    Dim line As String = buffer.ToString()
                                    lines.Add(line)

                                    Dim displayLine = line.Replace(Chr(12), "<FF>")
                                    Dim pageLineNum As Integer = (lineCount Mod LinesPerPage) + 1

                                    Console.WriteLine($"[{pageLineNum,2}] {displayLine}")
                                    currentPdfPageLines.Add(displayLine)

                                    lineCount += 1
                                    buffer.Clear()

                                    If pageLineNum = LinesPerPage OrElse forcePageBreakRequested Then
                                        Console.WriteLine(New String("-"c, 30) & " PAGE BREAK " & New String("-"c, 30))
                                        forcePageBreakRequested = False
                                        Await SavePdfPageAsync()
                                        currentPdfPageLines.Clear()
                                    End If

                                    If ch = vbCr AndAlso i + 1 < receivedText.Length AndAlso receivedText(i + 1) = vbLf Then
                                        i += 1
                                    End If
                                ElseIf ch = Chr(12) Then
                                    buffer.Append(ch)
                                Else
                                    buffer.Append(ch)
                                End If

                                i += 1
                            End While

                            If forcePageBreakRequested AndAlso buffer.Length > 0 Then
                                Dim line As String = buffer.ToString()
                                lines.Add(line)

                                Dim displayLine = line.Replace(Chr(12), "<FF>")
                                Dim pageLineNum As Integer = (lineCount Mod LinesPerPage) + 1

                                Console.WriteLine($"[{pageLineNum,2}] {displayLine}")
                                currentPdfPageLines.Add(displayLine)

                                lineCount += 1
                                buffer.Clear()

                                Console.WriteLine(New String("-"c, 30) & " PAGE BREAK " & New String("-"c, 30))
                                forcePageBreakRequested = False
                                Await SavePdfPageAsync()
                                currentPdfPageLines.Clear()
                            End If
                        End While
                    End Using
                End Using

                Console.WriteLine("Waiting for next client...")
            End While

            listener.Stop()

        Catch ex As OperationCanceledException
            ' Expected on cancellation
        Catch ex As Exception
            Console.WriteLine("Error: " & ex.Message)
        End Try
    End Function

    Private Async Function MaybePrintPageBreakAsync() As Task
        If (lineCount Mod LinesPerPage) <> 0 Then
            Console.WriteLine(New String("-"c, 30) & " PAGE BREAK " & New String("-"c, 30))
        End If
        Await Task.CompletedTask
    End Function

    Private Async Function SavePdfPageAsync() As Task
        If currentPdfPageLines.Count = 0 Then Return

        pdfPageNumber += 1
        Dim pdf = New PdfDocument()
        pdf.Info.Title = $"Page {pdfPageNumber}"

        Dim page = pdf.AddPage()
        page.Size = PageSize.Letter
        page.Orientation = PageOrientation.Landscape

        Dim gfx = XGraphics.FromPdfPage(page)

        ' Draw background image scaled to full page size
        Dim bgImagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fonts", "greenbar_new.jpg")
        If File.Exists(bgImagePath) Then
            Using image = XImage.FromFile(bgImagePath)
                gfx.DrawImage(image, 0, 0, page.Width, page.Height)
            End Using
        Else
            Console.WriteLine($"Background image file not found: {bgImagePath}")
        End If

        ' Text margins from devs.vb
        Dim marginLeft = 31
        Dim marginTop = 20
        Dim marginBottom = 20

        Dim availableHeight = page.Height.Point - marginTop - marginBottom
        Dim desiredLines = 66
        Dim lineHeight = availableHeight / desiredLines

        ' Approximate font size based on line height
        Dim fontSize = lineHeight * 0.8
        Dim font = New XFont("LinePrinter", fontSize, XFontStyleEx.Regular)

        ' Measure actual line height and adjust
        Dim size = gfx.MeasureString("X", font)
        lineHeight = size.Height

        Dim y = marginTop

        For Each lineText In currentPdfPageLines
            Dim fixedLine = lineText.PadRight(132).Substring(0, 132)
            gfx.DrawString(fixedLine, font, XBrushes.Black, New XPoint(marginLeft, y))
            y += lineHeight
            If y > page.Height.Point - marginBottom Then Exit For
        Next

        Dim timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss")
        Dim filename = $"{timestamp}_Page_{pdfPageNumber}.pdf"
        pdf.Save(filename)
        Console.WriteLine($"Saved PDF page {pdfPageNumber} to '{filename}'")

        Await Task.CompletedTask
    End Function
End Module

Public Class FileFontResolver
    Implements IFontResolver

    Private ReadOnly fontPath As String
    Private fontBytes As Byte()

    Public Sub New(fontFilePath As String)
        Me.fontPath = fontFilePath
        If Not File.Exists(fontPath) Then
            Throw New FileNotFoundException("Font file not found", fontFilePath)
        End If
        fontBytes = File.ReadAllBytes(fontPath)
    End Sub

    Public Function ResolveTypeface(familyName As String, isBold As Boolean, isItalic As Boolean) As FontResolverInfo _
        Implements IFontResolver.ResolveTypeface

        If familyName.ToLowerInvariant().Contains("lineprinter") Then
            Return New FontResolverInfo("LinePrinter#Regular")
        End If

        Return Nothing
    End Function

    Public Function GetFont(faceName As String) As Byte() Implements IFontResolver.GetFont
        If faceName = "LinePrinter#Regular" Then
            Return fontBytes
        End If

        Throw New ArgumentException("Font not found: " & faceName)
    End Function
End Class
