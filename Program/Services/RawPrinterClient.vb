Imports System.IO
Imports System.Net.Sockets
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks

Namespace Services
    Public NotInheritable Class RawPrinterClient
        Private Sub New()
        End Sub

        Public Shared Async Function SendAsync(
            address As String,
            port As Integer,
            zpl As String,
            connectTimeoutMilliseconds As Integer,
            cancellationToken As CancellationToken
        ) As Task(Of Boolean)
            cancellationToken.ThrowIfCancellationRequested()

            Try
                Using client As New TcpClient()
                    Dim connectTask As Task = client.ConnectAsync(address, port)
                    Dim timeoutTask As Task = Task.Delay(
                        connectTimeoutMilliseconds,
                        cancellationToken
                    )
                    Dim completed As Task = Await Task.WhenAny(
                        connectTask,
                        timeoutTask
                    ).ConfigureAwait(False)

                    If completed IsNot connectTask Then
                        cancellationToken.ThrowIfCancellationRequested()
                        Return False
                    End If

                    Await connectTask.ConfigureAwait(False)
                    Dim payload As Byte() = New UTF8Encoding(False).GetBytes(zpl)

                    Using stream As NetworkStream = client.GetStream()
                        Await stream.WriteAsync(
                            payload,
                            0,
                            payload.Length,
                            cancellationToken
                        ).ConfigureAwait(False)
                        Await stream.FlushAsync(cancellationToken).ConfigureAwait(False)
                    End Using
                End Using
                Return True
            Catch ex As OperationCanceledException
                Throw
            Catch ex As SocketException
                Return False
            Catch ex As IOException
                Return False
            Catch ex As ObjectDisposedException
                Return False
            End Try
        End Function
    End Class
End Namespace
