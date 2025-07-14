Imports System.Net
Imports System.Net.Sockets
Imports System.Text
Imports System.Threading.Tasks

Module Program
    Sub Main()
        MainAsync().GetAwaiter().GetResult()
    End Sub

    Async Function MainAsync() As Task
        Dim lines As New List(Of String)()
        Dim buffer As New StringBuilder()

        Try
            Dim listener As New TcpListener(IPAddress.Any, 1234)
            listener.Start()
            Console.WriteLine("Listening on port 1234...")

            While True
                Using client As TcpClient = Await listener.AcceptTcpClientAsync()
                    Console.WriteLine("Client connected: " & client.Client.RemoteEndPoint.ToString())

                    Using stream As NetworkStream = client.GetStream()
                        buffer.Clear()
                        lines.Clear()

                        Dim readBuffer(1024) As Byte

                        While True
                            Dim bytesRead As Integer = Await stream.ReadAsync(readBuffer, 0, readBuffer.Length)
                            If bytesRead = 0 Then
                                Console.WriteLine("Client disconnected.")
                                Exit While
                            End If

                            Dim receivedText As String = Encoding.UTF8.GetString(readBuffer, 0, bytesRead)
                            Dim i As Integer = 0

                            While i < receivedText.Length
                                Dim ch As Char = receivedText(i)

                                If ch = vbCr OrElse ch = vbLf Then
                                    If buffer.Length > 0 Then
                                        Dim line As String = buffer.ToString()
                                        lines.Add(line)
                                        Console.WriteLine("[" & line & "]")
                                        buffer.Clear()
                                    End If

                                    ' Skip LF if it's part of CRLF
                                    If ch = vbCr AndAlso i + 1 < receivedText.Length AndAlso receivedText(i + 1) = vbLf Then
                                        i += 1
                                    End If
                                Else
                                    buffer.Append(ch)
                                End If

                                i += 1
                            End While
                        End While
                    End Using
                End Using

                Console.WriteLine("Waiting for next client...")
            End While

        Catch ex As Exception
            Console.WriteLine("Error: " & ex.Message)
        End Try
    End Function
End Module
