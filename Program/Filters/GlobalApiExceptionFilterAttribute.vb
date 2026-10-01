Imports REST_API.Infrastructure
Imports System.Data.SqlClient
Imports System.Globalization
Imports System.Net
Imports System.Net.Http
Imports System.Threading
Imports System.Web.Http.Filters

Namespace Filters
    Public Class GlobalApiExceptionFilterAttribute
        Inherits ExceptionFilterAttribute

        Public Overrides Sub OnException(
            actionExecutedContext As HttpActionExecutedContext
        )
            Dim correlationId As String =
                GetCorrelationId(actionExecutedContext.Request)
            Dim exception As Exception = actionExecutedContext.Exception

            AsyncLogger.Error(
                String.Format(
                    CultureInfo.InvariantCulture,
                    "Controller error. CorrelationId={0}; Method={1}; Path={2}",
                    correlationId,
                    actionExecutedContext.Request.Method,
                    actionExecutedContext.Request.RequestUri.AbsolutePath
                ),
                exception
            )

            Dim statusCode As HttpStatusCode
            Dim message As String

            If TypeOf exception Is OperationCanceledException Then
                statusCode = HttpStatusCode.RequestTimeout
                message = "Żądanie zostało anulowane lub przekroczyło limit czasu."
            ElseIf IsSqlTimeout(exception) Then
                statusCode = HttpStatusCode.ServiceUnavailable
                message = "Usługa zapisu danych jest chwilowo niedostępna."
            Else
                statusCode = HttpStatusCode.InternalServerError
                message = "Wystąpił wewnętrzny błąd serwera."
            End If

            actionExecutedContext.Response =
                actionExecutedContext.Request.CreateErrorResponse(
                    statusCode,
                    message
                )
            actionExecutedContext.Response.Headers.TryAddWithoutValidation(
                "X-Correlation-ID",
                correlationId
            )
        End Sub

        Private Shared Function IsSqlTimeout(exception As Exception) As Boolean
            Dim sqlException As SqlException =
                TryCast(exception, SqlException)
            Return sqlException IsNot Nothing AndAlso sqlException.Number = -2
        End Function

        Private Shared Function GetCorrelationId(
            request As HttpRequestMessage
        ) As String
            Dim value As Object = Nothing
            If request.Properties.TryGetValue(
                AsyncLogger.CorrelationIdPropertyName,
                value
            ) Then
                Return Convert.ToString(value, CultureInfo.InvariantCulture)
            End If

            Return Guid.NewGuid().ToString("N")
        End Function
    End Class
End Namespace
