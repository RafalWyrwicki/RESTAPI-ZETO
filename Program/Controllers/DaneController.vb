Imports System.Web.Http
Imports REST_API.Models
Imports REST_API.Filters
Imports REST_API.Services
Imports System.Configuration
Imports System.Data
Imports System.Data.SqlClient
Imports System.Net
Imports System.Net.Sockets
Imports System.Threading
Imports System.Threading.Tasks

Namespace Controllers
    Public Class DaneController
        Inherits ApiController

        Private Shared ReadOnly RequestTimeoutSeconds As Integer =
            GetPositiveAppSetting("ApiRequestTimeoutSeconds", 15)

        Private Shared ReadOnly PrinterPort As Integer =
            GetPositiveAppSetting("PrinterPort", 9100)

        Private Shared ReadOnly PrinterConnectTimeoutMilliseconds As Integer =
            GetPositiveAppSetting("PrinterConnectTimeoutMilliseconds", 3000)

        Private Shared ReadOnly SqlCommandTimeoutSeconds As Integer =
            GetPositiveAppSetting("SqlCommandTimeoutSeconds", 10)

        <HttpPost>
        <Route("api/dane")>
        <ApiTokenAuthorize>
        Public Async Function OdbierzDane(
            <FromBody> request As DaneRequest,
            cancellationToken As CancellationToken
        ) As Task(Of IHttpActionResult)

            If request Is Nothing Then
                Return BadRequest("Brak danych w żądaniu.")
            End If

            If Not request.Identyfikator.HasValue OrElse
               request.Identyfikator.Value < 1 OrElse
               request.Identyfikator.Value > 9999 Then
                Return BadRequest("Identyfikator - błędny format danych")
            End If

            If String.IsNullOrWhiteSpace(request.Wydzial) Then
                Return BadRequest("Pole Wydział jest wymagane.")
            End If

            If String.IsNullOrWhiteSpace(request.Repertorium) Then
                Return BadRequest("Pole Repertorium jest wymagane.")
            End If

            If Not request.Nr_Sprawy.HasValue OrElse
               request.Nr_Sprawy.Value < 1 OrElse
               request.Nr_Sprawy.Value > 99999 Then
                Return BadRequest("Nr_Sprawy - błędny format danych - dozwolone liczby od 1 do 99999")
            End If

            If Not request.Rok.HasValue OrElse
               request.Rok.Value < 0 OrElse
               request.Rok.Value > 9999 Then
                Return BadRequest("Rok - błędny format danych")
            End If

            If request.Sekcja.HasValue AndAlso
               (request.Sekcja.Value < 0 OrElse request.Sekcja.Value > 255) Then
                Return BadRequest("Sekcja - dozwolone liczby od 0 do 255.")
            End If

            If String.IsNullOrWhiteSpace(request.Sygnatura_Text) Then
                Return BadRequest("Pole Sygnatura_Text jest wymagane.")
            End If

            If String.IsNullOrWhiteSpace(request.Sygnatura_Barcode) Then
                Return BadRequest("Pole Sygnatura_Barcode jest wymagane.")
            End If

            If String.IsNullOrWhiteSpace(request.IP_Drukarka) Then
                Return BadRequest("Pole IP_Drukarka jest wymagane.")
            End If

            If Not CzyPoprawnyAdresIp(request.IP_Drukarka) Then
                Return BadRequest("Adres IP - nieprawidłowy format")
            End If

            If Not ModelState.IsValid Then
                Return BadRequest(ModelState)
            End If

            Dim epc As String
            Dim zpl As String
            Try
                epc = EpcEncoder.Encode(request)
                zpl = ZplTemplateService.Render(
                    request.Sygnatura_Barcode,
                    request.Sygnatura_Text,
                    epc
                )
            Catch ex As ArgumentException
                Return BadRequest(ex.Message)
            End Try

            Using requestTimeoutSource As CancellationTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)

                requestTimeoutSource.CancelAfter(
                    TimeSpan.FromSeconds(RequestTimeoutSeconds)
                )

                Dim printSucceeded As Boolean = Await RawPrinterClient.SendAsync(
                    request.IP_Drukarka,
                    PrinterPort,
                    zpl,
                    PrinterConnectTimeoutMilliseconds,
                    requestTimeoutSource.Token
                ).ConfigureAwait(False)

                If Not printSucceeded Then
                    Dim printError As String =
                        "Nie można połączyć się z drukarką na porcie " &
                        PrinterPort.ToString() & "."

                    Await PrintHistoryRepository.SaveAsync(
                        request,
                        epc,
                        zpl,
                        "ERROR",
                        printError,
                        requestTimeoutSource.Token
                    ).ConfigureAwait(False)

                    Return BadRequest(printError)
                End If

                Await PrintHistoryRepository.SaveAsync(
                    request,
                    epc,
                    zpl,
                    "OK",
                    Nothing,
                    requestTimeoutSource.Token
                ).ConfigureAwait(False)

                Await ZapiszDoBazyAsync(
                    request,
                    requestTimeoutSource.Token
                ).ConfigureAwait(False)
            End Using

            Return Ok(New With {
                .status = "OK",
                .komunikat = "Etykieta została wysłana do drukarki i dane zapisano w bazie."
            })

        End Function

        Private Shared Function CzyPoprawnyAdresIp(adres As String) As Boolean
            If adres Is Nothing OrElse adres <> adres.Trim() Then
                Return False
            End If

            Dim sparsowanyAdres As IPAddress = Nothing
            If Not IPAddress.TryParse(adres, sparsowanyAdres) Then
                Return False
            End If

            If sparsowanyAdres.AddressFamily = AddressFamily.InterNetworkV6 Then
                Return adres.Contains(":")
            End If

            If sparsowanyAdres.AddressFamily <> AddressFamily.InterNetwork Then
                Return False
            End If

            ' IPAddress.TryParse dopuszcza skrócone zapisy IPv4 (np. "127.1").
            ' Endpoint akceptuje wyłącznie pełny zapis czterech oktetów.
            Dim oktety As String() = adres.Split("."c)
            If oktety.Length <> 4 Then
                Return False
            End If

            For Each oktet As String In oktety
                Dim wartosc As Integer
                If oktet.Length = 0 OrElse oktet.Length > 3 OrElse
                   Not Integer.TryParse(oktet, wartosc) OrElse
                   wartosc < 0 OrElse wartosc > 255 Then
                    Return False
                End If

                For Each znak As Char In oktet
                    If znak < "0"c OrElse znak > "9"c Then
                        Return False
                    End If
                Next
            Next

            Return True
        End Function

        Private Shared Async Function ZapiszDoBazyAsync(
            request As DaneRequest,
            cancellationToken As CancellationToken
        ) As Task
            Dim connectionSetting As ConnectionStringSettings =
                ConfigurationManager.ConnectionStrings("SQLServer")

            If connectionSetting Is Nothing OrElse
               String.IsNullOrWhiteSpace(connectionSetting.ConnectionString) Then
                Throw New ConfigurationErrorsException(
                    "Brak konfiguracji połączenia SQLServer."
                )
            End If

            Dim connString As String = connectionSetting.ConnectionString
            Using conn As New SqlConnection(connString)
                Dim sql As String =
                    "INSERT INTO OdebraneDane (Identyfikator, Wydzial, Repertorium, Nr_Sprawy, Rok, Tom, IP_Drukarka, Sygnatura_Text, Sygnatura_Barcode) " &
                    "VALUES (@Identyfikator, @Wydzial, @Repertorium, @Nr_Sprawy, @Rok, @Tom, @IP_Drukarka, @Sygnatura_Text, @Sygnatura_Barcode)"

                Using cmd As New SqlCommand(sql, conn)
                    cmd.CommandTimeout = SqlCommandTimeoutSeconds
                    cmd.Parameters.Add(
                        "@Identyfikator",
                        SqlDbType.Int
                    ).Value = request.Identyfikator.Value
                    cmd.Parameters.Add(
                        "@Wydzial",
                        SqlDbType.NVarChar,
                        DaneRequest.MaxWydzialLength
                    ).Value = request.Wydzial
                    cmd.Parameters.Add(
                        "@Repertorium",
                        SqlDbType.NVarChar,
                        DaneRequest.MaxRepertoriumLength
                    ).Value = request.Repertorium
                    cmd.Parameters.Add(
                        "@Nr_Sprawy",
                        SqlDbType.Int
                    ).Value = request.Nr_Sprawy.Value
                    cmd.Parameters.Add(
                        "@Rok",
                        SqlDbType.Int
                    ).Value = request.Rok.Value
                    cmd.Parameters.Add(
                        "@Tom",
                        SqlDbType.NVarChar,
                        DaneRequest.MaxTomLength
                    ).Value = If(request.Tom, String.Empty)
                    cmd.Parameters.Add(
                        "@IP_Drukarka",
                        SqlDbType.NVarChar,
                        DaneRequest.MaxIpAddressLength
                    ).Value = request.IP_Drukarka
                    cmd.Parameters.Add(
                        "@Sygnatura_Text",
                        SqlDbType.NVarChar,
                        DaneRequest.MaxSignatureLength
                    ).Value = request.Sygnatura_Text
                    cmd.Parameters.Add(
                        "@Sygnatura_Barcode",
                        SqlDbType.NVarChar,
                        DaneRequest.MaxSignatureLength
                    ).Value = request.Sygnatura_Barcode

                    Await conn.OpenAsync(cancellationToken).ConfigureAwait(False)
                    Await cmd.ExecuteNonQueryAsync(
                        cancellationToken
                    ).ConfigureAwait(False)
                End Using
            End Using
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

    End Class

End Namespace
