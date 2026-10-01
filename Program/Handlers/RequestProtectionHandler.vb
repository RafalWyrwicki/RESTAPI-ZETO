Imports REST_API.Infrastructure
Imports System.Configuration
Imports System.Diagnostics
Imports System.Globalization
Imports System.Linq
Imports System.Net
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Threading
Imports System.Threading.Tasks

Namespace Handlers
    Public Class RequestProtectionHandler
        Inherits DelegatingHandler

        Private Shared ReadOnly MaxConcurrentRequests As Integer =
            GetPositiveAppSetting("MaxConcurrentRequests", 64)

        Private Shared ReadOnly RateLimitPerSecond As Integer =
            GetNonNegativeAppSetting("RateLimitPerSecond", 500)

        Private Shared ReadOnly MaxRequestBodyBytes As Long =
            GetPositiveLongAppSetting("MaxRequestBodyBytes", 65536L)

        Private Shared ReadOnly ConcurrencyGate As New SemaphoreSlim(
            MaxConcurrentRequests,
            MaxConcurrentRequests
        )

        Private Shared _rateWindow As Long
        Private Shared _requestsInWindow As Integer

        Protected Overrides Async Function SendAsync(
            request As HttpRequestMessage,
            cancellationToken As CancellationToken
        ) As Task(Of HttpResponseMessage)
            Dim correlationId As String = GetCorrelationId(request)
            request.Properties(AsyncLogger.CorrelationIdPropertyName) =
                correlationId

            Dim stopwatch As Stopwatch = Stopwatch.StartNew()
            Dim response As HttpResponseMessage = Nothing
            Dim gateEntered As Boolean = False

            Try
                If RequestBodyIsTooLarge(request) Then
                    response = request.CreateErrorResponse(
                        HttpStatusCode.RequestEntityTooLarge,
                        "Rozmiar żądania przekracza dozwolony limit."
                    )
                    Return response
                End If

                If Not TryEnterRateWindow() Then
                    response = CreateThrottledResponse(
                        request,
                        "Przekroczono limit żądań na sekundę."
                    )
                    Return response
                End If

                gateEntered = Await ConcurrencyGate.WaitAsync(
                    0,
                    cancellationToken
                ).ConfigureAwait(False)

                If Not gateEntered Then
                    response = CreateThrottledResponse(
                        request,
                        "Serwis obsługuje maksymalną liczbę równoległych żądań."
                    )
                    Return response
                End If

                response = Await MyBase.SendAsync(
                    request,
                    cancellationToken
                ).ConfigureAwait(False)

                Return response
            Catch ex As OperationCanceledException
                AsyncLogger.Warning(
                    String.Format(
                        CultureInfo.InvariantCulture,
                        "Request canceled. CorrelationId={0}; Method={1}; Path={2}",
                        correlationId,
                        request.Method,
                        request.RequestUri.AbsolutePath
                    )
                )
                Throw
            Catch ex As Exception
                AsyncLogger.Error(
                    String.Format(
                        CultureInfo.InvariantCulture,
                        "Unhandled pipeline error. CorrelationId={0}; Method={1}; Path={2}",
                        correlationId,
                        request.Method,
                        request.RequestUri.AbsolutePath
                    ),
                    ex
                )
                Throw
            Finally
                If gateEntered Then
                    ConcurrencyGate.Release()
                End If

                stopwatch.Stop()
                If response IsNot Nothing AndAlso
                   Not response.Headers.Contains("X-Correlation-ID") Then
                    response.Headers.TryAddWithoutValidation(
                        "X-Correlation-ID",
                        correlationId
                    )
                End If

                Dim status As String =
                    If(
                        response Is Nothing,
                        "NO_RESPONSE",
                        CInt(response.StatusCode).ToString(
                            CultureInfo.InvariantCulture
                        )
                    )

                AsyncLogger.Information(
                    String.Format(
                        CultureInfo.InvariantCulture,
                        "Request completed. CorrelationId={0}; Method={1}; Path={2}; Status={3}; DurationMs={4}",
                        correlationId,
                        request.Method,
                        request.RequestUri.AbsolutePath,
                        status,
                        stopwatch.ElapsedMilliseconds
                    )
                )
            End Try
        End Function

        Private Shared Function RequestBodyIsTooLarge(
            request As HttpRequestMessage
        ) As Boolean
            If request.Content Is Nothing OrElse
               Not request.Content.Headers.ContentLength.HasValue Then
                Return False
            End If

            Return request.Content.Headers.ContentLength.Value >
                MaxRequestBodyBytes
        End Function

        Private Shared Function TryEnterRateWindow() As Boolean
            If RateLimitPerSecond = 0 Then
                Return True
            End If

            Dim currentWindow As Long =
                DateTime.UtcNow.Ticks \ TimeSpan.TicksPerSecond

            Do
                Dim observedWindow As Long = Interlocked.Read(_rateWindow)

                If observedWindow <> currentWindow Then
                    If Interlocked.CompareExchange(
                        _rateWindow,
                        currentWindow,
                        observedWindow
                    ) = observedWindow Then
                        Interlocked.Exchange(_requestsInWindow, 0)
                    End If

                    Continue Do
                End If

                Return Interlocked.Increment(_requestsInWindow) <=
                    RateLimitPerSecond
            Loop
        End Function

        Private Shared Function CreateThrottledResponse(
            request As HttpRequestMessage,
            message As String
        ) As HttpResponseMessage
            Dim response As HttpResponseMessage =
                request.CreateErrorResponse(
                    CType(429, HttpStatusCode),
                    message
                )
            response.Headers.RetryAfter =
                New RetryConditionHeaderValue(TimeSpan.FromSeconds(1))
            Return response
        End Function

        Private Shared Function GetCorrelationId(
            request As HttpRequestMessage
        ) As String
            Dim suppliedValues As IEnumerable(Of String) = Nothing
            If request.Headers.TryGetValues(
                "X-Correlation-ID",
                suppliedValues
            ) Then
                Dim suppliedValue As String = suppliedValues.FirstOrDefault()
                If Not String.IsNullOrWhiteSpace(suppliedValue) AndAlso
                   suppliedValue.Length <= 64 Then
                    Return suppliedValue
                End If
            End If

            Return Guid.NewGuid().ToString("N")
        End Function

        Private Shared Function GetPositiveAppSetting(
            key As String,
            defaultValue As Integer
        ) As Integer
            Dim configuredValue As Integer
            If Integer.TryParse(
                ConfigurationManager.AppSettings(key),
                configuredValue
            ) AndAlso configuredValue > 0 Then
                Return configuredValue
            End If

            Return defaultValue
        End Function

        Private Shared Function GetNonNegativeAppSetting(
            key As String,
            defaultValue As Integer
        ) As Integer
            Dim configuredValue As Integer
            If Integer.TryParse(
                ConfigurationManager.AppSettings(key),
                configuredValue
            ) AndAlso configuredValue >= 0 Then
                Return configuredValue
            End If

            Return defaultValue
        End Function

        Private Shared Function GetPositiveLongAppSetting(
            key As String,
            defaultValue As Long
        ) As Long
            Dim configuredValue As Long
            If Long.TryParse(
                ConfigurationManager.AppSettings(key),
                configuredValue
            ) AndAlso configuredValue > 0 Then
                Return configuredValue
            End If

            Return defaultValue
        End Function
    End Class
End Namespace
