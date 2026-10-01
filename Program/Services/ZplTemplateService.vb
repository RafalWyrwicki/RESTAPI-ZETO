Imports System.Configuration
Imports System.IO
Imports System.Linq

Namespace Services
    Public NotInheritable Class ZplTemplateService
        Private Sub New()
        End Sub

        Public Shared Function Render(
            barcode As String,
            nazwa As String,
            epc As String
        ) As String
            ValidateReplacement(barcode, "Sygnatura_Barcode")
            ValidateReplacement(nazwa, "Sygnatura_Text")

            If epc Is Nothing OrElse epc.Length <> EpcEncoder.EpcHexLength OrElse
               Not epc.All(AddressOf IsHexCharacter) Then
                Throw New ArgumentException("EPC musi mieć dokładnie 64 znaki HEX.")
            End If

            Dim template As String = File.ReadAllText(
                ResolveTemplatePath(),
                Text.Encoding.UTF8
            )

            For Each placeholder As String In {"$BARCODE$", "$NAZWA$", "$EPC$"}
                If Not template.Contains(placeholder) Then
                    Throw New ConfigurationErrorsException(
                        "W szablonie ZPL brakuje znacznika " & placeholder & "."
                    )
                End If
            Next

            Return template.Replace("$BARCODE$", barcode).
                Replace("$NAZWA$", nazwa).
                Replace("$EPC$", epc)
        End Function

        Private Shared Function ResolveTemplatePath() As String
            Dim configuredPath As String = ConfigurationManager.AppSettings("ZplTemplatePath")
            If String.IsNullOrWhiteSpace(configuredPath) Then
                configuredPath = Path.Combine("Templates", "etykieta.zpl")
            End If

            Dim result As String = configuredPath
            If Not Path.IsPathRooted(result) Then
                result = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, result)
            End If
            result = Path.GetFullPath(result)

            If Not File.Exists(result) Then
                Throw New ConfigurationErrorsException(
                    "Nie znaleziono szablonu ZPL: " & result
                )
            End If
            Return result
        End Function

        Private Shared Sub ValidateReplacement(value As String, fieldName As String)
            If String.IsNullOrWhiteSpace(value) Then
                Throw New ArgumentException("Pole " & fieldName & " jest wymagane.")
            End If
            If value.IndexOfAny(New Char() {"^"c, "~"c, ControlChars.Cr, ControlChars.Lf}) >= 0 Then
                Throw New ArgumentException(
                    "Pole " & fieldName & " zawiera znaki niedozwolone w ZPL (^, ~ lub znak nowej linii)."
                )
            End If
        End Sub

        Private Shared Function IsHexCharacter(value As Char) As Boolean
            Return (value >= "0"c AndAlso value <= "9"c) OrElse
                (value >= "A"c AndAlso value <= "F"c)
        End Function
    End Class
End Namespace
