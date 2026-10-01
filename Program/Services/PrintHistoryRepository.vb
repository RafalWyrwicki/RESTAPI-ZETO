Imports REST_API.Models
Imports System.Configuration
Imports System.Data
Imports System.Data.SqlClient
Imports System.Threading
Imports System.Threading.Tasks

Namespace Services
    Public NotInheritable Class PrintHistoryRepository
        Private Sub New()
        End Sub

        Private Const MaxDocumentTypeLength As Integer = 100
        Private Const MaxDocumentNameLength As Integer = 500
        Private Const MaxBarcodeLength As Integer = 200
        Private Const MaxEpcLength As Integer = 128
        Private Const MaxPrinterAddressLength As Integer = 100
        Private Const MaxStatusLength As Integer = 20
        Private Const MaxErrorMessageLength As Integer = 2000

        Public Shared Async Function SaveAsync(
            request As DaneRequest,
            epc As String,
            rawZpl As String,
            status As String,
            errorMessage As String,
            cancellationToken As CancellationToken
        ) As Task(Of Integer)
            If request Is Nothing Then
                Throw New ArgumentNullException(NameOf(request))
            End If

            Dim documentType As String = GetDocumentType()
            Dim templateId As Integer = GetNonNegativeAppSetting(
                "PrintHistoryTemplateId",
                0
            )

            ValidateLength(documentType, MaxDocumentTypeLength, "DocumentType")
            ValidateLength(request.Sygnatura_Text, MaxDocumentNameLength, "DocumentName")
            ValidateLength(request.Sygnatura_Barcode, MaxBarcodeLength, "BarcodeValue")
            ValidateLength(epc, MaxEpcLength, "EpcValue")
            ValidateLength(request.IP_Drukarka, MaxPrinterAddressLength, "PrinterAddress")
            ValidateLength(status, MaxStatusLength, "Status")
            ValidateLength(errorMessage, MaxErrorMessageLength, "ErrorMessage")

            Dim connectionSetting As ConnectionStringSettings =
                ConfigurationManager.ConnectionStrings("SQLServer")
            If connectionSetting Is Nothing OrElse
               String.IsNullOrWhiteSpace(connectionSetting.ConnectionString) Then
                Throw New ConfigurationErrorsException(
                    "Brak konfiguracji połączenia SQLServer."
                )
            End If

            Const sql As String =
                "INSERT INTO dbo.PrintHistory " &
                "(DocumentType, SourceSystemId, DocumentName, BarcodeValue, EpcValue, TemplateId, PrinterAddress, [Timestamp], Status, ErrorMessage, RawZplSent) " &
                "OUTPUT INSERTED.Id " &
                "VALUES (@DocumentType, @SourceSystemId, @DocumentName, @BarcodeValue, @EpcValue, @TemplateId, @PrinterAddress, GETDATE(), @Status, @ErrorMessage, @RawZplSent)"

            Using connection As New SqlConnection(connectionSetting.ConnectionString)
                Using command As New SqlCommand(sql, connection)
                    command.CommandTimeout = GetPositiveAppSetting(
                        "SqlCommandTimeoutSeconds",
                        10
                    )
                    command.Parameters.Add("@DocumentType", SqlDbType.NVarChar, MaxDocumentTypeLength).Value = documentType
                    command.Parameters.Add("@SourceSystemId", SqlDbType.Int).Value = request.Identyfikator.Value
                    command.Parameters.Add("@DocumentName", SqlDbType.NVarChar, MaxDocumentNameLength).Value = request.Sygnatura_Text
                    command.Parameters.Add("@BarcodeValue", SqlDbType.NVarChar, MaxBarcodeLength).Value = request.Sygnatura_Barcode
                    command.Parameters.Add("@EpcValue", SqlDbType.NVarChar, MaxEpcLength).Value = DbValue(epc)
                    command.Parameters.Add("@TemplateId", SqlDbType.Int).Value = templateId
                    command.Parameters.Add("@PrinterAddress", SqlDbType.NVarChar, MaxPrinterAddressLength).Value = request.IP_Drukarka
                    command.Parameters.Add("@Status", SqlDbType.NVarChar, MaxStatusLength).Value = status
                    command.Parameters.Add("@ErrorMessage", SqlDbType.NVarChar, MaxErrorMessageLength).Value = DbValue(errorMessage)
                    command.Parameters.Add("@RawZplSent", SqlDbType.NVarChar, -1).Value = DbValue(rawZpl)

                    Await connection.OpenAsync(cancellationToken).ConfigureAwait(False)
                    Dim insertedId As Object = Await command.ExecuteScalarAsync(
                        cancellationToken
                    ).ConfigureAwait(False)
                    Return Convert.ToInt32(insertedId)
                End Using
            End Using
        End Function

        Private Shared Function DbValue(value As String) As Object
            If value Is Nothing Then
                Return DBNull.Value
            End If
            Return value
        End Function

        Private Shared Sub ValidateLength(
            value As String,
            maximumLength As Integer,
            fieldName As String
        )
            If value IsNot Nothing AndAlso value.Length > maximumLength Then
                Throw New ArgumentException(
                    fieldName & " przekracza długość kolumny PrintHistory."
                )
            End If
        End Sub

        Private Shared Function GetDocumentType() As String
            Dim configuredValue As String =
                ConfigurationManager.AppSettings("PrintHistoryDocumentType")
            If String.IsNullOrWhiteSpace(configuredValue) Then
                Return "Akta Sądowe"
            End If
            Return configuredValue.Trim()
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
    End Class
End Namespace
