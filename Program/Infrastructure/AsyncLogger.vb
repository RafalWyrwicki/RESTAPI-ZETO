Imports System.Collections.Concurrent
Imports System.Diagnostics
Imports System.Threading

Namespace Infrastructure
    Public Module AsyncLogger
        Public Const CorrelationIdPropertyName As String =
            "REST_API.CorrelationId"

        Private Const QueueCapacity As Integer = 2048
        Private ReadOnly Entries As New ConcurrentQueue(Of LogEntry)()
        Private _queuedEntries As Integer
        Private _workerScheduled As Integer

        Public Sub Information(message As String)
            Enqueue("INFO", message)
        End Sub

        Public Sub Warning(message As String)
            Enqueue("WARN", message)
        End Sub

        Public Sub [Error](message As String, exception As Exception)
            Dim exceptionDescription As String = String.Empty
            If exception IsNot Nothing Then
                exceptionDescription = String.Format(
                    " Exception={0}: {1}",
                    exception.GetType().FullName,
                    exception.Message
                )
            End If

            Enqueue("ERROR", message & exceptionDescription)
        End Sub

        Private Sub Enqueue(level As String, message As String)
            Dim count As Integer = Interlocked.Increment(_queuedEntries)
            If count > QueueCapacity Then
                Interlocked.Decrement(_queuedEntries)
                Return
            End If

            Entries.Enqueue(
                New LogEntry With {
                    .CreatedUtc = DateTime.UtcNow,
                    .Level = level,
                    .Message = message
                }
            )

            ScheduleWorker()
        End Sub

        Private Sub ScheduleWorker()
            If Interlocked.CompareExchange(_workerScheduled, 1, 0) <> 0 Then
                Return
            End If

            ThreadPool.QueueUserWorkItem(AddressOf DrainQueue)
        End Sub

        Private Sub DrainQueue(state As Object)
            Dim entry As LogEntry = Nothing

            While Entries.TryDequeue(entry)
                Interlocked.Decrement(_queuedEntries)
                Dim formattedMessage As String = String.Format(
                    "{0:O} [{1}] {2}",
                    entry.CreatedUtc,
                    entry.Level,
                    entry.Message
                )

                Select Case entry.Level
                    Case "ERROR"
                        Trace.TraceError(formattedMessage)
                    Case "WARN"
                        Trace.TraceWarning(formattedMessage)
                    Case Else
                        Trace.TraceInformation(formattedMessage)
                End Select
            End While

            Interlocked.Exchange(_workerScheduled, 0)
            If Not Entries.IsEmpty Then
                ScheduleWorker()
            End If
        End Sub

        Private Class LogEntry
            Public Property CreatedUtc As DateTime
            Public Property Level As String
            Public Property Message As String
        End Class
    End Module
End Namespace
