Imports REST_API.Models
Imports System.Globalization
Imports System.Text
Imports System.Text.RegularExpressions

Namespace Services
    Public NotInheritable Class EpcEncoder
        Private Sub New()
        End Sub

        Public Const EpcHexLength As Integer = 64

        Public Shared Function Encode(request As DaneRequest) As String
            If request Is Nothing Then
                Throw New ArgumentNullException(NameOf(request))
            End If

            Dim idSadu As String = FixedHex(
                request.Identyfikator.Value,
                6,
                "Identyfikator"
            )
            Dim parsedWydzial As ParsedWydzial = ParseWydzialAndSekcja(
                request.Wydzial,
                request.Sekcja
            )
            Dim wydzial As String = FixedHex(
                parsedWydzial.Wydzial,
                4,
                "Wydzial"
            )
            Dim sekcja As String = FixedHex(
                parsedWydzial.Sekcja,
                2,
                "Sekcja"
            )
            Dim repertorium As String = EncodeAscii(
                request.Repertorium.Replace("_"c, " "c),
                7,
                True,
                "Repertorium"
            )
            Dim numerSprawy As String = FixedHex(
                request.Nr_Sprawy.Value,
                12,
                "Nr_Sprawy"
            )
            Dim rok As String = FixedHex(
                NormalizeYear(request.Rok.Value),
                4,
                "Rok"
            )
            Dim tom As String = EncodeAscii(
                If(request.Tom, String.Empty),
                8,
                False,
                "Tom"
            )

            Dim encoded As String = String.Concat(
                idSadu,
                wydzial,
                sekcja,
                repertorium,
                numerSprawy,
                rok,
                tom
            )

            If encoded.Length > EpcHexLength Then
                Throw New ArgumentException("Dane EPC przekraczają 256 bitów.")
            End If

            Return encoded.PadRight(EpcHexLength, "0"c)
        End Function

        Public Shared Function RadarPrefix(epc As String) As String
            If String.IsNullOrEmpty(epc) OrElse epc.Length Mod 2 <> 0 Then
                Throw New ArgumentException("EPC musi zawierać pełne bajty HEX.", NameOf(epc))
            End If

            Dim result As String = epc
            While result.EndsWith("00", StringComparison.Ordinal)
                result = result.Substring(0, result.Length - 2)
            End While
            Return result
        End Function

        Private Shared Function NormalizeYear(yearValue As Integer) As Integer
            If yearValue < 100 Then
                Return If(yearValue > 50, 1900 + yearValue, 2000 + yearValue)
            End If
            Return yearValue
        End Function

        Private Shared Function ParseWydzialAndSekcja(
            value As String,
            explicitSekcja As Nullable(Of Integer)
        ) As ParsedWydzial
            If String.IsNullOrWhiteSpace(value) Then
                Throw New ArgumentException("Pole Wydział jest wymagane.")
            End If

            Dim match As Match = Regex.Match(
                value,
                "^\s*(?<wydzial>[IVXLCDM]+|\d+)(?:(?:\s*\.\s*|\s+)(?<sekcja>\d+))?\s*$",
                RegexOptions.IgnoreCase Or RegexOptions.CultureInvariant
            )
            If Not match.Success Then
                Throw New ArgumentException(
                    "Wydział musi mieć format np. XC, XC.2 albo XC 2."
                )
            End If

            Dim wydzialValue As Integer = ParseWydzialValue(
                match.Groups("wydzial").Value
            )
            Dim sekcjaValue As Integer = If(explicitSekcja.HasValue, explicitSekcja.Value, 0)

            If match.Groups("sekcja").Success Then
                Dim embeddedSekcja As Integer
                If Not Integer.TryParse(
                    match.Groups("sekcja").Value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    embeddedSekcja
                ) OrElse embeddedSekcja < 0 OrElse embeddedSekcja > 255 Then
                    Throw New ArgumentException(
                        "Sekcja w polu Wydział musi być liczbą od 0 do 255."
                    )
                End If

                If explicitSekcja.HasValue AndAlso explicitSekcja.Value <> embeddedSekcja Then
                    Throw New ArgumentException(
                        "Sekcja podana w polu Wydział jest inna niż wartość pola Sekcja."
                    )
                End If
                sekcjaValue = embeddedSekcja
            End If

            If sekcjaValue < 0 OrElse sekcjaValue > 255 Then
                Throw New ArgumentException("Sekcja musi być liczbą od 0 do 255.")
            End If

            Return New ParsedWydzial With {
                .Wydzial = wydzialValue,
                .Sekcja = sekcjaValue
            }
        End Function

        Private Shared Function ParseWydzialValue(value As String) As Integer
            Dim normalized As String = value.Trim().ToUpperInvariant()
            Dim arabicValue As Integer
            If Integer.TryParse(
                normalized,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                arabicValue
            ) Then
                If arabicValue < 1 Then
                    Throw New ArgumentException("Wydział musi być dodatnią liczbą rzymską lub arabską.")
                End If
                Return arabicValue
            End If

            Dim total As Integer = 0
            Dim previous As Integer = 0
            For index As Integer = normalized.Length - 1 To 0 Step -1
                Dim current As Integer = RomanDigitValue(normalized(index))
                If current = 0 Then
                    Throw New ArgumentException("Wydział zawiera nieprawidłową liczbę rzymską.")
                End If
                If current < previous Then
                    total -= current
                Else
                    total += current
                    previous = current
                End If
            Next

            If total < 1 OrElse ToRoman(total) <> normalized Then
                Throw New ArgumentException("Wydział zawiera nieprawidłową liczbę rzymską.")
            End If
            Return total
        End Function

        Private Class ParsedWydzial
            Public Property Wydzial As Integer
            Public Property Sekcja As Integer
        End Class

        Private Shared Function RomanDigitValue(character As Char) As Integer
            Select Case character
                Case "I"c : Return 1
                Case "V"c : Return 5
                Case "X"c : Return 10
                Case "L"c : Return 50
                Case "C"c : Return 100
                Case "D"c : Return 500
                Case "M"c : Return 1000
                Case Else : Return 0
            End Select
        End Function

        Private Shared Function ToRoman(value As Integer) As String
            Dim values As Integer() = {1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1}
            Dim symbols As String() = {"M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I"}
            Dim remaining As Integer = value
            Dim result As New StringBuilder()

            For index As Integer = 0 To values.Length - 1
                While remaining >= values(index)
                    result.Append(symbols(index))
                    remaining -= values(index)
                End While
            Next
            Return result.ToString()
        End Function

        Private Shared Function FixedHex(
            value As Long,
            width As Integer,
            fieldName As String
        ) As String
            If value < 0 Then
                Throw New ArgumentException(fieldName & " nie może być ujemne.")
            End If

            Dim hex As String = value.ToString("X", CultureInfo.InvariantCulture)
            If hex.Length > width Then
                Throw New ArgumentException(fieldName & " nie mieści się w polu EPC.")
            End If
            Return hex.PadLeft(width, "0"c)
        End Function

        Private Shared Function EncodeAscii(
            value As String,
            byteLength As Integer,
            padLeft As Boolean,
            fieldName As String
        ) As String
            If value.Length > byteLength Then
                Throw New ArgumentException(
                    String.Format(
                        CultureInfo.InvariantCulture,
                        "Pole {0} może mieć maksymalnie {1} znaków ASCII.",
                        fieldName,
                        byteLength
                    )
                )
            End If

            For Each character As Char In value
                If AscW(character) < 0 OrElse AscW(character) > 127 Then
                    Throw New ArgumentException("Pole " & fieldName & " może zawierać wyłącznie znaki ASCII.")
                End If
            Next

            Dim hex As New StringBuilder(byteLength * 2)
            For Each character As Char In value
                hex.Append(AscW(character).ToString("X2", CultureInfo.InvariantCulture))
            Next

            Dim expectedLength As Integer = byteLength * 2
            If padLeft Then
                Return hex.ToString().PadLeft(expectedLength, "0"c)
            End If
            Return hex.ToString().PadRight(expectedLength, "0"c)
        End Function
    End Class
End Namespace
