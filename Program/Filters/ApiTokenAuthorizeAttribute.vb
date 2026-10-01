Imports System.Net
Imports System.Net.Http
Imports System.Web.Http.Controllers
Imports System.Web.Http.Filters
Imports System.Configuration
Imports System.Net.Http.Headers
Imports System.Text

Namespace Filters

    Public Class ApiTokenAuthorizeAttribute
        Inherits AuthorizationFilterAttribute

        Private Shared ReadOnly ExpectedToken As String =
            ConfigurationManager.AppSettings("ApiToken")

        Public Overrides Sub OnAuthorization(actionContext As HttpActionContext)

            Dim authHeader As AuthenticationHeaderValue =
                actionContext.Request.Headers.Authorization

            If authHeader Is Nothing Then
                actionContext.Response = actionContext.Request.CreateResponse(
                    HttpStatusCode.Unauthorized,
                    "Brak nagłówka Authorization."
                )
                Return
            End If

            If Not String.Equals(
                authHeader.Scheme,
                "Bearer",
                StringComparison.OrdinalIgnoreCase
            ) Then
                actionContext.Response = actionContext.Request.CreateResponse(
                    HttpStatusCode.Unauthorized,
                    "Nieprawidłowy typ autoryzacji. Wymagany Bearer."
                )
                Return
            End If

            Dim receivedToken As String = authHeader.Parameter

            If String.IsNullOrWhiteSpace(receivedToken) Then
                actionContext.Response = actionContext.Request.CreateResponse(
                    HttpStatusCode.Unauthorized,
                    "Brak tokenu Bearer."
                )
                Return
            End If

            If Not TokensAreEqual(ExpectedToken, receivedToken) Then
                actionContext.Response = actionContext.Request.CreateResponse(
                    HttpStatusCode.Unauthorized,
                    "Nieprawidłowy token."
                )
                Return
            End If

            MyBase.OnAuthorization(actionContext)

        End Sub

        Private Shared Function TokensAreEqual(
            expected As String,
            received As String
        ) As Boolean
            If String.IsNullOrEmpty(expected) OrElse received Is Nothing Then
                Return False
            End If

            Dim expectedBytes As Byte() = Encoding.UTF8.GetBytes(expected)
            Dim receivedBytes As Byte() = Encoding.UTF8.GetBytes(received)
            Dim difference As Integer =
                expectedBytes.Length Xor receivedBytes.Length
            Dim comparisonLength As Integer =
                Math.Max(expectedBytes.Length, receivedBytes.Length)

            For index As Integer = 0 To comparisonLength - 1
                Dim expectedByte As Integer =
                    If(index < expectedBytes.Length, expectedBytes(index), 0)
                Dim receivedByte As Integer =
                    If(index < receivedBytes.Length, receivedBytes(index), 0)
                difference = difference Or (expectedByte Xor receivedByte)
            Next

            Return difference = 0
        End Function

    End Class

End Namespace
